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

    // Modulo Residentes: TODAS las personas/empresas de TODAS las unidades del tenant (RLS ya
    // acota al tenant activo), cada una con el codigo de su unidad (TORRE-NUMERO, ej. A1-101).
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

}
