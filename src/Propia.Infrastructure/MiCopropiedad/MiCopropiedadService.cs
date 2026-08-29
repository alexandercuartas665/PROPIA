using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

public class MiCopropiedadService : IMiCopropiedadService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly Storage.IBlobStorage _blob;
    private readonly Application.UsuariosAccesos.ISeedUsuarioRolService _seed;
    private readonly Application.Directorio.IDirectorioService _dir;
    public MiCopropiedadService(PropiaDbContext db, ITenantContext tenant, Storage.IBlobStorage blob,
        Application.UsuariosAccesos.ISeedUsuarioRolService seed,
        Application.Directorio.IDirectorioService dir)
    {
        _db = db;
        _tenant = tenant;
        _blob = blob;
        _seed = seed;
        _dir = dir;
    }

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
                u.ReferenciaPago))
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
            CuotaMensual = req.CuotaMensual
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
            unidad.Estado, unidad.Observaciones, unidad.MatriculaInmobiliaria, unidad.PagaAdministracion, unidad.CuotaMensual);
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

        u.Numero = numeroTrim;
        u.Tipo = req.Tipo;
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
            u.Estado, u.Observaciones, u.MatriculaInmobiliaria, u.PagaAdministracion, u.CuotaMensual);
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
            .Select(d => new UnidadCampoDefinicionDto(d.Id, d.Label, d.Orden))
            .ToListAsync(ct);

    public async Task<UnidadCampoDefinicionDto> CrearCampoDefinicionAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];

        var existente = await _db.UnidadCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null)
            return new UnidadCampoDefinicionDto(existente.Id, existente.Label, existente.Orden);

        var maxOrden = await _db.UnidadCamposDefiniciones.AnyAsync(ct)
            ? await _db.UnidadCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new UnidadCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1 };
        _db.UnidadCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Campo personalizado '{label}' agregado a todas las unidades.", ct);
        return new UnidadCampoDefinicionDto(def.Id, def.Label, def.Orden);
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
        return defs.Select(d => new UnidadCampoDto(
            d.Id, d.Label, d.Orden,
            valores.FirstOrDefault(v => v.DefinicionId == d.Id)?.Valor)).ToList();
    }

    public async Task SetCampoValorUnidadAsync(Guid unidadId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        _ = await _db.UnidadCamposDefiniciones.AnyAsync(d => d.Id == definicionId, ct)
            ? true : throw new InvalidOperationException("Campo no encontrado.");
        _ = await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)
            ? true : throw new InvalidOperationException("Unidad no encontrada.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();

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

    // ===================== Prototipo v3: bloques nuevos de la ficha de inmueble =====================

    // -------- Placas habilitadas para ingreso --------
    public async Task<IReadOnlyList<UnidadPlacaDto>> ListPlacasUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadPlacas.AsNoTracking()
            .Where(p => p.UnidadId == unidadId)
            .OrderBy(p => p.Placa)
            .Select(p => new UnidadPlacaDto(p.Id, p.Placa, p.TipoVehiculo))
            .ToListAsync(ct);

    public async Task<UnidadPlacaDto?> AgregarPlacaUnidadAsync(Guid unidadId, CrearUnidadPlacaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var placa = (req.Placa ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(placa)) throw new InvalidOperationException("La placa es obligatoria.");
        if (placa.Length > 15) placa = placa[..15];
        var e = new UnidadPlaca { TenantId = tid, UnidadId = unidadId, Placa = placa, TipoVehiculo = req.TipoVehiculo };
        _db.UnidadPlacas.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Placa '{placa}' habilitada en la unidad.", ct, unidadId);
        return new UnidadPlacaDto(e.Id, e.Placa, e.TipoVehiculo);
    }

    public async Task<bool> EliminarPlacaUnidadAsync(Guid placaId, CancellationToken ct)
    {
        var e = await _db.UnidadPlacas.FirstOrDefaultAsync(x => x.Id == placaId, ct);
        if (e is null) return false;
        _db.UnidadPlacas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Arriendos y cobros mensuales --------
    public async Task<IReadOnlyList<UnidadArriendoDto>> ListArriendosUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadArriendos.AsNoTracking()
            .Where(a => a.UnidadId == unidadId)
            .OrderBy(a => a.Concepto)
            .Select(a => new UnidadArriendoDto(a.Id, a.Concepto, a.ValorMensual, a.Referencia))
            .ToListAsync(ct);

    public async Task<UnidadArriendoDto?> AgregarArriendoUnidadAsync(Guid unidadId, CrearUnidadArriendoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var concepto = (req.Concepto ?? "").Trim();
        if (string.IsNullOrWhiteSpace(concepto)) throw new InvalidOperationException("El concepto es obligatorio.");
        if (req.ValorMensual < 0) throw new InvalidOperationException("El valor no puede ser negativo.");
        if (concepto.Length > 120) concepto = concepto[..120];
        var refTxt = string.IsNullOrWhiteSpace(req.Referencia) ? null : req.Referencia.Trim();
        var e = new UnidadArriendo { TenantId = tid, UnidadId = unidadId, Concepto = concepto, ValorMensual = req.ValorMensual, Referencia = refTxt };
        _db.UnidadArriendos.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Arriendo/cobro '{concepto}' agregado a la unidad.", ct, unidadId);
        return new UnidadArriendoDto(e.Id, e.Concepto, e.ValorMensual, e.Referencia);
    }

    public async Task<bool> EliminarArriendoUnidadAsync(Guid arriendoId, CancellationToken ct)
    {
        var e = await _db.UnidadArriendos.FirstOrDefaultAsync(x => x.Id == arriendoId, ct);
        if (e is null) return false;
        _db.UnidadArriendos.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Mascotas --------
    public async Task<IReadOnlyList<UnidadMascotaDto>> ListMascotasUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadMascotas.AsNoTracking()
            .Where(m => m.UnidadId == unidadId)
            .OrderBy(m => m.Nombre)
            .Select(m => new UnidadMascotaDto(m.Id, m.Nombre, m.Tipo, m.Raza))
            .ToListAsync(ct);

    public async Task<UnidadMascotaDto?> AgregarMascotaUnidadAsync(Guid unidadId, CrearUnidadMascotaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre)) throw new InvalidOperationException("El nombre de la mascota es obligatorio.");
        if (nombre.Length > 80) nombre = nombre[..80];
        var raza = string.IsNullOrWhiteSpace(req.Raza) ? null : req.Raza.Trim();
        var e = new UnidadMascota { TenantId = tid, UnidadId = unidadId, Nombre = nombre, Tipo = req.Tipo, Raza = raza };
        _db.UnidadMascotas.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Mascota '{nombre}' registrada en la unidad.", ct, unidadId);
        return new UnidadMascotaDto(e.Id, e.Nombre, e.Tipo, e.Raza);
    }

    public async Task<bool> EliminarMascotaUnidadAsync(Guid mascotaId, CancellationToken ct)
    {
        var e = await _db.UnidadMascotas.FirstOrDefaultAsync(x => x.Id == mascotaId, ct);
        if (e is null) return false;
        _db.UnidadMascotas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Empleada(s) de servicio --------
    public async Task<IReadOnlyList<UnidadEmpleadaDto>> ListEmpleadasUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadEmpleadas.AsNoTracking()
            .Where(x => x.UnidadId == unidadId)
            .OrderBy(x => x.Nombre)
            .Select(x => new UnidadEmpleadaDto(x.Id, x.Nombre, x.Documento, x.Celular, x.Horario, x.PersonaId))
            .ToListAsync(ct);

    public async Task<UnidadEmpleadaDto?> AgregarEmpleadaUnidadAsync(Guid unidadId, CrearUnidadEmpleadaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre)) throw new InvalidOperationException("El nombre es obligatorio.");
        if (nombre.Length > 120) nombre = nombre[..120];
        var e = new UnidadEmpleada
        {
            TenantId = tid,
            UnidadId = unidadId,
            PersonaId = req.PersonaId,
            Nombre = nombre,
            Documento = string.IsNullOrWhiteSpace(req.Documento) ? null : req.Documento.Trim(),
            Celular = string.IsNullOrWhiteSpace(req.Celular) ? null : req.Celular.Trim(),
            Horario = string.IsNullOrWhiteSpace(req.Horario) ? null : req.Horario.Trim()
        };
        _db.UnidadEmpleadas.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Empleada de servicio '{nombre}' registrada en la unidad.", ct, unidadId);
        return new UnidadEmpleadaDto(e.Id, e.Nombre, e.Documento, e.Celular, e.Horario, e.PersonaId);
    }

    public async Task<bool> EliminarEmpleadaUnidadAsync(Guid empleadaId, CancellationToken ct)
    {
        var e = await _db.UnidadEmpleadas.FirstOrDefaultAsync(x => x.Id == empleadaId, ct);
        if (e is null) return false;
        _db.UnidadEmpleadas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Oleada 2: Historico de titularidad (propietarios) --------
    public async Task<IReadOnlyList<UnidadTitularidadDto>> ListTitularidadUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadTitularidades.AsNoTracking()
            .Where(t => t.UnidadId == unidadId)
            .OrderByDescending(t => t.Desde)
            .Select(t => new UnidadTitularidadDto(t.Id, t.Nombre, t.Rol, t.Desde, t.Hasta, t.Hasta == null, t.PersonaId))
            .ToListAsync(ct);

    public async Task<UnidadTitularidadDto?> AgregarTitularidadUnidadAsync(Guid unidadId, CrearUnidadTitularidadRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre)) throw new InvalidOperationException("El nombre del titular es obligatorio.");
        if (nombre.Length > 160) nombre = nombre[..160];
        if (req.Hasta is { } h && h < req.Desde) throw new InvalidOperationException("La fecha 'hasta' no puede ser anterior a 'desde'.");
        var e = new UnidadTitularidad
        {
            TenantId = tid,
            UnidadId = unidadId,
            PersonaId = req.PersonaId,
            Nombre = nombre,
            Rol = string.IsNullOrWhiteSpace(req.Rol) ? null : req.Rol.Trim(),
            Desde = req.Desde,
            Hasta = req.Hasta
        };
        _db.UnidadTitularidades.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Titularidad historica '{nombre}' agregada a la unidad.", ct, unidadId);
        return new UnidadTitularidadDto(e.Id, e.Nombre, e.Rol, e.Desde, e.Hasta, e.Hasta == null, e.PersonaId);
    }

    public async Task<bool> EliminarTitularidadUnidadAsync(Guid titularidadId, CancellationToken ct)
    {
        var e = await _db.UnidadTitularidades.FirstOrDefaultAsync(x => x.Id == titularidadId, ct);
        if (e is null) return false;
        _db.UnidadTitularidades.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Oleada 2: Campos dinamicos por persona vinculada --------
    public async Task<IReadOnlyList<UnidadPersonaCampoDto>> ListCamposPersonaAsync(Guid unidadPersonaId, CancellationToken ct)
        => await _db.UnidadPersonaCampos.AsNoTracking()
            .Where(c => c.UnidadPersonaId == unidadPersonaId)
            .OrderBy(c => c.Label)
            .Select(c => new UnidadPersonaCampoDto(c.Id, c.Label, c.Valor))
            .ToListAsync(ct);

    public async Task<UnidadPersonaCampoDto?> AgregarCampoPersonaAsync(Guid unidadPersonaId, CrearUnidadPersonaCampoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadPersonas.AnyAsync(p => p.Id == unidadPersonaId, ct)) return null;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var e = new UnidadPersonaCampo
        {
            TenantId = tid,
            UnidadPersonaId = unidadPersonaId,
            Label = label,
            Valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim()
        };
        _db.UnidadPersonaCampos.Add(e);
        await _db.SaveChangesAsync(ct);
        return new UnidadPersonaCampoDto(e.Id, e.Label, e.Valor);
    }

    public async Task<bool> EliminarCampoPersonaAsync(Guid campoId, CancellationToken ct)
    {
        var e = await _db.UnidadPersonaCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (e is null) return false;
        _db.UnidadPersonaCampos.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Rebalanceo de coeficientes (Ley 675 art. 26: suma = 100) -----------------------------

    public async Task<RebalanceoCoeficientesDto> RebalancearCoeficientesAsync(CancellationToken ct)
    {
        var unidades = await _db.UnidadesPrivadas.ToListAsync(ct);
        var sumaAnterior = unidades.Sum(u => u.CoeficientePropiedad);

        // Estrategia: solo unidades con PagaAdministracion=true reciben coeficiente > 0.
        // Las que no pagan (parqueadero, deposito) van a 0. Distribucion equitativa con
        // compensacion en la ultima para que el total sea exactamente 100.0000.
        var pagan = unidades.Where(u => u.PagaAdministracion).OrderBy(u => u.Numero).ToList();
        var noPagan = unidades.Where(u => !u.PagaAdministracion).ToList();

        if (pagan.Count == 0)
        {
            // Nada que rebalancear, dejar todas en 0
            foreach (var u in unidades) u.CoeficientePropiedad = 0m;
            await _db.SaveChangesAsync(ct);
            return new RebalanceoCoeficientesDto(unidades.Count, sumaAnterior, 0m, 0m, 0, noPagan.Count);
        }

        var coefBase = Math.Round(100m / pagan.Count, 4, MidpointRounding.ToZero);
        decimal acumulado = 0m;
        for (int i = 0; i < pagan.Count; i++)
        {
            if (i == pagan.Count - 1)
            {
                // Compensacion en la ultima para evitar floating drift y forzar suma=100
                pagan[i].CoeficientePropiedad = Math.Round(100m - acumulado, 4);
            }
            else
            {
                pagan[i].CoeficientePropiedad = coefBase;
                acumulado += coefBase;
            }
        }
        foreach (var u in noPagan) u.CoeficientePropiedad = 0m;

        await _db.SaveChangesAsync(ct);
        var sumaNueva = unidades.Sum(u => u.CoeficientePropiedad);
        await RegistrarBitacoraAsync("Distribucion",
            $"Coeficientes rebalanceados: {pagan.Count} apartamentos a {coefBase}% (ultima ajustada), {noPagan.Count} en 0%. Suma anterior {sumaAnterior:0.##}% -> nueva {sumaNueva:0.##}%.", ct);
        return new RebalanceoCoeficientesDto(unidades.Count, sumaAnterior, sumaNueva, coefBase, pagan.Count, noPagan.Count);
    }

    // ----------------------------- Cuota consolidada (principal + asociadas con factura) -----------------------------

    public async Task<CuotaConsolidadaDto?> GetCuotaConsolidadaAsync(Guid unidadId, CancellationToken ct)
    {
        var principal = await _db.UnidadesPrivadas.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unidadId, ct);
        if (principal is null) return null;

        var asociadas = await _db.UnidadVinculos.AsNoTracking()
            .Include(v => v.UnidadAsociada)
            .Where(v => v.UnidadPrincipalId == unidadId)
            .ToListAsync(ct);

        var coefAsociadasFactura = asociadas
            .Where(v => v.IncluyeEnFacturacion)
            .Sum(v => v.UnidadAsociada!.CoeficientePropiedad);

        return new CuotaConsolidadaDto(
            principal.Id, principal.Numero,
            principal.CoeficientePropiedad,
            coefAsociadasFactura,
            principal.CoeficientePropiedad + coefAsociadasFactura,
            asociadas.Count,
            asociadas.Count(v => v.IncluyeEnFacturacion));
    }

    public async Task<UnidadDto?> ObtenerUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        return await _db.UnidadesPrivadas
            .AsNoTracking()
            .Include(u => u.Torre)
            .Where(u => u.Id == unidadId)
            .Select(u => new UnidadDto(
                u.Id, u.Numero, u.Tipo,
                u.TorreId, u.Torre != null ? u.Torre.Nombre : null, u.Piso,
                u.CoeficientePropiedad, u.AreaM2,
                u.Habitaciones, u.Banos, u.Parqueaderos,
                u.Estado, u.Observaciones, u.MatriculaInmobiliaria, u.PagaAdministracion, u.CuotaMensual,
                // Mismo propietario que en el listado, para que el DTO diga lo mismo venga de donde venga.
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
                (from v in _db.UnidadVinculos where v.UnidadAsociadaId == u.Id select (Guid?)v.UnidadPrincipalId).FirstOrDefault(),
                u.ReferenciaPago))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TipoUnidadCustomDto>> ListTiposUnidadCustomAsync(CancellationToken ct)
    {
        return await _db.TiposUnidadCustom
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoUnidadCustomDto(t.Id, t.Nombre, t.PagaAdministracionPorDefecto, t.Descripcion, t.Activo))
            .ToListAsync(ct);
    }

    public async Task<TipoUnidadCustomDto> CrearTipoUnidadCustomAsync(CrearTipoUnidadCustomRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del tipo es obligatorio.");
        var nombre = req.Nombre.Trim();
        if (await _db.TiposUnidadCustom.AnyAsync(t => t.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un tipo de unidad llamado '{nombre}' en esta copropiedad.");

        var t = new TipoUnidadCustom
        {
            Nombre = nombre,
            PagaAdministracionPorDefecto = req.PagaAdministracionPorDefecto,
            Descripcion = req.Descripcion,
            Activo = true
        };
        _db.TiposUnidadCustom.Add(t);
        await _db.SaveChangesAsync(ct);
        return new TipoUnidadCustomDto(t.Id, t.Nombre, t.PagaAdministracionPorDefecto, t.Descripcion, t.Activo);
    }

    public async Task<bool> EliminarTipoUnidadCustomAsync(Guid tipoId, CancellationToken ct)
    {
        var t = await _db.TiposUnidadCustom.FirstOrDefaultAsync(x => x.Id == tipoId, ct);
        if (t is null) return false;
        _db.TiposUnidadCustom.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Tipos de coeficiente (B2) -----------------------------

    public async Task<IReadOnlyList<TipoCoeficienteDto>> ListTiposCoeficienteAsync(CancellationToken ct)
    {
        // Si no hay ninguno, sembramos el default "Propiedad" (lazy init).
        var existen = await _db.TiposCoeficiente.AnyAsync(ct);
        if (!existen)
        {
            _db.TiposCoeficiente.Add(new TipoCoeficiente
            {
                Nombre = "Propiedad",
                Descripcion = "Coeficiente principal de la PH segun Ley 675",
                EsPrincipal = true,
                Activo = true
            });
            await _db.SaveChangesAsync(ct);
        }

        var tipos = await _db.TiposCoeficiente
            .AsNoTracking()
            .OrderByDescending(t => t.EsPrincipal).ThenBy(t => t.Nombre)
            .ToListAsync(ct);

        // Calcular suma actual por tipo (incluyendo el campo legacy CoeficientePropiedad para el tipo principal).
        var sumas = await _db.UnidadCoeficientes
            .AsNoTracking()
            .GroupBy(uc => uc.TipoCoeficienteId)
            .Select(g => new { TipoId = g.Key, Suma = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.TipoId, x => x.Suma, ct);

        var sumaLegacyPropiedad = await _db.UnidadesPrivadas.SumAsync(u => (decimal?)u.CoeficientePropiedad, ct) ?? 0m;

        return tipos.Select(t =>
        {
            var suma = sumas.GetValueOrDefault(t.Id, 0m);
            if (t.EsPrincipal) suma += sumaLegacyPropiedad - (sumas.GetValueOrDefault(t.Id, 0m) > 0 ? 0m : 0m);
            // Para principal: si hay valores en UnidadCoeficientes los usamos; si no, caemos al legacy.
            // Simplificacion: para el tipo principal sumamos LO QUE HAYA en UnidadCoeficientes;
            // si no hay registros (suma == 0), exponemos el legacy.
            var sumaReal = t.EsPrincipal && sumas.GetValueOrDefault(t.Id, 0m) == 0m
                ? sumaLegacyPropiedad
                : sumas.GetValueOrDefault(t.Id, 0m);
            return new TipoCoeficienteDto(t.Id, t.Nombre, t.Descripcion, t.EsPrincipal, t.Activo, sumaReal);
        }).ToList();
    }

    public async Task<TipoCoeficienteDto> CrearTipoCoeficienteAsync(CrearTipoCoeficienteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del tipo de coeficiente es obligatorio.");
        var nombre = req.Nombre.Trim();
        if (await _db.TiposCoeficiente.AnyAsync(x => x.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un tipo de coeficiente '{nombre}'.");

        var t = new TipoCoeficiente
        {
            Nombre = nombre,
            Descripcion = req.Descripcion,
            EsPrincipal = false,
            Activo = true
        };
        _db.TiposCoeficiente.Add(t);
        await _db.SaveChangesAsync(ct);
        return new TipoCoeficienteDto(t.Id, t.Nombre, t.Descripcion, t.EsPrincipal, t.Activo, 0m);
    }

    public async Task<bool> EliminarTipoCoeficienteAsync(Guid tipoId, CancellationToken ct)
    {
        var t = await _db.TiposCoeficiente.FirstOrDefaultAsync(x => x.Id == tipoId, ct);
        if (t is null) return false;
        if (t.EsPrincipal)
            throw new InvalidOperationException("No se puede eliminar el tipo principal de coeficiente.");

        // Cascade eliminara UnidadCoeficientes asociados via FK
        _db.TiposCoeficiente.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<UnidadCoeficienteDto>> ListCoeficientesUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        return await _db.UnidadCoeficientes
            .AsNoTracking()
            .Include(uc => uc.TipoCoeficiente)
            .Where(uc => uc.UnidadId == unidadId)
            .Select(uc => new UnidadCoeficienteDto(uc.TipoCoeficienteId, uc.TipoCoeficiente!.Nombre, uc.Valor))
            .ToListAsync(ct);
    }

    public async Task<UnidadCoeficienteDto> SetCoeficienteUnidadAsync(Guid unidadId, SetCoeficienteUnidadRequest req, CancellationToken ct)
    {
        if (req.Valor < 0)
            throw new InvalidOperationException("El coeficiente debe ser >= 0.");

        var unidad = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == unidadId, ct)
            ?? throw new InvalidOperationException("Unidad no encontrada.");
        var tipo = await _db.TiposCoeficiente.FirstOrDefaultAsync(t => t.Id == req.TipoCoeficienteId, ct)
            ?? throw new InvalidOperationException("Tipo de coeficiente no encontrado.");

        var existente = await _db.UnidadCoeficientes
            .FirstOrDefaultAsync(uc => uc.UnidadId == unidadId && uc.TipoCoeficienteId == req.TipoCoeficienteId, ct);

        if (existente is null)
        {
            existente = new UnidadCoeficiente
            {
                UnidadId = unidadId,
                TipoCoeficienteId = req.TipoCoeficienteId,
                Valor = req.Valor
            };
            _db.UnidadCoeficientes.Add(existente);
        }
        else
        {
            existente.Valor = req.Valor;
        }

        // Si es el tipo principal, mantenemos el campo legacy de la unidad sincronizado
        if (tipo.EsPrincipal)
            unidad.CoeficientePropiedad = req.Valor;

        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Coeficiente",
            $"Unidad '{unidad.Numero}': coeficiente '{tipo.Nombre}' = {req.Valor:0.####}%.", ct);
        return new UnidadCoeficienteDto(req.TipoCoeficienteId, tipo.Nombre, req.Valor);
    }

    public async Task<GenerarUnidadesResponse> GenerarUnidadesAsync(GenerarUnidadesRequest req, CancellationToken ct)
    {
        if (req.Torres is null || req.Torres.Count == 0)
            throw new InvalidOperationException("Debes definir al menos una torre/agrupacion.");
        if (req.CoeficientePorUnidad < 0)
            throw new InvalidOperationException("Coeficiente por unidad no puede ser negativo.");

        var torresCreadas = new List<Torre>();
        var unidadesCreadas = new List<UnidadPrivada>();
        var torresExistentes = await _db.Torres.CountAsync(ct);
        var unidadesExistentesNumeros = await _db.UnidadesPrivadas.Select(u => u.Numero).ToListAsync(ct);
        var unidadesGeneradas = new HashSet<string>(unidadesExistentesNumeros, StringComparer.OrdinalIgnoreCase);

        // Si hay torres pre-existentes o se piden multiples torres nuevas, usamos
        // prefijo de indice de torre para garantizar unicidad global de identificadores.
        var usarPrefijoIdx = req.Torres.Count > 1 || torresExistentes > 0;
        var corridoSeq = unidadesExistentesNumeros.Count + 1;
        var torreIdx = torresExistentes;

        foreach (var spec in req.Torres)
        {
            torreIdx++;
            if (string.IsNullOrWhiteSpace(spec.Nombre))
                throw new InvalidOperationException("Cada torre debe tener nombre.");
            if (spec.CantidadPisos <= 0 || spec.UnidadesPorPiso <= 0)
                throw new InvalidOperationException($"Torre '{spec.Nombre}': pisos y unidades por piso deben ser > 0.");
            if (await _db.Torres.AnyAsync(t => t.Nombre == spec.Nombre, ct))
                throw new InvalidOperationException($"Ya existe una torre llamada '{spec.Nombre}' en esta copropiedad.");

            var torre = new Torre { Nombre = spec.Nombre.Trim(), CantidadPisos = spec.CantidadPisos };
            _db.Torres.Add(torre);
            torresCreadas.Add(torre);

            for (var piso = 1; piso <= spec.CantidadPisos; piso++)
            {
                for (var n = 1; n <= spec.UnidadesPorPiso; n++)
                {
                    // PisoNumero: si ya hay torres en la copropiedad o se piden multiples,
                    // prefijamos con el indice de torre (Torre 1 piso 1 #1 -> 1101) para
                    // garantizar unicidad global. Con 1 sola torre y tenant vacio: 101, 102 (spec).
                    string numero;
                    if (req.Patron == PatronNumeracion.Corrido)
                    {
                        numero = corridoSeq.ToString();
                        corridoSeq++;
                    }
                    else
                    {
                        numero = usarPrefijoIdx ? $"{torreIdx}{piso}{n:D2}" : $"{piso}{n:D2}";
                    }
                    if (!unidadesGeneradas.Add(numero))
                        throw new InvalidOperationException(
                            $"Colision de identificador '{numero}'. Otra unidad ya tiene ese numero. " +
                            "Renombra la torre o cambia el patron de numeracion.");
                    var unidad = new UnidadPrivada
                    {
                        Numero = numero,
                        Tipo = req.TipoUnidadDefault,
                        Torre = torre,
                        Piso = piso,
                        CoeficientePropiedad = req.CoeficientePorUnidad
                    };
                    _db.UnidadesPrivadas.Add(unidad);
                    unidadesCreadas.Add(unidad);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        var nombresTorre = string.Join(", ", torresCreadas.Select(t => t.Nombre));
        await RegistrarBitacoraAsync("Distribucion",
            $"Generador automatico: {torresCreadas.Count} torre(s) [{nombresTorre}] + {unidadesCreadas.Count} unidades creadas (coef {req.CoeficientePorUnidad}% c/u).", ct);
        return new GenerarUnidadesResponse(
            torresCreadas.Count,
            unidadesCreadas.Count,
            torresCreadas.Select(t => t.Id).ToList(),
            unidadesCreadas.Select(u => u.Id).ToList());
    }

    public async Task<ImportarUnidadesResponse> ImportarUnidadesCsvAsync(ImportarUnidadesRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CsvContent))
            return new ImportarUnidadesResponse(false, 0, 0, 0m, new List<ImportacionFilaError>
            {
                new(0, "CSV", "El archivo viene vacio.")
            });

        // Formato esperado (header en fila 1):
        //   identificador,tipo_unidad,agrupacion,piso,coeficiente,area_m2,paga_administracion
        // Solo identificador, tipo_unidad y coeficiente son obligatorios.

        var lineas = req.CsvContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var errores = new List<ImportacionFilaError>();
        var filas = new List<(int Linea, string Numero, TipoUnidad Tipo, string? Agrupacion, int? Piso, decimal Coef, decimal? Area)>();

        if (lineas.Length < 2)
        {
            errores.Add(new(0, "CSV", "Necesitas al menos un encabezado y una fila de datos."));
            return new ImportarUnidadesResponse(false, 0, 0, 0m, errores);
        }

        var header = lineas[0].Split(',').Select(c => c.Trim().ToLowerInvariant()).ToList();
        int idxId = header.IndexOf("identificador");
        int idxTipo = header.IndexOf("tipo_unidad");
        int idxAgr = header.IndexOf("agrupacion");
        int idxPiso = header.IndexOf("piso");
        int idxCoef = header.IndexOf("coeficiente");
        int idxArea = header.IndexOf("area_m2");

        if (idxId < 0 || idxTipo < 0 || idxCoef < 0)
        {
            errores.Add(new(1, "header", "Faltan columnas obligatorias: identificador, tipo_unidad, coeficiente."));
            return new ImportarUnidadesResponse(false, 0, 0, 0m, errores);
        }

        var numerosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal suma = 0m;

        for (int i = 1; i < lineas.Length; i++)
        {
            var linea = lineas[i];
            if (string.IsNullOrWhiteSpace(linea)) continue;
            var celdas = linea.Split(',');
            var nroFila = i + 1;

            string numero = celdas.Length > idxId ? celdas[idxId].Trim() : "";
            string tipoTxt = celdas.Length > idxTipo ? celdas[idxTipo].Trim() : "";
            string coefTxt = celdas.Length > idxCoef ? celdas[idxCoef].Trim() : "";

            if (string.IsNullOrEmpty(numero))
            {
                errores.Add(new(nroFila, "identificador", "Campo obligatorio."));
                continue;
            }
            if (!numerosVistos.Add(numero))
            {
                errores.Add(new(nroFila, "identificador", $"Identificador '{numero}' aparece duplicado en el archivo."));
            }
            if (!Enum.TryParse<TipoUnidad>(tipoTxt, ignoreCase: true, out var tipo))
            {
                errores.Add(new(nroFila, "tipo_unidad", $"Tipo '{tipoTxt}' no esta en el catalogo base."));
                continue;
            }
            if (!decimal.TryParse(coefTxt, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var coef) || coef < 0)
            {
                errores.Add(new(nroFila, "coeficiente", $"Formato invalido. Usa decimal positivo (ej. 0.023400)."));
                continue;
            }

            string? agrupacion = idxAgr >= 0 && celdas.Length > idxAgr ? celdas[idxAgr].Trim() : null;
            if (string.IsNullOrWhiteSpace(agrupacion)) agrupacion = null;

            int? piso = null;
            if (idxPiso >= 0 && celdas.Length > idxPiso && int.TryParse(celdas[idxPiso].Trim(), out var p)) piso = p;

            decimal? area = null;
            if (idxArea >= 0 && celdas.Length > idxArea && decimal.TryParse(celdas[idxArea].Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var a)) area = a;

            suma += coef;
            filas.Add((nroFila, numero, tipo, agrupacion, piso, coef, area));
        }

        if (errores.Count > 0)
            return new ImportarUnidadesResponse(false, filas.Count, 0, suma, errores);

        // Identificadores duplicados contra BD
        var numerosArchivo = filas.Select(f => f.Numero).ToList();
        var duplicados = await _db.UnidadesPrivadas
            .Where(u => numerosArchivo.Contains(u.Numero))
            .Select(u => u.Numero)
            .ToListAsync(ct);
        foreach (var dup in duplicados)
            errores.Add(new(0, "identificador", $"El identificador '{dup}' ya existe en la copropiedad."));
        if (errores.Count > 0)
            return new ImportarUnidadesResponse(false, filas.Count, 0, suma, errores);

        // Crear torres faltantes
        var nombresTorres = filas.Where(f => f.Agrupacion is not null).Select(f => f.Agrupacion!).Distinct().ToList();
        var torresExistentes = await _db.Torres
            .Where(t => nombresTorres.Contains(t.Nombre))
            .ToDictionaryAsync(t => t.Nombre, t => t, ct);
        foreach (var nombreTorre in nombresTorres.Where(n => !torresExistentes.ContainsKey(n)))
        {
            var nuevaTorre = new Torre { Nombre = nombreTorre };
            _db.Torres.Add(nuevaTorre);
            torresExistentes[nombreTorre] = nuevaTorre;
        }

        foreach (var f in filas)
        {
            var torreId = f.Agrupacion is not null ? torresExistentes[f.Agrupacion].Id : (Guid?)null;
            var torre = f.Agrupacion is not null ? torresExistentes[f.Agrupacion] : null;
            _db.UnidadesPrivadas.Add(new UnidadPrivada
            {
                Numero = f.Numero,
                Tipo = f.Tipo,
                Piso = f.Piso,
                CoeficientePropiedad = f.Coef,
                AreaM2 = f.Area,
                Torre = torre  // EF asigna torreId al guardar la torre nueva
            });
        }
        await _db.SaveChangesAsync(ct);
        return new ImportarUnidadesResponse(true, filas.Count, filas.Count, suma, Array.Empty<ImportacionFilaError>());
    }

    public async Task<bool> EliminarUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        var u = await _db.UnidadesPrivadas.FirstOrDefaultAsync(x => x.Id == unidadId, ct);
        if (u is null) return false;
        _db.UnidadesPrivadas.Remove(u);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "No se puede eliminar: la unidad tiene registros asociados (cartera, PQRSD, reservas, etc.). Desvinculalos primero.");
        }
        return true;
    }

    // ----------------------------- Seccion 4: Gobierno -----------------------------

    public async Task<IReadOnlyList<MiembroConsejoDto>> ListMiembrosConsejoAsync(CancellationToken ct)
    {
        return await _db.MiembrosConsejo
            .AsNoTracking()
            .Include(m => m.Persona)
            .OrderBy(m => m.Cargo)
            .Select(m => new MiembroConsejoDto(
                m.Id, m.PersonaId,
                m.Persona != null ? $"{m.Persona.Nombres} {m.Persona.Apellidos}" : "Sin asignar",
                m.Cargo, m.FechaInicio, m.FechaFin, m.Activo))
            .ToListAsync(ct);
    }

    public async Task<MiembroConsejoDto> AgregarMiembroConsejoAsync(AgregarMiembroConsejoRequest req, CancellationToken ct)
    {
        var persona = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.PersonaId, ct);
        if (persona is null) throw new InvalidOperationException("Persona no encontrada en el Directorio.");

        // Regla: solo puede haber 1 miembro activo por cargo (excepto Vocal y Suplente)
        if (req.Cargo != CargoConsejo.Vocal && req.Cargo != CargoConsejo.Suplente)
        {
            var existe = await _db.MiembrosConsejo.AnyAsync(m => m.Cargo == req.Cargo && m.Activo, ct);
            if (existe) throw new InvalidOperationException($"Ya existe un miembro activo con cargo {req.Cargo}. Desactivalo primero.");
        }

        var m = new MiembroConsejo
        {
            PersonaId = req.PersonaId,
            Cargo = req.Cargo,
            FechaInicio = req.FechaInicio,
            FechaFin = req.FechaFin,
            Activo = true
        };
        _db.MiembrosConsejo.Add(m);
        await _db.SaveChangesAsync(ct);
        return new MiembroConsejoDto(m.Id, m.PersonaId,
            $"{persona.Nombres} {persona.Apellidos}",
            m.Cargo, m.FechaInicio, m.FechaFin, m.Activo);
    }

    public async Task<bool> DesactivarMiembroConsejoAsync(Guid miembroId, CancellationToken ct)
    {
        var m = await _db.MiembrosConsejo.FirstOrDefaultAsync(x => x.Id == miembroId, ct);
        if (m is null) return false;
        m.Activo = false;
        m.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 5: Servicios -----------------------------

    public async Task<IReadOnlyList<ContratoServicioDto>> ListContratosAsync(CancellationToken ct)
    {
        await AsegurarEtapasBaseAsync(ct);
        var contratos = await _db.ContratosServicio
            .AsNoTracking()
            .Include(x => x.Adjuntos)
            .OrderBy(c => c.Tipo)
            .ToListAsync(ct);
        var ids = contratos.Select(c => c.Id).ToList();
        var valores = await _db.ContratoCampoValores.AsNoTracking()
            .Where(v => ids.Contains(v.ContratoId))
            .ToListAsync(ct);
        var porContrato = valores.GroupBy(v => v.ContratoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ContratoCampoValorDto>)g
                .Select(v => new ContratoCampoValorDto(v.ContratoCampoId, v.Valor)).ToList());

        // "Asociado a": resolver nombres de equipos/zonas referenciados, en batch.
        var equipoIds = contratos.Where(c => c.AsociadoTipo == TipoActivoMantenimiento.Equipo && c.AsociadoId.HasValue).Select(c => c.AsociadoId!.Value).Distinct().ToList();
        var zonaIds = contratos.Where(c => c.AsociadoTipo == TipoActivoMantenimiento.ZonaComun && c.AsociadoId.HasValue).Select(c => c.AsociadoId!.Value).Distinct().ToList();
        var equipoNombres = equipoIds.Count == 0 ? new() : await _db.EquiposActivos.AsNoTracking().Where(e => equipoIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nombre, ct);
        var zonaNombres = zonaIds.Count == 0 ? new() : await _db.ZonasComunes.AsNoTracking().Where(z => zonaIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Nombre, ct);
        string? AsocNombre(ContratoServicio c) => c.AsociadoId is not { } id ? null
            : c.AsociadoTipo == TipoActivoMantenimiento.Equipo ? equipoNombres.GetValueOrDefault(id)
            : c.AsociadoTipo == TipoActivoMantenimiento.ZonaComun ? zonaNombres.GetValueOrDefault(id)
            : null;

        return contratos.Select(c => ToContratoDto(c, porContrato.GetValueOrDefault(c.Id), AsocNombre(c))).ToList();
    }

    public async Task<ContratoServicioDto> CrearContratoAsync(CrearContratoServicioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Proveedor))
            throw new InvalidOperationException("Proveedor obligatorio.");
        var c = new ContratoServicio
        {
            Tipo = req.Tipo,
            ProveedorPersonaId = req.ProveedorPersonaId,
            ProveedorEmpresaId = req.ProveedorEmpresaId,
            Proveedor = req.Proveedor,
            NitProveedor = req.NitProveedor,
            ContactoPersonaId = req.ContactoPersonaId,
            Contacto = req.Contacto,
            FechaInicio = req.FechaInicio,
            FechaFin = req.FechaFin,
            ValorMensual = req.ValorMensual,
            Observaciones = req.Observaciones,
            DiasAnticipacionAlerta = req.DiasAnticipacionAlerta <= 0 ? 30 : req.DiasAnticipacionAlerta,
            RenovacionAutomatica = req.RenovacionAutomatica,
            ServicioId = req.ServicioId,
            ExpedienteId = req.ExpedienteId,
            ProyectoTareaId = req.ProyectoTareaId,
            // ----- Campos del pedido de Contratos (Ola 1) -----
            NumeroContrato = string.IsNullOrWhiteSpace(req.NumeroContrato) ? null : req.NumeroContrato.Trim(),
            TipoContrato = req.TipoContrato,
            Categoria = req.Categoria,
            ValorTotal = req.ValorTotal,
            FormaPagoCuotas = req.FormaPagoCuotas,
            PagoMensual = req.PagoMensual,
            AsociadoTipo = req.AsociadoTipo,
            AsociadoId = req.AsociadoId
        };
        _db.ContratosServicio.Add(c);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Contrato", $"Contrato con '{c.Proveedor}' creado.", ct, c.Id);
        return ToContratoDto(c);
    }

    public async Task<bool> ActualizarContratoAsync(Guid contratoId, ActualizarContratoRequest req, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        // "Vencido" se deriva por fecha; el admin solo declara Vigente o EnRenovacion.
        c.Estado = req.Estado == EstadoContrato.Vencido ? EstadoContrato.Vigente : req.Estado;
        c.DiasAnticipacionAlerta = req.DiasAnticipacionAlerta <= 0 ? 30 : req.DiasAnticipacionAlerta;
        // MERGE de datos del contrato (solo lo provisto; conserva el resto). La tool MCP no manda estos.
        if (req.Tipo.HasValue) c.Tipo = req.Tipo.Value;
        if (!string.IsNullOrWhiteSpace(req.Proveedor)) c.Proveedor = req.Proveedor.Trim();
        if (req.NitProveedor is not null) c.NitProveedor = string.IsNullOrWhiteSpace(req.NitProveedor) ? null : req.NitProveedor.Trim();
        if (req.Contacto is not null) c.Contacto = string.IsNullOrWhiteSpace(req.Contacto) ? null : req.Contacto.Trim();
        if (req.FechaInicio.HasValue) c.FechaInicio = req.FechaInicio.Value;
        if (req.FechaFin.HasValue) c.FechaFin = req.FechaFin.Value;
        if (req.ValorMensual.HasValue) c.ValorMensual = req.ValorMensual.Value;
        if (req.Observaciones is not null) c.Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones.Trim();
        // ----- Campos del pedido de Contratos (Ola 1). MERGE: se aplican si vienen. -----
        if (req.NumeroContrato is not null) c.NumeroContrato = string.IsNullOrWhiteSpace(req.NumeroContrato) ? null : req.NumeroContrato.Trim();
        if (req.TipoContrato.HasValue) c.TipoContrato = req.TipoContrato.Value;
        if (req.Categoria.HasValue) c.Categoria = req.Categoria.Value;
        if (req.ValorTotal.HasValue) c.ValorTotal = req.ValorTotal.Value;
        if (req.FormaPagoCuotas.HasValue) c.FormaPagoCuotas = req.FormaPagoCuotas.Value;
        if (req.PagoMensual.HasValue) c.PagoMensual = req.PagoMensual.Value;
        if (req.LimpiarAsociado) { c.AsociadoTipo = null; c.AsociadoId = null; }
        else if (req.AsociadoTipo.HasValue && req.AsociadoId.HasValue) { c.AsociadoTipo = req.AsociadoTipo.Value; c.AsociadoId = req.AsociadoId.Value; }
        // Vinculos: solo el editor de la pagina los toca (ActualizarVinculos=true). La tool MCP no.
        if (req.ActualizarVinculos)
        {
            c.RenovacionAutomatica = req.RenovacionAutomatica;
            c.ServicioId = req.ServicioId;
            c.ExpedienteId = req.ExpedienteId;
            c.ProyectoTareaId = req.ProyectoTareaId;
            // Tercero del Directorio (contratista): se persisten los FK del selector.
            c.ProveedorPersonaId = req.ProveedorPersonaId;
            c.ProveedorEmpresaId = req.ProveedorEmpresaId;
            c.ContactoPersonaId = req.ContactoPersonaId;
        }
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Contrato", $"Contrato con '{c.Proveedor}' actualizado.", ct, c.Id);
        return true;
    }

    /// <summary>Semaforo de vencimiento por % de dias totales (Ola 3): sin fecha fin = Ninguno;
    /// vencido = Rojo; &lt;=10% restante = Rojo; &lt;=20% = Amarillo; resto = Verde.</summary>
    public static SemaforoContrato CalcularSemaforoContrato(DateOnly inicio, DateOnly? fin, DateOnly hoy)
    {
        if (fin is not { } f) return SemaforoContrato.Ninguno;
        var restante = f.DayNumber - hoy.DayNumber;
        if (restante < 0) return SemaforoContrato.Rojo;                 // vencido
        var total = f.DayNumber - inicio.DayNumber;
        if (total <= 0) return SemaforoContrato.Rojo;                   // fin <= inicio: critico
        var pct = (double)restante / total;
        return pct <= 0.10 ? SemaforoContrato.Rojo
             : pct <= 0.20 ? SemaforoContrato.Amarillo
             : SemaforoContrato.Verde;
    }

    private static ContratoServicioDto ToContratoDto(ContratoServicio c, IReadOnlyList<ContratoCampoValorDto>? valores = null, string? asociadoNombre = null)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        int? dias = c.FechaFin.HasValue ? c.FechaFin.Value.DayNumber - hoy.DayNumber : null;
        var estado = (c.FechaFin.HasValue && c.FechaFin.Value < hoy) ? EstadoContrato.Vencido : c.Estado;
        // Semaforo por % de dias totales del contrato (Ola 3): 20% -> amarillo, 10% o vencido -> rojo.
        var semaforo = CalcularSemaforoContrato(c.FechaInicio, c.FechaFin, hoy);
        var alerta = semaforo is SemaforoContrato.Amarillo or SemaforoContrato.Rojo;
        return new ContratoServicioDto(c.Id, c.Tipo, c.Proveedor, c.NitProveedor, c.Contacto,
            c.FechaInicio, c.FechaFin, c.ValorMensual, c.Observaciones,
            estado, c.DiasAnticipacionAlerta, dias, alerta,
            c.RenovacionAutomatica, c.ServicioId, c.ExpedienteId, c.ProyectoTareaId,
            c.Adjuntos?.Count ?? 0, valores, c.EtapaId,
            c.NumeroContrato, c.TipoContrato, c.Categoria, c.ValorTotal, c.FormaPagoCuotas, c.PagoMensual,
            c.AsociadoTipo, c.AsociadoId, asociadoNombre,
            c.ProveedorPersonaId, c.ProveedorEmpresaId, c.ContactoPersonaId, semaforo);
    }

    public async Task<bool> EliminarContratoAsync(Guid contratoId, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        // Limpiar los valores EAV del contrato (no hay cascade configurado).
        var valores = await _db.ContratoCampoValores.Where(v => v.ContratoId == contratoId).ToListAsync(ct);
        if (valores.Count > 0) _db.ContratoCampoValores.RemoveRange(valores);
        var vincs = await _db.ContratoExpedientes.Where(v => v.ContratoId == contratoId).ToListAsync(ct);
        if (vincs.Count > 0) _db.ContratoExpedientes.RemoveRange(vincs);
        _db.ContratosServicio.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Expedientes vinculados a un contrato (Ola 2: pestana Documentos) ----
    public async Task<IReadOnlyList<ContratoExpedienteDto>> ListExpedientesContratoAsync(Guid contratoId, CancellationToken ct)
    {
        // Dos queries (evita el Join entre DbSets con HasQueryFilter, que EF no traduce).
        var ids = await _db.ContratoExpedientes.AsNoTracking()
            .Where(v => v.ContratoId == contratoId)
            .Select(v => v.ExpedienteId)
            .ToListAsync(ct);
        if (ids.Count == 0) return Array.Empty<ContratoExpedienteDto>();
        return await _db.Expedientes.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .OrderBy(e => e.Codigo)
            .Select(e => new ContratoExpedienteDto(e.Id, e.Codigo, e.Nombre))
            .ToListAsync(ct);
    }

    public async Task<bool> VincularExpedienteContratoAsync(Guid contratoId, Guid expedienteId, CancellationToken ct)
    {
        if (!await _db.ContratosServicio.AnyAsync(c => c.Id == contratoId, ct)) return false;
        if (!await _db.Expedientes.AnyAsync(e => e.Id == expedienteId, ct)) return false;
        if (await _db.ContratoExpedientes.AnyAsync(v => v.ContratoId == contratoId && v.ExpedienteId == expedienteId, ct))
            return true;   // ya vinculado, idempotente
        _db.ContratoExpedientes.Add(new ContratoExpediente { ContratoId = contratoId, ExpedienteId = expedienteId });
        await _db.SaveChangesAsync(ct);
        var cod = await _db.Expedientes.Where(e => e.Id == expedienteId).Select(e => e.Codigo).FirstOrDefaultAsync(ct);
        await RegistrarBitacoraAsync("Contrato", $"Expediente '{cod}' conectado al contrato.", ct, contratoId);
        return true;
    }

    public async Task<bool> DesvincularExpedienteContratoAsync(Guid contratoId, Guid expedienteId, CancellationToken ct)
    {
        var v = await _db.ContratoExpedientes.FirstOrDefaultAsync(x => x.ContratoId == contratoId && x.ExpedienteId == expedienteId, ct);
        if (v is null) return false;
        _db.ContratoExpedientes.Remove(v);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Contrato", "Expediente desconectado del contrato.", ct, contratoId);
        return true;
    }

    // ---- Campos personalizados (EAV) de contratos ----
    public async Task<IReadOnlyList<ContratoCampoDto>> ListContratoCamposAsync(CancellationToken ct)
    {
        return await _db.ContratoCampos.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new ContratoCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.Descripcion, c.Activo))
            .ToListAsync(ct);
    }

    public async Task<ContratoCampoDto> CrearContratoCampoAsync(CrearContratoCampoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new InvalidOperationException("El nombre del campo es obligatorio.");
        var maxOrden = await _db.ContratoCampos.AnyAsync(ct) ? await _db.ContratoCampos.MaxAsync(c => (int?)c.Orden, ct) ?? 0 : 0;
        var campo = new ContratoCampo
        {
            Label = req.Label.Trim(),
            Tipo = req.Tipo,
            Opciones = string.IsNullOrWhiteSpace(req.Opciones) ? null : req.Opciones.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Orden = maxOrden + 1,
            Activo = true
        };
        _db.ContratoCampos.Add(campo);
        await _db.SaveChangesAsync(ct);
        return new ContratoCampoDto(campo.Id, campo.Label, campo.Orden, campo.Tipo, campo.Opciones, campo.Descripcion, campo.Activo);
    }

    public async Task<bool> ActualizarContratoCampoAsync(Guid campoId, ActualizarContratoCampoRequest req, CancellationToken ct)
    {
        var campo = await _db.ContratoCampos.FirstOrDefaultAsync(c => c.Id == campoId, ct);
        if (campo is null) return false;
        if (!string.IsNullOrWhiteSpace(req.Label)) campo.Label = req.Label.Trim();
        campo.Tipo = req.Tipo;
        campo.Opciones = string.IsNullOrWhiteSpace(req.Opciones) ? null : req.Opciones.Trim();
        campo.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        campo.Orden = req.Orden;
        campo.Activo = req.Activo;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarContratoCampoAsync(Guid campoId, CancellationToken ct)
    {
        var campo = await _db.ContratoCampos.FirstOrDefaultAsync(c => c.Id == campoId, ct);
        if (campo is null) return false;
        var valores = await _db.ContratoCampoValores.Where(v => v.ContratoCampoId == campoId).ToListAsync(ct);
        if (valores.Count > 0) _db.ContratoCampoValores.RemoveRange(valores);
        _db.ContratoCampos.Remove(campo);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> GuardarContratoCampoValorAsync(Guid contratoId, Guid campoId, GuardarContratoCampoValorRequest req, CancellationToken ct)
    {
        var contrato = await _db.ContratosServicio.AnyAsync(c => c.Id == contratoId, ct);
        if (!contrato) return false;
        var campo = await _db.ContratoCampos.AnyAsync(c => c.Id == campoId, ct);
        if (!campo) return false;
        var val = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.ContratoCampoValores
            .FirstOrDefaultAsync(v => v.ContratoId == contratoId && v.ContratoCampoId == campoId, ct);
        if (existente is null)
        {
            if (val is null) return true;   // nada que guardar
            _db.ContratoCampoValores.Add(new ContratoCampoValor { ContratoId = contratoId, ContratoCampoId = campoId, Valor = val });
        }
        else if (val is null)
        {
            _db.ContratoCampoValores.Remove(existente);
        }
        else
        {
            existente.Valor = val;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Etapas de flujo (Kanban) de contratos ----
    // Siembra las 4 etapas base por copropiedad si no existen y ancla los contratos sin etapa a "Activo".
    private async Task AsegurarEtapasBaseAsync(CancellationToken ct)
    {
        if (await _db.ContratoEtapas.AnyAsync(ct)) return;
        var baseEtapas = new (string Nombre, string Color)[]
        {
            ("En tramite", "#3B82F6"),
            ("Pendiente aprobacion asamblea", "#F59E0B"),
            ("Activo", "#22C55E"),
            ("Terminado", "#6B7280"),
        };
        var creadas = new List<ContratoEtapa>();
        for (int i = 0; i < baseEtapas.Length; i++)
        {
            var e = new ContratoEtapa { Nombre = baseEtapas[i].Nombre, Color = baseEtapas[i].Color, Orden = i + 1 };
            _db.ContratoEtapas.Add(e);
            creadas.Add(e);
        }
        await _db.SaveChangesAsync(ct);
        // Contratos existentes sin etapa -> "Activo" (la tercera).
        var activo = creadas[2];
        await _db.ContratosServicio.Where(c => c.EtapaId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EtapaId, activo.Id), ct);
    }

    public async Task<IReadOnlyList<ContratoEtapaDto>> ListContratoEtapasAsync(CancellationToken ct)
    {
        await AsegurarEtapasBaseAsync(ct);
        return await _db.ContratoEtapas.AsNoTracking()
            .OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new ContratoEtapaDto(e.Id, e.Nombre, e.Orden, e.Color))
            .ToListAsync(ct);
    }

    public async Task<ContratoEtapaDto> CrearContratoEtapaAsync(CrearContratoEtapaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la etapa es obligatorio.");
        await AsegurarEtapasBaseAsync(ct);
        var maxOrden = await _db.ContratoEtapas.AnyAsync(ct) ? await _db.ContratoEtapas.MaxAsync(e => (int?)e.Orden, ct) ?? 0 : 0;
        var etapa = new ContratoEtapa { Nombre = req.Nombre.Trim(), Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim(), Orden = maxOrden + 1 };
        _db.ContratoEtapas.Add(etapa);
        await _db.SaveChangesAsync(ct);
        return new ContratoEtapaDto(etapa.Id, etapa.Nombre, etapa.Orden, etapa.Color);
    }

    public async Task<bool> ActualizarContratoEtapaAsync(Guid etapaId, ActualizarContratoEtapaRequest req, CancellationToken ct)
    {
        var etapa = await _db.ContratoEtapas.FirstOrDefaultAsync(e => e.Id == etapaId, ct);
        if (etapa is null) return false;
        if (!string.IsNullOrWhiteSpace(req.Nombre)) etapa.Nombre = req.Nombre.Trim();
        etapa.Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarContratoEtapaAsync(Guid etapaId, CancellationToken ct)
    {
        var etapa = await _db.ContratoEtapas.FirstOrDefaultAsync(e => e.Id == etapaId, ct);
        if (etapa is null) return false;
        // No permitir borrar la ultima etapa; reasignar los contratos a otra etapa antes de borrar.
        var otras = await _db.ContratoEtapas.Where(e => e.Id != etapaId).OrderBy(e => e.Orden).ToListAsync(ct);
        if (otras.Count == 0) return false;
        var destino = otras.First().Id;
        await _db.ContratosServicio.Where(c => c.EtapaId == etapaId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EtapaId, destino), ct);
        _db.ContratoEtapas.Remove(etapa);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReordenarContratoEtapasAsync(ReordenarContratoEtapasRequest req, CancellationToken ct)
    {
        if (req.Orden is null || req.Orden.Count == 0) return;
        var etapas = await _db.ContratoEtapas.ToListAsync(ct);
        for (int i = 0; i < req.Orden.Count; i++)
        {
            var e = etapas.FirstOrDefault(x => x.Id == req.Orden[i]);
            if (e is not null) e.Orden = i + 1;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> CambiarEtapaContratoAsync(Guid contratoId, CambiarEtapaContratoRequest req, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        if (req.EtapaId is { } eid && !await _db.ContratoEtapas.AnyAsync(e => e.Id == eid, ct)) return false;
        c.EtapaId = req.EtapaId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 6: Zonas Comunes -----------------------------

    public async Task<IReadOnlyList<ZonaComunDto>> ListZonasComunesAsync(CancellationToken ct)
    {
        return await _db.ZonasComunes
            .AsNoTracking()
            .OrderBy(z => z.Nombre)
            .Select(z => new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion,
                z.EsReservable, z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso, z.Estado))
            .ToListAsync(ct);
    }

    public async Task<ZonaComunDto> CrearZonaComunAsync(CrearZonaComunRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre de la zona obligatorio.");
        var z = new ZonaComun
        {
            Nombre = req.Nombre,
            Categoria = req.Categoria,
            Descripcion = req.Descripcion,
            EsReservable = req.EsReservable,
            TarifaReserva = req.TarifaReserva,
            CapacidadPersonas = req.CapacidadPersonas,
            HorariosUso = req.HorariosUso,
            ReglasUso = req.ReglasUso
        };
        _db.ZonasComunes.Add(z);
        await _db.SaveChangesAsync(ct);
        return new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion,
            z.EsReservable, z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso, z.Estado);
    }

    public async Task<ZonaComunDto?> ActualizarZonaComunAsync(Guid zonaId, ActualizarZonaComunRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre de la zona obligatorio.");
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return null;
        var prevEstado = z.Estado;
        z.Nombre = req.Nombre.Trim();
        z.Categoria = req.Categoria;
        z.Descripcion = req.Descripcion;
        z.TarifaReserva = req.TarifaReserva;
        z.ReglasUso = req.ReglasUso;
        z.Estado = req.Estado;
        // Reserva y aforo solo se tocan si vienen en el request (edicion inline de la tabla);
        // null = conservar el valor actual (la plantilla y la ficha no los mandan por aqui).
        if (req.EsReservable is bool r) z.EsReservable = r;
        if (req.CapacidadPersonas is not null) z.CapacidadPersonas = req.CapacidadPersonas;
        await _db.SaveChangesAsync(ct);
        if (prevEstado != req.Estado)
            await RegistrarBitacoraAsync("Zona", $"Zona '{z.Nombre}': estado {prevEstado} -> {req.Estado}.", ct);
        return new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion,
            z.EsReservable, z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso, z.Estado);
    }

    public async Task<bool> CambiarEstadoZonaAsync(Guid zonaId, CambiarEstadoZonaRequest req, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return false;
        var prev = z.Estado;
        z.Estado = req.Estado;  // RN-13: EnMantenimiento bloquea reservas en 2.13; RN-14: Inactiva conserva el registro
        await _db.SaveChangesAsync(ct);
        if (prev != req.Estado)
            await RegistrarBitacoraAsync("Zona", $"Zona '{z.Nombre}': estado {prev} -> {req.Estado}.", ct);
        return true;
    }

    public async Task<bool> EliminarZonaComunAsync(Guid zonaId, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return false;
        // Guarda: no eliminar una zona con reservas asociadas (FK Restrict en Reserva).
        var nReservas = await _db.Reservas.CountAsync(r => r.ZonaComunId == zonaId, ct);
        if (nReservas > 0)
            throw new InvalidOperationException($"No se puede eliminar: la zona tiene {nReservas} reserva(s) asociada(s).");
        _db.ZonasComunes.Remove(z);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 7: Equipos -----------------------------

    public async Task<IReadOnlyList<EquipoActivoDto>> ListEquiposAsync(CancellationToken ct)
    {
        return await _db.EquiposActivos
            .AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => ToEquipoActivoDto(e))
            .ToListAsync(ct);
    }

    private static EquipoActivoDto ToEquipoActivoDto(EquipoActivo e) => new(
        e.Id, e.Nombre, e.Categoria,
        e.Tipo, e.Cantidad, e.EsReservable,
        e.Marca, e.Modelo, e.NumeroSerie, e.CodigoBarra, e.FechaInstalacion, e.GarantiaHasta,
        e.Ubicacion, e.Observaciones,
        e.VidaUtilAnios, e.FechaAdquisicion, e.ValorAdquisicion,
        e.Proveedor, e.NumeroFactura, e.Estado, e.CondicionesUso);

    public async Task<EquipoActivoDto> CrearEquipoAsync(CrearEquipoActivoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del equipo obligatorio.");
        if (req.Cantidad < 1)
            throw new InvalidOperationException("Cantidad debe ser al menos 1.");
        var e = new EquipoActivo
        {
            Nombre = req.Nombre.Trim(),
            Categoria = req.Categoria,
            Tipo = req.Tipo,
            Cantidad = req.Tipo == TipoElemento.Activo ? req.Cantidad : 1,
            EsReservable = req.EsReservable
        };
        _db.EquiposActivos.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Equipos", $"{e.Tipo} '{e.Nombre}' agregado (cantidad {e.Cantidad}).", ct);
        return ToEquipoActivoDto(e);
    }

    public async Task<EquipoActivoDto?> ActualizarEquipoAsync(Guid id, ActualizarEquipoActivoRequest req, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre obligatorio.");
        e.Nombre = req.Nombre.Trim();
        e.Categoria = req.Categoria;
        e.Tipo = req.Tipo;
        e.Cantidad = req.Tipo == TipoElemento.Activo ? Math.Max(1, req.Cantidad) : 1;
        e.EsReservable = req.EsReservable;
        e.Modelo = req.Modelo;
        e.NumeroSerie = req.NumeroSerie;
        e.CodigoBarra = string.IsNullOrWhiteSpace(req.CodigoBarra) ? null : req.CodigoBarra.Trim();
        e.CondicionesUso = string.IsNullOrWhiteSpace(req.CondicionesUso) ? null : req.CondicionesUso.Trim();
        e.FechaInstalacion = req.FechaInstalacion;
        e.GarantiaHasta = req.GarantiaHasta;
        e.Ubicacion = req.Ubicacion;
        e.Observaciones = req.Observaciones;
        e.VidaUtilAnios = req.VidaUtilAnios;
        e.FechaAdquisicion = req.FechaAdquisicion;
        e.ValorAdquisicion = req.ValorAdquisicion;
        e.Proveedor = req.Proveedor;
        e.NumeroFactura = req.NumeroFactura;
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Equipos", $"Ficha tecnica de '{e.Nombre}' actualizada.", ct);
        return ToEquipoActivoDto(e);
    }

    // ----------------------------- Ventanas de disponibilidad -----------------------------

    public async Task<IReadOnlyList<VentanaDisponibilidadDto>> ListVentanasAsync(TipoEntidadDisponibilidad tipo, Guid entidadId, CancellationToken ct)
    {
        return await _db.VentanasDisponibilidad.AsNoTracking()
            .Where(v => v.TipoEntidad == tipo && v.EntidadId == entidadId)
            .OrderBy(v => v.DiaSemana).ThenBy(v => v.HoraInicio)
            .Select(v => new VentanaDisponibilidadDto(v.Id, v.TipoEntidad, v.EntidadId, v.DiaSemana, v.HoraInicio, v.HoraFin, v.Activa))
            .ToListAsync(ct);
    }

    public async Task GuardarVentanasAsync(GuardarVentanasRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid)
            throw new InvalidOperationException("Sin tenant activo.");
        // Reemplazo total: borra las existentes y crea las nuevas. Simple y correcto para un editor.
        var existentes = _db.VentanasDisponibilidad.Where(v => v.TipoEntidad == req.TipoEntidad && v.EntidadId == req.EntidadId);
        _db.VentanasDisponibilidad.RemoveRange(existentes);
        foreach (var v in req.Ventanas ?? Array.Empty<NuevaVentanaItem>())
        {
            if (v.HoraFin <= v.HoraInicio) continue; // ignora rangos invertidos
            _db.VentanasDisponibilidad.Add(new VentanaDisponibilidad
            {
                TenantId = tid,
                TipoEntidad = req.TipoEntidad,
                EntidadId = req.EntidadId,
                DiaSemana = v.DiaSemana,
                HoraInicio = v.HoraInicio,
                HoraFin = v.HoraFin,
                Activa = v.Activa
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> CambiarEstadoEquipoAsync(Guid equipoId, CambiarEstadoEquipoRequest req, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.FirstOrDefaultAsync(x => x.Id == equipoId, ct);
        if (e is null) return false;
        var prev = e.Estado;
        e.Estado = req.Estado;
        await _db.SaveChangesAsync(ct);
        if (prev != req.Estado)
            await RegistrarBitacoraAsync("Equipo", $"Equipo '{e.Nombre}': estado {prev} -> {req.Estado}.", ct);
        return true;
    }

    public async Task<bool> EliminarEquipoAsync(Guid equipoId, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.FirstOrDefaultAsync(x => x.Id == equipoId, ct);
        if (e is null) return false;
        _db.EquiposActivos.Remove(e);
        // Los hijos de ficha (fotos/mejoras/campos) caen en cascada; si algo mas lo referencia, avisar.
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("No se puede eliminar: el equipo/activo tiene registros asociados.");
        }
        return true;
    }

    // ----------------------------- Ficha tecnica completa del equipo/activo -----------------------------

    public async Task<EquipoFichaDto?> GetEquipoFichaAsync(Guid equipoId, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == equipoId, ct);
        if (e is null) return null;

        var fotos = (await _db.EquipoFotos.AsNoTracking().Where(f => f.EquipoActivoId == equipoId)
            .Select(f => new { f.Id, f.Url }).ToListAsync(ct))
            .Select(f => new EquipoFotoDto(f.Id, _blob.ResolveUrl(f.Url) ?? f.Url)).ToList();

        var mejoras = (await _db.EquipoMejoras.AsNoTracking().Where(m => m.EquipoActivoId == equipoId)
            .OrderByDescending(m => m.Fecha)
            .Select(m => new { m.Id, m.Descripcion, m.Valor, m.Fecha, m.DocumentoUrl }).ToListAsync(ct))
            .Select(m => new EquipoMejoraDto(m.Id, m.Descripcion, m.Valor, m.Fecha, _blob.ResolveUrl(m.DocumentoUrl))).ToList();

        var campos = await _db.EquipoCamposPersonalizados.AsNoTracking().Where(c => c.EquipoActivoId == equipoId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new EquipoCampoDto(c.Id, c.Label, c.Valor)).ToListAsync(ct);

        var depreciacion = CalcularDepreciacion(e, mejoras.Sum(m => m.Valor));

        // Contratos vinculados + disponibles
        var contratosVinculadosIds = await _db.EquipoContratoVinculos.AsNoTracking()
            .Where(v => v.EquipoActivoId == equipoId).Select(v => v.ContratoServicioId).ToListAsync(ct);
        var contratos = await _db.ContratosServicio.AsNoTracking().ToListAsync(ct);
        var contratosVinculados = contratos.Where(c => contratosVinculadosIds.Contains(c.Id))
            .Select(c => new EquipoRefDto(c.Id, $"{c.Tipo} - {c.Proveedor}", c.Estado.ToString())).ToList();
        var contratosDisponibles = contratos.Where(c => !contratosVinculadosIds.Contains(c.Id))
            .Select(c => new EquipoRefDto(c.Id, $"{c.Tipo} - {c.Proveedor}", null)).ToList();

        // Activos vinculados + disponibles (otros equipos)
        var vinculadosIds = await _db.EquipoVinculos.AsNoTracking()
            .Where(v => v.EquipoActivoId == equipoId).Select(v => v.EquipoVinculadoId).ToListAsync(ct);
        var todos = await _db.EquiposActivos.AsNoTracking().Where(x => x.Id != equipoId)
            .Select(x => new { x.Id, x.Nombre, x.Categoria }).ToListAsync(ct);
        var activosVinculados = todos.Where(x => vinculadosIds.Contains(x.Id))
            .Select(x => new EquipoRefDto(x.Id, x.Nombre, x.Categoria.ToString())).ToList();
        var activosDisponibles = todos.Where(x => !vinculadosIds.Contains(x.Id))
            .Select(x => new EquipoRefDto(x.Id, x.Nombre, null)).ToList();

        // Mantenimientos (intervenciones de este activo)
        var intervenciones = await _db.MantenimientoIntervenciones.AsNoTracking()
            .Where(i => i.ActivoId == equipoId)
            .OrderByDescending(i => i.FechaProgramada)
            .Take(20)
            .ToListAsync(ct);
        var mantenimientos = intervenciones.Select(i => new MantenimientoRefDto(
            i.Id, i.Tipo.ToString(), i.Titulo, null, i.FechaProgramada, i.Estado.ToString(), i.TareaId)).ToList();

        // Tareas asociadas (las intervenciones que generaron tarea en 2.10)
        var tareaIds = intervenciones.Where(i => i.TareaId.HasValue).Select(i => i.TareaId!.Value).ToList();
        var tareas = await _db.Tareas.AsNoTracking().Where(t => tareaIds.Contains(t.Id))
            .Select(t => new TareaRefDto(t.Id, t.Titulo, "Mantenimiento", t.Estado != null ? t.Estado.Nombre : "—"))
            .ToListAsync(ct);

        return new EquipoFichaDto(
            ToEquipoActivoDto(e), depreciacion, fotos, mejoras, campos,
            contratosVinculados, contratosDisponibles,
            activosVinculados, activosDisponibles,
            mantenimientos, tareas);
    }

    /// <summary>Depreciacion lineal: base = costo + mejoras; depAnual = base/vidaUtil; acumulada por anios de uso.</summary>
    private static DepreciacionDto CalcularDepreciacion(EquipoActivo e, decimal mejoras)
    {
        var costo = e.ValorAdquisicion ?? 0m;
        var baseDep = costo + mejoras;
        var vida = e.VidaUtilAnios.GetValueOrDefault();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = e.FechaAdquisicion ?? e.FechaInstalacion;
        var aniosUso = 0;
        if (inicio.HasValue)
        {
            aniosUso = hoy.Year - inicio.Value.Year;
            if (hoy.Month < inicio.Value.Month || (hoy.Month == inicio.Value.Month && hoy.Day < inicio.Value.Day)) aniosUso--;
            if (aniosUso < 0) aniosUso = 0;
        }
        var depAnual = vida > 0 ? Math.Round(baseDep / vida, 2) : 0m;
        var depAcum = vida > 0 ? Math.Min(baseDep, depAnual * Math.Min(aniosUso, vida)) : 0m;
        var valorLibros = baseDep - depAcum;
        var pct = baseDep > 0 ? (int)Math.Round(depAcum / baseDep * 100) : 0;
        return new DepreciacionDto(costo, mejoras, baseDep, vida, aniosUso, depAnual, depAcum, valorLibros, pct);
    }

    public async Task<EquipoFotoDto?> AgregarFotoEquipoAsync(Guid equipoId, string url, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(url)) return null;
        var f = new EquipoFoto { TenantId = tid, EquipoActivoId = equipoId, Url = url.Trim() };
        _db.EquipoFotos.Add(f);
        await _db.SaveChangesAsync(ct);
        return new EquipoFotoDto(f.Id, _blob.ResolveUrl(f.Url) ?? f.Url);
    }

    public async Task<bool> EliminarFotoEquipoAsync(Guid fotoId, CancellationToken ct)
    {
        var f = await _db.EquipoFotos.FirstOrDefaultAsync(x => x.Id == fotoId, ct);
        if (f is null) return false;
        _db.EquipoFotos.Remove(f);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EquipoMejoraDto?> AgregarMejoraEquipoAsync(Guid equipoId, AgregarMejoraRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(req.Descripcion)) throw new InvalidOperationException("Descripcion de la mejora obligatoria.");
        if (req.Valor < 0) throw new InvalidOperationException("El valor no puede ser negativo.");
        var m = new EquipoMejora { TenantId = tid, EquipoActivoId = equipoId, Descripcion = req.Descripcion.Trim(), Valor = req.Valor, Fecha = req.Fecha };
        _db.EquipoMejoras.Add(m);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Equipos", $"Mejora capitalizada registrada: {m.Descripcion} ({m.Valor:N0}).", ct);
        return new EquipoMejoraDto(m.Id, m.Descripcion, m.Valor, m.Fecha, _blob.ResolveUrl(m.DocumentoUrl));
    }

    public async Task<bool> EliminarMejoraEquipoAsync(Guid mejoraId, CancellationToken ct)
    {
        var m = await _db.EquipoMejoras.FirstOrDefaultAsync(x => x.Id == mejoraId, ct);
        if (m is null) return false;
        _db.EquipoMejoras.Remove(m);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ToggleActivoVinculadoAsync(Guid equipoId, ToggleVinculoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return;
        var existente = await _db.EquipoVinculos.FirstOrDefaultAsync(v => v.EquipoActivoId == equipoId && v.EquipoVinculadoId == req.Id, ct);
        if (req.Vincular && existente is null)
            _db.EquipoVinculos.Add(new EquipoVinculo { TenantId = tid, EquipoActivoId = equipoId, EquipoVinculadoId = req.Id });
        else if (!req.Vincular && existente is not null)
            _db.EquipoVinculos.Remove(existente);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ToggleContratoVinculadoAsync(Guid equipoId, ToggleVinculoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return;
        var existente = await _db.EquipoContratoVinculos.FirstOrDefaultAsync(v => v.EquipoActivoId == equipoId && v.ContratoServicioId == req.Id, ct);
        if (req.Vincular && existente is null)
            _db.EquipoContratoVinculos.Add(new EquipoContratoVinculo { TenantId = tid, EquipoActivoId = equipoId, ContratoServicioId = req.Id });
        else if (!req.Vincular && existente is not null)
            _db.EquipoContratoVinculos.Remove(existente);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EquipoCampoDto?> AgregarCampoEquipoAsync(Guid equipoId, AgregarCampoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        var c = new EquipoCampoPersonalizado
        {
            TenantId = tid,
            EquipoActivoId = equipoId,
            Label = req.Label.Trim(),
            Valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim()
        };
        _db.EquipoCamposPersonalizados.Add(c);
        await _db.SaveChangesAsync(ct);
        return new EquipoCampoDto(c.Id, c.Label, c.Valor);
    }

    public async Task<bool> EliminarCampoEquipoAsync(Guid campoId, CancellationToken ct)
    {
        var c = await _db.EquipoCamposPersonalizados.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        _db.EquipoCamposPersonalizados.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 4 ampliada: Comites + Revisor Fiscal -----------------------------

    public async Task<IReadOnlyList<ComiteDto>> ListComitesAsync(CancellationToken ct)
    {
        return await _db.Comites
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new ComiteDto(c.Id, c.Nombre, c.Descripcion, c.FechaConformacion, c.Activo,
                _db.ComiteMiembros.Count(m => m.ComiteId == c.Id && m.Activo)))
            .ToListAsync(ct);
    }

    public async Task<ComiteDto> CrearComiteAsync(CrearComiteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del comite es obligatorio.");
        var nombre = req.Nombre.Trim();
        if (await _db.Comites.AnyAsync(x => x.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un comite llamado '{nombre}'.");

        var c = new Comite
        {
            Nombre = nombre,
            Descripcion = req.Descripcion,
            FechaConformacion = req.FechaConformacion,
            Activo = true
        };
        _db.Comites.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ComiteDto(c.Id, c.Nombre, c.Descripcion, c.FechaConformacion, c.Activo, 0);
    }

    public async Task<bool> DesactivarComiteAsync(Guid comiteId, CancellationToken ct)
    {
        var c = await _db.Comites.FirstOrDefaultAsync(x => x.Id == comiteId, ct);
        if (c is null) return false;
        c.Activo = false;
        c.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ComiteMiembroDto>> ListMiembrosComiteAsync(Guid comiteId, CancellationToken ct)
    {
        return await _db.ComiteMiembros
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.ComiteId == comiteId)
            .Select(m => new ComiteMiembroDto(m.Id, m.ComiteId, m.PersonaId,
                m.Persona!.Nombres + " " + m.Persona.Apellidos, m.CargoEnComite, m.Activo))
            .ToListAsync(ct);
    }

    public async Task<ComiteMiembroDto> AgregarMiembroComiteAsync(AgregarComiteMiembroRequest req, CancellationToken ct)
    {
        var existe = await _db.Comites.AnyAsync(c => c.Id == req.ComiteId, ct);
        if (!existe) throw new InvalidOperationException("Comite no encontrado.");
        var personaOk = await _db.Personas.AnyAsync(p => p.Id == req.PersonaId, ct);
        if (!personaOk) throw new InvalidOperationException("Persona no encontrada.");
        if (await _db.ComiteMiembros.AnyAsync(m => m.ComiteId == req.ComiteId && m.PersonaId == req.PersonaId, ct))
            throw new InvalidOperationException("Esta persona ya esta en el comite.");

        var m = new ComiteMiembro
        {
            ComiteId = req.ComiteId,
            PersonaId = req.PersonaId,
            CargoEnComite = req.CargoEnComite,
            Activo = true
        };
        _db.ComiteMiembros.Add(m);
        await _db.SaveChangesAsync(ct);
        var persona = await _db.Personas.FirstAsync(p => p.Id == req.PersonaId, ct);
        return new ComiteMiembroDto(m.Id, m.ComiteId, m.PersonaId, $"{persona.Nombres} {persona.Apellidos}", m.CargoEnComite, m.Activo);
    }

    public async Task<bool> RetirarMiembroComiteAsync(Guid miembroId, CancellationToken ct)
    {
        var m = await _db.ComiteMiembros.FirstOrDefaultAsync(x => x.Id == miembroId, ct);
        if (m is null) return false;
        m.Activo = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RevisorFiscalDto?> GetRevisorFiscalActivoAsync(CancellationToken ct)
    {
        return await _db.RevisoresFiscales
            .AsNoTracking()
            .Include(r => r.Persona)
            .Where(r => r.Activo)
            .OrderByDescending(r => r.FechaPosesion)
            .Select(r => new RevisorFiscalDto(r.Id, r.PersonaId,
                r.Persona!.Nombres + " " + r.Persona.Apellidos,
                r.NumeroTarjetaProfesional, r.FechaPosesion, r.FechaFin, r.Activo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RevisorFiscalDto> DesignarRevisorFiscalAsync(DesignarRevisorFiscalRequest req, CancellationToken ct)
    {
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Id == req.PersonaId, ct)
            ?? throw new InvalidOperationException("Persona no encontrada.");

        // Retira cualquier revisor previo activo
        var previos = await _db.RevisoresFiscales.Where(r => r.Activo).ToListAsync(ct);
        foreach (var prev in previos)
        {
            prev.Activo = false;
            prev.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var r = new RevisorFiscal
        {
            PersonaId = req.PersonaId,
            NumeroTarjetaProfesional = req.NumeroTarjetaProfesional,
            FechaPosesion = req.FechaPosesion,
            Activo = true
        };
        _db.RevisoresFiscales.Add(r);
        await _db.SaveChangesAsync(ct);
        return new RevisorFiscalDto(r.Id, r.PersonaId, $"{persona.Nombres} {persona.Apellidos}",
            r.NumeroTarjetaProfesional, r.FechaPosesion, r.FechaFin, r.Activo);
    }

    public async Task<bool> RetirarRevisorFiscalAsync(Guid revisorId, CancellationToken ct)
    {
        var r = await _db.RevisoresFiscales.FirstOrDefaultAsync(x => x.Id == revisorId, ct);
        if (r is null) return false;
        r.Activo = false;
        r.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 3: Equipo de trabajo -----------------------------

    public async Task<IReadOnlyList<MiembroEquipoDto>> ListEquipoAsync(CancellationToken ct)
    {
        return await _db.MiembrosEquipo
            .AsNoTracking()
            .Include(m => m.Persona)
            .OrderByDescending(m => m.Activo).ThenBy(m => m.Rol).ThenBy(m => m.Persona!.Apellidos)
            .Select(m => new MiembroEquipoDto(m.Id, m.PersonaId,
                m.Persona!.Nombres + " " + m.Persona.Apellidos,
                m.Rol, m.RolPersonalizado, m.Tipo,
                m.FechaVinculacion, m.FechaFin, m.Activo, m.EsUsuarioSistema,
                m.Telefono, m.Email))
            .ToListAsync(ct);
    }

    public async Task<MiembroEquipoDto> AgregarMiembroEquipoAsync(AgregarMiembroEquipoRequest req, CancellationToken ct)
    {
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Id == req.PersonaId, ct)
            ?? throw new InvalidOperationException("Persona no encontrada. Usa /vincular-persona para crearla.");

        var m = new MiembroEquipo
        {
            PersonaId = req.PersonaId,
            Rol = req.Rol,
            RolPersonalizado = req.Rol == RolEquipo.Otro ? req.RolPersonalizado?.Trim() : null,
            Tipo = req.Tipo,
            FechaVinculacion = req.FechaVinculacion,
            Activo = true,
            Telefono = req.Telefono,
            Email = req.Email,
            Observaciones = req.Observaciones
        };
        _db.MiembrosEquipo.Add(m);
        await _db.SaveChangesAsync(ct);
        return new MiembroEquipoDto(m.Id, m.PersonaId, $"{persona.Nombres} {persona.Apellidos}",
            m.Rol, m.RolPersonalizado, m.Tipo,
            m.FechaVinculacion, m.FechaFin, m.Activo, m.EsUsuarioSistema,
            m.Telefono, m.Email);
    }

    public async Task<bool> DesactivarMiembroEquipoAsync(Guid miembroId, CancellationToken ct)
    {
        var m = await _db.MiembrosEquipo.FirstOrDefaultAsync(x => x.Id == miembroId, ct);
        if (m is null) return false;
        m.Activo = false;
        m.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<GobiernoPersonaDto> GetGobiernoPersonaAsync(Guid personaId, CancellationToken ct)
    {
        var consejo = await _db.MiembrosConsejo.AsNoTracking()
            .Where(m => m.PersonaId == personaId && m.Activo)
            .Select(m => new GobiernoConsejoTag(m.Id, m.Cargo))
            .FirstOrDefaultAsync(ct);

        var comites = await _db.ComiteMiembros.AsNoTracking()
            .Where(cm => cm.PersonaId == personaId && cm.Activo)
            .Join(_db.Comites, cm => cm.ComiteId, c => c.Id,
                  (cm, c) => new GobiernoComiteTag(cm.Id, c.Id, c.Nombre, cm.CargoEnComite))
            .ToListAsync(ct);

        var revisor = await _db.RevisoresFiscales.AsNoTracking()
            .Where(r => r.PersonaId == personaId && r.Activo)
            .Select(r => new GobiernoRevisorTag(r.Id, r.NumeroTarjetaProfesional))
            .FirstOrDefaultAsync(ct);

        var equipo = await _db.MiembrosEquipo.AsNoTracking()
            .Where(e => e.PersonaId == personaId && e.Activo)
            .Select(e => new GobiernoEquipoTag(e.Id, e.Rol, e.RolPersonalizado))
            .FirstOrDefaultAsync(ct);

        return new GobiernoPersonaDto(consejo, comites, revisor, equipo);
    }

    public async Task<Guid> VincularPersonaPorDocumentoAsync(VincularPersonaPorDocumentoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Documento))
            throw new InvalidOperationException("Documento es obligatorio.");
        var doc = req.Documento.Trim();

        var existente = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Documento == doc, ct);
        if (existente is not null)
        {
            // Ya existia en la plataforma (quiza creada en otra copropiedad). Se vincula a
            // esta para que aparezca en el Directorio y en todos los selectores de persona.
            await Directorio.VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, existente.Id, ct);
            return existente.Id;
        }

        if (string.IsNullOrWhiteSpace(req.Nombres) || string.IsNullOrWhiteSpace(req.Apellidos))
            throw new InvalidOperationException("Nombres y apellidos son obligatorios al crear una persona nueva.");

        var nueva = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = doc,
            Nombres = req.Nombres.Trim(),
            Apellidos = req.Apellidos.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Telefono = req.Telefono
        };
        _db.Personas.Add(nueva);
        await _db.SaveChangesAsync(ct);
        await Directorio.VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, nueva.Id, ct);
        return nueva.Id;
    }

    // ----------------------------- Seccion 8: Finanzas -----------------------------

    // Maximo legal de la tasa de mora MENSUAL (placeholder configurable). La spec preve
    // actualizarlo desde la Superfinanciera - diferido. Sirve para validar la tasa fija (RN-18).
    private const decimal TasaMoraMaximaLegalMensual = 2.5m;

    // Catalogo de monedas (ISO 4217) estatico - reference data fija.
    private static readonly IReadOnlyList<MonedaDto> _monedas = new List<MonedaDto>
    {
        new("COP", "Peso colombiano", "$"),
        new("USD", "Dolar estadounidense", "US$"),
        new("EUR", "Euro", "EUR"),
        new("MXN", "Peso mexicano", "MX$"),
        new("PEN", "Sol peruano", "S/"),
        new("CLP", "Peso chileno", "CLP$"),
        new("ARS", "Peso argentino", "AR$"),
        new("BRL", "Real brasileno", "R$"),
    };

    public IReadOnlyList<MonedaDto> ListMonedas() => _monedas;

    public async Task<FinanzasParametrosDto> GetFinanzasParametrosAsync(Guid tenantId, CancellationToken ct)
    {
        var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");
        return ToFinanzasParametros(t);
    }

    public async Task<FinanzasParametrosDto> ActualizarFinanzasAsync(Guid tenantId, ActualizarFinanzasRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");

        var moneda = (req.Moneda ?? "").Trim().ToUpperInvariant();
        if (!_monedas.Any(m => m.Codigo == moneda))
            throw new InvalidOperationException("Moneda invalida. Usa un codigo del catalogo (ISO 4217).");

        if (req.DiaCorte < 1 || req.DiaCorte > 28)
            throw new InvalidOperationException("El dia de corte debe estar entre 1 y 28 (RN-17).");

        if (req.PeriodoGraciaDias < 0 || req.PeriodoGraciaDias > 30)
            throw new InvalidOperationException("El periodo de gracia debe estar entre 0 y 30 dias.");

        if (!req.TasaMoraEsLegal)
        {
            if (req.TasaMoraValor is null || req.TasaMoraValor < 0)
                throw new InvalidOperationException("Ingresa una tasa de mora valida.");
            if (req.TasaMoraValor > TasaMoraMaximaLegalMensual)
                throw new InvalidOperationException(
                    $"La tasa fija ({req.TasaMoraValor:0.##}%) supera el maximo legal mensual permitido ({TasaMoraMaximaLegalMensual:0.##}%) (RN-18).");
        }

        // Valores previos para la bitacora (RN-06)
        var monedaPrev = t.Moneda;
        var cortePrev = t.DiaCorte;

        t.Moneda = moneda;
        t.DiaCorte = req.DiaCorte;
        t.TasaMoraEsLegal = req.TasaMoraEsLegal;
        t.TasaMoraValor = req.TasaMoraEsLegal ? null : req.TasaMoraValor;
        t.PeriodoGraciaDias = req.PeriodoGraciaDias;
        t.FinanzasConfiguradas = true;
        await _db.SaveChangesAsync(ct);

        if (monedaPrev != moneda)
            await RegistrarBitacoraAsync("Finanzas", $"Cambio de moneda de {monedaPrev} a {moneda}.", ct);
        if (cortePrev != req.DiaCorte)
            await RegistrarBitacoraAsync("Finanzas", $"Cambio del dia de corte de {cortePrev} a {req.DiaCorte}.", ct);

        return ToFinanzasParametros(t);
    }

    private static FinanzasParametrosDto ToFinanzasParametros(Tenant t) =>
        new(t.Moneda, t.DiaCorte, t.TasaMoraEsLegal, t.TasaMoraValor, t.PeriodoGraciaDias,
            t.FinanzasConfiguradas, TasaMoraMaximaLegalMensual);

    // ----------------------------- Configuracion avanzada de Finanzas -----------------------------

    public async Task<ConfiguracionFinanzasDto> GetConfiguracionFinanzasAsync(Guid tenantId, CancellationToken ct)
    {
        var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");
        return ToConfiguracionFinanzas(t);
    }

    public async Task<ConfiguracionFinanzasDto> ActualizarConfiguracionFinanzasAsync(Guid tenantId, ActualizarConfiguracionFinanzasRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");

        if (req.MinimoSaldoProntoPago < 0 || req.MinimoSaldoCartera < 0)
            throw new InvalidOperationException("Los minimos de saldo no pueden ser negativos.");
        if (req.EstratoFacturacion is < 1 or > 6)
            throw new InvalidOperationException("El estrato debe estar entre 1 y 6.");

        t.MultiploRedondeo = req.MultiploRedondeo;
        t.MultiploRedondeoCuotaExtra = req.MultiploRedondeoCuotaExtra;
        t.MultiploRedondeoProntoPago = req.MultiploRedondeoProntoPago;
        t.ConsecutivoFactura = NormalizarConsec(req.ConsecutivoFactura);
        t.ConsecutivoRC = NormalizarConsec(req.ConsecutivoRC);
        t.ConsecutivoNotaCredito = NormalizarConsec(req.ConsecutivoNotaCredito);
        t.ConsecutivoPazYSalvo = NormalizarConsec(req.ConsecutivoPazYSalvo);
        t.ConsecutivoActaConsejo = NormalizarConsec(req.ConsecutivoActaConsejo);
        t.ConsecutivoActaAsamblea = NormalizarConsec(req.ConsecutivoActaAsamblea);
        t.ConvenioRecaudo = string.IsNullOrWhiteSpace(req.ConvenioRecaudo) ? null : req.ConvenioRecaudo.Trim();
        t.Chartld = string.IsNullOrWhiteSpace(req.Chartld) ? null : req.Chartld.Trim();
        t.ComunicacionFactura = string.IsNullOrWhiteSpace(req.ComunicacionFactura) ? null : req.ComunicacionFactura.Trim();
        t.WenjoyCodigoRecaudo = string.IsNullOrWhiteSpace(req.WenjoyCodigoRecaudo) ? null : req.WenjoyCodigoRecaudo.Trim();
        t.TiposPagoPermitidos = req.TiposPagoPermitidos;
        t.FormasDePago = string.IsNullOrWhiteSpace(req.FormasDePago) ? null : req.FormasDePago.Trim();
        t.MinimoSaldoProntoPago = req.MinimoSaldoProntoPago;
        t.MinimoSaldoCartera = req.MinimoSaldoCartera;
        t.CuentaContable = string.IsNullOrWhiteSpace(req.CuentaContable) ? null : req.CuentaContable.Trim();
        t.ZonaFacturacion = string.IsNullOrWhiteSpace(req.ZonaFacturacion) ? null : req.ZonaFacturacion.Trim();
        t.EstratoFacturacion = req.EstratoFacturacion;
        await _db.SaveChangesAsync(ct);

        await RegistrarBitacoraAsync("Finanzas", "Configuracion avanzada actualizada (mas informacion).", ct);
        return ToConfiguracionFinanzas(t);
    }

    /// <summary>Normaliza un consecutivo (texto libre: admite prefijos tipo "FAC-001", "RC-2026-0042").
    /// Vacio o solo espacios se guarda como null para que el front muestre placeholder.</summary>
    private static string? NormalizarConsec(string? v)
        => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static ConfiguracionFinanzasDto ToConfiguracionFinanzas(Tenant t) => new(
        t.MultiploRedondeo, t.MultiploRedondeoCuotaExtra, t.MultiploRedondeoProntoPago,
        t.ConsecutivoFactura, t.ConsecutivoRC, t.ConsecutivoNotaCredito, t.ConsecutivoPazYSalvo,
        t.ConsecutivoActaConsejo, t.ConsecutivoActaAsamblea,
        t.ConvenioRecaudo, t.Chartld, t.ComunicacionFactura, t.WenjoyCodigoRecaudo,
        t.TiposPagoPermitidos, t.FormasDePago,
        t.MinimoSaldoProntoPago, t.MinimoSaldoCartera,
        t.CuentaContable, t.ZonaFacturacion, t.EstratoFacturacion);

    // ----------------------------- Cuentas bancarias -----------------------------

    public async Task<IReadOnlyList<CuentaBancariaDto>> ListCuentasBancariasAsync(CancellationToken ct)
    {
        return await _db.CuentasBancarias.AsNoTracking()
            .OrderBy(c => c.Cancelada)
            .ThenBy(c => c.Banco)
            .Select(c => new CuentaBancariaDto(c.Id, c.NumeroCuenta, c.TipoCuenta, c.Banco, c.VerEnFactura, c.Cancelada, c.FechaCancelacion))
            .ToListAsync(ct);
    }

    public async Task<CuentaBancariaDto> CrearCuentaBancariaAsync(CrearCuentaBancariaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid)
            throw new InvalidOperationException("Sin tenant activo.");
        if (string.IsNullOrWhiteSpace(req.NumeroCuenta))
            throw new InvalidOperationException("Numero de cuenta requerido.");
        if (string.IsNullOrWhiteSpace(req.Banco))
            throw new InvalidOperationException("Banco requerido.");

        var c = new CuentaBancaria
        {
            TenantId = tid,
            NumeroCuenta = req.NumeroCuenta.Trim(),
            TipoCuenta = req.TipoCuenta,
            Banco = req.Banco.Trim(),
            VerEnFactura = req.VerEnFactura,
            Cancelada = false
        };
        _db.CuentasBancarias.Add(c);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Finanzas", $"Cuenta bancaria agregada: {c.Banco} {c.NumeroCuenta}.", ct);
        return new CuentaBancariaDto(c.Id, c.NumeroCuenta, c.TipoCuenta, c.Banco, c.VerEnFactura, c.Cancelada, c.FechaCancelacion);
    }

    public async Task<CuentaBancariaDto?> ActualizarCuentaBancariaAsync(Guid id, ActualizarCuentaBancariaRequest req, CancellationToken ct)
    {
        var c = await _db.CuentasBancarias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        if (string.IsNullOrWhiteSpace(req.NumeroCuenta) || string.IsNullOrWhiteSpace(req.Banco))
            throw new InvalidOperationException("Numero de cuenta y banco son obligatorios.");
        var seCancela = req.Cancelada && !c.Cancelada;
        c.NumeroCuenta = req.NumeroCuenta.Trim();
        c.TipoCuenta = req.TipoCuenta;
        c.Banco = req.Banco.Trim();
        c.VerEnFactura = req.VerEnFactura;
        c.Cancelada = req.Cancelada;
        c.FechaCancelacion = req.Cancelada ? (c.FechaCancelacion ?? DateTimeOffset.UtcNow) : null;
        await _db.SaveChangesAsync(ct);
        if (seCancela)
            await RegistrarBitacoraAsync("Finanzas", $"Cuenta bancaria cancelada: {c.Banco} {c.NumeroCuenta}.", ct);
        return new CuentaBancariaDto(c.Id, c.NumeroCuenta, c.TipoCuenta, c.Banco, c.VerEnFactura, c.Cancelada, c.FechaCancelacion);
    }

    public async Task<bool> EliminarCuentaBancariaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.CuentasBancarias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        _db.CuentasBancarias.Remove(c);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Finanzas", $"Cuenta bancaria eliminada: {c.Banco} {c.NumeroCuenta}.", ct);
        return true;
    }

    // ----------------------------- Bitacora de cambios (RN-06) -----------------------------

    public async Task<IReadOnlyList<BitacoraEntradaDto>> ListBitacoraAsync(int limit, CancellationToken ct)
    {
        return await _db.BitacoraMiCopropiedad
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit <= 0 ? 50 : limit)
            .Select(b => new BitacoraEntradaDto(b.Id, b.Categoria, b.Descripcion, b.Autor, b.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Bitacora filtrada por una entidad concreta (ej. una unidad), para su ficha.</summary>
    public async Task<IReadOnlyList<BitacoraEntradaDto>> ListBitacoraEntidadAsync(Guid entidadId, int limit, CancellationToken ct)
    {
        return await _db.BitacoraMiCopropiedad
            .AsNoTracking()
            .Where(b => b.EntidadId == entidadId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit <= 0 ? 100 : limit)
            .Select(b => new BitacoraEntradaDto(b.Id, b.Categoria, b.Descripcion, b.Autor, b.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Registra una entrada de bitacora (persistencia propia). RN-06.
    /// entidadId (opcional) enlaza el evento a una entidad concreta para su ficha.</summary>
    public async Task RegistrarBitacoraAsync(string categoria, string descripcion, CancellationToken ct, Guid? entidadId = null)
    {
        _db.BitacoraMiCopropiedad.Add(new BitacoraMiCopropiedad
        {
            Categoria = categoria,
            Descripcion = descripcion,
            EntidadId = entidadId
        });
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------- Ficha completa de zona comun (seccion 4) -----------------------------

    public async Task<ZonaFichaDto?> GetZonaFichaAsync(Guid zonaId, Guid? personaId, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return null;

        var zonaDto = new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion, z.EsReservable,
            z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso, z.Estado);

        var facturas = await _db.ZonaFacturas.AsNoTracking().Where(f => f.ZonaComunId == zonaId)
            .OrderByDescending(f => f.Fecha)
            .Select(f => new ZonaFacturaDto(f.Id, f.Concepto, f.Valor, f.Fecha)).ToListAsync(ct);

        var docs = (await _db.ZonaDocumentos.AsNoTracking().Where(d => d.ZonaComunId == zonaId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Nombre, d.Url }).ToListAsync(ct))
            .Select(d => new ZonaDocumentoDto(d.Id, d.Nombre, _blob.ResolveUrl(d.Url) ?? d.Url)).ToList();

        var campos = await _db.ZonaCamposPersonalizados.AsNoTracking().Where(c => c.ZonaComunId == zonaId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ZonaCampoDto(c.Id, c.Label, c.Valor)).ToListAsync(ct);

        var horarios = await _db.VentanasDisponibilidad.AsNoTracking()
            .Where(v => v.TipoEntidad == TipoEntidadDisponibilidad.ZonaComun && v.EntidadId == zonaId)
            .OrderBy(v => v.DiaSemana).ThenBy(v => v.HoraInicio)
            .Select(v => new VentanaDisponibilidadDto(v.Id, v.TipoEntidad, v.EntidadId, v.DiaSemana, v.HoraInicio, v.HoraFin, v.Activa))
            .ToListAsync(ct);

        var contratos = await _db.ContratosServicio.AsNoTracking()
            .Select(c => new ZonaContratoRefDto(c.Id, c.Tipo + " - " + c.Proveedor)).ToListAsync(ct);

        return new ZonaFichaDto(zonaDto, _blob.ResolveUrl(z.ImagenUrl), z.MantenimientoTipo, z.MantenimientoContrato,
            z.MantenimientoFrecuencia, z.MantenimientoDiaMes, facturas, docs, campos, horarios, contratos);
    }

    public async Task<bool> GuardarZonaFichaAsync(Guid zonaId, GuardarZonaFichaRequest req, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return false;
        z.ImagenUrl = string.IsNullOrWhiteSpace(req.ImagenUrl) ? null : req.ImagenUrl.Trim();
        z.MantenimientoTipo = string.IsNullOrWhiteSpace(req.MantenimientoTipo) ? "Interno" : req.MantenimientoTipo.Trim();
        z.MantenimientoContrato = string.IsNullOrWhiteSpace(req.MantenimientoContrato) ? null : req.MantenimientoContrato.Trim();
        z.MantenimientoFrecuencia = string.IsNullOrWhiteSpace(req.MantenimientoFrecuencia) ? "Mensual" : req.MantenimientoFrecuencia.Trim();
        z.MantenimientoDiaMes = req.MantenimientoDiaMes;
        z.EsReservable = req.EsReservable;
        z.CapacidadPersonas = req.CapacidadPersonas;
        z.ReglasUso = string.IsNullOrWhiteSpace(req.ReglasUso) ? null : req.ReglasUso.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string?> SetZonaImagenAsync(Guid zonaId, string url, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return null;
        z.ImagenUrl = url;
        await _db.SaveChangesAsync(ct);
        return url;
    }

    public async Task<ZonaFacturaDto?> AgregarZonaFacturaAsync(Guid zonaId, AgregarZonaFacturaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.ZonasComunes.AnyAsync(z => z.Id == zonaId, ct)) return null;
        var f = new ZonaFactura { TenantId = tid, ZonaComunId = zonaId, Concepto = req.Concepto.Trim(), Valor = req.Valor, Fecha = req.Fecha };
        _db.ZonaFacturas.Add(f);
        await _db.SaveChangesAsync(ct);
        return new ZonaFacturaDto(f.Id, f.Concepto, f.Valor, f.Fecha);
    }

    public async Task<bool> EliminarZonaFacturaAsync(Guid facturaId, CancellationToken ct)
    {
        var f = await _db.ZonaFacturas.FirstOrDefaultAsync(x => x.Id == facturaId, ct);
        if (f is null) return false;
        _db.ZonaFacturas.Remove(f);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ZonaDocumentoDto?> AgregarZonaDocumentoAsync(Guid zonaId, string nombre, string url, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.ZonasComunes.AnyAsync(z => z.Id == zonaId, ct)) return null;
        var d = new ZonaDocumento { TenantId = tid, ZonaComunId = zonaId, Nombre = nombre.Trim(), Url = url };
        _db.ZonaDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);
        return new ZonaDocumentoDto(d.Id, d.Nombre, _blob.ResolveUrl(d.Url) ?? d.Url);
    }

    public async Task<bool> EliminarZonaDocumentoAsync(Guid docId, CancellationToken ct)
    {
        var d = await _db.ZonaDocumentos.FirstOrDefaultAsync(x => x.Id == docId, ct);
        if (d is null) return false;
        _db.ZonaDocumentos.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ZonaCampoDto?> AgregarZonaCampoAsync(Guid zonaId, AgregarZonaCampoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(req.Label)) return null;
        if (!await _db.ZonasComunes.AnyAsync(z => z.Id == zonaId, ct)) return null;
        var c = new ZonaCampoPersonalizado { TenantId = tid, ZonaComunId = zonaId, Label = req.Label.Trim(), Valor = req.Valor?.Trim() };
        _db.ZonaCamposPersonalizados.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ZonaCampoDto(c.Id, c.Label, c.Valor);
    }

    public async Task<bool> EliminarZonaCampoAsync(Guid campoId, CancellationToken ct)
    {
        var c = await _db.ZonaCamposPersonalizados.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        _db.ZonaCamposPersonalizados.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> ResolverNombrePersonaAsync(Guid? personaId, string fallback, CancellationToken ct)
    {
        if (personaId is not Guid pid) return fallback;
        var n = await _db.Personas.AsNoTracking().Where(p => p.Id == pid)
            .Select(p => p.Nombres + " " + p.Apellidos).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(n) ? fallback : n.Trim();
    }

    private static string Iniciales(string? nombre)
    {
        var parts = (nombre ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        return ("" + parts[0][0] + parts[1][0]).ToUpperInvariant();
    }

    private static string FechaRel(DateTimeOffset dt)
    {
        var d = DateTimeOffset.UtcNow - dt;
        if (d.TotalMinutes < 1) return "ahora";
        if (d.TotalMinutes < 60) return "hace " + (int)d.TotalMinutes + " min";
        if (d.TotalHours < 24) return "hace " + (int)d.TotalHours + " h";
        if (d.TotalDays < 30) return "hace " + (int)d.TotalDays + " d";
        return dt.ToString("yyyy-MM-dd");
    }
}
