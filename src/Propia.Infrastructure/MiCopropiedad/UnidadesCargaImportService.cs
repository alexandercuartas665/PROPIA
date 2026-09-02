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
                    var tipo = ParseEnum(Val(row, "TIPO"), TipoUnidad.Apartamento);
                    var coef = ParseDecimal(Val(row, "COEFICIENTE"));
                    var matricula = NullIfEmpty(Val(row, "MATRICULA"));
                    var refPago = NullIfEmpty(Val(row, "REF PAGO"));

                    // Modo MODULO (recarga desde Unidades Privadas): si la unidad ya existe (por su
                    // numero exacto), se ACTUALIZA en vez de crear. Solo se pisan los campos que trae la
                    // plantilla (tipo, coeficiente, matricula, ref pago); torre, piso, area, etc. se
                    // conservan. Modo ONBOARDING (todas): siempre crea (la copropiedad es nueva).
                    var existente = todas
                        ? null
                        : await _db.UnidadesPrivadas.FirstOrDefaultAsync(x => x.Numero == numero, ct);
                    if (existente is not null)
                    {
                        var upd = new ActualizarUnidadRequest(
                            existente.Numero, tipo, existente.TorreId, existente.Piso,
                            coef, existente.AreaM2, existente.Habitaciones, existente.Banos, existente.Parqueaderos,
                            existente.Estado, existente.Observaciones,
                            matricula ?? existente.MatriculaInmobiliaria, existente.PagaAdministracion,
                            existente.CuotaMensual, refPago ?? existente.ReferenciaPago);
                        await _mi.ActualizarUnidadAsync(existente.Id, upd, ct);
                        numeroToId[numero] = existente.Id;
                        nUniAct++;
                    }
                    else
                    {
                        var req = new CrearUnidadRequest(
                            numero, tipo, null, null,
                            coef, null, null, null, null, null, null,
                            matricula, true, null, refPago);
                        var creada = await _mi.CrearUnidadAsync(req, ct);
                        numeroToId[numero] = creada.Id;
                        nUni++;
                    }

                    var agr = Val(row, "AGRUPACION").Trim();
                    var principal = Val(row, "PRINCIPAL").Trim();
                    if (agr.StartsWith("3") && principal.Length > 0)
                        anexosPend.Add((fila, principal, numero));
                }
                catch (Exception ex) { errores.Add(new("UNIDADES PRIVADAS", fila, Fallo(ex))); }
            }

            // ---- Anexos (2a pasada: ya existen ambas unidades) ----
            foreach (var (fila, principal, asociada) in anexosPend)
            {
                try
                {
                    _db.ChangeTracker.Clear();
                    var pid = numeroToId.GetValueOrDefault(principal);
                    if (pid == Guid.Empty) idxCodigo.TryGetValue(principal, out pid);
                    var aid = numeroToId.GetValueOrDefault(asociada);
                    if (pid == Guid.Empty || aid == Guid.Empty)
                    {
                        errores.Add(new("UNIDADES PRIVADAS", fila, $"Anexo: no se encontro la principal '{principal}'.")); continue;
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
                    await _mi.AgregarPersonaUnidadAsync(uid, req, ct);
                    nPer++;
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
                var placasVistas = (await _db.UnidadPlacas.AsNoTracking()
                        .Select(p => new { p.UnidadId, p.Placa }).ToListAsync(ct))
                    .Select(p => p.UnidadId + "|" + p.Placa).ToHashSet();
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
                        if (placasVistas.Add(uid + "|" + placaUp))
                        {
                            _db.UnidadPlacas.Add(new UnidadPlaca { UnidadId = uid, Placa = placaUp, TipoVehiculo = tipoVeh });
                            await _db.SaveChangesAsync(ct);
                        }
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
                    await _mi.AgregarMascotaUnidadAsync(uid, new CrearUnidadMascotaRequest(
                        nom, ParseEnum(Val(row, "TIPO MASCOTA"), TipoMascota.Perro), NullIfEmpty(Val(row, "RAZA"))), ct);
                    nMas++;
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
