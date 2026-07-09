using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Directorio;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Directorio;

public class DirectorioService : IDirectorioService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IBlobStorage _blob;
    private static readonly int[] PesosNit = { 41, 37, 29, 23, 19, 17, 13, 7, 3 };

    public DirectorioService(PropiaDbContext db, ITenantContext tenantContext, IBlobStorage blob)
    {
        _db = db;
        _tenantContext = tenantContext;
        _blob = blob;
    }

    // ============================ Persona ============================

    public async Task<PersonaDetalleDto?> BuscarPersonaPorDocumentoAsync(BuscarPorDocumentoRequest req, CancellationToken ct)
    {
        var doc = (req.Documento ?? "").Trim();
        if (string.IsNullOrEmpty(doc)) return null;
        var p = await _db.Personas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TipoDocumento == req.TipoDocumento && x.Documento == doc, ct);
        return p is null ? null : ToPersonaDetalle(p);
    }

    public async Task<PersonaDetalleDto?> ObtenerPersonaAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : ToPersonaDetalle(p);
    }

    public async Task<PersonaDetalleDto> CrearPersonaAsync(CrearPersonaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Documento)) throw new InvalidOperationException("Documento obligatorio.");
        if (string.IsNullOrWhiteSpace(req.Nombres) || string.IsNullOrWhiteSpace(req.Apellidos))
            throw new InvalidOperationException("Nombres y apellidos obligatorios.");
        var doc = req.Documento.Trim();

        var existente = await _db.Personas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TipoDocumento == req.TipoDocumento && p.Documento == doc, ct);
        if (existente is not null)
            throw new InvalidOperationException("Esta persona ya existe en la plataforma. Vinculala en lugar de crearla.");

        var p = new Persona
        {
            TipoDocumento = req.TipoDocumento,
            Documento = doc,
            Nombres = req.Nombres.Trim(),
            Apellidos = req.Apellidos.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Telefono = req.Telefono,
            FechaNacimiento = req.FechaNacimiento,
            Genero = req.Genero,
            PerfilIncompleto = string.IsNullOrEmpty(req.Email) || req.FechaNacimiento is null,
            CanalAceptacion = CanalAceptacionDatos.Manual,
            EstadoDirectorio = EstadoDirectorio.Activo
        };
        _db.Personas.Add(p);
        await _db.SaveChangesAsync(ct);
        return ToPersonaDetalle(p);
    }

    public async Task<PersonaDetalleDto?> ActualizarPersonaAsync(Guid id, ActualizarPersonaRequest req, CancellationToken ct)
    {
        var p = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        if (string.IsNullOrWhiteSpace(req.Nombres) || string.IsNullOrWhiteSpace(req.Apellidos))
            throw new InvalidOperationException("Nombres y apellidos obligatorios.");
        p.Nombres = req.Nombres.Trim();
        p.Apellidos = req.Apellidos.Trim();
        p.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        p.Telefono = req.Telefono;
        p.FotoUrl = req.FotoUrl;
        p.FechaNacimiento = req.FechaNacimiento;
        p.Genero = req.Genero;
        p.PerfilIncompleto = string.IsNullOrEmpty(p.Email);
        await _db.SaveChangesAsync(ct);
        return ToPersonaDetalle(p);
    }

    public async Task<IReadOnlyList<PersonaResumenDto>> ListarPersonasDelTenantAsync(string? query, CancellationToken ct)
    {
        // Personas con al menos un vinculo en el tenant activo
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay tenant activo.");
        var q = (query ?? "").Trim().ToLowerInvariant();

        var personasIds = await _db.DirectorioVinculos
            .Where(v => v.EntidadTipo == EntidadDirectorio.Persona)
            .Select(v => v.EntidadId)
            .Distinct()
            .ToListAsync(ct);

        var personasQuery = _db.Personas.IgnoreQueryFilters()
            .Where(p => personasIds.Contains(p.Id));
        if (!string.IsNullOrEmpty(q))
        {
            personasQuery = personasQuery.Where(p =>
                p.Nombres.ToLower().Contains(q) ||
                p.Apellidos.ToLower().Contains(q) ||
                p.Documento.Contains(q) ||
                (p.Email != null && p.Email.ToLower().Contains(q)));
        }

        var rows = await personasQuery
            .OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres)
            .Select(p => new { p.Id, p.TipoDocumento, p.Documento, p.Nombres, p.Apellidos, p.Email, p.Telefono, p.FotoUrl, p.PerfilIncompleto, p.EstadoDirectorio })
            .Take(200)
            .ToListAsync(ct);
        return rows.Select(p => new PersonaResumenDto(
            p.Id, p.TipoDocumento, p.Documento, p.Nombres, p.Apellidos, p.Email, p.Telefono,
            _blob.ResolveUrl(p.FotoUrl), p.PerfilIncompleto, p.EstadoDirectorio)).ToList();
    }

    public async Task<Persona360Dto?> GetPersona360Async(Guid personaId, CancellationToken ct)
    {
        var p = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == personaId, ct);
        if (p is null) return null;

        var vinculos = await GetVinculosAsync(EntidadDirectorio.Persona, personaId, ct);
        var contactos = await GetContactosAsync(EntidadDirectorio.Persona, personaId, ct);

        return new Persona360Dto(ToPersonaDetalle(p), vinculos, contactos);
    }

    // ============================ Empresa ============================

    public async Task<EmpresaDetalleDto?> BuscarEmpresaPorNitAsync(string nit, CancellationToken ct)
    {
        var nitN = (nit ?? "").Trim().Replace(".", "").Replace("-", "");
        if (string.IsNullOrEmpty(nitN)) return null;
        var e = await _db.Empresas.IgnoreQueryFilters()
            .Include(x => x.RepresentanteLegal)
            .FirstOrDefaultAsync(x => x.Nit == nitN, ct);
        return e is null ? null : ToEmpresaDetalle(e);
    }

    public async Task<EmpresaDetalleDto?> ObtenerEmpresaAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.Empresas.IgnoreQueryFilters()
            .Include(x => x.RepresentanteLegal)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : ToEmpresaDetalle(e);
    }

    public async Task<EmpresaDetalleDto> CrearEmpresaAsync(CrearEmpresaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nit)) throw new InvalidOperationException("NIT obligatorio.");
        if (string.IsNullOrWhiteSpace(req.RazonSocial) || req.RazonSocial.Trim().Length < 3)
            throw new InvalidOperationException("Razon social minimo 3 caracteres.");
        var nitN = req.Nit.Trim().Replace(".", "").Replace("-", "");
        if (!nitN.All(char.IsDigit)) throw new InvalidOperationException("NIT debe contener solo digitos.");

        var existente = await _db.Empresas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Nit == nitN, ct);
        if (existente is not null)
            throw new InvalidOperationException("Esta empresa ya existe en la plataforma. Vinculala en lugar de crearla.");

        var dv = req.DigitoVerificacion;
        if (string.IsNullOrEmpty(dv))
            dv = CalcularDigitoVerificacionNit(nitN);
        else if (dv != CalcularDigitoVerificacionNit(nitN))
            throw new InvalidOperationException($"DV invalido. El DV correcto para {nitN} es {CalcularDigitoVerificacionNit(nitN)}.");

        var e = new Empresa
        {
            Nit = nitN,
            DigitoVerificacion = dv,
            RazonSocial = req.RazonSocial.Trim(),
            NombreComercial = req.NombreComercial,
            Email = req.Email,
            Telefono = req.Telefono,
            Direccion = req.Direccion,
            TipoEmpresa = req.TipoEmpresa,
            SectorEconomico = req.SectorEconomico,
            RegimenTributario = req.RegimenTributario,
            SitioWeb = req.SitioWeb,
            PerfilIncompleto = string.IsNullOrEmpty(req.Email),
            EstadoDirectorio = EstadoDirectorio.Activo
        };
        _db.Empresas.Add(e);
        await _db.SaveChangesAsync(ct);
        return ToEmpresaDetalle(e);
    }

    public async Task<EmpresaDetalleDto?> ActualizarEmpresaAsync(Guid id, ActualizarEmpresaRequest req, CancellationToken ct)
    {
        var e = await _db.Empresas.IgnoreQueryFilters()
            .Include(x => x.RepresentanteLegal)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;
        if (string.IsNullOrWhiteSpace(req.RazonSocial) || req.RazonSocial.Trim().Length < 3)
            throw new InvalidOperationException("Razon social minimo 3 caracteres.");

        e.RazonSocial = req.RazonSocial.Trim();
        e.NombreComercial = req.NombreComercial;
        e.Email = req.Email;
        e.Telefono = req.Telefono;
        e.Direccion = req.Direccion;
        e.TipoEmpresa = req.TipoEmpresa;
        e.SectorEconomico = req.SectorEconomico;
        e.RegimenTributario = req.RegimenTributario;
        e.SitioWeb = req.SitioWeb;
        e.LogoUrl = req.LogoUrl;
        e.RepresentanteLegalPersonaId = req.RepresentanteLegalPersonaId;
        e.PerfilIncompleto = string.IsNullOrEmpty(e.Email);
        await _db.SaveChangesAsync(ct);

        if (req.RepresentanteLegalPersonaId.HasValue)
            e.RepresentanteLegal = await _db.Personas.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == req.RepresentanteLegalPersonaId.Value, ct);
        return ToEmpresaDetalle(e);
    }

    public async Task<IReadOnlyList<EmpresaResumenDto>> ListarEmpresasDelTenantAsync(string? query, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay tenant activo.");
        var q = (query ?? "").Trim().ToLowerInvariant();

        var empresasIds = await _db.DirectorioVinculos
            .Where(v => v.EntidadTipo == EntidadDirectorio.Empresa)
            .Select(v => v.EntidadId)
            .Distinct()
            .ToListAsync(ct);

        var empresasQuery = _db.Empresas.IgnoreQueryFilters()
            .Where(e => empresasIds.Contains(e.Id));
        if (!string.IsNullOrEmpty(q))
        {
            empresasQuery = empresasQuery.Where(e =>
                e.RazonSocial.ToLower().Contains(q) ||
                e.Nit.Contains(q) ||
                (e.NombreComercial != null && e.NombreComercial.ToLower().Contains(q)) ||
                (e.Email != null && e.Email.ToLower().Contains(q)));
        }

        var rows = await empresasQuery
            .OrderBy(e => e.RazonSocial)
            .Select(e => new { e.Id, e.Nit, e.DigitoVerificacion, e.RazonSocial, e.NombreComercial, e.Email, e.Telefono, e.LogoUrl, e.PerfilIncompleto, e.EstadoDirectorio })
            .Take(200)
            .ToListAsync(ct);
        return rows.Select(e => new EmpresaResumenDto(
            e.Id, e.Nit, e.DigitoVerificacion, e.RazonSocial, e.NombreComercial, e.Email, e.Telefono,
            _blob.ResolveUrl(e.LogoUrl), e.PerfilIncompleto, e.EstadoDirectorio)).ToList();
    }

    public async Task<Empresa360Dto?> GetEmpresa360Async(Guid empresaId, CancellationToken ct)
    {
        var e = await _db.Empresas.IgnoreQueryFilters()
            .Include(x => x.RepresentanteLegal)
            .FirstOrDefaultAsync(x => x.Id == empresaId, ct);
        if (e is null) return null;

        var vinculos = await GetVinculosAsync(EntidadDirectorio.Empresa, empresaId, ct);
        var contactos = await GetContactosAsync(EntidadDirectorio.Empresa, empresaId, ct);

        var equipo = await _db.PersonaEmpresas
            .Include(pe => pe.Persona)
            .Where(pe => pe.EmpresaId == empresaId)
            .Select(pe => new EquipoEmpresaDto(
                pe.Id, pe.PersonaId,
                pe.Persona!.Nombres + " " + pe.Persona.Apellidos,
                pe.Persona.Documento,
                pe.Cargo, pe.EsRepresentanteLegal, pe.EsContactoPrincipal, pe.Estado))
            .ToListAsync(ct);

        return new Empresa360Dto(ToEmpresaDetalle(e), vinculos, contactos, equipo);
    }

    // ============================ Vinculos ============================

    public async Task<VinculoDto> CrearVinculoAsync(CrearVinculoRequest req, CancellationToken ct)
    {
        var existeEntidad = req.EntidadTipo == EntidadDirectorio.Persona
            ? await _db.Personas.IgnoreQueryFilters().AnyAsync(p => p.Id == req.EntidadId, ct)
            : await _db.Empresas.IgnoreQueryFilters().AnyAsync(e => e.Id == req.EntidadId, ct);
        if (!existeEntidad) throw new InvalidOperationException("La entidad referenciada no existe.");

        var yaExiste = await _db.DirectorioVinculos.AnyAsync(v =>
            v.EntidadTipo == req.EntidadTipo && v.EntidadId == req.EntidadId && v.Estado == EstadoVinculo.Activo, ct);
        if (yaExiste) throw new InvalidOperationException("Esta entidad ya tiene un vinculo activo con la copropiedad.");

        var v = new DirectorioVinculo
        {
            EntidadTipo = req.EntidadTipo,
            EntidadId = req.EntidadId,
            FechaDesde = req.FechaDesde,
            Estado = EstadoVinculo.Activo
        };
        _db.DirectorioVinculos.Add(v);
        await _db.SaveChangesAsync(ct);

        if (req.EtiquetaIds is { Count: > 0 })
        {
            foreach (var eid in req.EtiquetaIds.Distinct())
            {
                _db.DirectorioEtiquetas.Add(new DirectorioEtiqueta { VinculoId = v.Id, EtiquetaId = eid });
            }
            await _db.SaveChangesAsync(ct);
        }

        return await GetVinculoConEtiquetasAsync(v.Id, ct);
    }

    public async Task<bool> InactivarVinculoAsync(Guid vinculoId, string? motivo, CancellationToken ct)
    {
        var v = await _db.DirectorioVinculos.FirstOrDefaultAsync(x => x.Id == vinculoId, ct);
        if (v is null) return false;
        v.Estado = EstadoVinculo.Inactivo;
        v.FechaHasta = DateOnly.FromDateTime(DateTime.UtcNow);
        v.MotivoInactivacion = motivo;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<VinculoDto> AsignarEtiquetaAsync(AsignarEtiquetaRequest req, CancellationToken ct)
    {
        var v = await _db.DirectorioVinculos.FirstOrDefaultAsync(x => x.Id == req.VinculoId, ct)
            ?? throw new InvalidOperationException("Vinculo no encontrado.");
        var existe = await _db.DirectorioEtiquetas
            .AnyAsync(de => de.VinculoId == req.VinculoId && de.EtiquetaId == req.EtiquetaId, ct);
        if (existe) throw new InvalidOperationException("Ya esta asignada esa etiqueta.");

        var etiqueta = await _db.EtiquetasCatalogo.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == req.EtiquetaId, ct)
            ?? throw new InvalidOperationException("Etiqueta no existe.");

        _db.DirectorioEtiquetas.Add(new DirectorioEtiqueta { VinculoId = req.VinculoId, EtiquetaId = req.EtiquetaId });
        await _db.SaveChangesAsync(ct);
        return await GetVinculoConEtiquetasAsync(req.VinculoId, ct);
    }

    public async Task<bool> QuitarEtiquetaAsync(Guid asignacionId, CancellationToken ct)
    {
        var de = await _db.DirectorioEtiquetas.FirstOrDefaultAsync(x => x.Id == asignacionId, ct);
        if (de is null) return false;
        _db.DirectorioEtiquetas.Remove(de);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ============================ Contactos ============================

    public async Task<ContactoDto> AgregarContactoAsync(AgregarContactoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Valor)) throw new InvalidOperationException("Valor de contacto obligatorio.");
        var c = new DirectorioContacto
        {
            EntidadTipo = req.EntidadTipo,
            EntidadId = req.EntidadId,
            Tipo = req.Tipo,
            SubtipoLabel = req.SubtipoLabel,
            Valor = req.Valor.Trim(),
            Ciudad = req.Ciudad,
            Departamento = req.Departamento,
            EsPrincipal = req.EsPrincipal,
            Visibilidad = req.Visibilidad,
            Activo = true
        };
        _db.DirectorioContactos.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ContactoDto(c.Id, c.EntidadTipo, c.EntidadId, c.Tipo, c.SubtipoLabel,
            c.Valor, c.Ciudad, c.Departamento, c.EsPrincipal, c.Visibilidad, c.Activo);
    }

    public async Task<bool> EliminarContactoAsync(Guid contactoId, CancellationToken ct)
    {
        var c = await _db.DirectorioContactos.FirstOrDefaultAsync(x => x.Id == contactoId, ct);
        if (c is null) return false;
        _db.DirectorioContactos.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ============================ Catalogo de etiquetas ============================

    public async Task<IReadOnlyList<EtiquetaCatalogoDto>> ListarEtiquetasAsync(AplicaEtiqueta? aplicaA, GrupoEtiqueta? grupo, CancellationToken ct)
    {
        var q = _db.EtiquetasCatalogo.AsNoTracking().Where(e => e.Activo);
        if (aplicaA.HasValue)
            q = q.Where(e => e.AplicaA == aplicaA.Value || e.AplicaA == AplicaEtiqueta.Ambos);
        if (grupo.HasValue) q = q.Where(e => e.Grupo == grupo.Value);
        return await q
            .OrderByDescending(e => e.EsBase).ThenBy(e => e.Nombre)
            .Select(e => new EtiquetaCatalogoDto(e.Id, e.Codigo, e.Nombre, e.Grupo, e.AplicaA,
                e.EsBase, e.TieneLogicaEspecial, e.Activo))
            .ToListAsync(ct);
    }

    public async Task<EtiquetaCatalogoDto> CrearEtiquetaCustomAsync(CrearEtiquetaCustomRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("Sin tenant activo.");
        var nombre = req.Nombre.Trim();
        var codigo = "CUSTOM_" + new string(nombre.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        if (await _db.EtiquetasCatalogo.AnyAsync(e => e.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe etiqueta '{nombre}'.");

        var e = new EtiquetaCatalogo
        {
            Codigo = codigo,
            Nombre = nombre,
            Grupo = req.Grupo,
            AplicaA = req.AplicaA,
            EsBase = false,
            TieneLogicaEspecial = false,
            TenantId = tenantId,
            Activo = true
        };
        _db.EtiquetasCatalogo.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EtiquetaCatalogoDto(e.Id, e.Codigo, e.Nombre, e.Grupo, e.AplicaA, e.EsBase, e.TieneLogicaEspecial, e.Activo);
    }

    public async Task<bool> EliminarEtiquetaCustomAsync(Guid etiquetaId, CancellationToken ct)
    {
        var e = await _db.EtiquetasCatalogo.FirstOrDefaultAsync(x => x.Id == etiquetaId, ct);
        if (e is null) return false;
        if (e.EsBase) throw new InvalidOperationException("No se puede eliminar una etiqueta base. Solo se puede desactivar.");
        var enUso = await _db.DirectorioEtiquetas.AnyAsync(de => de.EtiquetaId == etiquetaId, ct);
        if (enUso) throw new InvalidOperationException("La etiqueta esta en uso. Inactivala en lugar de eliminar.");

        _db.EtiquetasCatalogo.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ============================ Helpers ============================

    /// <summary>
    /// Calcula el digito de verificacion DIAN para un NIT colombiano.
    /// Algoritmo: suma ponderada de digitos por pesos { 41, 37, 29, 23, 19, 17, 13, 7, 3 } modulo 11.
    /// </summary>
    public string CalcularDigitoVerificacionNit(string nit)
    {
        var n = (nit ?? "").Trim().Replace(".", "").Replace("-", "");
        if (string.IsNullOrEmpty(n) || !n.All(char.IsDigit))
            throw new InvalidOperationException("NIT invalido.");
        n = n.PadLeft(9, '0');
        var suma = 0;
        for (int i = 0; i < 9; i++)
        {
            var digito = n[8 - i] - '0';
            suma += digito * PesosNit[i];
        }
        var resto = suma % 11;
        var dv = resto > 1 ? 11 - resto : resto;
        return dv.ToString();
    }

    private PersonaDetalleDto ToPersonaDetalle(Persona p) =>
        new(p.Id, p.TipoDocumento, p.Documento, p.Nombres, p.Apellidos,
            p.Email, p.Telefono, _blob.ResolveUrl(p.FotoUrl), p.FechaNacimiento, p.Genero,
            p.AceptoTratamientoDatos, p.FechaAceptacionDatos,
            p.PerfilIncompleto, p.EstadoDirectorio);

    private EmpresaDetalleDto ToEmpresaDetalle(Empresa e) =>
        new(e.Id, e.Nit, e.DigitoVerificacion, e.RazonSocial, e.NombreComercial,
            e.Email, e.Telefono, e.Direccion,
            e.TipoEmpresa, e.SectorEconomico, e.RegimenTributario,
            e.SitioWeb, _blob.ResolveUrl(e.LogoUrl),
            e.RepresentanteLegalPersonaId,
            e.RepresentanteLegal is null ? null : $"{e.RepresentanteLegal.Nombres} {e.RepresentanteLegal.Apellidos}",
            e.PerfilIncompleto, e.EstadoDirectorio);

    private async Task<IReadOnlyList<VinculoDto>> GetVinculosAsync(EntidadDirectorio tipo, Guid entidadId, CancellationToken ct)
    {
        var vinculos = await _db.DirectorioVinculos
            .Where(v => v.EntidadTipo == tipo && v.EntidadId == entidadId)
            .OrderByDescending(v => v.Estado == EstadoVinculo.Activo).ThenByDescending(v => v.FechaDesde)
            .ToListAsync(ct);
        var result = new List<VinculoDto>();
        foreach (var v in vinculos)
            result.Add(await GetVinculoConEtiquetasAsync(v.Id, ct));
        return result;
    }

    private async Task<VinculoDto> GetVinculoConEtiquetasAsync(Guid vinculoId, CancellationToken ct)
    {
        var v = await _db.DirectorioVinculos.FirstAsync(x => x.Id == vinculoId, ct);
        var etiquetas = await _db.DirectorioEtiquetas
            .Include(de => de.Etiqueta)
            .Where(de => de.VinculoId == vinculoId)
            .Select(de => new EtiquetaAsignadaDto(de.Id, de.EtiquetaId,
                de.Etiqueta!.Codigo, de.Etiqueta.Nombre, de.Etiqueta.Grupo))
            .ToListAsync(ct);

        string nombre = "(desconocido)";
        string? doc = null;
        if (v.EntidadTipo == EntidadDirectorio.Persona)
        {
            var p = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == v.EntidadId, ct);
            if (p is not null) { nombre = $"{p.Nombres} {p.Apellidos}"; doc = p.Documento; }
        }
        else
        {
            var e = await _db.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == v.EntidadId, ct);
            if (e is not null) { nombre = e.RazonSocial; doc = e.Nit; }
        }

        return new VinculoDto(v.Id, v.EntidadTipo, v.EntidadId, nombre, doc,
            v.FechaDesde, v.FechaHasta, v.Estado, etiquetas);
    }

    private async Task<IReadOnlyList<ContactoDto>> GetContactosAsync(EntidadDirectorio tipo, Guid entidadId, CancellationToken ct)
    {
        return await _db.DirectorioContactos
            .Where(c => c.EntidadTipo == tipo && c.EntidadId == entidadId && c.Activo)
            .OrderByDescending(c => c.EsPrincipal).ThenBy(c => c.Tipo)
            .Select(c => new ContactoDto(c.Id, c.EntidadTipo, c.EntidadId, c.Tipo, c.SubtipoLabel,
                c.Valor, c.Ciudad, c.Departamento, c.EsPrincipal, c.Visibilidad, c.Activo))
            .ToListAsync(ct);
    }
}
