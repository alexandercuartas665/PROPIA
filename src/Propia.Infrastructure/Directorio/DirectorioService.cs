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
        // Toda persona creada queda vinculada a la copropiedad activa. Antes dependia de que
        // el llamador hiciera un POST /vinculos aparte, y varios no lo hacian (alta de
        // terceros en Servicios, ficha de unidad), dejando gente fuera de los selectores.
        await VinculoDirectorio.AsegurarPersonaAsync(_db, _tenantContext, p.Id, ct);
        if (req.Contactos is { Count: > 0 })
            await PersistirContactosAsync(EntidadDirectorio.Persona, p.Id, req.Contactos, reemplazar: false, ct);
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
        var chips = await CargarChipsPorEntidadAsync(EntidadDirectorio.Persona, rows.Select(r => r.Id).ToList(), ct);
        return rows.Select(p => new PersonaResumenDto(
            p.Id, p.TipoDocumento, p.Documento, p.Nombres, p.Apellidos, p.Email, p.Telefono,
            _blob.ResolveUrl(p.FotoUrl), p.PerfilIncompleto, p.EstadoDirectorio,
            chips.GetValueOrDefault(p.Id))).ToList();
    }

    /// <summary>Etiquetas asignadas (chips con icono/color) por entidad, para pintarlas en el listado y filtrar.</summary>
    private async Task<Dictionary<Guid, List<EtiquetaChipDto>>> CargarChipsPorEntidadAsync(
        EntidadDirectorio tipo, List<Guid> entidadIds, CancellationToken ct)
    {
        if (entidadIds.Count == 0) return new();
        var rows = await (from de in _db.DirectorioEtiquetas
                          join v in _db.DirectorioVinculos on de.VinculoId equals v.Id
                          join et in _db.EtiquetasCatalogo.IgnoreQueryFilters() on de.EtiquetaId equals et.Id
                          where v.EntidadTipo == tipo && entidadIds.Contains(v.EntidadId)
                          select new { v.EntidadId, et.Id, et.Nombre, et.Grupo, et.Icono, et.Color })
                         .ToListAsync(ct);
        return rows.GroupBy(r => r.EntidadId).ToDictionary(
            g => g.Key,
            g => g.GroupBy(x => x.Id).Select(gg => gg.First())
                  .Select(x => new EtiquetaChipDto(x.Id, x.Nombre, x.Grupo, x.Icono, x.Color)).ToList());
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
        // Igual que las personas: la empresa creada queda vinculada a la copropiedad activa para
        // aparecer en los selectores, y se persisten sus contactos ricos si vinieron.
        await VinculoDirectorio.AsegurarEmpresaAsync(_db, _tenantContext, e.Id, ct);
        if (req.Contactos is { Count: > 0 })
            await PersistirContactosAsync(EntidadDirectorio.Empresa, e.Id, req.Contactos, reemplazar: false, ct);
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
        var chips = await CargarChipsPorEntidadAsync(EntidadDirectorio.Empresa, rows.Select(r => r.Id).ToList(), ct);
        return rows.Select(e => new EmpresaResumenDto(
            e.Id, e.Nit, e.DigitoVerificacion, e.RazonSocial, e.NombreComercial, e.Email, e.Telefono,
            _blob.ResolveUrl(e.LogoUrl), e.PerfilIncompleto, e.EstadoDirectorio,
            chips.GetValueOrDefault(e.Id))).ToList();
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

        // Idempotente: si ya esta vinculada se devuelve el vinculo existente en vez de fallar.
        // Ahora que el alta de persona crea el vinculo sola, el flujo de la UI (crear +
        // vincular) llegaria aqui con el vinculo ya hecho; y pedir vincular a alguien que ya
        // esta no es un error que valga la pena mostrarle al usuario.
        var vinculoExistente = await _db.DirectorioVinculos.FirstOrDefaultAsync(v =>
            v.EntidadTipo == req.EntidadTipo && v.EntidadId == req.EntidadId && v.Estado == EstadoVinculo.Activo, ct);
        if (vinculoExistente is not null) return await GetVinculoConEtiquetasAsync(vinculoExistente.Id, ct);

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

    /// <summary>Mapa rol de unidad -> NOMBRE de la etiqueta base del Directorio. Se casa por nombre
    /// (no por codigo) porque el codigo puede variar entre semillas; el nombre es la llave de dedupe.</summary>
    private static readonly IReadOnlyDictionary<RolUnidadPersona, string> RolEtiquetaNombre =
        new Dictionary<RolUnidadPersona, string>
        {
            [RolUnidadPersona.Propietario] = "Propietario",
            [RolUnidadPersona.Residente] = "Residente",
            [RolUnidadPersona.Familiar] = "Familiar",
            [RolUnidadPersona.Arrendatario] = "Arrendatario",
            [RolUnidadPersona.Apoderado] = "Apoderado",
        };

    /// <summary>
    /// Idempotente: asegura que la persona/empresa vinculada a la copropiedad quede con la etiqueta
    /// base que corresponde a su rol en la unidad (Propietario -> "Propietario", etc.). Solo AGREGA la
    /// etiqueta del rol si falta; nunca quita otras. Respeta AplicaA (no pone una etiqueta de solo-persona
    /// a una empresa, ni viceversa). Pensado para llamarse al vincular una persona a una unidad
    /// (best-effort: quien la invoca la envuelve en try/catch para no romper el alta).
    /// </summary>
    public async Task AsegurarEtiquetaPorRolAsync(EntidadDirectorio tipo, Guid entidadId, RolUnidadPersona rol, CancellationToken ct)
    {
        if (entidadId == Guid.Empty) return;
        if (!RolEtiquetaNombre.TryGetValue(rol, out var nombre)) return;

        // Garantiza que existan las etiquetas base globales (incluida la del rol).
        await AsegurarEtiquetasBaseAsync(ct);

        // Vinculo persona/empresa <-> copropiedad actual: es la fila que alimenta el Directorio.
        var vinculo = await _db.DirectorioVinculos.FirstOrDefaultAsync(v =>
            v.EntidadTipo == tipo && v.EntidadId == entidadId && v.Estado == EstadoVinculo.Activo, ct);
        if (vinculo is null) return;

        // Casa por nombre (no por codigo): puede haber bases pre-sembradas con codigos legacy distintos.
        var nombreLower = nombre.ToLower();
        var etiqueta = await _db.EtiquetasCatalogo.IgnoreQueryFilters()
            .Where(e => e.EsBase && e.Nombre.ToLower() == nombreLower)
            .OrderBy(e => e.Orden).FirstOrDefaultAsync(ct);
        if (etiqueta is null) return;

        // Respeta AplicaA: no asignes una etiqueta de solo-persona a una empresa (o viceversa).
        var esPersona = tipo == EntidadDirectorio.Persona;
        if (etiqueta.AplicaA == AplicaEtiqueta.Persona && !esPersona) return;
        if (etiqueta.AplicaA == AplicaEtiqueta.Empresa && esPersona) return;

        var ya = await _db.DirectorioEtiquetas.AnyAsync(de =>
            de.VinculoId == vinculo.Id && de.EtiquetaId == etiqueta.Id, ct);
        if (ya) return;

        _db.DirectorioEtiquetas.Add(new DirectorioEtiqueta { VinculoId = vinculo.Id, EtiquetaId = etiqueta.Id });
        await _db.SaveChangesAsync(ct);
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

    /// <summary>Etiquetas base predefinidas: (Codigo, Nombre, Grupo, AplicaA, IconoKey, Color, Orden). El icono es una CLAVE del set SVG (no emoji).</summary>
    private static readonly (string Codigo, string Nombre, GrupoEtiqueta Grupo, AplicaEtiqueta Aplica, string Icono, string Color, int Orden)[] EtiquetasBase = new[]
    {
        ("BASE_PROPIETARIO",   "Propietario",       GrupoEtiqueta.Identidad, AplicaEtiqueta.Ambos,   "key",        "#6D4FE3", 1),
        ("BASE_RESIDENTE",     "Residente",         GrupoEtiqueta.Identidad, AplicaEtiqueta.Persona, "home",       "#0EA5E9", 2),
        ("BASE_ARRENDATARIO",  "Arrendatario",      GrupoEtiqueta.Identidad, AplicaEtiqueta.Persona, "file-text",  "#F59E0B", 3),
        ("BASE_FAMILIAR",      "Familiar",          GrupoEtiqueta.Identidad, AplicaEtiqueta.Persona, "users",      "#EC4899", 4),
        ("BASE_APODERADO",     "Apoderado",         GrupoEtiqueta.Identidad, AplicaEtiqueta.Persona, "briefcase",  "#475569", 5),
        ("BASE_PERSONAL",      "Personal de apoyo", GrupoEtiqueta.Cargo,     AplicaEtiqueta.Persona, "paintbrush", "#14B8A6", 6),
        ("BASE_CONTRATISTA",   "Contratista",       GrupoEtiqueta.Cargo,     AplicaEtiqueta.Ambos,   "hard-hat",   "#F97316", 7),
        ("BASE_PROVEEDOR",     "Proveedor",         GrupoEtiqueta.Cargo,     AplicaEtiqueta.Ambos,   "package",    "#8B5CF6", 8),
    };

    /// <summary>Claves de icono validas (set SVG). Sirve para migrar valores viejos (emojis) a un icono generico.</summary>
    private static readonly HashSet<string> IconoKeysValidas = new()
    {
        "tag","home","key","user","users","briefcase","hard-hat","wrench","paintbrush","package",
        "file-text","shield","truck","building","car","award","phone","bell","star","heart"
    };

    /// <summary>
    /// Idempotente: para cada etiqueta base deseada, si ya existe una base con ese NOMBRE le fija su
    /// icono (clave SVG) y color; si no existe, la crea. Ademas, cualquier icono viejo que no sea una
    /// clave valida (ej. emojis previos) se normaliza a "tag" para que el render SVG no falle.
    /// </summary>
    private async Task AsegurarEtiquetasBaseAsync(CancellationToken ct)
    {
        var bases = await _db.EtiquetasCatalogo.IgnoreQueryFilters()
            .Where(e => e.EsBase).ToListAsync(ct);
        var cambios = false;
        foreach (var b in EtiquetasBase)
        {
            var existente = bases.FirstOrDefault(x => x.Nombre.ToLower() == b.Nombre.ToLower());
            if (existente is not null)
            {
                // Fija la clave de icono correcta (sobreescribe emojis o iconos vacios).
                if (existente.Icono != b.Icono) { existente.Icono = b.Icono; cambios = true; }
                if (string.IsNullOrEmpty(existente.Color)) { existente.Color = b.Color; cambios = true; }
            }
            else
            {
                _db.EtiquetasCatalogo.Add(new EtiquetaCatalogo
                {
                    Codigo = b.Codigo,
                    Nombre = b.Nombre,
                    Grupo = b.Grupo,
                    AplicaA = b.Aplica,
                    EsBase = true,
                    TieneLogicaEspecial = false,
                    Icono = b.Icono,
                    Color = b.Color,
                    Orden = b.Orden,
                    TenantId = null,
                    Activo = true
                });
                cambios = true;
            }
        }
        // Normaliza cualquier etiqueta (base o custom) cuyo icono ya no sea una clave valida (ej. emojis previos).
        var conIconoInvalido = await _db.EtiquetasCatalogo.IgnoreQueryFilters()
            .Where(e => e.Icono != null && e.Icono != "").ToListAsync(ct);
        foreach (var e in conIconoInvalido.Where(e => !IconoKeysValidas.Contains(e.Icono!)))
        {
            e.Icono = "tag"; cambios = true;
        }
        if (cambios) await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EtiquetaCatalogoDto>> ListarEtiquetasAsync(AplicaEtiqueta? aplicaA, GrupoEtiqueta? grupo, CancellationToken ct)
    {
        await AsegurarEtiquetasBaseAsync(ct);
        var q = _db.EtiquetasCatalogo.AsNoTracking().Where(e => e.Activo);
        if (aplicaA.HasValue)
            q = q.Where(e => e.AplicaA == aplicaA.Value || e.AplicaA == AplicaEtiqueta.Ambos);
        if (grupo.HasValue) q = q.Where(e => e.Grupo == grupo.Value);
        return await q
            .OrderByDescending(e => e.EsBase).ThenBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new EtiquetaCatalogoDto(e.Id, e.Codigo, e.Nombre, e.Grupo, e.AplicaA,
                e.EsBase, e.TieneLogicaEspecial, e.Activo, e.Icono, e.Color, e.Orden))
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

        var maxOrden = await _db.EtiquetasCatalogo.AnyAsync(e => e.TenantId == tenantId, ct)
            ? await _db.EtiquetasCatalogo.Where(e => e.TenantId == tenantId).MaxAsync(e => e.Orden, ct) : 100;
        var e = new EtiquetaCatalogo
        {
            Codigo = codigo,
            Nombre = nombre,
            Grupo = req.Grupo,
            AplicaA = req.AplicaA,
            EsBase = false,
            TieneLogicaEspecial = false,
            Icono = string.IsNullOrWhiteSpace(req.Icono) ? null : req.Icono.Trim(),
            Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim(),
            Orden = maxOrden + 1,
            TenantId = tenantId,
            Activo = true
        };
        _db.EtiquetasCatalogo.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EtiquetaCatalogoDto(e.Id, e.Codigo, e.Nombre, e.Grupo, e.AplicaA, e.EsBase, e.TieneLogicaEspecial, e.Activo, e.Icono, e.Color, e.Orden);
    }

    public async Task<bool> ActualizarEtiquetaAsync(Guid etiquetaId, EditarEtiquetaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var e = await _db.EtiquetasCatalogo.FirstOrDefaultAsync(x => x.Id == etiquetaId, ct);
        if (e is null) return false;
        if (e.EsBase) throw new InvalidOperationException("Las etiquetas base no se editan (solo las personalizadas).");
        var nombre = req.Nombre.Trim();
        if (await _db.EtiquetasCatalogo.AnyAsync(x => x.Id != etiquetaId && x.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe etiqueta '{nombre}'.");
        e.Nombre = nombre;
        e.Icono = string.IsNullOrWhiteSpace(req.Icono) ? null : req.Icono.Trim();
        e.Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim();
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
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
                de.Etiqueta!.Codigo, de.Etiqueta.Nombre, de.Etiqueta.Grupo, de.Etiqueta.Icono, de.Etiqueta.Color))
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

    // ============================ Selector de personas ============================
    // El autocompletado busca en las copropiedades de la ORGANIZACION del usuario, no en
    // toda la plataforma. Persona es global (sin tenant_id ni RLS), asi que el acotamiento
    // tiene que ser explicito aqui: se parte de los vinculos de esos tenants y solo despues
    // se cruza contra Personas. Nunca al reves.

    public async Task<IReadOnlyList<PersonaCandidatoDto>> BuscarCandidatosAsync(string query, CancellationToken ct)
    {
        var q = (query ?? "").Trim();
        // Minimo 3 caracteres: evita que alguien enumere el directorio letra por letra.
        if (q.Length < 3) return Array.Empty<PersonaCandidatoDto>();

        var tenantActual = _tenantContext.CurrentTenantId;
        if (tenantActual is null) return Array.Empty<PersonaCandidatoDto>();

        // Se consulta por la funcion SECURITY DEFINER y no por EF: directorio_vinculos esta
        // bajo RLS y el rol de la app no puede saltarla, asi que desde EF una busqueda
        // cross-copropiedad siempre saldria vacia. El tenant sale del contexto del servidor,
        // NUNCA de la peticion: es lo unico que acota el alcance de la funcion.
        var filas = new List<(Guid PersonaId, string Nombres, string Apellidos, int TipoDoc, string Documento, Guid TenantId, string TenantNombre)>();

        var conn = _db.Database.GetDbConnection();
        var abiertaAqui = conn.State != System.Data.ConnectionState.Open;
        if (abiertaAqui) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT persona_id, nombres, apellidos, tipo_documento, documento, tenant_id, tenant_nombre " +
                "FROM buscar_personas_organizacion(@p_tenant_id, @p_query)";

            var pTenant = cmd.CreateParameter();
            pTenant.ParameterName = "@p_tenant_id";
            pTenant.Value = tenantActual.Value;
            cmd.Parameters.Add(pTenant);

            var pQuery = cmd.CreateParameter();
            pQuery.ParameterName = "@p_query";
            pQuery.Value = q;
            cmd.Parameters.Add(pQuery);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                filas.Add((
                    reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                    reader.GetInt32(3), reader.GetString(4),
                    reader.GetGuid(5), reader.GetString(6)));
            }
        }
        finally
        {
            if (abiertaAqui) await conn.CloseAsync();
        }

        // Una persona puede venir repetida (una fila por copropiedad donde esta vinculada).
        var personas = filas
            .GroupBy(f => f.PersonaId)
            .Select(g =>
            {
                var f = g.First();
                var tenants = g.Select(x => x.TenantId).Distinct().ToList();
                return new PersonaCandidatoDto(
                    f.PersonaId,
                    $"{f.Nombres} {f.Apellidos}".Trim(),
                    (TipoDocumento)f.TipoDoc,
                    Enmascarar(f.Documento),
                    tenants.Contains(tenantActual.Value),
                    g.Where(x => x.TenantId != tenantActual.Value)
                     .Select(x => x.TenantNombre).Distinct().ToList(),
                    EntidadDirectorio.Persona);
            });

        // Mismo autocompletado, tambien sobre empresas (dueno/tercero juridico). Se mezclan en
        // una sola lista; el candidato lleva EntidadTipo para que el selector sepa que eligio.
        var empresas = await BuscarEmpresasCandidatasAsync(q, tenantActual.Value, ct);

        return personas.Concat(empresas)
            .OrderByDescending(c => c.EnEstaCopropiedad)   // lo de casa primero
            .ThenBy(c => c.NombreCompleto)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// Empresas de la organizacion que coinciden con la busqueda. Mismo molde SECURITY DEFINER que
    /// personas: directorio_vinculos esta bajo RLS y desde EF una busqueda cross-copropiedad saldria
    /// vacia. El resultado se moldea como PersonaCandidatoDto con EntidadTipo = Empresa.
    /// </summary>
    private async Task<List<PersonaCandidatoDto>> BuscarEmpresasCandidatasAsync(string q, Guid tenantActual, CancellationToken ct)
    {
        var filas = new List<(Guid EmpresaId, string RazonSocial, string Nit, Guid TenantId, string TenantNombre)>();

        var conn = _db.Database.GetDbConnection();
        var abiertaAqui = conn.State != System.Data.ConnectionState.Open;
        if (abiertaAqui) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT empresa_id, razon_social, nit, tenant_id, tenant_nombre " +
                "FROM buscar_empresas_organizacion(@p_tenant_id, @p_query)";

            var pTenant = cmd.CreateParameter();
            pTenant.ParameterName = "@p_tenant_id";
            pTenant.Value = tenantActual;
            cmd.Parameters.Add(pTenant);

            var pQuery = cmd.CreateParameter();
            pQuery.ParameterName = "@p_query";
            pQuery.Value = q;
            cmd.Parameters.Add(pQuery);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                filas.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                           reader.GetGuid(3), reader.GetString(4)));
            }
        }
        finally
        {
            if (abiertaAqui) await conn.CloseAsync();
        }

        return filas
            .GroupBy(f => f.EmpresaId)
            .Select(g =>
            {
                var f = g.First();
                var tenants = g.Select(x => x.TenantId).Distinct().ToList();
                return new PersonaCandidatoDto(
                    f.EmpresaId,
                    f.RazonSocial,
                    TipoDocumento.NIT,
                    Enmascarar(f.Nit),
                    tenants.Contains(tenantActual),
                    g.Where(x => x.TenantId != tenantActual).Select(x => x.TenantNombre).Distinct().ToList(),
                    EntidadDirectorio.Empresa);
            })
            .ToList();
    }

    public async Task<bool> VincularCandidatoEmpresaAsync(Guid empresaId, CancellationToken ct)
    {
        var tenantActual = _tenantContext.CurrentTenantId;
        if (tenantActual is null) return false;

        var empresa = await _db.Empresas.AsNoTracking().IgnoreQueryFilters()
            .Where(e => e.Id == empresaId).Select(e => new { e.RazonSocial }).FirstOrDefaultAsync(ct);
        if (empresa is null) throw new InvalidOperationException("Esa empresa no existe.");

        // Solo se traen empresas que ya esten en la organizacion (mismo criterio que personas).
        var candidatas = await BuscarEmpresasCandidatasAsync(empresa.RazonSocial, tenantActual.Value, ct);
        if (!candidatas.Any(c => c.PersonaId == empresaId))
            throw new InvalidOperationException("Esa empresa no pertenece a tu organizacion.");

        await VinculoDirectorio.AsegurarEmpresaAsync(_db, _tenantContext, empresaId, ct);
        return true;
    }

    // ============================ Contactos rapidos (globales) ============================

    public async Task<IReadOnlyList<ContactoRapidoDto>> ObtenerContactosRapidosAsync(EntidadDirectorio tipo, Guid entidadId, CancellationToken ct)
    {
        // DirectorioContacto es global (sin RLS ni filtro por tenant): se lee por (tipo, id).
        return await _db.DirectorioContactos.AsNoTracking()
            .Where(c => c.EntidadTipo == tipo && c.EntidadId == entidadId && c.Activo &&
                        (c.Tipo == TipoContacto.Email || c.Tipo == TipoContacto.Telefono || c.Tipo == TipoContacto.Direccion))
            .OrderBy(c => c.Tipo).ThenByDescending(c => c.EsPrincipal)
            .Select(c => new ContactoRapidoDto(c.Tipo, c.Valor, c.SubtipoLabel, c.Ciudad, c.EsPrincipal))
            .ToListAsync(ct);
    }

    public async Task ReemplazarContactosAsync(ReemplazarContactosRequest req, CancellationToken ct)
    {
        var existe = req.EntidadTipo == EntidadDirectorio.Persona
            ? await _db.Personas.IgnoreQueryFilters().AnyAsync(p => p.Id == req.EntidadId, ct)
            : await _db.Empresas.IgnoreQueryFilters().AnyAsync(e => e.Id == req.EntidadId, ct);
        if (!existe) throw new InvalidOperationException("La entidad de contacto no existe.");

        await PersistirContactosAsync(req.EntidadTipo, req.EntidadId, req.Contactos, reemplazar: true, ct);
    }

    /// <summary>
    /// Escribe la lista de contactos ricos (email/telefono/direccion) y sincroniza el
    /// Email/Telefono/Direccion "principal" denormalizado de la persona/empresa. Con reemplazar=true
    /// borra primero los del mismo tipo (edicion en bloque desde la ficha); con false solo agrega.
    /// </summary>
    private async Task PersistirContactosAsync(
        EntidadDirectorio tipo, Guid entidadId, IReadOnlyList<ContactoRapidoDto> contactos, bool reemplazar, CancellationToken ct)
    {
        var tiposGestionados = new[] { TipoContacto.Email, TipoContacto.Telefono, TipoContacto.Direccion };

        if (reemplazar)
        {
            var previos = await _db.DirectorioContactos
                .Where(c => c.EntidadTipo == tipo && c.EntidadId == entidadId && tiposGestionados.Contains(c.Tipo))
                .ToListAsync(ct);
            _db.DirectorioContactos.RemoveRange(previos);
        }

        var limpios = (contactos ?? Array.Empty<ContactoRapidoDto>())
            .Where(c => tiposGestionados.Contains(c.Tipo) && !string.IsNullOrWhiteSpace(c.Valor))
            .ToList();

        foreach (var c in limpios)
        {
            _db.DirectorioContactos.Add(new DirectorioContacto
            {
                EntidadTipo = tipo,
                EntidadId = entidadId,
                Tipo = c.Tipo,
                SubtipoLabel = string.IsNullOrWhiteSpace(c.Etiqueta) ? null : c.Etiqueta.Trim(),
                Valor = c.Valor.Trim(),
                Ciudad = string.IsNullOrWhiteSpace(c.Ciudad) ? null : c.Ciudad.Trim(),
                EsPrincipal = c.Principal,
                Visibilidad = VisibilidadContacto.Plataforma,   // global: visible donde aparezca la entidad
                Activo = true
            });
        }

        // Principal por tipo: el marcado, o el primero como respaldo.
        string? PrincipalDe(TipoContacto t) =>
            limpios.FirstOrDefault(x => x.Tipo == t && x.Principal)?.Valor
            ?? limpios.FirstOrDefault(x => x.Tipo == t)?.Valor;

        if (tipo == EntidadDirectorio.Persona)
        {
            var p = await _db.Personas.FirstOrDefaultAsync(x => x.Id == entidadId, ct);
            if (p is not null)
            {
                var email = PrincipalDe(TipoContacto.Email)?.Trim();
                if (!string.IsNullOrEmpty(email) && !string.Equals(email, p.Email, StringComparison.OrdinalIgnoreCase))
                {
                    if (await _db.Personas.AnyAsync(x => x.Id != p.Id && x.Email == email, ct))
                        throw new InvalidOperationException("El correo principal ya esta en uso por otra persona.");
                    p.Email = email;
                }
                var tel = PrincipalDe(TipoContacto.Telefono)?.Trim();
                if (!string.IsNullOrEmpty(tel)) p.Telefono = tel;
            }
        }
        else
        {
            var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == entidadId, ct);
            if (e is not null)
            {
                var email = PrincipalDe(TipoContacto.Email)?.Trim();
                if (!string.IsNullOrEmpty(email)) e.Email = email;
                var tel = PrincipalDe(TipoContacto.Telefono)?.Trim();
                if (!string.IsNullOrEmpty(tel)) e.Telefono = tel;
                var dir = PrincipalDe(TipoContacto.Direccion)?.Trim();
                if (!string.IsNullOrEmpty(dir)) e.Direccion = dir;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> VincularCandidatoAsync(Guid personaId, CancellationToken ct)
    {
        var tenantActual = _tenantContext.CurrentTenantId;
        if (tenantActual is null) return false;

        // Solo se puede traer gente que ya este en la organizacion. Se comprueba con la misma
        // funcion acotada: sin esto, el endpoint permitiria vincular cualquier persona de la
        // plataforma con solo conocer su id.
        var persona = await _db.Personas.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.Id == personaId)
            .Select(p => new { p.Documento })
            .FirstOrDefaultAsync(ct);
        if (persona is null) throw new InvalidOperationException("Esa persona no existe.");

        var candidatos = await BuscarCandidatosAsync(persona.Documento, ct);
        if (!candidatos.Any(c => c.PersonaId == personaId))
            throw new InvalidOperationException("Esa persona no pertenece a tu organizacion.");

        await VinculoDirectorio.AsegurarPersonaAsync(_db, _tenantContext, personaId, ct);
        return true;
    }

    /// <summary>
    /// Copropiedades activas de la organizacion duena del tenant dado. Si la copropiedad no
    /// tiene organizacion, el alcance se reduce a ella misma (nunca se amplia).
    /// </summary>
    private async Task<List<Guid>> TenantsDeLaOrganizacionAsync(Guid tenantId, CancellationToken ct)
    {
        var orgId = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId).Select(t => t.OrganizacionId).FirstOrDefaultAsync(ct);
        if (orgId is null) return new List<Guid> { tenantId };

        return await _db.Tenants.AsNoTracking()
            .Where(t => t.OrganizacionId == orgId && t.Estado == EstadoCopropiedad.Activa)
            .Select(t => t.Id).ToListAsync(ct);
    }

    /// <summary>Deja visibles solo los ultimos 4 digitos: basta para desambiguar homonimos.</summary>
    private static string Enmascarar(string? documento)
    {
        var d = (documento ?? "").Trim();
        if (d.Length <= 4) return d;
        return new string('*', Math.Min(4, d.Length - 4)) + d[^4..];
    }

    // ============================ Adjuntos (documentos de la identidad) ============================
    // GLOBAL como los contactos: el documento viaja con la persona/empresa entre copropiedades.

    public async Task<IReadOnlyList<DirectorioAdjuntoDto>> ListarAdjuntosAsync(EntidadDirectorio tipo, Guid entidadId, CancellationToken ct)
        => await _db.DirectorioAdjuntos.AsNoTracking()
            .Where(a => a.EntidadTipo == tipo && a.EntidadId == entidadId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new DirectorioAdjuntoDto(a.Id, a.Nombre, a.Url, a.ContentType, a.TamanoBytes, a.CreatedAt))
            .ToListAsync(ct);

    public async Task<DirectorioAdjuntoDto> AgregarAdjuntoAsync(EntidadDirectorio tipo, Guid entidadId, string nombreArchivo, string? contentType, long tamanoBytes, Stream contenido, CancellationToken ct)
    {
        var ext = Path.GetExtension(nombreArchivo);
        var key = $"directorio/{tipo.ToString().ToLowerInvariant()}/{entidadId:N}/docs/{Guid.NewGuid():N}{ext}";
        var url = await _blob.UploadAsync(key, contenido, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType!, ct);

        var adj = new DirectorioAdjunto
        {
            Id = Guid.NewGuid(),
            EntidadTipo = tipo,
            EntidadId = entidadId,
            Nombre = nombreArchivo.Trim(),
            Url = url,
            ContentType = contentType,
            TamanoBytes = tamanoBytes,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.DirectorioAdjuntos.Add(adj);
        await _db.SaveChangesAsync(ct);
        return new DirectorioAdjuntoDto(adj.Id, adj.Nombre, adj.Url, adj.ContentType, adj.TamanoBytes, adj.CreatedAt);
    }

    public async Task<bool> EliminarAdjuntoAsync(Guid adjuntoId, CancellationToken ct)
    {
        var adj = await _db.DirectorioAdjuntos.FirstOrDefaultAsync(a => a.Id == adjuntoId, ct);
        if (adj is null) return false;
        _db.DirectorioAdjuntos.Remove(adj);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
