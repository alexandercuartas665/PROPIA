using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

// Particion de MiCopropiedadService extraida por area tematica (mismo comportamiento,
// clase parcial: comparte _db/_tenant/_blob/_seed/_dir del constructor principal).
public partial class MiCopropiedadService
{
    // Resumen + Seccion 1 Identidad + Seccion 2 Distribucion + vinculos, personas vinculadas, campos y documentos de unidad.
    // ----------------------------- Resumen -----------------------------

    public async Task<ResumenMiCopropiedadDto?> GetResumenAsync(Guid tenantId, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        if (t is null) return null;

        var torres = await _db.Torres.CountAsync(ct);
        var unidades = await _db.UnidadesPrivadas.CountAsync(ct);
        var coefSum = await _db.UnidadesPrivadas.SumAsync(u => (decimal?)u.CoeficientePropiedad, ct) ?? 0;
        var zonas = await _db.ZonasComunes.CountAsync(ct);
        var equipos = await _db.EquiposActivos.CountAsync(ct);
        var contratos = await _db.ContratosServicio.CountAsync(ct);
        var miembros = await _db.MiembrosConsejo.CountAsync(c => c.Activo, ct);
        var miembrosEquipoActivos = await _db.MiembrosEquipo.CountAsync(m => m.Activo, ct);
        var comitesActivos = await _db.Comites.CountAsync(c => c.Activo, ct);
        var hayRevisorFiscal = await _db.RevisoresFiscales.AnyAsync(r => r.Activo, ct);

        // Heuristicas de completitud por seccion (spec 2.3 v1.0)
        var completas = new Dictionary<string, bool>
        {
            ["Identidad"] = !string.IsNullOrWhiteSpace(t.Nombre)
                            && !string.IsNullOrWhiteSpace(t.Nit)
                            && !string.IsNullOrWhiteSpace(t.Direccion)
                            && t.TipoCopropiedad.HasValue,
            ["Distribucion"] = torres > 0 && unidades > 0 && Math.Abs(coefSum - 100m) <= 1m,
            ["EquipoTrabajo"] = miembrosEquipoActivos > 0,
            // Gobierno completo: consejo con minimo 3 activos + revisor fiscal (si la PH lo requiere) + al menos un comite
            ["Gobierno"] = miembros >= 3
                           && (unidades <= 30 || hayRevisorFiscal)
                           && comitesActivos > 0,
            ["Servicios"] = contratos > 0,
            ["ZonasComunes"] = zonas > 0,
            ["Equipos"] = equipos > 0,
            ["Finanzas"] = t.FinanzasConfiguradas  // el admin guardo los parametros de la seccion 8
        };
        var pct = (int)(completas.Values.Count(b => b) * 100.0 / completas.Count);

        return new ResumenMiCopropiedadDto(
            ToIdentidadDto(t),
            torres, unidades, coefSum, zonas, equipos, contratos, miembros,
            pct, completas);
    }

    private IdentidadDto ToIdentidadDto(Tenant t) =>
        new(t.Id, t.Nombre, t.Nit, t.DigitoVerificacion,
            t.Direccion, t.Ciudad, t.Departamento,
            t.CodigoPropia, t.TipoCopropiedad, t.Estrato,
            _blob.ResolveUrl(t.FotoFachadaUrl), _blob.ResolveUrl(t.LogoUrl), t.Descripcion,
            t.NumeroReglamentoPh, t.NotariaRegistro,
            t.MatriculaInmobiliaria, t.LicenciaConstruccion,
            t.FechaConstitucion,
            t.LabelAgrupacion, t.LabelPiso,
            t.TelefonoContacto, t.EmailContacto,
            t.Pais,
            t.CertificadoMayorExtension);

    // ----------------------------- Seccion 1: Identidad -----------------------------

    public async Task<IdentidadDto?> ActualizarIdentidadAsync(Guid tenantId, ActualizarIdentidadRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        if (t is null) return null;
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la copropiedad es obligatorio.");

        t.Nombre = req.Nombre;
        t.Nit = req.Nit;
        t.DigitoVerificacion = req.DigitoVerificacion;
        t.Direccion = req.Direccion;
        t.Ciudad = req.Ciudad;
        t.Departamento = req.Departamento;
        t.Pais = string.IsNullOrWhiteSpace(req.Pais) ? null : req.Pais.Trim();
        t.TipoCopropiedad = req.Tipo;
        t.Estrato = req.Estrato;
        t.FotoFachadaUrl = req.FotoFachadaUrl;
        t.LogoUrl = req.LogoUrl;
        t.Descripcion = req.Descripcion;
        // Identidad registral
        t.NumeroReglamentoPh = req.NumeroReglamentoPh;
        t.NotariaRegistro = req.NotariaRegistro;
        t.MatriculaInmobiliaria = req.MatriculaInmobiliaria;
        t.LicenciaConstruccion = req.LicenciaConstruccion;
        t.FechaConstitucion = req.FechaConstitucion;
        t.CertificadoMayorExtension = string.IsNullOrWhiteSpace(req.CertificadoMayorExtension) ? null : req.CertificadoMayorExtension.Trim();
        // Labels personalizables
        t.LabelAgrupacion = string.IsNullOrWhiteSpace(req.LabelAgrupacion) ? null : req.LabelAgrupacion.Trim();
        t.LabelPiso = string.IsNullOrWhiteSpace(req.LabelPiso) ? null : req.LabelPiso.Trim();
        t.TelefonoContacto = string.IsNullOrWhiteSpace(req.TelefonoContacto) ? null : req.TelefonoContacto.Trim();
        t.EmailContacto = string.IsNullOrWhiteSpace(req.EmailContacto) ? null : req.EmailContacto.Trim();
        await _db.SaveChangesAsync(ct);

        return ToIdentidadDto(t);
    }

    // ----------------------------- Seccion 2: Distribucion -----------------------------

    public async Task<IReadOnlyList<TorreDto>> ListTorresAsync(CancellationToken ct)
    {
        return await _db.Torres
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .Select(t => new TorreDto(t.Id, t.Nombre, t.CantidadPisos, t.Descripcion,
                _db.UnidadesPrivadas.Count(u => u.TorreId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<TorreDto> CrearTorreAsync(CrearTorreRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la torre es obligatorio.");
        var torre = new Torre { Nombre = req.Nombre, CantidadPisos = req.CantidadPisos, Descripcion = req.Descripcion };
        _db.Torres.Add(torre);
        await _db.SaveChangesAsync(ct);
        return new TorreDto(torre.Id, torre.Nombre, torre.CantidadPisos, torre.Descripcion, 0);
    }

    public async Task<bool> EliminarTorreAsync(Guid torreId, CancellationToken ct)
    {
        var t = await _db.Torres.FirstOrDefaultAsync(x => x.Id == torreId, ct);
        if (t is null) return false;
        // Guarda: no eliminar una torre/bloque que aun tiene unidades (evita orfanarlas via SetNull).
        var nUnidades = await _db.UnidadesPrivadas.CountAsync(u => u.TorreId == torreId, ct);
        if (nUnidades > 0)
            throw new InvalidOperationException($"No se puede eliminar: tiene {nUnidades} unidad(es) asignada(s). Elimina o reasigna las unidades primero.");
        _db.Torres.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<UnidadDto>> ListUnidadesAsync(CancellationToken ct)
    {
        // El propietario se resuelve con subconsultas dentro del mismo query (no con una llamada a
        // las personas por unidad): la tabla de Distribucion lista todas las unidades de la
        // copropiedad y pedirlas una a una seria N+1. Si hay varios propietarios se devuelve el
        // primero + el conteo, y la UI decide como mostrarlo.
        return await _db.UnidadesPrivadas
            .AsNoTracking()
            .Include(u => u.Torre)
            .OrderBy(u => u.Torre!.Nombre).ThenBy(u => u.Numero)
            .Select(u => new UnidadDto(
                u.Id, u.Numero, u.Tipo,
                u.TorreId, u.Torre != null ? u.Torre.Nombre : null, u.Piso,
                u.CoeficientePropiedad, u.AreaM2,
                u.Habitaciones, u.Banos, u.Parqueaderos,
                u.Estado, u.Observaciones, u.MatriculaInmobiliaria, u.PagaAdministracion, u.CuotaMensual,
                // Nombre del primer propietario. Contempla dueno persona O empresa (juridico):
                // el join simple contra Personas dejaria fuera los apartamentos de una empresa.
                (from up in _db.UnidadPersonas
                 where up.UnidadId == u.Id && up.Rol == RolUnidadPersona.Propietario
                 orderby up.EntidadTipo, up.Id
                 select up.EntidadTipo == EntidadDirectorio.Empresa
                     ? _db.Empresas.Where(e => e.Id == up.EmpresaId).Select(e => e.RazonSocial).FirstOrDefault()
                     : _db.Personas.Where(p => p.Id == up.PersonaId).Select(p => (p.Nombres + " " + p.Apellidos).Trim()).FirstOrDefault()
                ).FirstOrDefault(),
                _db.UnidadPersonas.Count(up => up.UnidadId == u.Id && up.Rol == RolUnidadPersona.Propietario),
                // Si esta unidad es una asociada (anexo) de otra, su principal (para anidar como fila hija).
                (from v in _db.UnidadVinculos where v.UnidadAsociadaId == u.Id select (Guid?)v.UnidadPrincipalId).FirstOrDefault(),
                u.ReferenciaPago,
                u.TipoCustomId,
                u.TipoCustomId != null ? _db.TiposUnidadCustom.Where(t => t.Id == u.TipoCustomId).Select(t => t.Nombre).FirstOrDefault() : null,
                // Dentro de un Select de EF no se pueden omitir parametros opcionales (CS0854):
                // los modulos contributivos van explicitos.
                u.ModuloContributivo1, u.ModuloContributivo2, u.ModuloContributivo3,
                u.ModuloContributivo4, u.ModuloContributivo5))
            .ToListAsync(ct);
    }

    public async Task<UnidadDto> CrearUnidadAsync(CrearUnidadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Numero))
            throw new InvalidOperationException("Numero de unidad obligatorio.");
        if (req.CoeficientePropiedad < 0 || req.CoeficientePropiedad > 100)
            throw new InvalidOperationException("Coeficiente debe estar entre 0 y 100.");

        var unidad = new UnidadPrivada
        {
            Numero = req.Numero,
            Tipo = req.Tipo,
            TipoCustomId = req.TipoCustomId,
            TorreId = req.TorreId,
            Piso = req.Piso,
            CoeficientePropiedad = req.CoeficientePropiedad,
            AreaM2 = req.AreaM2,
            Habitaciones = req.Habitaciones,
            Banos = req.Banos,
            Parqueaderos = req.Parqueaderos,
            Estado = req.Estado,
            Observaciones = req.Observaciones,
            MatriculaInmobiliaria = req.MatriculaInmobiliaria,
            ReferenciaPago = req.ReferenciaPago,
            PagaAdministracion = req.PagaAdministracion,
            CuotaMensual = req.CuotaMensual,
            ModuloContributivo1 = req.ModuloContributivo1,
            ModuloContributivo2 = req.ModuloContributivo2,
            ModuloContributivo3 = req.ModuloContributivo3,
            ModuloContributivo4 = req.ModuloContributivo4,
            ModuloContributivo5 = req.ModuloContributivo5
        };
        _db.UnidadesPrivadas.Add(unidad);
        await _db.SaveChangesAsync(ct);
        var torreNombre = unidad.TorreId.HasValue
            ? await _db.Torres.Where(t => t.Id == unidad.TorreId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
            : null;
        await RegistrarBitacoraAsync("Unidad", $"Unidad '{unidad.Numero}' creada ({unidad.Tipo}, coef {unidad.CoeficientePropiedad}%).", ct, unidad.Id);
        return new UnidadDto(unidad.Id, unidad.Numero, unidad.Tipo,
            unidad.TorreId, torreNombre, unidad.Piso,
            unidad.CoeficientePropiedad, unidad.AreaM2,
            unidad.Habitaciones, unidad.Banos, unidad.Parqueaderos,
            unidad.Estado, unidad.Observaciones, unidad.MatriculaInmobiliaria, unidad.PagaAdministracion, unidad.CuotaMensual,
            TipoCustomId: unidad.TipoCustomId,
            ModuloContributivo1: unidad.ModuloContributivo1, ModuloContributivo2: unidad.ModuloContributivo2,
            ModuloContributivo3: unidad.ModuloContributivo3, ModuloContributivo4: unidad.ModuloContributivo4,
            ModuloContributivo5: unidad.ModuloContributivo5);
    }

    public async Task<UnidadDto?> ActualizarUnidadAsync(Guid unidadId, ActualizarUnidadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Numero))
            throw new InvalidOperationException("Numero de unidad obligatorio.");
        if (req.CoeficientePropiedad < 0 || req.CoeficientePropiedad > 100)
            throw new InvalidOperationException("Coeficiente debe estar entre 0 y 100.");

        var u = await _db.UnidadesPrivadas.FirstOrDefaultAsync(x => x.Id == unidadId, ct);
        if (u is null) return null;

        // Diff en lenguaje natural para la bitacora (RN-06): que cambio de esta unidad.
        var cambios = new List<string>();
        void Dif(string campo, string? antes, string? ahora)
        {
            var a = antes ?? "-"; var b = ahora ?? "-";
            if (!string.Equals(a, b, StringComparison.Ordinal)) cambios.Add($"{campo}: {a} -> {b}");
        }
        string? torreNombreDe(Guid? id) => id.HasValue
            ? _db.Torres.Where(t => t.Id == id).Select(t => t.Nombre).FirstOrDefault()
            : null;

        var numeroTrim = req.Numero.Trim();
        Dif("Numero", u.Numero, numeroTrim);
        Dif("Tipo", u.Tipo.ToString(), req.Tipo.ToString());
        if (u.TorreId != req.TorreId) Dif("Torre", torreNombreDe(u.TorreId), torreNombreDe(req.TorreId));
        Dif("Piso", u.Piso?.ToString(), req.Piso?.ToString());
        Dif("Coeficiente", u.CoeficientePropiedad.ToString("0.####"), req.CoeficientePropiedad.ToString("0.####"));
        Dif("Area", u.AreaM2?.ToString("0.##"), req.AreaM2?.ToString("0.##"));
        Dif("Habitaciones", u.Habitaciones?.ToString(), req.Habitaciones?.ToString());
        Dif("Banos", u.Banos?.ToString(), req.Banos?.ToString());
        Dif("Parqueaderos", u.Parqueaderos?.ToString(), req.Parqueaderos?.ToString());
        Dif("Estado", u.Estado, req.Estado);
        Dif("Matricula", u.MatriculaInmobiliaria, req.MatriculaInmobiliaria);
        if (u.PagaAdministracion != req.PagaAdministracion) Dif("Paga administracion", u.PagaAdministracion ? "Si" : "No", req.PagaAdministracion ? "Si" : "No");
        Dif("Cuota", u.CuotaMensual?.ToString("N0"), req.CuotaMensual?.ToString("N0"));
        Dif("Modulo contributivo 1", u.ModuloContributivo1?.ToString("0.####"), req.ModuloContributivo1?.ToString("0.####"));
        Dif("Modulo contributivo 2", u.ModuloContributivo2?.ToString("0.####"), req.ModuloContributivo2?.ToString("0.####"));
        Dif("Modulo contributivo 3", u.ModuloContributivo3?.ToString("0.####"), req.ModuloContributivo3?.ToString("0.####"));
        Dif("Modulo contributivo 4", u.ModuloContributivo4?.ToString("0.####"), req.ModuloContributivo4?.ToString("0.####"));
        Dif("Modulo contributivo 5", u.ModuloContributivo5?.ToString("0.####"), req.ModuloContributivo5?.ToString("0.####"));

        u.Numero = numeroTrim;
        u.Tipo = req.Tipo;
        u.TipoCustomId = req.TipoCustomId;
        u.TorreId = req.TorreId;
        u.Piso = req.Piso;
        u.CoeficientePropiedad = req.CoeficientePropiedad;
        u.AreaM2 = req.AreaM2;
        u.Habitaciones = req.Habitaciones;
        u.Banos = req.Banos;
        u.Parqueaderos = req.Parqueaderos;
        u.Estado = req.Estado;
        u.Observaciones = req.Observaciones;
        u.MatriculaInmobiliaria = req.MatriculaInmobiliaria;
        u.PagaAdministracion = req.PagaAdministracion;
        u.CuotaMensual = req.CuotaMensual;
        // Ficha completa: el request manda el estado deseado de los 5 modulos contributivos
        // (null = sin definir). Quien solo trae algunos campos, como el importador, resuelve
        // antes el "no pisar con null" pasando el valor existente.
        u.ModuloContributivo1 = req.ModuloContributivo1;
        u.ModuloContributivo2 = req.ModuloContributivo2;
        u.ModuloContributivo3 = req.ModuloContributivo3;
        u.ModuloContributivo4 = req.ModuloContributivo4;
        u.ModuloContributivo5 = req.ModuloContributivo5;
        if (req.ReferenciaPago is not null) u.ReferenciaPago = req.ReferenciaPago;

        await _db.SaveChangesAsync(ct);

        var torreNombre = u.TorreId.HasValue
            ? await _db.Torres.Where(t => t.Id == u.TorreId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
            : null;

        if (cambios.Count > 0)
            await RegistrarBitacoraAsync("Unidad", $"Unidad '{u.Numero}': {string.Join("; ", cambios)}.", ct, u.Id);

        return new UnidadDto(u.Id, u.Numero, u.Tipo,
            u.TorreId, torreNombre, u.Piso,
            u.CoeficientePropiedad, u.AreaM2,
            u.Habitaciones, u.Banos, u.Parqueaderos,
            u.Estado, u.Observaciones, u.MatriculaInmobiliaria, u.PagaAdministracion, u.CuotaMensual,
            TipoCustomId: u.TipoCustomId,
            ModuloContributivo1: u.ModuloContributivo1, ModuloContributivo2: u.ModuloContributivo2,
            ModuloContributivo3: u.ModuloContributivo3, ModuloContributivo4: u.ModuloContributivo4,
            ModuloContributivo5: u.ModuloContributivo5);
    }

    // ----------------------------- Vinculos entre unidades (RN-09) -----------------------------

    public async Task<IReadOnlyList<UnidadVinculoDto>> ListVinculosAsync(Guid unidadPrincipalId, CancellationToken ct)
    {
        return await _db.UnidadVinculos
            .AsNoTracking()
            .Include(v => v.UnidadAsociada)
            .Where(v => v.UnidadPrincipalId == unidadPrincipalId)
            .OrderBy(v => v.UnidadAsociada!.Numero)
            .Select(v => new UnidadVinculoDto(v.Id, v.UnidadAsociadaId,
                v.UnidadAsociada!.Numero, v.UnidadAsociada.Tipo, v.IncluyeEnFacturacion))
            .ToListAsync(ct);
    }

    public async Task<UnidadVinculoDto> CrearVinculoAsync(Guid unidadPrincipalId, CrearVinculoUnidadRequest req, CancellationToken ct)
    {
        if (unidadPrincipalId == req.UnidadAsociadaId)
            throw new InvalidOperationException("Una unidad no puede asociarse a si misma.");

        var principal = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == unidadPrincipalId, ct)
            ?? throw new InvalidOperationException("Unidad principal no encontrada.");
        var asociada = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == req.UnidadAsociadaId, ct)
            ?? throw new InvalidOperationException("Unidad asociada no encontrada.");

        // RN-09 (no circular): la asociada no puede ser, a su vez, principal de la unidad principal,
        // y una asociada solo puede tener un principal.
        if (await _db.UnidadVinculos.AnyAsync(v => v.UnidadAsociadaId == req.UnidadAsociadaId, ct))
            throw new InvalidOperationException($"La unidad {asociada.Numero} ya esta asociada a otra unidad.");
        if (await _db.UnidadVinculos.AnyAsync(v => v.UnidadPrincipalId == req.UnidadAsociadaId && v.UnidadAsociadaId == unidadPrincipalId, ct))
            throw new InvalidOperationException("Vinculo circular no permitido (RN-09).");

        var v = new UnidadVinculo
        {
            UnidadPrincipalId = unidadPrincipalId,
            UnidadAsociadaId = req.UnidadAsociadaId,
            IncluyeEnFacturacion = req.IncluyeEnFacturacion
        };
        _db.UnidadVinculos.Add(v);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"{asociada.Tipo} '{asociada.Numero}' vinculado a unidad '{principal.Numero}' ({(req.IncluyeEnFacturacion ? "factura" : "no factura")}).", ct, principal.Id);
        return new UnidadVinculoDto(v.Id, v.UnidadAsociadaId, asociada.Numero, asociada.Tipo, v.IncluyeEnFacturacion);
    }

    public async Task<bool> EliminarVinculoAsync(Guid vinculoId, CancellationToken ct)
    {
        var v = await _db.UnidadVinculos.FirstOrDefaultAsync(x => x.Id == vinculoId, ct);
        if (v is null) return false;
        _db.UnidadVinculos.Remove(v);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Personas vinculadas a una unidad -----------------------------

    public async Task<IReadOnlyList<UnidadPersonaDto>> ListPersonasUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        // Ya no es un join contra Personas: un miembro puede ser empresa (dueno/residente juridico),
        // que vive en otra tabla. Se traen las filas y se resuelven persona/empresa aparte.
        var rows = await _db.UnidadPersonas.AsNoTracking()
            .Where(up => up.UnidadId == unidadId)
            .ToListAsync(ct);
        if (rows.Count == 0) return Array.Empty<UnidadPersonaDto>();

        var (personas, empresas) = await ResolverEntidadesAsync(rows, ct);
        return rows.Select(up => ToUnidadPersonaDto(up, personas, empresas))
            .OrderBy(d => d.Rol).ThenBy(d => d.PersonaNombre)
            .ToList();
    }

    // Modulo Residentes: TODAS las personas/empresas de TODAS las unidades del tenant (RLS ya
    // acota al tenant activo), cada una con el codigo de su unidad (TORRE-NUMERO, ej. A1-101).
    // ===================== Vistas agregadas: vehiculos y mascotas =====================
    // Los modulos /vehiculos y /mascotas necesitan TODOS los registros de la copropiedad; hasta
    // ahora solo existian los endpoints por unidad (unidades/{id}/placas y .../mascotas), que
    // obligaban a abrir la ficha de cada unidad para ver o editar uno. Mismo patron que residentes.

    public async Task<IReadOnlyList<VehiculoResumenDto>> ListVehiculosAsync(CancellationToken ct)
    {
        var rows = await _db.UnidadPlacas.AsNoTracking()
            .Select(v => new { v.Id, v.UnidadId, v.Placa, v.TipoVehiculo }).ToListAsync(ct);
        if (rows.Count == 0) return Array.Empty<VehiculoResumenDto>();

        var datos = await DatosDeUnidadesAsync(rows.Select(r => r.UnidadId), ct);
        return rows
            .Select(v =>
            {
                var (numero, codigo, torre, propietario) = datos.GetValueOrDefault(v.UnidadId);
                return new VehiculoResumenDto(v.Id, v.UnidadId, numero, codigo, torre,
                    v.Placa, v.TipoVehiculo, propietario);
            })
            .OrderBy(v => v.UnidadCodigo, StringComparer.CurrentCultureIgnoreCase).ThenBy(v => v.Placa)
            .ToList();
    }

    public async Task<IReadOnlyList<MascotaResumenDto>> ListMascotasAsync(CancellationToken ct)
    {
        var rows = await _db.UnidadMascotas.AsNoTracking()
            .Select(m => new { m.Id, m.UnidadId, m.Nombre, m.Tipo, m.Raza }).ToListAsync(ct);
        if (rows.Count == 0) return Array.Empty<MascotaResumenDto>();

        var datos = await DatosDeUnidadesAsync(rows.Select(r => r.UnidadId), ct);
        return rows
            .Select(m =>
            {
                var (numero, codigo, torre, propietario) = datos.GetValueOrDefault(m.UnidadId);
                return new MascotaResumenDto(m.Id, m.UnidadId, numero, codigo, torre,
                    m.Nombre, m.Tipo, m.Raza, propietario);
            })
            .OrderBy(m => m.UnidadCodigo, StringComparer.CurrentCultureIgnoreCase).ThenBy(m => m.Nombre)
            .ToList();
    }

    /// <summary>
    /// Numero, codigo TORRE-NUMERO, torre y primer propietario de cada unidad pedida, en UNA consulta.
    /// Lo comparten las vistas agregadas para no repetir el join por fila (hay copropiedades de 500+
    /// unidades). El propietario contempla dueno persona O empresa, igual que ListUnidadesAsync.
    /// </summary>
    private async Task<Dictionary<Guid, (string Numero, string Codigo, string? Torre, string? Propietario)>>
        DatosDeUnidadesAsync(IEnumerable<Guid> unidadIds, CancellationToken ct)
    {
        var ids = unidadIds.Distinct().ToList();
        var filas = await _db.UnidadesPrivadas.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Numero,
                TorreNombre = u.Torre != null ? u.Torre.Nombre : null,
                Propietario = (from up in _db.UnidadPersonas
                               where up.UnidadId == u.Id && up.Rol == RolUnidadPersona.Propietario
                               orderby up.EntidadTipo, up.Id
                               select up.EntidadTipo == EntidadDirectorio.Empresa
                                   ? _db.Empresas.Where(e => e.Id == up.EmpresaId).Select(e => e.RazonSocial).FirstOrDefault()
                                   : _db.Personas.Where(p => p.Id == up.PersonaId).Select(p => (p.Nombres + " " + p.Apellidos).Trim()).FirstOrDefault()
                              ).FirstOrDefault()
            })
            .ToListAsync(ct);

        var res = new Dictionary<Guid, (string, string, string?, string?)>(filas.Count);
        foreach (var u in filas)
        {
            // Mismo codigo TORRE-NUMERO que arma ListResidentesAsync: ultima palabra de la torre.
            var torreShort = string.IsNullOrWhiteSpace(u.TorreNombre) ? "" : u.TorreNombre!.Split(' ').Last();
            var codigo = torreShort.Length > 0 ? $"{torreShort}-{u.Numero}" : u.Numero;
            res[u.Id] = (u.Numero, codigo, u.TorreNombre, u.Propietario);
        }
        return res;
    }

    public async Task<IReadOnlyList<ResidenteResumenDto>> ListResidentesAsync(CancellationToken ct)
    {
        var rows = await _db.UnidadPersonas.AsNoTracking().ToListAsync(ct);
        if (rows.Count == 0) return Array.Empty<ResidenteResumenDto>();

        var (personas, empresas) = await ResolverEntidadesAsync(rows, ct);

        var unidadIds = rows.Select(r => r.UnidadId).Distinct().ToList();
        var unidades = await _db.UnidadesPrivadas.AsNoTracking().Include(u => u.Torre)
            .Where(u => unidadIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Numero, TorreNombre = u.Torre != null ? u.Torre.Nombre : null })
            .ToDictionaryAsync(u => u.Id, ct);

        var lista = new List<ResidenteResumenDto>(rows.Count);
        foreach (var up in rows)
        {
            unidades.TryGetValue(up.UnidadId, out var u);
            var numero = u?.Numero ?? "";
            var torre = u?.TorreNombre;
            var torreShort = string.IsNullOrWhiteSpace(torre) ? "" : torre!.Split(' ').Last();
            var codigo = torreShort.Length > 0 ? $"{torreShort}-{numero}" : numero;

            string nombre, documento; string? email, tel; Guid? personaId = null, empresaId = null;
            if (up.EntidadTipo == EntidadDirectorio.Empresa && up.EmpresaId is Guid eid && empresas.TryGetValue(eid, out var e))
            {
                nombre = e.RazonSocial; documento = NitConDv(e); email = e.Email; tel = e.Telefono; empresaId = e.Id;
            }
            else if (up.PersonaId is Guid pid && personas.TryGetValue(pid, out var p))
            {
                nombre = ($"{p.Nombres} {p.Apellidos}").Trim(); documento = p.Documento; email = p.Email; tel = p.Telefono; personaId = p.Id;
            }
            else { nombre = "(desconocido)"; documento = ""; email = null; tel = null; }

            lista.Add(new ResidenteResumenDto(
                up.Id, up.UnidadId, numero, codigo, torre,
                up.EntidadTipo, personaId, empresaId,
                nombre, documento, email, tel,
                up.Rol, up.Habita, up.Parentesco, up.Activo));
        }
        return lista.OrderBy(r => r.UnidadCodigo).ThenBy(r => r.Rol).ThenBy(r => r.Nombre).ToList();
    }

    private async Task<(Dictionary<Guid, Persona> Personas, Dictionary<Guid, Empresa> Empresas)>
        ResolverEntidadesAsync(List<UnidadPersona> rows, CancellationToken ct)
    {
        var personaIds = rows.Where(r => r.PersonaId != null).Select(r => r.PersonaId!.Value).Distinct().ToList();
        var empresaIds = rows.Where(r => r.EmpresaId != null).Select(r => r.EmpresaId!.Value).Distinct().ToList();
        var personas = await _db.Personas.AsNoTracking().Where(p => personaIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var empresas = await _db.Empresas.AsNoTracking().Where(e => empresaIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        return (personas, empresas);
    }

    private static string NitConDv(Empresa e) =>
        e.Nit + (string.IsNullOrEmpty(e.DigitoVerificacion) ? "" : "-" + e.DigitoVerificacion);

    private static UnidadPersonaDto ToUnidadPersonaDto(
        UnidadPersona up, Dictionary<Guid, Persona> personas, Dictionary<Guid, Empresa> empresas)
    {
        if (up.EntidadTipo == EntidadDirectorio.Empresa && up.EmpresaId is Guid eid && empresas.TryGetValue(eid, out var e))
        {
            // Para empresa: la ficha muestra razon social como "nombre" y NIT como "documento".
            return new UnidadPersonaDto(up.Id, Guid.Empty, e.RazonSocial, NitConDv(e), e.Email, e.Telefono,
                up.Rol, up.Habita, up.Parentesco, e.RazonSocial, "", EntidadDirectorio.Empresa, e.Id, up.Activo);
        }
        if (up.PersonaId is Guid pid && personas.TryGetValue(pid, out var p))
        {
            return new UnidadPersonaDto(up.Id, p.Id, ($"{p.Nombres} {p.Apellidos}").Trim(), p.Documento,
                p.Email, p.Telefono, up.Rol, up.Habita, up.Parentesco, p.Nombres, p.Apellidos, EntidadDirectorio.Persona, null, up.Activo);
        }
        return new UnidadPersonaDto(up.Id, up.PersonaId ?? Guid.Empty, "(desconocido)", "", null, null,
            up.Rol, up.Habita, up.Parentesco, "", "", up.EntidadTipo, up.EmpresaId, up.Activo);
    }

    public async Task<UnidadPersonaDto> AgregarPersonaUnidadAsync(Guid unidadId, AgregarPersonaUnidadRequest req, CancellationToken ct)
    {
        var unidad = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == unidadId, ct)
            ?? throw new InvalidOperationException("Unidad no encontrada.");

        // ----- Empresa (dueno/residente juridico). La identidad la resuelve el selector. -----
        if (req.EntidadTipo == EntidadDirectorio.Empresa)
        {
            if (req.EmpresaId is not Guid empId || empId == Guid.Empty)
                throw new InvalidOperationException("Debes elegir la empresa.");
            var empresa = await _db.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == empId, ct)
                ?? throw new InvalidOperationException("La empresa seleccionada no existe.");
            await Directorio.VinculoDirectorio.AsegurarEmpresaAsync(_db, _tenant, empId, ct);

            if (await _db.UnidadPersonas.AnyAsync(x => x.UnidadId == unidadId && x.EmpresaId == empId && x.Rol == req.Rol, ct))
                throw new InvalidOperationException($"Esta empresa ya es {req.Rol} de la unidad {unidad.Numero}.");

            var upE = new UnidadPersona
            {
                UnidadId = unidadId,
                EntidadTipo = EntidadDirectorio.Empresa,
                EmpresaId = empId,
                Rol = req.Rol,
                Habita = req.Habita,
                Activo = req.Activo,
                Parentesco = string.IsNullOrWhiteSpace(req.Parentesco) ? null : req.Parentesco.Trim()
            };
            _db.UnidadPersonas.Add(upE);
            await _db.SaveChangesAsync(ct);
            // Etiqueta automatica en el Directorio segun el rol (best-effort, no rompe el alta).
            try { await _dir.AsegurarEtiquetaPorRolAsync(EntidadDirectorio.Empresa, empId, req.Rol, ct); } catch { /* no bloquear el vinculo */ }
            await RegistrarBitacoraAsync("Unidad", $"{req.Rol} '{empresa.RazonSocial}' (empresa) vinculado a unidad '{unidad.Numero}'.", ct, unidad.Id);
            return new UnidadPersonaDto(upE.Id, Guid.Empty, empresa.RazonSocial, NitConDv(empresa), empresa.Email, empresa.Telefono,
                upE.Rol, upE.Habita, upE.Parentesco, empresa.RazonSocial, "", EntidadDirectorio.Empresa, empresa.Id);
        }

        // ----- Persona natural -----
        // Con PersonaId la identidad ya viene resuelta por el SelectorPersona; sin el, se
        // exigen los datos para poder buscar o crear la persona por documento.
        if (req.PersonaId is null)
        {
            if (string.IsNullOrWhiteSpace(req.Documento)) throw new InvalidOperationException("Documento obligatorio.");
            if (string.IsNullOrWhiteSpace(req.Nombres)) throw new InvalidOperationException("Nombres obligatorios.");
            if (string.IsNullOrWhiteSpace(req.Apellidos)) throw new InvalidOperationException("Apellidos obligatorios.");
        }

        Guid personaId;
        if (req.PersonaId is Guid elegida)
        {
            if (!await _db.Personas.IgnoreQueryFilters().AnyAsync(p => p.Id == elegida, ct))
                throw new InvalidOperationException("La persona seleccionada no existe.");
            personaId = elegida;
            // El selector ya la vincula, pero se asegura por si llega por otra via (API, MCP).
            await Directorio.VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, personaId, ct);
        }
        else
        {
            // Camino viejo: busca o crea Persona por documento (tambien deja el vinculo).
            personaId = await VincularPersonaPorDocumentoAsync(
                new VincularPersonaPorDocumentoRequest(req.Documento, req.Nombres, req.Apellidos, req.Email, req.Telefono), ct);
        }

        // Evita duplicar mismo (unidad + persona + rol)
        var existente = await _db.UnidadPersonas
            .FirstOrDefaultAsync(x => x.UnidadId == unidadId && x.PersonaId == personaId && x.Rol == req.Rol, ct);
        if (existente is not null)
            throw new InvalidOperationException($"Esta persona ya es {req.Rol} de la unidad {unidad.Numero}.");

        var up = new UnidadPersona
        {
            UnidadId = unidadId,
            EntidadTipo = EntidadDirectorio.Persona,
            PersonaId = personaId,
            Rol = req.Rol,
            Habita = req.Habita,
            Activo = req.Activo,
            Parentesco = string.IsNullOrWhiteSpace(req.Parentesco) ? null : req.Parentesco.Trim()
        };
        _db.UnidadPersonas.Add(up);
        await _db.SaveChangesAsync(ct);

        // Siembra automatica: si un rol Personalizado declara esta faceta como semilla,
        // crea/asegura usuario+login+directorio con ese rol (best-effort, no rompe el alta).
        try { await _seed.SembrarPorFacetaAsync(personaId, req.Rol, ct); } catch { /* no bloquear el vinculo */ }
        // Etiqueta automatica en el Directorio segun el rol (Propietario/Residente/...).
        try { await _dir.AsegurarEtiquetaPorRolAsync(EntidadDirectorio.Persona, personaId, req.Rol, ct); } catch { /* no bloquear el vinculo */ }

        var persona = await _db.Personas.AsNoTracking().FirstAsync(p => p.Id == personaId, ct);
        await RegistrarBitacoraAsync("Unidad", $"{req.Rol} '{persona.Nombres} {persona.Apellidos}' vinculado a unidad '{unidad.Numero}'.", ct, unidad.Id);

        return new UnidadPersonaDto(up.Id, personaId,
            ($"{persona.Nombres} {persona.Apellidos}").Trim(), persona.Documento, persona.Email, persona.Telefono,
            up.Rol, up.Habita, up.Parentesco, persona.Nombres, persona.Apellidos, EntidadDirectorio.Persona, null, up.Activo);
    }

    public async Task<UnidadPersonaDto?> EditarPersonaUnidadAsync(Guid unidadPersonaId, AgregarPersonaUnidadRequest req, CancellationToken ct)
    {
        var up = await _db.UnidadPersonas.FirstOrDefaultAsync(x => x.Id == unidadPersonaId, ct);
        if (up is null) return null;

        // ----- Empresa: solo se edita el vinculo (rol/habita/parentesco); la identidad va por Directorio. -----
        if (up.EntidadTipo == EntidadDirectorio.Empresa)
        {
            var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == up.EmpresaId, ct);
            if (empresa is null) return null;
            if (up.Rol != req.Rol &&
                await _db.UnidadPersonas.AnyAsync(x => x.Id != up.Id && x.UnidadId == up.UnidadId && x.EmpresaId == up.EmpresaId && x.Rol == req.Rol, ct))
                throw new InvalidOperationException($"Esta empresa ya tiene el rol {req.Rol} en la unidad.");
            up.Rol = req.Rol;
            up.Habita = req.Habita;
            up.Activo = req.Activo;
            up.Parentesco = string.IsNullOrWhiteSpace(req.Parentesco) ? null : req.Parentesco.Trim();
            await _db.SaveChangesAsync(ct);
            try { await _dir.AsegurarEtiquetaPorRolAsync(EntidadDirectorio.Empresa, up.EmpresaId ?? Guid.Empty, up.Rol, ct); } catch { /* no bloquear */ }
            return new UnidadPersonaDto(up.Id, Guid.Empty, empresa.RazonSocial, NitConDv(empresa), empresa.Email, empresa.Telefono,
                up.Rol, up.Habita, up.Parentesco, empresa.RazonSocial, "", EntidadDirectorio.Empresa, empresa.Id, up.Activo);
        }

        // ----- Persona natural -----
        if (string.IsNullOrWhiteSpace(req.Nombres)) throw new InvalidOperationException("Nombres obligatorios.");
        if (string.IsNullOrWhiteSpace(req.Apellidos)) throw new InvalidOperationException("Apellidos obligatorios.");

        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Id == up.PersonaId, ct);
        if (persona is null) return null;

        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email is not null && await _db.Personas.AnyAsync(p => p.Id != persona.Id && p.Email == email, ct))
            throw new InvalidOperationException("El correo ya esta en uso por otra persona.");

        var doc = string.IsNullOrWhiteSpace(req.Documento) ? persona.Documento : req.Documento.Trim();
        if (!string.Equals(doc, persona.Documento, StringComparison.Ordinal))
        {
            if (await _db.Personas.AnyAsync(p => p.Id != persona.Id && p.TipoDocumento == persona.TipoDocumento && p.Documento == doc, ct))
                throw new InvalidOperationException("El documento ya esta en uso por otra persona.");
            persona.Documento = doc;
        }

        if (up.Rol != req.Rol)
        {
            if (await _db.UnidadPersonas.AnyAsync(x => x.Id != up.Id && x.UnidadId == up.UnidadId && x.PersonaId == up.PersonaId && x.Rol == req.Rol, ct))
                throw new InvalidOperationException($"Esta persona ya tiene el rol {req.Rol} en la unidad.");
            up.Rol = req.Rol;
        }

        persona.Nombres = req.Nombres.Trim();
        persona.Apellidos = req.Apellidos.Trim();
        persona.Email = email;
        persona.Telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim();
        up.Habita = req.Habita;
        up.Activo = req.Activo;
        up.Parentesco = string.IsNullOrWhiteSpace(req.Parentesco) ? null : req.Parentesco.Trim();

        await _db.SaveChangesAsync(ct);
        // Re-siembra por si cambio la faceta o se agrego el email (habilita login).
        try { await _seed.SembrarPorFacetaAsync(persona.Id, up.Rol, ct); } catch { /* no bloquear */ }
        // Asegura la etiqueta del rol actual (aditivo: no quita la del rol anterior).
        try { await _dir.AsegurarEtiquetaPorRolAsync(EntidadDirectorio.Persona, persona.Id, up.Rol, ct); } catch { /* no bloquear */ }
        await RegistrarBitacoraAsync("Unidad", $"Datos de '{persona.Nombres} {persona.Apellidos}' actualizados.", ct, up.UnidadId);

        return new UnidadPersonaDto(up.Id, persona.Id,
            ($"{persona.Nombres} {persona.Apellidos}").Trim(), persona.Documento, persona.Email, persona.Telefono,
            up.Rol, up.Habita, up.Parentesco, persona.Nombres, persona.Apellidos, EntidadDirectorio.Persona, null, up.Activo);
    }

    public async Task<bool> EliminarPersonaUnidadAsync(Guid unidadPersonaId, CancellationToken ct)
    {
        var up = await _db.UnidadPersonas.FirstOrDefaultAsync(x => x.Id == unidadPersonaId, ct);
        if (up is null) return false;
        _db.UnidadPersonas.Remove(up);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Campos personalizados de unidad (definicion compartida por copropiedad + valor por unidad) --------

    public async Task<IReadOnlyList<UnidadCampoDefinicionDto>> ListCamposDefinicionAsync(CancellationToken ct)
        => await _db.UnidadCamposDefiniciones.AsNoTracking()
            .OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new UnidadCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones))
            .ToListAsync(ct);

    public async Task<UnidadCampoDefinicionDto> CrearCampoDefinicionAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var opciones = await PrepararOpcionesAsync(req.Tipo, req.Opciones, null, ct);

        var existente = await _db.UnidadCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null)
            return new UnidadCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones);

        var maxOrden = await _db.UnidadCamposDefiniciones.AnyAsync(ct)
            ? await _db.UnidadCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new UnidadCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = opciones };
        _db.UnidadCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Campo personalizado '{label}' agregado a todas las unidades.", ct);
        return new UnidadCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones);
    }

    public async Task<bool> ActualizarCampoDefinicionAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.UnidadCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label;
        def.Tipo = req.Tipo;
        def.Opciones = await PrepararOpcionesAsync(req.Tipo, req.Opciones, def.Id, ct);
        def.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // La columna Opciones es POLIMORFICA segun Tipo: para Seleccion guarda las opciones (una por linea);
    // para Formula (Fase 2) guarda el JSON de CampoFormulaConfig (operacion + campos fuente); para el resto
    // va null. Cada tipo parsea SOLO su formato (no colisionan). Aqui se valida y normaliza segun el tipo.
    private async Task<string?> PrepararOpcionesAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo == TipoCampoTablero.Seleccion) return NormalizarOpciones(opciones);
        if (tipo == TipoCampoTablero.Formula)
        {
            var cfg = CampoFormulaConfig.Parse(opciones)
                ?? throw new InvalidOperationException("La formula necesita una operacion y al menos un campo fuente.");
            // Fuentes = solo campos PROPIOS Numero/Moneda (cd:{guid}) en esta version (replicable). El
            // resolver de Tipo puede extenderse a campos de sistema por superficie si Alex lo aprueba.
            var defs = await _db.UnidadCamposDefiniciones.AsNoTracking()
                .Where(d => excluirId == null || d.Id != excluirId)
                .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct);
            var porGuid = defs.ToDictionary(d => d.Id, d => d.Tipo);
            TipoCampoTablero? TipoDe(string clave)
            {
                if (clave.StartsWith("cd:", StringComparison.Ordinal))
                    return Guid.TryParse(clave[3..], out var g) && porGuid.TryGetValue(g, out var t) ? t : (TipoCampoTablero?)null;
                return FuentesSistemaUnidad.TryGetValue(clave, out var ts) ? ts : (TipoCampoTablero?)null;   // fuente de sistema
            }
            var err = CampoFormulaConfig.Validar(cfg, TipoDe);
            if (err is not null) throw new InvalidOperationException(err);
            return cfg.Serializar();
        }
        return null;
    }

    // Normaliza las opciones del tipo Seleccion (una por linea; vacio -> null).
    private static string? NormalizarOpciones(string? opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones)) return null;
        var limpias = opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return limpias.Length == 0 ? null : string.Join('\n', limpias);
    }

    public async Task<bool> EliminarCampoDefinicionAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.UnidadCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.UnidadCamposDefiniciones.Remove(def);  // cascade borra los valores por unidad
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Campo personalizado '{def.Label}' eliminado de todas las unidades.", ct);
        return true;
    }

    public async Task<IReadOnlyList<UnidadCampoDto>> ListCamposUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        var defs = await _db.UnidadCamposDefiniciones.AsNoTracking()
            .OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var valores = await _db.UnidadCamposValores.AsNoTracking()
            .Where(v => v.UnidadId == unidadId).ToListAsync(ct);
        var porDef = valores.GroupBy(v => v.DefinicionId).ToDictionary(g => g.Key, g => g.First().Valor);
        var nums = await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == unidadId)
            .Select(u => new NumerosUnidad(u.CoeficientePropiedad, u.AreaM2, u.Piso, u.Habitaciones, u.Banos,
                u.Parqueaderos, u.CuotaMensual, u.ModuloContributivo1, u.ModuloContributivo2, u.ModuloContributivo3,
                u.ModuloContributivo4, u.ModuloContributivo5)).FirstOrDefaultAsync(ct);
        // Formula: se COMPUTA en lectura (no persiste); el resto toma su valor almacenado.
        return defs.Select(d => new UnidadCampoDto(
            d.Id, d.Label, d.Orden,
            d.Tipo == TipoCampoTablero.Formula ? ComputarFormulaTexto(d.Opciones, porDef, nums) : porDef.GetValueOrDefault(d.Id),
            d.Tipo, d.Opciones)).ToList();
    }

    public async Task<IReadOnlyList<UnidadCampoValorFlatDto>> ListTodosCamposValoresAsync(CancellationToken ct)
    {
        var valores = await _db.UnidadCamposValores.AsNoTracking()
            .Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.UnidadId, v.DefinicionId, v.Valor })
            .ToListAsync(ct);
        var res = valores.Select(v => new UnidadCampoValorFlatDto(v.UnidadId, v.DefinicionId, v.Valor)).ToList();

        // Campos Formula: valor calculado en lectura por unidad (no hay fila almacenada). Se emite para
        // TODAS las unidades (asi Conteo=0 y agregados vacios se ven correctamente en la tabla).
        var formulaDefs = await _db.UnidadCamposDefiniciones.AsNoTracking()
            .Where(d => d.Tipo == TipoCampoTablero.Formula)
            .Select(d => new { d.Id, d.Opciones }).ToListAsync(ct);
        if (formulaDefs.Count > 0)
        {
            var unidades = await _db.UnidadesPrivadas.AsNoTracking()
                .Select(u => new { u.Id, N = new NumerosUnidad(u.CoeficientePropiedad, u.AreaM2, u.Piso, u.Habitaciones,
                    u.Banos, u.Parqueaderos, u.CuotaMensual, u.ModuloContributivo1, u.ModuloContributivo2,
                    u.ModuloContributivo3, u.ModuloContributivo4, u.ModuloContributivo5) })
                .ToListAsync(ct);
            var porUnidad = valores.GroupBy(v => v.UnidadId)
                .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, string?>)g.ToDictionary(x => x.DefinicionId, x => x.Valor));
            IReadOnlyDictionary<Guid, string?> vacio = new Dictionary<Guid, string?>();
            foreach (var u in unidades)
            {
                var dict = porUnidad.TryGetValue(u.Id, out var d0) ? d0 : vacio;
                foreach (var fd in formulaDefs)
                {
                    var txt = ComputarFormulaTexto(fd.Opciones, dict, u.N);
                    if (txt is not null) res.Add(new UnidadCampoValorFlatDto(u.Id, fd.Id, txt));
                }
            }
        }
        return res;
    }

    // Fase 2: id -> nombre de las personas VINCULADAS al tenant (usuarios del tenant + personas del
    // directorio). Lo usan los campos Usuario/Directorio para mostrar el NOMBRE en vez del Guid, tambien
    // tras recargar. Tenant-scoped: los ids salen de usuarios_tenant y directorio_vinculos (ambas RLS),
    // asi que solo se resuelven personas ligadas a ESTE tenant (no expone personas de otros).
    public async Task<IReadOnlyList<PersonaVinculadaDto>> ListPersonasVinculadasAsync(CancellationToken ct)
    {
        var idsUsuarios = await _db.UsuariosTenant.AsNoTracking().Select(u => u.PersonaId).ToListAsync(ct);
        var idsDir = await _db.DirectorioVinculos.AsNoTracking()
            .Where(v => v.EntidadTipo == EntidadDirectorio.Persona && v.Estado == EstadoVinculo.Activo)
            .Select(v => v.EntidadId).ToListAsync(ct);
        var ids = idsUsuarios.Concat(idsDir).Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<PersonaVinculadaDto>();
        var personas = await _db.Personas.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombres, p.Apellidos })
            .ToListAsync(ct);
        return personas.Select(p => new PersonaVinculadaDto(p.Id, $"{p.Nombres} {p.Apellidos}".Trim())).ToList();
    }

    // Computa el texto de un campo Formula para una unidad. Fuentes: campos PROPIOS Numero/Moneda
    // (cd:{guid}, via el mapa {definicionId -> valor}) y campos de SISTEMA Numero/Moneda (via los numeros
    // de la unidad). Solo lectura; devuelve null si la config no es valida o el agregado es vacio.
    private static string? ComputarFormulaTexto(string? opciones, IReadOnlyDictionary<Guid, string?> valoresPorDef, NumerosUnidad? nums)
    {
        var cfg = CampoFormulaConfig.Parse(opciones);
        if (cfg is null) return null;
        decimal? ValorDe(string clave)
        {
            if (clave.StartsWith("cd:", StringComparison.Ordinal))
                return Guid.TryParse(clave[3..], out var g) && valoresPorDef.TryGetValue(g, out var v) ? ParseDecimalInv(v) : null;
            return nums is not null ? ValorSistemaUnidad(clave, nums) : null;
        }
        var r = cfg.Computar(ValorDe);
        if (r is null) return null;
        // Formato limpio: los campos de sistema son numeric con escala (2.0000), asi que se recortan los
        // ceros de cola para no mostrar "10.0000" (InvariantCulture -> separador '.').
        var s = r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (s.Contains('.')) s = s.TrimEnd('0').TrimEnd('.');
        return s;
    }

    private static decimal? ParseDecimalInv(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().Replace(",", ".");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
    }

    // ---- Fuentes de SISTEMA para las Formulas (per-surface: propio de la ficha de Unidad) ----
    // El picker de fuentes (componente compartido) ofrece los campos del Catalogo con Tipo Numero/Moneda;
    // el helper de operacion es compartido; SOLO este resolver (clave -> Tipo y clave -> valor de la fila)
    // es especifico de cada superficie, porque las columnas del sistema difieren. En Unidades: coeficiente,
    // area, piso, habitaciones, banos, parqueaderos, los 5 modulos contributivos y la cuota.
    private static readonly IReadOnlyDictionary<string, TipoCampoTablero> FuentesSistemaUnidad =
        new Dictionary<string, TipoCampoTablero>(StringComparer.Ordinal)
        {
            ["coef"] = TipoCampoTablero.Numero, ["area"] = TipoCampoTablero.Numero, ["piso"] = TipoCampoTablero.Numero,
            ["habitaciones"] = TipoCampoTablero.Numero, ["banos"] = TipoCampoTablero.Numero, ["parqueaderos"] = TipoCampoTablero.Numero,
            ["modcontrib1"] = TipoCampoTablero.Numero, ["modcontrib2"] = TipoCampoTablero.Numero, ["modcontrib3"] = TipoCampoTablero.Numero,
            ["modcontrib4"] = TipoCampoTablero.Numero, ["modcontrib5"] = TipoCampoTablero.Numero, ["cuota"] = TipoCampoTablero.Moneda,
        };

    // Numeros de una unidad relevantes para las formulas (los campos de sistema Numero/Moneda).
    private sealed record NumerosUnidad(
        decimal Coef, decimal? Area, int? Piso, int? Hab, int? Banos, int? Parq, decimal? Cuota,
        decimal? M1, decimal? M2, decimal? M3, decimal? M4, decimal? M5);

    private static decimal? ValorSistemaUnidad(string clave, NumerosUnidad n) => clave switch
    {
        "coef" => n.Coef, "area" => n.Area, "piso" => n.Piso, "habitaciones" => n.Hab, "banos" => n.Banos,
        "parqueaderos" => n.Parq, "cuota" => n.Cuota, "modcontrib1" => n.M1, "modcontrib2" => n.M2,
        "modcontrib3" => n.M3, "modcontrib4" => n.M4, "modcontrib5" => n.M5, _ => (decimal?)null
    };

    // ---- Configuracion de campos FIJOS del sistema (alias + opciones de lista) ----
    // La misma tabla guarda la config de la ficha de unidad y la de las fichas vinculadas;
    // 'entidad' discrimina cual. Sin entidad se asume "unidad" (comportamiento historico).
    // Ecosistema de la unidad + modulos de administracion que adoptan la config compartida por
    // copropiedad (decision de Alex 2026-09-14). La tabla UnidadCamposConfig es generica (Entidad,
    // CampoClave), asi que admitir mas entidades no requiere migracion. (Mantenimiento/zona/equipo
    // entraran cuando YUNQUE adopte.)
    private static readonly string[] EntidadesCampoConfig =
        { "unidad", "personas", "vehiculos", "mascotas", "terceros", "pqrsd", "contrato", "poliza",
          "programacion", "zona", "equipo" };

    private static string NormalizarEntidadCampoConfig(string? entidad)
    {
        var e = (entidad ?? "").Trim().ToLowerInvariant();
        if (e.Length == 0) e = "unidad";
        if (!EntidadesCampoConfig.Contains(e))
            throw new InvalidOperationException(
                $"Entidad '{entidad}' no valida. Debe ser una de: {string.Join(", ", EntidadesCampoConfig)}.");
        return e;
    }

    public async Task<IReadOnlyList<UnidadCampoConfigDto>> ListCamposConfigAsync(string? entidad, CancellationToken ct)
    {
        var ent = NormalizarEntidadCampoConfig(entidad);
        return await _db.UnidadCamposConfig.AsNoTracking()
            .Where(c => c.Entidad == ent)
            .Select(c => new UnidadCampoConfigDto(
                c.CampoClave, c.Alias, c.Opciones,
                c.Tipo, c.Formato, c.Oculto, c.Orden, c.Entidad))
            .ToListAsync(ct);
    }

    // El request es el ESTADO COMPLETO deseado de la fila (la UI siempre manda la fila entera),
    // asi que los campos se asignan tal cual vienen. La clave del upsert es (entidad, campo_clave).
    public async Task<UnidadCampoConfigDto> GuardarCampoConfigAsync(GuardarUnidadCampoConfigRequest req, CancellationToken ct)
    {
        var clave = (req.CampoClave ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(clave)) throw new InvalidOperationException("CampoClave obligatorio.");
        var entidad = NormalizarEntidadCampoConfig(req.Entidad);
        var alias = string.IsNullOrWhiteSpace(req.Alias) ? null : req.Alias.Trim();
        var opciones = string.IsNullOrWhiteSpace(req.Opciones)
            ? null
            : string.Join('\n', req.Opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var formato = string.IsNullOrWhiteSpace(req.Formato) ? null : req.Formato.Trim();

        var c = await _db.UnidadCamposConfig.FirstOrDefaultAsync(x => x.Entidad == entidad && x.CampoClave == clave, ct);
        if (c is null)
        {
            c = new UnidadCampoConfig { Entidad = entidad, CampoClave = clave };
            _db.UnidadCamposConfig.Add(c);
        }
        Aplicar(c, alias, opciones, formato, req);
        await _db.SaveChangesAsync(ct);
        return new UnidadCampoConfigDto(c.CampoClave, c.Alias, c.Opciones, c.Tipo, c.Formato, c.Oculto, c.Orden, c.Entidad);
    }

    // Guarda VARIAS filas de configuracion en una sola transaccion. Lo usa el reordenamiento por
    // drag & drop, que manda la fila completa de cada campo (no solo su posicion): de lo contrario
    // las filas creadas al vuelo tomarian el default de 'oculto' y se harian visibles solas.
    // La deduplicacion y la busqueda van por el par (entidad, campo_clave).
    public async Task<IReadOnlyList<UnidadCampoConfigDto>> GuardarCamposConfigLoteAsync(
        List<GuardarUnidadCampoConfigRequest> filas, CancellationToken ct)
    {
        if (filas is null || filas.Count == 0) return await ListCamposConfigAsync(null, ct);

        // Normaliza y descarta claves vacias o repetidas (gana la primera aparicion).
        var pedidos = new List<(string Entidad, string Clave, GuardarUnidadCampoConfigRequest Req)>();
        foreach (var req in filas)
        {
            var clave = (req?.CampoClave ?? "").Trim().ToLowerInvariant();
            if (clave.Length == 0) continue;
            var entidad = NormalizarEntidadCampoConfig(req!.Entidad);
            if (pedidos.Any(p => p.Entidad == entidad && p.Clave == clave)) continue;
            pedidos.Add((entidad, clave, req));
        }
        if (pedidos.Count == 0) return await ListCamposConfigAsync(null, ct);

        var entidades = pedidos.Select(p => p.Entidad).Distinct().ToList();
        var claves = pedidos.Select(p => p.Clave).Distinct().ToList();
        var existentes = await _db.UnidadCamposConfig
            .Where(x => entidades.Contains(x.Entidad) && claves.Contains(x.CampoClave))
            .ToListAsync(ct);

        foreach (var (entidad, clave, req) in pedidos)
        {
            var c = existentes.FirstOrDefault(x => x.Entidad == entidad && x.CampoClave == clave);
            if (c is null)
            {
                c = new UnidadCampoConfig { Entidad = entidad, CampoClave = clave };
                _db.UnidadCamposConfig.Add(c);
            }
            var alias = string.IsNullOrWhiteSpace(req.Alias) ? null : req.Alias.Trim();
            var opciones = string.IsNullOrWhiteSpace(req.Opciones)
                ? null
                : string.Join('\n', req.Opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            var formato = string.IsNullOrWhiteSpace(req.Formato) ? null : req.Formato.Trim();
            Aplicar(c, alias, opciones, formato, req);
        }
        await _db.SaveChangesAsync(ct);

        // Si el lote toca una sola entidad se devuelve esa ficha completa (comportamiento historico
        // del caso unidad); si mezcla varias, se devuelven todas las entidades afectadas.
        if (entidades.Count == 1) return await ListCamposConfigAsync(entidades[0], ct);
        return await _db.UnidadCamposConfig.AsNoTracking()
            .Where(c => entidades.Contains(c.Entidad))
            .Select(c => new UnidadCampoConfigDto(
                c.CampoClave, c.Alias, c.Opciones,
                c.Tipo, c.Formato, c.Oculto, c.Orden, c.Entidad))
            .ToListAsync(ct);
    }

    private static void Aplicar(UnidadCampoConfig c, string? alias, string? opciones, string? formato,
        GuardarUnidadCampoConfigRequest req)
    {
        c.Alias = alias;
        c.Opciones = opciones;
        c.Tipo = req.Tipo;
        c.Formato = formato;
        c.Oculto = req.Oculto;
        c.Orden = req.Orden;
    }

    public async Task<IReadOnlyList<UnidadEstadoUsoDto>> ContarUnidadesPorEstadoAsync(CancellationToken ct)
        => await _db.UnidadesPrivadas.AsNoTracking()
            .Where(u => u.Estado != null && u.Estado != "")
            .GroupBy(u => u.Estado!)
            .Select(g => new UnidadEstadoUsoDto(g.Key, g.Count()))
            .ToListAsync(ct);

    // Migracion de dato: renombra el valor de Estado en las unidades que lo llevan. La lista de
    // opciones NO se toca aqui: de eso se encarga GuardarCampoConfigAsync con la lista nueva.
    public async Task<int> RenombrarEstadoAsync(string anterior, string nuevo, CancellationToken ct)
    {
        var destino = (nuevo ?? "").Trim();
        if (destino.Length == 0) return 0;
        if (string.Equals(destino, anterior, StringComparison.Ordinal)) return 0;

        // El DbSet lleva HasQueryFilter por tenant, asi que el UPDATE queda acotado al tenant activo.
        var unidades = await _db.UnidadesPrivadas.Where(u => u.Estado == anterior).ToListAsync(ct);
        if (unidades.Count == 0) return 0;
        foreach (var u in unidades) u.Estado = destino;
        await _db.SaveChangesAsync(ct);
        return unidades.Count;
    }

    public async Task SetCampoValorUnidadAsync(Guid unidadId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var def = await _db.UnidadCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct)
            ?? throw new InvalidOperationException("Campo no encontrado.");
        _ = await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)
            ? true : throw new InvalidOperationException("Unidad no encontrada.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();

        // Type-gate del valor por el Tipo del campo (Fase 2):
        // - Formula es calculado y de SOLO LECTURA: no admite valor.
        // - Usuario/Directorio guardan un Guid y se valida pertenencia al tenant (personas es GLOBAL).
        //   Las consultas van acotadas por RLS al tenant activo, asi que un id de OTRO tenant no valida.
        if (def.Tipo == TipoCampoTablero.Formula)
            throw new InvalidOperationException("Un campo Formula es calculado y de solo lectura; no admite valor.");
        if (valor is not null && def.Tipo == TipoCampoTablero.Usuario)
        {
            if (!Guid.TryParse(valor, out var personaId))
                throw new InvalidOperationException("El valor de un campo Usuario debe ser el id de un usuario del tenant.");
            var esUsuarioDelTenant = await _db.UsuariosTenant.AnyAsync(u => u.PersonaId == personaId, ct);
            if (!esUsuarioDelTenant)
                throw new InvalidOperationException("El usuario seleccionado no pertenece a esta copropiedad.");
        }
        if (valor is not null && def.Tipo == TipoCampoTablero.Directorio)
        {
            if (!Guid.TryParse(valor, out var entidadId))
                throw new InvalidOperationException("El valor de un campo Directorio debe ser el id de una persona del directorio.");
            var enDirectorio = await _db.DirectorioVinculos.AnyAsync(v =>
                v.EntidadTipo == EntidadDirectorio.Persona && v.EntidadId == entidadId && v.Estado == EstadoVinculo.Activo, ct);
            if (!enDirectorio)
                throw new InvalidOperationException("La persona seleccionada no esta en el directorio de esta copropiedad.");
        }

        var existente = await _db.UnidadCamposValores
            .FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.UnidadId == unidadId, ct);
        if (existente is null)
            _db.UnidadCamposValores.Add(new UnidadCampoValor { TenantId = tid, DefinicionId = definicionId, UnidadId = unidadId, Valor = valor });
        else
            existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    // -------- Documentos / anexos de una unidad --------

    public async Task<IReadOnlyList<UnidadDocumentoDto>> ListDocumentosUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        var rows = await _db.UnidadDocumentos.AsNoTracking()
            .Where(d => d.UnidadId == unidadId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Nombre, d.Url, d.Tamano })
            .ToListAsync(ct);
        // ResolveUrl normaliza URLs viejas absolutas (ej. localhost:8080/uploads/...) a ruta del mismo origen.
        return rows.Select(d => new UnidadDocumentoDto(d.Id, d.Nombre, _blob.ResolveUrl(d.Url) ?? d.Url, d.Tamano)).ToList();
    }

    public async Task<UnidadDocumentoDto?> AgregarDocumentoUnidadAsync(Guid unidadId, string nombre, string url, long tamano, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var d = new UnidadDocumento { TenantId = tid, UnidadId = unidadId, Nombre = nombre.Trim(), Url = url, Tamano = tamano };
        _db.UnidadDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Documento '{d.Nombre}' adjuntado a la unidad.", ct, unidadId);
        return new UnidadDocumentoDto(d.Id, d.Nombre, _blob.ResolveUrl(d.Url) ?? d.Url, d.Tamano);
    }

    public async Task<bool> EliminarDocumentoUnidadAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.UnidadDocumentos.FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return false;
        _db.UnidadDocumentos.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }

}
