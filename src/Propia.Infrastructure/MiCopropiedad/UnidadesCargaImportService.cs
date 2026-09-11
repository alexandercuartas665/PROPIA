using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Directorio;
using Propia.Application.MiCopropiedad;
using Propia.Application.Porteria;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Directorio;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>
/// Importa la plantilla multi-hoja y carga en VARIAS copropiedades del cliente. Reusa
/// IMiCopropiedadService / IPorteriaService (validacion, RLS, bitacora). Cambia el contexto de
/// tenant por copropiedad para que cada escritura caiga en la copropiedad correcta.
/// </summary>
public sealed class UnidadesCargaImportService : IUnidadesCargaImportService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextAccessor _http;
    private readonly IMiCopropiedadService _mi;
    private readonly IPorteriaService _porteria;
    private readonly IDirectorioService _dir;

    public UnidadesCargaImportService(PropiaDbContext db, ITenantContext tenant, IHttpContextAccessor http,
        IMiCopropiedadService mi, IPorteriaService porteria, IDirectorioService dir)
    {
        _db = db;
        _tenant = tenant;
        _http = http;
        _mi = mi;
        _porteria = porteria;
        _dir = dir;
    }

    public async Task<ResultadoCargaUnidades> ImportarAsync(Stream contenidoXlsx, CancellationToken ct, bool forzarTenantActual = false, bool reemplazarDependientes = false)
    {
        var errores = new List<CargaUnidadesError>();
        using var wb = new XLWorkbook(contenidoXlsx);

        var copros = await CopropiedadesDelClienteAsync(ct);   // nombreLower -> tenantId
        var tenantOriginal = _tenant.CurrentTenantId;

        var unidades = LeerHoja(wb, "UNIDADES PRIVADAS");
        var personas = LeerHoja(wb, "PERSONAS");
        var vehiculos = LeerHoja(wb, "VEHICULOS");
        var mascotas = LeerHoja(wb, "MASCOTAS");
        var zonas = LeerHoja(wb, "ZONAS COMUNES");
        var equipos = LeerHoja(wb, "EQUIPOS");
        // Terceros se procesan aparte (no por grupo de copropiedad): un tercero puede ir a "Todas las
        // copropiedades" o a una sola, y su columna COPROPIEDAD NO debe crear grupos en el loop.
        var terceros = LeerHoja(wb, "TERCEROS");

        // Columnas de campos dinamicos ([Label]) de cada hoja: se calculan UNA vez (los encabezados
        // son los mismos para todas las filas) y solo si existen se consultan los catalogos.
        // TERCEROS queda fuera: su catalogo (TerceroCamposDefiniciones) es de las EMPLEADAS de una
        // unidad, y esta hoja carga terceros del DIRECTORIO (empresa/persona global + vinculo), que
        // no crean el registro de empleada al que colgarle el valor.
        var dinUni = LabelsDinamicos(unidades);
        var dinPer = LabelsDinamicos(personas);
        var dinVeh = LabelsDinamicos(vehiculos);
        var dinMas = LabelsDinamicos(mascotas);
        var dinZon = LabelsDinamicos(zonas);
        var dinEqu = LabelsDinamicos(equipos);
        // Avisos ya emitidos ("hoja|label"): una columna [X] desconocida se reporta UNA vez por hoja,
        // no una por fila (hay cargas de 200+ filas) ni una por copropiedad.
        var avisados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var nombresCopro = unidades.Concat(personas).Concat(vehiculos).Concat(mascotas).Concat(zonas).Concat(equipos)
            .Select(r => Val(r.Row, "COPROPIEDAD"))
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        int nCopro = 0, nUni = 0, nUniAct = 0, nAnexo = 0, nPer = 0, nVeh = 0, nMas = 0, nZon = 0, nEqu = 0, nTer = 0;

        // Grupos a procesar. Modo normal: un grupo por cada nombre de COPROPIEDAD de la plantilla
        // (resuelto contra las copropiedades del cliente). Modo onboarding (forzarTenantActual):
        // un solo grupo con el tenant activo, tomando TODAS las filas sin mirar la columna COPROPIEDAD.
        // SoloVacias: grupo del tenant activo que recoge las filas SIN COPROPIEDAD (archivos de una sola
        // copropiedad, o plantillas antiguas sin esa columna) para que se carguen en la copropiedad activa.
        var grupos = new List<(string Nombre, Guid Tid, bool Todas, bool SoloVacias)>();
        if (forzarTenantActual && _tenant.CurrentTenantId is { } actual)
        {
            grupos.Add(("", actual, true, false));
        }
        else
        {
            foreach (var nombre in nombresCopro)
            {
                if (!copros.TryGetValue(nombre.ToLowerInvariant(), out var tid))
                {
                    errores.Add(new("GENERAL", 0, $"Copropiedad '{nombre}' no existe o no la administras."));
                    continue;
                }
                grupos.Add((nombre, tid, false, false));
            }
            // Filas sin COPROPIEDAD -> a la copropiedad ACTIVA (si hay una y no es ya un grupo).
            var hayVacias = unidades.Concat(personas).Concat(vehiculos).Concat(mascotas).Concat(zonas).Concat(equipos)
                .Any(r => string.IsNullOrWhiteSpace(Val(r.Row, "COPROPIEDAD")));
            if (hayVacias && _tenant.CurrentTenantId is { } act2 && grupos.All(g => g.Tid != act2))
                grupos.Add(("(copropiedad activa)", act2, false, true));
        }

        foreach (var (nombre, tid, todas, soloVacias) in grupos)
        {
            await SetTenantSqlAsync(tid, ct);   // fija tenant en EF y en la sesion SQL (RLS)
            nCopro++;
            // Predicado de pertenencia de una fila a este grupo (por nombre, todas, o solo vacias).
            bool Coincide(Dictionary<string, string> row) => todas
                || (soloVacias ? string.IsNullOrWhiteSpace(Val(row, "COPROPIEDAD")) : Eq(Val(row, "COPROPIEDAD"), nombre));

            // Catalogos de campos dinamicos de ESTA copropiedad (las definiciones son por tenant, y
            // el tenant ya quedo fijado arriba). Solo se consultan si la hoja trae columnas [Label].
            var defsUni = dinUni.Count == 0 ? SinCampos : Catalogo(await _mi.ListCamposDefinicionAsync(ct), d => d.Id, d => d.Label);
            var defsPer = dinPer.Count == 0 ? SinCampos : Catalogo(await _mi.ListCamposDefPersonaAsync(ct), d => d.Id, d => d.Label);
            var defsVeh = dinVeh.Count == 0 ? SinCampos : Catalogo(await _mi.ListCamposDefVehiculoAsync(ct), d => d.Id, d => d.Label);
            var defsMas = dinMas.Count == 0 ? SinCampos : Catalogo(await _mi.ListCamposDefMascotaAsync(ct), d => d.Id, d => d.Label);
            var defsZon = dinZon.Count == 0 ? SinCampos : Catalogo(await _mi.ListCamposDefZonaAsync(ct), d => d.Id, d => d.Label);
            var defsEqu = dinEqu.Count == 0 ? SinCampos : Catalogo(await _mi.ListCamposDefEquipoAsync(ct), d => d.Id, d => d.Label);

            // Tipos de unidad PROPIOS de ESTA copropiedad (nombre -> id), para resolver la columna TIPO.
            // Son por tenant, igual que los campos dinamicos, y el tenant ya quedo fijado arriba.
            var tiposPropios = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var tp in await _db.TiposUnidadCustom.AsNoTracking()
                         .Select(t => new { t.Id, t.Nombre }).ToListAsync(ct))
                tiposPropios.TryAdd((tp.Nombre ?? "").Trim(), tp.Id);

            var numeroToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var idxCodigo = await BuildUnidadIndexAsync(ct);   // unidades existentes por Numero y por codigo TORRE-NUMERO
            var anexosPend = new List<(int Fila, string Principal, string Asociada)>();

            // ---- Unidades ----
            foreach (var (row, fila) in unidades.Where(r => Coincide(r.Row)))
            {
                try
                {
                    _db.ChangeTracker.Clear();   // evita que el tracker crezca (DetectChanges O(n^2))
                    var numero = Val(row, "UNIDAD PRIVADA").Trim();
                    if (numero.Length == 0) { errores.Add(new("UNIDADES PRIVADAS", fila, "Falta UNIDAD PRIVADA")); continue; }
                    // TIPO puede ser del sistema (por nombre de enum o por su etiqueta, "Cuarto util")
                    // o PROPIO de la copropiedad. null = la columna no vino o vino vacia -> no se toca
                    // el tipo que ya tiene la unidad. Un texto que no resuelve es un ERROR de la fila:
                    // antes caia en Apartamento sin avisar, y quedaba mal clasificada para siempre.
                    var tipoTexto = Val(row, "TIPO").Trim();
                    (TipoUnidad Tipo, Guid? Custom)? tipoRes = null;
                    if (tipoTexto.Length > 0)
                    {
                        if (TiposUnidadCatalogo.TryResolver(tipoTexto, out var tSis))
                            tipoRes = (tSis, null);
                        else if (tiposPropios.TryGetValue(tipoTexto, out var propioId))
                            tipoRes = (TipoUnidad.Apartamento, propioId);   // el propio se guarda en TipoCustomId
                        else
                        {
                            errores.Add(new("UNIDADES PRIVADAS", fila,
                                $"TIPO '{tipoTexto}' no existe en esta copropiedad (ni del sistema ni propio). "
                                + "Revisa la lista desplegable de la columna TIPO."));
                            continue;
                        }
                    }
                    var coef = ParseDecimal(Val(row, "COEFICIENTE"));
                    var matricula = NullIfEmpty(Val(row, "MATRICULA"));
                    var refPago = NullIfEmpty(Val(row, "REF PAGO"));
                    // Modulos contributivos: la plantilla solo trae las columnas de los campos
                    // activos de la copropiedad, asi que una columna ausente se lee como celda
                    // vacia -> null (y en la actualizacion no pisa el valor existente).
                    var mod1 = ParseDecimalNull(Val(row, "MODULO CONTRIBUTIVO 1"));
                    var mod2 = ParseDecimalNull(Val(row, "MODULO CONTRIBUTIVO 2"));
                    var mod3 = ParseDecimalNull(Val(row, "MODULO CONTRIBUTIVO 3"));
                    var mod4 = ParseDecimalNull(Val(row, "MODULO CONTRIBUTIVO 4"));
                    var mod5 = ParseDecimalNull(Val(row, "MODULO CONTRIBUTIVO 5"));
                    // Resto de campos de sistema que la copropiedad puede activar. Se leen por el
                    // encabezado del catalogo unico (UnidadCamposSistema), asi que cualquier campo que
                    // la plantilla emita por estar visible se puede importar. Todos son opcionales:
                    // null = la columna no vino o vino vacia -> no pisa lo que ya tiene la unidad.
                    var estado = NullIfEmpty(Val(row, "ESTADO"));
                    var area = ParseDecimalNull(Val(row, "AREA"));
                    var piso = ParseIntNull(Val(row, "PISO"));
                    var habitaciones = ParseIntNull(Val(row, "HABITACIONES"));
                    var banos = ParseIntNull(Val(row, "BANOS"));
                    var parqueaderos = ParseIntNull(Val(row, "PARQUEADEROS"));
                    var pagaAdmin = ParseSiNoNull(Val(row, "PAGA ADMIN"));
                    var cuota = ParseDecimalNull(Val(row, "CUOTA MENSUAL"));
                    var observaciones = NullIfEmpty(Val(row, "OBSERVACIONES"));

                    // Modo MODULO (recarga desde Unidades Privadas): si la unidad ya existe (por su
                    // numero exacto), se ACTUALIZA en vez de crear. Solo se pisa lo que la plantilla
                    // trae con valor; una columna ausente o vacia conserva el dato actual (la torre
                    // no se toca nunca: no hay columna para ella).
                    // Modo ONBOARDING (todas): siempre crea (la copropiedad es nueva).
                    var existente = todas
                        ? null
                        : await _db.UnidadesPrivadas.FirstOrDefaultAsync(x => x.Numero == numero, ct);
                    if (existente is not null)
                    {
                        var upd = new ActualizarUnidadRequest(
                            existente.Numero, tipoRes?.Tipo ?? existente.Tipo, existente.TorreId, piso ?? existente.Piso,
                            coef, area ?? existente.AreaM2,
                            habitaciones ?? existente.Habitaciones, banos ?? existente.Banos,
                            parqueaderos ?? existente.Parqueaderos,
                            estado ?? existente.Estado, observaciones ?? existente.Observaciones,
                            matricula ?? existente.MatriculaInmobiliaria, pagaAdmin ?? existente.PagaAdministracion,
                            cuota ?? existente.CuotaMensual, refPago ?? existente.ReferenciaPago,
                            // Si la fila trae TIPO manda ese (y limpia el propio cuando eligio uno del
                            // sistema); si no trae, se conserva el que tenia. Antes no se enviaba nunca,
                            // asi que cada recarga borraba el tipo propio de la unidad.
                            TipoCustomId: tipoRes is { } tr ? tr.Custom : existente.TipoCustomId,
                            ModuloContributivo1: mod1 ?? existente.ModuloContributivo1,
                            ModuloContributivo2: mod2 ?? existente.ModuloContributivo2,
                            ModuloContributivo3: mod3 ?? existente.ModuloContributivo3,
                            ModuloContributivo4: mod4 ?? existente.ModuloContributivo4,
                            ModuloContributivo5: mod5 ?? existente.ModuloContributivo5);
                        await _mi.ActualizarUnidadAsync(existente.Id, upd, ct);
                        numeroToId[numero] = existente.Id;
                        nUniAct++;
                    }
                    else
                    {
                        var req = new CrearUnidadRequest(
                            numero, tipoRes?.Tipo ?? TipoUnidad.Apartamento, null, piso,
                            coef, area, habitaciones, banos, parqueaderos,
                            estado, observaciones,
                            matricula, pagaAdmin ?? true, cuota, refPago,
                            TipoCustomId: tipoRes?.Custom,
                            ModuloContributivo1: mod1, ModuloContributivo2: mod2,
                            ModuloContributivo3: mod3, ModuloContributivo4: mod4,
                            ModuloContributivo5: mod5);
                        var creada = await _mi.CrearUnidadAsync(req, ct);
                        numeroToId[numero] = creada.Id;
                        nUni++;
                    }

                    // Campos dinamicos [Label] de la unidad: mismo camino al crear y al actualizar
                    // (numeroToId ya tiene el id en ambos casos).
                    var unidadId = numeroToId[numero];
                    await EscribirDinamicosAsync("UNIDADES PRIVADAS", row, defsUni,
                        (d, v) => _mi.SetCampoValorUnidadAsync(unidadId, d, new SetCampoValorRequest(v), ct),
                        errores, avisados);

                    var agr = Val(row, "AGRUPACION").Trim();
                    var principal = Val(row, "PRINCIPAL").Trim();
                    if (agr.StartsWith("3") && principal.Length > 0)
                        anexosPend.Add((fila, principal, numero));
                }
                catch (Exception ex) { errores.Add(new("UNIDADES PRIVADAS", fila, Fallo(ex))); }
            }

            // ---- Anexos (2a pasada: ya existen ambas unidades) ----
            // IDEMPOTENTE: en una recarga los vinculos de la carga anterior siguen existiendo (las
            // unidades hacen upsert, no se borran). Si la asociada ya esta vinculada al MISMO principal
            // se omite sin error; si cambio el principal y el usuario pidio "eliminar y cargar de nuevo"
            // se re-apunta; si no, se reporta el conflicto real.
            foreach (var (fila, principal, asociada) in anexosPend)
            {
                try
                {
                    _db.ChangeTracker.Clear();
                    var pid = numeroToId.GetValueOrDefault(principal);
                    if (pid == Guid.Empty) idxCodigo.TryGetValue(principal, out pid);
                    var aid = numeroToId.GetValueOrDefault(asociada);
                    if (aid == Guid.Empty) idxCodigo.TryGetValue(asociada, out aid);
                    if (pid == Guid.Empty || aid == Guid.Empty)
                    {
                        errores.Add(new("UNIDADES PRIVADAS", fila, $"Anexo: no se encontro la principal '{principal}'.")); continue;
                    }
                    var existente = await _db.UnidadVinculos.FirstOrDefaultAsync(v => v.UnidadAsociadaId == aid, ct);
                    if (existente is not null)
                    {
                        if (existente.UnidadPrincipalId == pid) { nAnexo++; continue; }   // ya correcto: no re-crea
                        if (reemplazarDependientes)
                        {
                            _db.UnidadVinculos.Remove(existente);
                            await _db.SaveChangesAsync(ct);
                            _db.ChangeTracker.Clear();
                        }
                        else
                        {
                            errores.Add(new("UNIDADES PRIVADAS", fila, $"La unidad {asociada} ya esta asociada a otra unidad.")); continue;
                        }
                    }
                    await _mi.CrearVinculoAsync(pid, new CrearVinculoUnidadRequest(aid, false), ct);
                    nAnexo++;
                }
                catch (Exception ex) { errores.Add(new("UNIDADES PRIVADAS", fila, Fallo(ex))); }
            }

            // ---- Personas (reemplazo si aplica) ----
            var personasGrp = personas.Where(r => Coincide(r.Row)).ToList();
            if (await ReemplazarSiAplicaAsync("PERSONAS", personasGrp.Count > 0, reemplazarDependientes,
                    c => _db.UnidadPersonas.ExecuteDeleteAsync(c), errores, ct))
            foreach (var (row, fila) in personasGrp)
            {
                try
                {
                    _db.ChangeTracker.Clear();
                    var uid = ResolverUnidad(Val(row, "UNIDAD PRIVADA"), numeroToId, idxCodigo);
                    if (uid == Guid.Empty) { errores.Add(new("PERSONAS", fila, $"Unidad '{Val(row, "UNIDAD PRIVADA")}' no encontrada")); continue; }
                    var doc = Val(row, "IDENTIFICACION").Trim();
                    if (doc.Length == 0) { errores.Add(new("PERSONAS", fila, "Falta IDENTIFICACION")); continue; }
                    var (nombres, apellidos) = SplitNombre(Val(row, "NOMBRE"));
                    var req = new AgregarPersonaUnidadRequest(
                        doc, nombres, apellidos, NullIfEmpty(Val(row, "EMAIL")), NullIfEmpty(Val(row, "TELEFONO")),
                        ParseEnum(Val(row, "TIPO RESIDENTE"), RolUnidadPersona.Propietario));
                    // El id de la persona-unidad es la clave de sus campos dinamicos: antes se
                    // descartaba el DTO devuelto.
                    var creadaPer = await _mi.AgregarPersonaUnidadAsync(uid, req, ct);
                    nPer++;
                    await EscribirDinamicosAsync("PERSONAS", row, defsPer,
                        (d, v) => _mi.SetCampoValorPersonaDefAsync(creadaPer.Id, d, new SetCampoValorRequest(v), ct),
                        errores, avisados);
                }
                catch (Exception ex) { errores.Add(new("PERSONAS", fila, Fallo(ex))); }
            }

            // ---- Vehiculos (reemplazo si aplica) ----
            // Los vehiculos NO se borran (su historial de porteria en registros_vehiculo es append-only,
            // RN-10, y un DELETE dispararia un UPDATE prohibido por el FK SET NULL). Se DESACTIVAN los
            // activos (soft-delete): eso preserva la auditoria y libera la placa para los nuevos.
            // En la recarga (reemplazar) se DESACTIVAN los vehiculos de porteria (soft-delete, RN-10) y
            // ADEMAS se limpian las placas habilitadas de la unidad (unidad_placas), que es lo que muestra
            // el modal de consulta de la unidad; asi la recarga las repone sin duplicar.
            var vehiculosGrp = vehiculos.Where(r => Coincide(r.Row)).ToList();
            if (await ReemplazarSiAplicaAsync("VEHICULOS", vehiculosGrp.Count > 0, reemplazarDependientes,
                    async c =>
                    {
                        var n = await _db.VehiculosAutorizados.Where(v => v.Activo)
                            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Activo, false), c);
                        await _db.UnidadPlacas.ExecuteDeleteAsync(c);
                        return n;
                    }, errores, ct))
            {
                // Espejo de las placas ya existentes para no duplicar (no hay unique en unidad+placa).
                // Guarda tambien el Id: es la clave de los campos dinamicos de vehiculo, y en una
                // recarga sin reemplazo la placa ya existe (no se vuelve a insertar) pero sus campos
                // dinamicos SI deben actualizarse.
                var placasVistas = new Dictionary<string, Guid>();
                foreach (var p in await _db.UnidadPlacas.AsNoTracking()
                             .Select(p => new { p.Id, p.UnidadId, p.Placa }).ToListAsync(ct))
                    placasVistas.TryAdd(p.UnidadId + "|" + p.Placa, p.Id);
                foreach (var (row, fila) in vehiculosGrp)
                {
                    try
                    {
                        _db.ChangeTracker.Clear();
                        var uid = ResolverUnidad(Val(row, "UNIDAD PRIVADA"), numeroToId, idxCodigo);
                        if (uid == Guid.Empty) { errores.Add(new("VEHICULOS", fila, $"Unidad '{Val(row, "UNIDAD PRIVADA")}' no encontrada")); continue; }
                        var placa = Val(row, "PLACA").Trim();
                        if (placa.Length == 0) { errores.Add(new("VEHICULOS", fila, "Falta PLACA")); continue; }
                        var tipoVeh = ParseEnum(Val(row, "TIPO DE VEHICULO"), TipoVehiculo.Automovil);
                        await _porteria.CrearVehiculoAutorizadoAsync(new CrearVehiculoRequest(
                            uid, placa, tipoVeh,
                            NullIfEmpty(Val(row, "MARCA")), NullIfEmpty(Val(row, "MODELO")), NullIfEmpty(Val(row, "COLOR")), null), ct);
                        nVeh++;
                        // Placa habilitada de la unidad (lo que lee la ficha/modal de la unidad). Igual que
                        // agregar a mano en el modal: placa en mayusculas, max 15, mismo enum de tipo.
                        var placaUp = placa.ToUpperInvariant();
                        if (placaUp.Length > 15) placaUp = placaUp[..15];
                        var clavePlaca = uid + "|" + placaUp;
                        if (!placasVistas.TryGetValue(clavePlaca, out var placaId))
                        {
                            var nuevaPlaca = new UnidadPlaca { UnidadId = uid, Placa = placaUp, TipoVehiculo = tipoVeh };
                            _db.UnidadPlacas.Add(nuevaPlaca);
                            await _db.SaveChangesAsync(ct);
                            placaId = nuevaPlaca.Id;
                            placasVistas[clavePlaca] = placaId;
                        }
                        // Campos dinamicos del vehiculo: cuelgan de la placa habilitada (unidad_placas).
                        await EscribirDinamicosAsync("VEHICULOS", row, defsVeh,
                            (d, v) => _mi.SetCampoValorVehiculoDefAsync(placaId, d, new SetCampoValorRequest(v), ct),
                            errores, avisados);
                    }
                    catch (Exception ex) { errores.Add(new("VEHICULOS", fila, Fallo(ex))); }
                }
            }

            // ---- Mascotas (reemplazo si aplica) ----
            var mascotasGrp = mascotas.Where(r => Coincide(r.Row)).ToList();
            if (await ReemplazarSiAplicaAsync("MASCOTAS", mascotasGrp.Count > 0, reemplazarDependientes,
                    c => _db.UnidadMascotas.ExecuteDeleteAsync(c), errores, ct))
            foreach (var (row, fila) in mascotasGrp)
            {
                try
                {
                    _db.ChangeTracker.Clear();
                    var uid = ResolverUnidad(Val(row, "UNIDAD PRIVADA"), numeroToId, idxCodigo);
                    if (uid == Guid.Empty) { errores.Add(new("MASCOTAS", fila, $"Unidad '{Val(row, "UNIDAD PRIVADA")}' no encontrada")); continue; }
                    var nom = NullIfEmpty(Val(row, "NOMBRE")) ?? "Mascota";
                    // Antes se descartaba el DTO devuelto; su Id es la clave de los campos dinamicos.
                    var creadaMas = await _mi.AgregarMascotaUnidadAsync(uid, new CrearUnidadMascotaRequest(
                        nom, ParseEnum(Val(row, "TIPO MASCOTA"), TipoMascota.Perro), NullIfEmpty(Val(row, "RAZA"))), ct);
                    nMas++;
                    if (creadaMas is not null)
                        await EscribirDinamicosAsync("MASCOTAS", row, defsMas,
                            (d, v) => _mi.SetCampoValorMascotaDefAsync(creadaMas.Id, d, new SetCampoValorRequest(v), ct),
                            errores, avisados);
                }
                catch (Exception ex) { errores.Add(new("MASCOTAS", fila, Fallo(ex))); }
            }

            // ---- Zonas comunes (reemplazo si aplica) ----
            var zonasGrp = zonas.Where(r => Coincide(r.Row)).ToList();
            if (await ReemplazarSiAplicaAsync("ZONAS COMUNES", zonasGrp.Count > 0, reemplazarDependientes,
                    c => _db.ZonasComunes.ExecuteDeleteAsync(c), errores, ct))
            foreach (var (row, fila) in zonasGrp)
            {
                try
                {
                    _db.ChangeTracker.Clear();
                    var nom = Val(row, "NOMBRE").Trim();
                    if (nom.Length == 0) { errores.Add(new("ZONAS COMUNES", fila, "Falta NOMBRE")); continue; }
                    var req = new CrearZonaComunRequest(
                        nom, ParseEnum(Val(row, "CATEGORIA"), CategoriaZonaComun.Otros),
                        NullIfEmpty(Val(row, "DESCRIPCION")), ParseSiNo(Val(row, "RESERVABLE")),
                        ParseDecimalNull(Val(row, "TARIFA RESERVA")), ParseIntNull(Val(row, "AFORO")),
                        null, NullIfEmpty(Val(row, "REGLAS DE USO")));
                    var creada = await _mi.CrearZonaComunAsync(req, ct);
                    var est = ParseEnum(Val(row, "ESTADO"), EstadoZonaComunMantenimiento.Activa);
                    if (est != EstadoZonaComunMantenimiento.Activa)
                        await _mi.CambiarEstadoZonaAsync(creada.Id, new CambiarEstadoZonaRequest(est), ct);
                    await EscribirDinamicosAsync("ZONAS COMUNES", row, defsZon,
                        (d, v) => _mi.SetCampoValorZonaDefAsync(creada.Id, d, new SetCampoValorRequest(v), ct),
                        errores, avisados);
                    nZon++;
                }
                catch (Exception ex) { errores.Add(new("ZONAS COMUNES", fila, Fallo(ex))); }
            }

            // ---- Equipos y activos (reemplazo si aplica) ----
            var equiposGrp = equipos.Where(r => Coincide(r.Row)).ToList();
            if (await ReemplazarSiAplicaAsync("EQUIPOS", equiposGrp.Count > 0, reemplazarDependientes,
                    c => _db.EquiposActivos.ExecuteDeleteAsync(c), errores, ct))
            foreach (var (row, fila) in equiposGrp)
            {
                try
                {
                    _db.ChangeTracker.Clear();
                    var nom = Val(row, "NOMBRE").Trim();
                    if (nom.Length == 0) { errores.Add(new("EQUIPOS", fila, "Falta NOMBRE")); continue; }
                    var cat = ParseEnum(Val(row, "CATEGORIA"), CategoriaEquipo.Otros);
                    var tipo = ParseEnum(Val(row, "TIPO"), TipoElemento.Equipo);
                    var cant = Math.Max(1, ParseIntNull(Val(row, "CANTIDAD")) ?? 1);
                    var reservable = ParseSiNo(Val(row, "RESERVABLE"));
                    var creado = await _mi.CrearEquipoAsync(new CrearEquipoActivoRequest(nom, cat, tipo, cant, reservable), ct);

                    // Ficha completa (solo si viene algun dato adicional).
                    var modelo = NullIfEmpty(Val(row, "MODELO"));
                    var serie = NullIfEmpty(Val(row, "NUMERO DE SERIE"));
                    var ubic = NullIfEmpty(Val(row, "UBICACION"));
                    var obs = NullIfEmpty(Val(row, "OBSERVACIONES"));
                    var vida = ParseIntNull(Val(row, "VIDA UTIL"));
                    var valor = ParseDecimalNull(Val(row, "VALOR ADQUISICION"));
                    var prov = NullIfEmpty(Val(row, "PROVEEDOR"));
                    var fact = NullIfEmpty(Val(row, "NUMERO FACTURA"));
                    if (modelo != null || serie != null || ubic != null || obs != null || vida != null || valor != null || prov != null || fact != null)
                        await _mi.ActualizarEquipoAsync(creado.Id, new ActualizarEquipoActivoRequest(
                            nom, cat, tipo, cant, reservable, modelo, serie, null, null, ubic, obs, vida, null, valor, prov, fact), ct);

                    var est = ParseEnum(Val(row, "ESTADO"), EstadoEquipoActivo.Operativo);
                    if (est != EstadoEquipoActivo.Operativo)
                        await _mi.CambiarEstadoEquipoAsync(creado.Id, new CambiarEstadoEquipoRequest(est), ct);
                    await EscribirDinamicosAsync("EQUIPOS", row, defsEqu,
                        (d, v) => _mi.SetCampoValorEquipoDefAsync(creado.Id, d, new SetCampoValorRequest(v), ct),
                        errores, avisados);
                    nEqu++;
                }
                catch (Exception ex) { errores.Add(new("EQUIPOS", fila, Fallo(ex))); }
            }
        }

        // ---- Terceros (Directorio) ----
        // Un tercero NO se relaciona con una unidad; solo con la copropiedad. La Persona/Empresa es
        // GLOBAL (dedup por documento/NIT) y se ASEGURA un vinculo en cada copropiedad destino
        // ("Todas las copropiedades" -> todas las del cliente). Las EMPRESAS se cargan por LOTES (NIT
        // es la unica restriccion unica, no hay email unico), evitando miles de round-trips. Las
        // PERSONAS van por fila porque su email es UNICO y un lote fallaria entero con un duplicado.
        {
            // 1) Parsear filas validas.
            var filasTer = new List<(int Fila, List<Guid> Destinos, bool EsEmpresa, string Doc, TipoDocumento TipoDoc, string Nombre, string? Email, string? Tel)>();
            foreach (var (row, fila) in terceros)
            {
                var cop = Val(row, "COPROPIEDAD").Trim();
                List<Guid> destinos;
                if (forzarTenantActual && _tenant.CurrentTenantId is { } a1) destinos = new() { a1 };
                else if (cop.Length == 0 && _tenant.CurrentTenantId is { } a2) destinos = new() { a2 };
                else if (cop.Length == 0) { errores.Add(new("TERCEROS", fila, "Falta COPROPIEDAD")); continue; }
                else if (Eq(cop, UnidadesPlantillaService.TodasLasCopropiedades)) destinos = copros.Values.Distinct().ToList();
                else if (copros.TryGetValue(cop.ToLowerInvariant(), out var tidc)) destinos = new() { tidc };
                else { errores.Add(new("TERCEROS", fila, $"Copropiedad '{cop}' no existe o no la administras.")); continue; }
                if (destinos.Count == 0) { errores.Add(new("TERCEROS", fila, "No hay copropiedades destino.")); continue; }
                var doc = Val(row, "IDENTIFICACION").Trim();
                if (doc.Length == 0) { errores.Add(new("TERCEROS", fila, "Falta IDENTIFICACION")); continue; }
                var tipoId = Val(row, "TIPO ID").Trim();
                filasTer.Add((fila, destinos, string.Equals(tipoId, "NIT", StringComparison.OrdinalIgnoreCase),
                    doc, ParseTipoDocumento(tipoId), Val(row, "NOMBRE").Trim(),
                    NullIfEmpty(Val(row, "EMAIL")), NullIfEmpty(Val(row, "TELEFONO"))));
            }

            // 2) EMPRESAS por LOTES (dedup por NIT).
            var empRows = filasTer.Where(f => f.EsEmpresa).ToList();
            if (empRows.Count > 0)
            {
                try
                {
                    static string NitDe(string d) => d.Replace(".", "").Replace("-", "").Trim();
                    var nits = empRows.Select(f => NitDe(f.Doc)).Where(n => n.Length > 0).Distinct().ToList();
                    var empPorNit = await _db.Empresas.IgnoreQueryFilters()
                        .Where(e => nits.Contains(e.Nit)).Select(e => new { e.Nit, e.Id })
                        .ToDictionaryAsync(x => x.Nit, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);
                    var nuevasEmp = new List<Empresa>();
                    foreach (var f in empRows)
                    {
                        var nit = NitDe(f.Doc);
                        if (nit.Length == 0 || empPorNit.ContainsKey(nit)) continue;
                        var e = new Empresa
                        {
                            Nit = nit,
                            RazonSocial = string.IsNullOrWhiteSpace(f.Nombre) ? nit : f.Nombre,
                            Email = f.Email, Telefono = f.Tel,
                            EstadoDirectorio = EstadoDirectorio.Activo,
                            PerfilIncompleto = string.IsNullOrEmpty(f.Email)
                        };
                        empPorNit[nit] = e.Id; nuevasEmp.Add(e);
                    }
                    if (nuevasEmp.Count > 0) { _db.Empresas.AddRange(nuevasEmp); await _db.SaveChangesAsync(ct); }

                    // Vinculos por copropiedad destino, en lote.
                    var empPorTenant = new Dictionary<Guid, HashSet<Guid>>();
                    foreach (var f in empRows)
                    {
                        var nit = NitDe(f.Doc);
                        if (!empPorNit.TryGetValue(nit, out var id)) continue;
                        foreach (var tid in f.Destinos)
                        {
                            if (!empPorTenant.TryGetValue(tid, out var set)) { set = new(); empPorTenant[tid] = set; }
                            set.Add(id);
                        }
                    }
                    foreach (var (tid, ids) in empPorTenant)
                    {
                        await SetTenantSqlAsync(tid, ct);
                        var idList = ids.ToList();
                        var yaVinc = (await _db.DirectorioVinculos
                            .Where(v => v.EntidadTipo == EntidadDirectorio.Empresa && v.Estado == EstadoVinculo.Activo && idList.Contains(v.EntidadId))
                            .Select(v => v.EntidadId).ToListAsync(ct)).ToHashSet();
                        var nuevosV = ids.Where(id => !yaVinc.Contains(id)).Select(id => new DirectorioVinculo
                        {
                            EntidadTipo = EntidadDirectorio.Empresa, EntidadId = id,
                            FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow), Estado = EstadoVinculo.Activo
                        }).ToList();
                        if (nuevosV.Count > 0) { _db.DirectorioVinculos.AddRange(nuevosV); await _db.SaveChangesAsync(ct); }
                    }
                    nTer += empRows.Count;
                }
                catch (Exception ex) { errores.Add(new("TERCEROS", 0, "Empresas: " + Fallo(ex))); }
            }

            // 3) PERSONAS (tercero natural) por fila — su email es unico.
            foreach (var f in filasTer.Where(f => !f.EsEmpresa))
            {
                try
                {
                    await SetTenantSqlAsync(f.Destinos[0], ct);
                    var (nombres, apellidos) = SplitNombre(f.Nombre);
                    if (apellidos.Length == 0) apellidos = "-";
                    var per = await _dir.BuscarPersonaPorDocumentoAsync(new BuscarPorDocumentoRequest(f.TipoDoc, f.Doc), ct);
                    var perId = per?.Id ?? (await _dir.CrearPersonaAsync(new CrearPersonaRequest(
                        f.TipoDoc, f.Doc, string.IsNullOrWhiteSpace(nombres) ? f.Doc : nombres, apellidos, f.Email, f.Tel, null, null), ct)).Id;
                    foreach (var tid in f.Destinos)
                    {
                        await SetTenantSqlAsync(tid, ct);
                        await VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, perId, ct);
                    }
                    nTer++;
                }
                catch (Exception ex) { errores.Add(new("TERCEROS", f.Fila, Fallo(ex))); }
            }
        }

        // Restaura el contexto de tenant original de la sesion.
        if (tenantOriginal is { } to) await SetTenantSqlAsync(to, ct);
        return new ResultadoCargaUnidades(nCopro, nUni, nAnexo, nPer, nVeh, nMas, nZon, nEqu, errores, nUniAct, nTer);
    }

    // Ultimo tenant fijado en la sesion SQL. Evita repetir el set_config (un round-trip) cuando ya
    // estamos en ese tenant: en archivos de una sola copropiedad esto ahorra miles de round-trips.
    private Guid? _ultimoTenantSql;

    // Fija el tenant en EF (ITenantContext, para HasQueryFilter + TenantId al guardar) y en la
    // sesion SQL (app.tenant_id, para que RLS acepte los INSERT del tenant destino).
    private async Task SetTenantSqlAsync(Guid tenantId, CancellationToken ct)
    {
        _tenant.SetTenant(tenantId);
        var conn = _db.Database.GetDbConnection();
        var abierta = conn.State == System.Data.ConnectionState.Open;
        // Salta el set_config solo si seguimos en el MISMO tenant Y la conexion sigue viva (el
        // set_config es a nivel de sesion: si la conexion se cerro, app.tenant_id se perdio).
        if (abierta && _ultimoTenantSql == tenantId) return;
        if (!abierta) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @t, false)";
        var p = cmd.CreateParameter(); p.ParameterName = "@t"; p.Value = tenantId.ToString(); cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
        _ultimoTenantSql = tenantId;
    }

    // ===================== Helpers =====================
    // Reemplazo de una categoria dependiente (personas/vehiculos/mascotas/zonas/equipos): si el archivo
    // trae filas de esa hoja para la copropiedad y el usuario confirmo el reemplazo, borra primero las
    // existentes del tenant (RLS + filtro EF ya acotan) y luego se recargan. Si el borrado falla (p.ej.
    // una zona comun con reservas: FK RESTRICT), reporta el motivo y OMITE la carga de esa hoja para no
    // duplicar ni dejar la copropiedad a medias. Las UNIDADES nunca pasan por aqui (se hace upsert).
    private async Task<bool> ReemplazarSiAplicaAsync(string hoja, bool hayFilas, bool reemplazar,
        Func<CancellationToken, Task<int>> borrar, List<CargaUnidadesError> errores, CancellationToken ct)
    {
        if (!reemplazar || !hayFilas) return true;
        try { await borrar(ct); return true; }
        catch (Exception ex)
        {
            errores.Add(new(hoja, 0, "No se pudo reemplazar (se omitio la carga de esta hoja): " + Fallo(ex)));
            return false;
        }
    }

    // Resuelve una referencia de unidad de la plantilla (columna UNIDAD PRIVADA). La plantilla pide el
    // "Codigo de la unidad" (TORRE-NUMERO, ej. B-101), pero las unidades guardan Numero suelto ("101") con
    // su torre aparte. Por eso el indice mapea AMBAS claves: el Numero crudo y el codigo TORRE-NUMERO.
    // Prioridad: 1) unidades creadas en esta misma carga (numeroToId), 2) indice de existentes.
    private static Guid ResolverUnidad(string numero, Dictionary<string, Guid> numeroToId, Dictionary<string, Guid> idxCodigo)
    {
        numero = numero.Trim();
        if (numero.Length == 0) return Guid.Empty;
        if (numeroToId.TryGetValue(numero, out var id)) return id;
        if (idxCodigo.TryGetValue(numero, out var id2)) return id2;
        return Guid.Empty;
    }

    // Indice de las unidades YA existentes del tenant activo (RLS ya acota). Cada unidad se indexa por su
    // Numero crudo y por su codigo TORRE-NUMERO (mismo calculo que Residentes/Distribucion), sin pisar un
    // match por Numero (el match exacto tiene prioridad).
    private async Task<Dictionary<string, Guid>> BuildUnidadIndexAsync(CancellationToken ct)
    {
        var idx = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var unis = await _db.UnidadesPrivadas.AsNoTracking()
            .Select(u => new { u.Id, u.Numero, Torre = u.Torre != null ? u.Torre.Nombre : null })
            .ToListAsync(ct);
        foreach (var u in unis)
        {
            var n = (u.Numero ?? "").Trim();
            if (n.Length > 0) idx[n] = u.Id;   // 1a pasada: Numero crudo
        }
        foreach (var u in unis)
        {
            var n = (u.Numero ?? "").Trim();
            if (n.Length == 0) continue;
            var torreShort = string.IsNullOrWhiteSpace(u.Torre) ? "" : u.Torre!.Split(' ').Last();
            if (torreShort.Length == 0) continue;
            idx.TryAdd($"{torreShort}-{n}", u.Id);   // 2a pasada: codigo TORRE-NUMERO, sin pisar
        }
        return idx;
    }

    // ===================== Campos dinamicos ([Label]) =====================
    // La plantilla emite una columna por cada campo dinamico del catalogo de la copropiedad, con el
    // encabezado ENTRE CORCHETES ("[N de medidor]"). LeerHoja normaliza los encabezados a MAYUSCULAS,
    // asi que esa columna llega como clave "[N DE MEDIDOR]": el label interior se resuelve contra el
    // catalogo con Trim e ignorando mayusculas/minusculas.

    // Catalogo vacio (la hoja no trae ninguna columna entre corchetes): evita consultar definiciones.
    private static readonly Dictionary<string, Guid> SinCampos = new(StringComparer.OrdinalIgnoreCase);

    // Devuelve el label interior de un encabezado entre corchetes ("[N DE MEDIDOR]" -> "N DE MEDIDOR")
    // o null si el encabezado no es una columna de campo dinamico.
    private static string? LabelEntreCorchetes(string header)
    {
        var h = (header ?? "").Trim();
        if (h.Length < 3 || h[0] != '[' || h[^1] != ']') return null;
        var label = h[1..^1].Trim();
        return label.Length == 0 ? null : label;
    }

    // Labels dinamicos presentes en una hoja. Los encabezados son los MISMOS para todas sus filas
    // (LeerHoja construye cada diccionario desde el mismo mapa), asi que basta inspeccionar una.
    private static List<string> LabelsDinamicos(List<(Dictionary<string, string> Row, int Fila)> filas)
    {
        var res = new List<string>();
        if (filas.Count == 0) return res;
        foreach (var clave in filas[0].Row.Keys)
        {
            var lbl = LabelEntreCorchetes(clave);
            if (lbl is not null) res.Add(lbl);
        }
        return res;
    }

    // Catalogo de definiciones de la copropiedad ACTIVA como label -> definicionId (case-insensitive).
    // TryAdd para tolerar labels duplicados por mayusculas/minusculas (gana el de menor Orden).
    private static Dictionary<string, Guid> Catalogo<T>(IReadOnlyList<T> defs, Func<T, Guid> id, Func<T, string> label)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in defs) map.TryAdd((label(d) ?? "").Trim(), id(d));
        return map;
    }

    // Guarda los campos dinamicos de UNA fila. Recorre solo las claves entre corchetes, resuelve el
    // label contra el catalogo de la entidad y delega en el SetCampoValor... correspondiente (upsert
    // por (definicion, registro), asi que recargar la misma plantilla deja el mismo resultado).
    // Celda VACIA: no se escribe nada (una recarga conserva el valor anterior, igual que hacen
    // MATRICULA/REF PAGO en unidades). Columna [X] sin campo configurado en la copropiedad: se
    // reporta como AVISO una sola vez por hoja+columna (nunca por fila) y el resto de la fila SI se
    // carga; asi el dato deja de descartarse en silencio.
    private static async Task EscribirDinamicosAsync(
        string hoja, Dictionary<string, string> row, Dictionary<string, Guid> defs,
        Func<Guid, string, Task> set, List<CargaUnidadesError> errores, HashSet<string> avisados)
    {
        foreach (var (clave, valor) in row)
        {
            var label = LabelEntreCorchetes(clave);
            if (label is null) continue;
            if (!defs.TryGetValue(label, out var definicionId))
            {
                if (avisados.Add(hoja + "|" + label))
                    errores.Add(new(hoja, 0, $"Aviso: la columna '[{label}]' no corresponde a ningun campo " +
                        "configurado de esta hoja en la copropiedad, asi que ese dato se ignoro. " +
                        "El resto de los datos de las filas SI se cargo."));
                continue;
            }
            if (string.IsNullOrWhiteSpace(valor)) continue;
            await set(definicionId, valor.Trim());
        }
    }

    private static List<(Dictionary<string, string> Row, int Fila)> LeerHoja(XLWorkbook wb, string nombre)
    {
        var res = new List<(Dictionary<string, string>, int)>();
        if (!wb.TryGetWorksheet(nombre, out var ws)) return res;
        var headers = new Dictionary<int, string>();
        foreach (var cell in ws.Row(2).CellsUsed())   // fila 1 = banner, fila 2 = encabezados
            headers[cell.Address.ColumnNumber] = cell.GetString().Trim().ToUpperInvariant();
        var last = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = 4; r <= last; r++)   // fila 3 = ayuda, datos desde la 4
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var any = false;
            foreach (var (col, h) in headers)
            {
                var v = ws.Cell(r, col).GetString().Trim();
                dict[h] = v;
                if (v.Length > 0) any = true;
            }
            if (!any) continue;
            // Ignora la fila de ejemplo de la plantilla (COPROPIEDAD = "EJEMPLO (borrar fila)").
            var cop = dict.TryGetValue("COPROPIEDAD", out var cc) ? cc.TrimStart() : "";
            if (cop.StartsWith("EJEMPLO", StringComparison.OrdinalIgnoreCase)) continue;
            res.Add((dict, r));
        }
        return res;
    }

    private static string Val(Dictionary<string, string> row, string header)
        => row.TryGetValue(header, out var v) ? v : "";

    private static bool Eq(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static decimal ParseDecimal(string s)
    {
        s = (s ?? "").Trim().Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static decimal? ParseDecimalNull(string s)
    {
        s = (s ?? "").Trim().Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
    }

    private static int? ParseIntNull(string s)
    {
        s = (s ?? "").Trim();
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : (int?)null;
    }

    // "Si"/"No" (o Si/1/true) -> bool. Reservable en zonas/equipos.
    private static bool ParseSiNo(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s is "si" or "sí" or "1" or "true" or "x" or "verdadero";
    }

    // Igual, pero celda VACIA -> null: la columna no vino o el usuario la dejo en blanco, asi que
    // no se pisa el valor que ya tiene la unidad (PAGA ADMIN nace en true al crear).
    private static bool? ParseSiNoNull(string s)
        => string.IsNullOrWhiteSpace(s) ? null : ParseSiNo(s);

    private static (string Nombres, string Apellidos) SplitNombre(string nombre)
    {
        nombre = (nombre ?? "").Trim();
        if (nombre.Length == 0) return ("Sin nombre", "");
        var parts = nombre.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "");
    }

    // Mapea el TIPO ID de la plantilla de terceros (CC/CE/Pasaporte/NIT/Otro) al enum TipoDocumento.
    private static TipoDocumento ParseTipoDocumento(string s) => (s ?? "").Trim().ToUpperInvariant() switch
    {
        "CC" => TipoDocumento.CC,
        "CE" => TipoDocumento.CE,
        "PASAPORTE" or "PA" => TipoDocumento.PA,
        "TI" => TipoDocumento.TI,
        "NIT" => TipoDocumento.NIT,
        _ => TipoDocumento.CC   // "Otro" y desconocidos -> CC por defecto
    };

    private static TEnum ParseEnum<TEnum>(string s, TEnum fallback) where TEnum : struct, Enum
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return fallback;
        foreach (var name in Enum.GetNames<TEnum>())
            if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase)) return Enum.Parse<TEnum>(name);
        return fallback;
    }

    // Registra el fallo de una fila: descarta la entidad fallida del ChangeTracker para NO contaminar
    // las filas siguientes de la misma copropiedad (un SaveChanges fallido deja la entidad "pegada"),
    // y devuelve un motivo legible.
    private string Fallo(Exception ex)
    {
        try { _db.ChangeTracker.Clear(); } catch { /* best-effort */ }
        return Msg(ex);
    }

    // Traduce la excepcion a un motivo legible (recorre inner exceptions por errores de BD comunes).
    private static string Msg(Exception ex)
    {
        if (ex is InvalidOperationException) return ex.Message;
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var m = e.Message ?? string.Empty;
            if (m.Contains("IX_personas_email")) return "Ya existe otra persona con ese EMAIL (debe ser unico).";
            if (m.Contains("IX_personas_documento") || m.Contains("_documento")) return "Ya existe otra persona con ese DOCUMENTO.";
            if (m.Contains("vehiculos") && m.Contains("placa")) return "Ya existe un vehiculo con esa PLACA.";
            if (m.Contains("duplicate key")) return "Registro duplicado (ya existe).";
            if (m.Contains("foreign key") || m.Contains("violates foreign key")) return "Referencia invalida (un dato relacionado no existe).";
        }
        return "No se pudo procesar: " + (ex.InnerException?.Message ?? ex.Message);
    }

    private async Task<Dictionary<string, Guid>> CopropiedadesDelClienteAsync(CancellationToken ct)
    {
        var personaId = Guid.TryParse(_http.HttpContext?.User?.FindFirst("persona_id")?.Value, out var pid) ? pid : (Guid?)null;
        var ids = new List<Guid>();
        if (personaId is not null)
        {
            var conn = _db.Database.GetDbConnection();
            var abierta = conn.State != System.Data.ConnectionState.Open;
            if (abierta) await conn.OpenAsync(ct);
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT tenant_id FROM get_tenants_for_persona(@p)";
                var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = personaId.Value; cmd.Parameters.Add(p);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0));
            }
            finally { if (abierta) await conn.CloseAsync(); }
        }
        if (ids.Count == 0 && _tenant.CurrentTenantId is { } curr) ids.Add(curr);

        var lista = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.Nombre }).ToListAsync(ct);
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in lista) map[t.Nombre.Trim().ToLowerInvariant()] = t.Id;
        return map;
    }
}
