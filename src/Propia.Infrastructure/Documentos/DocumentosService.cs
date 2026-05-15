using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Documentos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Documentos;

/// <summary>
/// Modulo 2.15 Documentos y Archivo Digital (spec v1.0 MVP).
///
/// Alcance MVP:
///  - Subida, versionado (con hash SHA-256 y N inmutable), categorias/carpetas, etiquetas.
///  - Auditoria append-only en documento_auditoria (RN-15 via trigger SQL).
///  - Eventos de consumo (vista/descarga) en documento_consumo (12 meses retencion).
///  - Soft delete RN-01 (no hay eliminacion fisica del archivo ni del registro).
///  - Visibilidad PRIVADO / EQUIPO / PUBLICO (control basico - sin combinaciones complejas).
///  - 9 categorias base + 7 etiquetas base sembradas via migracion (RN-12 inmutables).
///
/// Diferido a Fase 2/3:
///  - Firma electronica (EN_FIRMA, FIRMADO, OTP, flujo secuencial/paralelo).
///  - Busqueda full-text del cuerpo del PDF (solo se indexan metadatos).
///  - Generador IA, plantillas con merge fields.
///  - Visor PDF y conversion de Office a PDF.
/// </summary>
public class DocumentosService : IDocumentosService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly IBlobStorage _storage;

    public DocumentosService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http, IBlobStorage storage)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _storage = storage;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private Guid RequireTenantId() =>
        _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

    // ===========================================================================
    // Categorias
    // ===========================================================================

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct)
    {
        var rows = await _db.DocumentoCategorias.AsNoTracking()
            .Where(c => c.Activa)
            .Select(c => new
            {
                c.Id,
                c.EsBase,
                c.Nombre,
                c.Descripcion,
                c.Icono,
                c.Color,
                c.Activa,
                c.Orden,
                NumeroDocumentos = _db.Documentos.Count(d => d.CategoriaId == c.Id && d.Activo)
            })
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .ToListAsync(ct);

        return rows.Select(r => new CategoriaDto(
            r.Id, r.EsBase, r.Nombre, r.Descripcion, r.Icono, r.Color, r.Activa, r.Orden, r.NumeroDocumentos
        )).ToList();
    }

    public async Task<CategoriaDto?> GetCategoriaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.DocumentoCategorias.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        var n = await _db.Documentos.CountAsync(d => d.CategoriaId == id && d.Activo, ct);
        return new CategoriaDto(c.Id, c.EsBase, c.Nombre, c.Descripcion, c.Icono, c.Color, c.Activa, c.Orden, n);
    }

    public async Task<CategoriaDto> CrearCategoriaAsync(CrearCategoriaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var tenantId = RequireTenantId();
        var maxOrden = await _db.DocumentoCategorias
            .Where(c => c.TenantId == tenantId)
            .Select(c => (int?)c.Orden).MaxAsync(ct) ?? 0;
        var c = new DocumentoCategoria
        {
            TenantId = tenantId,
            EsBase = false,
            Nombre = req.Nombre.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Icono = req.Icono?.Trim(),
            Color = req.Color?.Trim(),
            Activa = true,
            Orden = maxOrden + 1
        };
        _db.DocumentoCategorias.Add(c);
        await _db.SaveChangesAsync(ct);
        return new CategoriaDto(c.Id, c.EsBase, c.Nombre, c.Descripcion, c.Icono, c.Color, c.Activa, c.Orden, 0);
    }

    public async Task<bool> ActualizarCategoriaAsync(Guid id, ActualizarCategoriaRequest req, CancellationToken ct)
    {
        var c = await _db.DocumentoCategorias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (c.EsBase) throw new InvalidOperationException("RN-12: Las categorias base PropIA no son editables.");
        c.Nombre = req.Nombre.Trim();
        c.Descripcion = req.Descripcion?.Trim();
        c.Icono = req.Icono?.Trim();
        c.Color = req.Color?.Trim();
        c.Orden = req.Orden;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesactivarCategoriaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.DocumentoCategorias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (c.EsBase) throw new InvalidOperationException("RN-12: Las categorias base PropIA no se pueden desactivar.");
        var tieneDocs = await _db.Documentos.AnyAsync(d => d.CategoriaId == id && d.Activo, ct);
        if (tieneDocs) throw new InvalidOperationException("La categoria tiene documentos activos. Archivelos o muevalos antes.");
        c.Activa = false;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Carpetas
    // ===========================================================================

    public async Task<IReadOnlyList<CarpetaDto>> ListarCarpetasAsync(Guid categoriaId, CancellationToken ct)
    {
        var carpetas = await _db.DocumentoCarpetas.AsNoTracking()
            .Where(c => c.CategoriaId == categoriaId && c.Activa)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .ToListAsync(ct);

        var docsPorCarpeta = await _db.Documentos.AsNoTracking()
            .Where(d => d.CarpetaId != null && d.Activo)
            .GroupBy(d => d.CarpetaId)
            .Select(g => new { CarpetaId = g.Key!.Value, Total = g.Count() })
            .ToDictionaryAsync(x => x.CarpetaId, x => x.Total, ct);

        // construir arbol desde raiz (PadreId == null)
        CarpetaDto Build(DocumentoCarpeta c) => new(
            c.Id, c.CategoriaId, c.PadreId, c.Nombre, c.Descripcion, c.Orden, c.Activa,
            docsPorCarpeta.TryGetValue(c.Id, out var n) ? n : 0,
            carpetas.Where(h => h.PadreId == c.Id).Select(Build).ToList());

        return carpetas.Where(c => c.PadreId == null).Select(Build).ToList();
    }

    public async Task<CarpetaDto> CrearCarpetaAsync(CrearCarpetaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var tenantId = RequireTenantId();
        // valida que la categoria exista y sea visible al tenant
        var existeCat = await _db.DocumentoCategorias.AnyAsync(c => c.Id == req.CategoriaId && c.Activa, ct);
        if (!existeCat) throw new InvalidOperationException("Categoria no encontrada.");
        if (req.PadreId is { } padreId)
        {
            var existePadre = await _db.DocumentoCarpetas.AnyAsync(c => c.Id == padreId && c.CategoriaId == req.CategoriaId, ct);
            if (!existePadre) throw new InvalidOperationException("Carpeta padre no pertenece a la categoria.");
        }

        var maxOrden = await _db.DocumentoCarpetas
            .Where(c => c.TenantId == tenantId && c.CategoriaId == req.CategoriaId && c.PadreId == req.PadreId)
            .Select(c => (int?)c.Orden).MaxAsync(ct) ?? 0;

        var c = new DocumentoCarpeta
        {
            TenantId = tenantId,
            CategoriaId = req.CategoriaId,
            PadreId = req.PadreId,
            Nombre = req.Nombre.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Activa = true,
            Orden = maxOrden + 1,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.DocumentoCarpetas.Add(c);
        await _db.SaveChangesAsync(ct);
        return new CarpetaDto(c.Id, c.CategoriaId, c.PadreId, c.Nombre, c.Descripcion, c.Orden, c.Activa, 0, Array.Empty<CarpetaDto>());
    }

    public async Task<bool> RenombrarCarpetaAsync(Guid id, RenombrarCarpetaRequest req, CancellationToken ct)
    {
        var c = await _db.DocumentoCarpetas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.Nombre = req.Nombre.Trim();
        c.Descripcion = req.Descripcion?.Trim();
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesactivarCarpetaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.DocumentoCarpetas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        var tieneDocs = await _db.Documentos.AnyAsync(d => d.CarpetaId == id && d.Activo, ct);
        if (tieneDocs) throw new InvalidOperationException("La carpeta tiene documentos activos.");
        var tieneSub = await _db.DocumentoCarpetas.AnyAsync(x => x.PadreId == id && x.Activa, ct);
        if (tieneSub) throw new InvalidOperationException("La carpeta tiene subcarpetas activas.");
        c.Activa = false;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Etiquetas
    // ===========================================================================

    public async Task<IReadOnlyList<EtiquetaDto>> ListarEtiquetasAsync(CancellationToken ct)
    {
        var rows = await _db.DocumentoEtiquetasCatalogo.AsNoTracking()
            .Where(e => e.Activa)
            .Select(e => new
            {
                e.Id,
                e.EsBase,
                e.Nombre,
                e.Color,
                e.Activa,
                NumeroDocumentos = _db.DocumentoEtiquetas.Count(de => de.EtiquetaCatalogoId == e.Id)
            })
            .OrderByDescending(e => e.EsBase).ThenBy(e => e.Nombre)
            .ToListAsync(ct);
        return rows.Select(r => new EtiquetaDto(r.Id, r.EsBase, r.Nombre, r.Color, r.Activa, r.NumeroDocumentos)).ToList();
    }

    public async Task<EtiquetaDto> CrearEtiquetaAsync(CrearEtiquetaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var tenantId = RequireTenantId();
        var e = new DocumentoEtiquetaCatalogo
        {
            TenantId = tenantId,
            EsBase = false,
            Nombre = req.Nombre.Trim(),
            Color = req.Color?.Trim(),
            Activa = true
        };
        _db.DocumentoEtiquetasCatalogo.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EtiquetaDto(e.Id, e.EsBase, e.Nombre, e.Color, e.Activa, 0);
    }

    public async Task<bool> ActualizarEtiquetaAsync(Guid id, ActualizarEtiquetaRequest req, CancellationToken ct)
    {
        var e = await _db.DocumentoEtiquetasCatalogo.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        if (e.EsBase) throw new InvalidOperationException("Etiqueta base no editable.");
        e.Nombre = req.Nombre.Trim();
        e.Color = req.Color?.Trim();
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesactivarEtiquetaAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.DocumentoEtiquetasCatalogo.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        if (e.EsBase) throw new InvalidOperationException("Etiqueta base no desactivable.");
        e.Activa = false;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Documentos
    // ===========================================================================

    public async Task<DocumentosPageDto> ListarDocumentosAsync(DocumentosFiltro filtro, CancellationToken ct)
    {
        var usuarioId = GetUsuarioActualId();
        var q = _db.Documentos.AsNoTracking().Where(d => d.Activo);

        if (filtro.CategoriaId is { } cat) q = q.Where(d => d.CategoriaId == cat);
        if (filtro.CarpetaId is { } car) q = q.Where(d => d.CarpetaId == car);
        if (filtro.Origen is { } org) q = q.Where(d => d.Origen == org);
        if (filtro.Estado is { } est) q = q.Where(d => d.Estado == est);
        if (!string.IsNullOrWhiteSpace(filtro.Visibilidad)) q = q.Where(d => d.Visibilidad == filtro.Visibilidad);
        if (!string.IsNullOrWhiteSpace(filtro.TextoBusqueda))
        {
            var t = filtro.TextoBusqueda.Trim().ToLower();
            q = q.Where(d => d.Titulo.ToLower().Contains(t)
                          || d.NombreArchivoOriginal.ToLower().Contains(t)
                          || (d.Descripcion != null && d.Descripcion.ToLower().Contains(t)));
        }
        if (filtro.EtiquetaId is { } eti)
            q = q.Where(d => _db.DocumentoEtiquetas.Any(de => de.DocumentoId == d.Id && de.EtiquetaCatalogoId == eti));
        if (filtro.SoloDestacados == true)
            q = q.Where(d => d.Destacado
                          || _db.DocumentoDestacadosPersonal.Any(p => p.DocumentoId == d.Id && p.UsuarioId == usuarioId));

        var total = await q.CountAsync(ct);

        var page = filtro.Page <= 0 ? 1 : filtro.Page;
        var pageSize = filtro.PageSize <= 0 || filtro.PageSize > 100 ? 30 : filtro.PageSize;

        var rows = await q.OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.Titulo,
                d.NombreArchivoOriginal,
                d.CategoriaId,
                CategoriaNombre = _db.DocumentoCategorias.Where(c => c.Id == d.CategoriaId).Select(c => c.Nombre).FirstOrDefault() ?? "",
                d.CarpetaId,
                CarpetaNombre = d.CarpetaId == null ? null : _db.DocumentoCarpetas.Where(c => c.Id == d.CarpetaId).Select(c => c.Nombre).FirstOrDefault(),
                d.Estado,
                d.Origen,
                d.Visibilidad,
                d.NumeroVersiones,
                Tamano = _db.DocumentoVersiones.Where(v => v.Id == d.VersionActualId).Select(v => v.TamanoBytes).FirstOrDefault(),
                Mime = _db.DocumentoVersiones.Where(v => v.Id == d.VersionActualId).Select(v => v.TipoMime).FirstOrDefault() ?? "",
                d.Destacado,
                DestacadoPersonal = _db.DocumentoDestacadosPersonal.Any(p => p.DocumentoId == d.Id && p.UsuarioId == usuarioId),
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync(ct);

        var docIds = rows.Select(r => r.Id).ToList();
        var etiquetas = await _db.DocumentoEtiquetas.AsNoTracking()
            .Where(de => docIds.Contains(de.DocumentoId))
            .Join(_db.DocumentoEtiquetasCatalogo, de => de.EtiquetaCatalogoId, ec => ec.Id,
                (de, ec) => new { de.DocumentoId, ec.Id, ec.Nombre, ec.Color })
            .ToListAsync(ct);

        var items = rows.Select(r => new DocumentoListaDto(
            r.Id, r.Titulo, r.NombreArchivoOriginal,
            r.CategoriaId, r.CategoriaNombre,
            r.CarpetaId, r.CarpetaNombre,
            r.Estado, r.Origen, r.Visibilidad,
            r.NumeroVersiones, r.Tamano, r.Mime,
            r.Destacado, r.DestacadoPersonal,
            r.CreatedAt, r.UpdatedAt,
            etiquetas.Where(e => e.DocumentoId == r.Id)
                .Select(e => new EtiquetaResumenDto(e.Id, e.Nombre, e.Color)).ToList()
        )).ToList();

        return new DocumentosPageDto(items, total, page, pageSize);
    }

    public async Task<DocumentoDetalleDto?> GetDocumentoAsync(Guid id, CancellationToken ct)
    {
        var d = await _db.Documentos.AsNoTracking()
            .Where(x => x.Id == id && x.Activo)
            .FirstOrDefaultAsync(ct);
        if (d is null) return null;

        var versiones = await _db.DocumentoVersiones.AsNoTracking()
            .Where(v => v.DocumentoId == id)
            .OrderByDescending(v => v.Numero)
            .ToListAsync(ct);

        var actual = versiones.FirstOrDefault(v => v.Id == d.VersionActualId) ?? versiones.FirstOrDefault();
        if (actual is null) return null;

        var categoria = await _db.DocumentoCategorias.AsNoTracking()
            .Where(c => c.Id == d.CategoriaId).Select(c => c.Nombre).FirstOrDefaultAsync(ct) ?? "";
        string? carpetaNombre = null;
        if (d.CarpetaId is { } cId)
            carpetaNombre = await _db.DocumentoCarpetas.AsNoTracking()
                .Where(c => c.Id == cId).Select(c => c.Nombre).FirstOrDefaultAsync(ct);

        var usuarioId = GetUsuarioActualId();
        var destacadoPersonal = await _db.DocumentoDestacadosPersonal
            .AnyAsync(p => p.DocumentoId == id && p.UsuarioId == usuarioId, ct);

        var etiquetas = await _db.DocumentoEtiquetas.AsNoTracking()
            .Where(de => de.DocumentoId == id)
            .Join(_db.DocumentoEtiquetasCatalogo, de => de.EtiquetaCatalogoId, ec => ec.Id,
                (de, ec) => new EtiquetaResumenDto(ec.Id, ec.Nombre, ec.Color))
            .ToListAsync(ct);

        VersionDto MapVer(DocumentoVersion v) => new(
            v.Id, v.Numero, v.NombreArchivo, v.TipoMime, v.TamanoBytes,
            v.HashSha256, v.UrlStorage, v.NotasCambio, v.SubidoPorUsuarioId, v.CreatedAt);

        return new DocumentoDetalleDto(
            d.Id, d.Titulo, d.Descripcion, d.NombreArchivoOriginal,
            d.CategoriaId, categoria, d.CarpetaId, carpetaNombre,
            d.Estado, d.Origen, d.OrigenEntidadId, d.Visibilidad,
            d.NumeroVersiones, d.Destacado, destacadoPersonal,
            d.CreatedAt, d.SubidoPorUsuarioId,
            MapVer(actual),
            versiones.Where(v => v.Id != actual.Id).Select(MapVer).ToList(),
            etiquetas);
    }

    public async Task<DocumentoDetalleDto> SubirDocumentoAsync(SubirDocumentoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo obligatorio.");
        if (string.IsNullOrWhiteSpace(req.NombreArchivo)) throw new InvalidOperationException("NombreArchivo obligatorio.");
        if (string.IsNullOrWhiteSpace(req.ContenidoBase64)) throw new InvalidOperationException("ContenidoBase64 obligatorio.");
        if (req.TamanoBytes <= 0) throw new InvalidOperationException("Tamano invalido.");

        var tenantId = RequireTenantId();
        var existeCat = await _db.DocumentoCategorias.AnyAsync(c => c.Id == req.CategoriaId && c.Activa, ct);
        if (!existeCat) throw new InvalidOperationException("Categoria no encontrada.");
        if (req.CarpetaId is { } folderId)
        {
            var ok = await _db.DocumentoCarpetas.AnyAsync(c => c.Id == folderId && c.CategoriaId == req.CategoriaId && c.Activa, ct);
            if (!ok) throw new InvalidOperationException("Carpeta no pertenece a la categoria.");
        }

        var contenido = Convert.FromBase64String(req.ContenidoBase64);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var documento = new Documento
        {
            TenantId = tenantId,
            CategoriaId = req.CategoriaId,
            CarpetaId = req.CarpetaId,
            Titulo = req.Titulo.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            NombreArchivoOriginal = req.NombreArchivo.Trim(),
            Origen = req.Origen,
            OrigenEntidadId = req.OrigenEntidadId,
            Estado = EstadoDocumento.Vigente,
            Visibilidad = string.IsNullOrWhiteSpace(req.Visibilidad) ? "EQUIPO" : req.Visibilidad.ToUpperInvariant(),
            NumeroVersiones = 1,
            SubidoPorUsuarioId = GetUsuarioActualId(),
            Activo = true
        };
        _db.Documentos.Add(documento);
        await _db.SaveChangesAsync(ct);  // genera Id para usarlo en la key

        var key = $"tenants/{tenantId}/documentos/{documento.Id}/v1/{SanitizeFileName(req.NombreArchivo)}";
        using (var ms = new MemoryStream(contenido))
        {
            await _storage.UploadAsync(key, ms, req.TipoMime, ct);
        }

        var version = new DocumentoVersion
        {
            TenantId = tenantId,
            DocumentoId = documento.Id,
            Numero = 1,
            NombreArchivo = req.NombreArchivo.Trim(),
            TipoMime = req.TipoMime,
            TamanoBytes = req.TamanoBytes,
            UrlStorage = key,
            HashSha256 = hash,
            NotasCambio = "Version inicial",
            SubidoPorUsuarioId = documento.SubidoPorUsuarioId
        };
        _db.DocumentoVersiones.Add(version);
        await _db.SaveChangesAsync(ct);

        documento.VersionActualId = version.Id;
        await _db.SaveChangesAsync(ct);

        // Etiquetas opcionales
        if (req.EtiquetaIds is not null)
        {
            foreach (var etiquetaId in req.EtiquetaIds.Distinct())
            {
                var existe = await _db.DocumentoEtiquetasCatalogo.AnyAsync(e => e.Id == etiquetaId, ct);
                if (!existe) continue;
                _db.DocumentoEtiquetas.Add(new DocumentoEtiqueta
                {
                    TenantId = tenantId,
                    DocumentoId = documento.Id,
                    EtiquetaCatalogoId = etiquetaId
                });
            }
            if (req.EtiquetaIds.Any()) await _db.SaveChangesAsync(ct);
        }

        await RegistrarAuditoriaAsync(documento.Id, TipoEventoDocumento.Subida, null, ct);
        return (await GetDocumentoAsync(documento.Id, ct))!;
    }

    public async Task<VersionDto> SubirNuevaVersionAsync(Guid documentoId, NuevaVersionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ContenidoBase64)) throw new InvalidOperationException("ContenidoBase64 obligatorio.");
        var d = await _db.Documentos.FirstOrDefaultAsync(x => x.Id == documentoId && x.Activo, ct)
            ?? throw new InvalidOperationException("Documento no encontrado.");

        var tenantId = RequireTenantId();
        var contenido = Convert.FromBase64String(req.ContenidoBase64);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();
        var nuevoNumero = d.NumeroVersiones + 1;

        var key = $"tenants/{tenantId}/documentos/{documentoId}/v{nuevoNumero}/{SanitizeFileName(req.NombreArchivo)}";
        using (var ms = new MemoryStream(contenido))
        {
            await _storage.UploadAsync(key, ms, req.TipoMime, ct);
        }

        var version = new DocumentoVersion
        {
            TenantId = tenantId,
            DocumentoId = documentoId,
            Numero = nuevoNumero,
            NombreArchivo = req.NombreArchivo.Trim(),
            TipoMime = req.TipoMime,
            TamanoBytes = req.TamanoBytes,
            UrlStorage = key,
            HashSha256 = hash,
            NotasCambio = req.NotasCambio?.Trim(),
            SubidoPorUsuarioId = GetUsuarioActualId()
        };
        _db.DocumentoVersiones.Add(version);
        await _db.SaveChangesAsync(ct);

        d.NumeroVersiones = nuevoNumero;
        d.VersionActualId = version.Id;
        d.NombreArchivoOriginal = req.NombreArchivo.Trim();
        d.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await RegistrarAuditoriaAsync(documentoId, TipoEventoDocumento.NuevaVersion,
            JsonSerializer.Serialize(new { versionAnterior = nuevoNumero - 1, versionNueva = nuevoNumero, hash }), ct);

        return new VersionDto(
            version.Id, version.Numero, version.NombreArchivo, version.TipoMime, version.TamanoBytes,
            version.HashSha256, version.UrlStorage, version.NotasCambio, version.SubidoPorUsuarioId, version.CreatedAt);
    }

    public async Task<bool> ActualizarMetadatosAsync(Guid id, ActualizarMetadatosRequest req, CancellationToken ct)
    {
        var d = await _db.Documentos.FirstOrDefaultAsync(x => x.Id == id && x.Activo, ct);
        if (d is null) return false;
        var tenantId = RequireTenantId();

        var cambios = new List<string>();
        if (d.Titulo != req.Titulo.Trim()) { cambios.Add("titulo"); d.Titulo = req.Titulo.Trim(); }
        var nuevaDesc = req.Descripcion?.Trim();
        if (d.Descripcion != nuevaDesc) { cambios.Add("descripcion"); d.Descripcion = nuevaDesc; }
        if (d.CategoriaId != req.CategoriaId) { cambios.Add("categoria"); d.CategoriaId = req.CategoriaId; }
        if (d.CarpetaId != req.CarpetaId) { cambios.Add("carpeta"); d.CarpetaId = req.CarpetaId; }
        var nuevaVis = string.IsNullOrWhiteSpace(req.Visibilidad) ? "EQUIPO" : req.Visibilidad.ToUpperInvariant();
        if (d.Visibilidad != nuevaVis) { cambios.Add("visibilidad"); d.Visibilidad = nuevaVis; }
        d.UpdatedAt = DateTimeOffset.UtcNow;

        if (req.EtiquetaIds is not null)
        {
            var actuales = await _db.DocumentoEtiquetas.Where(e => e.DocumentoId == id).ToListAsync(ct);
            _db.DocumentoEtiquetas.RemoveRange(actuales);
            foreach (var eid in req.EtiquetaIds.Distinct())
            {
                var existe = await _db.DocumentoEtiquetasCatalogo.AnyAsync(e => e.Id == eid, ct);
                if (!existe) continue;
                _db.DocumentoEtiquetas.Add(new DocumentoEtiqueta
                {
                    TenantId = tenantId,
                    DocumentoId = id,
                    EtiquetaCatalogoId = eid
                });
            }
            cambios.Add("etiquetas");
        }

        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync(id, TipoEventoDocumento.CambioMetadatos,
            JsonSerializer.Serialize(new { campos = cambios }), ct);
        return true;
    }

    public async Task<bool> CambiarEstadoAsync(Guid id, CambiarEstadoRequest req, CancellationToken ct)
    {
        var d = await _db.Documentos.FirstOrDefaultAsync(x => x.Id == id && x.Activo, ct);
        if (d is null) return false;
        // MVP: solo permitimos Borrador -> Vigente, Vigente -> Archivado, Archivado -> Vigente (reactivar).
        if (req.NuevoEstado == EstadoDocumento.EnFirma || req.NuevoEstado == EstadoDocumento.Firmado)
            throw new InvalidOperationException("Firma electronica fuera del MVP. Disponible en Fase 2.");
        if (d.Estado == req.NuevoEstado) return true;
        d.Estado = req.NuevoEstado;
        d.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        var tipo = req.NuevoEstado switch
        {
            EstadoDocumento.Archivado => TipoEventoDocumento.Archivado,
            EstadoDocumento.Vigente => TipoEventoDocumento.Reactivado,
            _ => TipoEventoDocumento.CambioEstado
        };
        await RegistrarAuditoriaAsync(id, tipo, JsonSerializer.Serialize(new { nuevoEstado = req.NuevoEstado.ToString() }), ct);
        return true;
    }

    public async Task<bool> EliminarDocumentoAsync(Guid id, CancellationToken ct)
    {
        var d = await _db.Documentos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return false;
        // RN-01: no eliminacion fisica. Marcamos Archivado + Activo=false.
        d.Activo = false;
        d.Estado = EstadoDocumento.Archivado;
        d.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync(id, TipoEventoDocumento.EliminacionLogica, null, ct);
        return true;
    }

    // ===========================================================================
    // Destacados
    // ===========================================================================

    public async Task<bool> MarcarDestacadoPersonalAsync(Guid documentoId, CancellationToken ct)
    {
        var existe = await _db.Documentos.AnyAsync(d => d.Id == documentoId && d.Activo, ct);
        if (!existe) return false;
        var userId = GetUsuarioActualId();
        var tenantId = RequireTenantId();
        var yaExiste = await _db.DocumentoDestacadosPersonal
            .AnyAsync(p => p.DocumentoId == documentoId && p.UsuarioId == userId, ct);
        if (yaExiste) return true;
        _db.DocumentoDestacadosPersonal.Add(new DocumentoDestacadoPersonal
        {
            TenantId = tenantId,
            DocumentoId = documentoId,
            UsuarioId = userId
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> QuitarDestacadoPersonalAsync(Guid documentoId, CancellationToken ct)
    {
        var userId = GetUsuarioActualId();
        var row = await _db.DocumentoDestacadosPersonal
            .FirstOrDefaultAsync(p => p.DocumentoId == documentoId && p.UsuarioId == userId, ct);
        if (row is null) return true;
        _db.DocumentoDestacadosPersonal.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MarcarDestacadoCopropiedadAsync(Guid documentoId, bool destacar, CancellationToken ct)
    {
        var d = await _db.Documentos.FirstOrDefaultAsync(x => x.Id == documentoId && x.Activo, ct);
        if (d is null) return false;
        if (d.Destacado == destacar) return true;
        d.Destacado = destacar;
        d.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Descarga / vista
    // ===========================================================================

    public async Task<DescargaDocumentoDto?> DescargarAsync(Guid documentoId, Guid? versionId, CancellationToken ct)
    {
        var d = await _db.Documentos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentoId && x.Activo, ct);
        if (d is null) return null;

        var version = versionId is { } vid
            ? await _db.DocumentoVersiones.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid && v.DocumentoId == documentoId, ct)
            : await _db.DocumentoVersiones.AsNoTracking().FirstOrDefaultAsync(v => v.Id == d.VersionActualId, ct);
        if (version is null) return null;

        var bytes = await _storage.DownloadAsync(version.UrlStorage, ct);
        if (bytes is null) return null;

        await RegistrarConsumoAsync(documentoId, version.Id, TipoEventoConsumo.Descarga, DispositivoConsumo.Unknown, ct);
        await RegistrarAuditoriaAsync(documentoId, TipoEventoDocumento.Descarga,
            JsonSerializer.Serialize(new { versionId = version.Id, version = version.Numero }), ct);

        return new DescargaDocumentoDto(version.NombreArchivo, version.TipoMime, version.TamanoBytes, Convert.ToBase64String(bytes));
    }

    public async Task RegistrarVistaAsync(Guid documentoId, DispositivoConsumo dispositivo, CancellationToken ct)
    {
        var d = await _db.Documentos.AsNoTracking()
            .Where(x => x.Id == documentoId && x.Activo)
            .Select(x => new { x.VersionActualId })
            .FirstOrDefaultAsync(ct);
        if (d is null || d.VersionActualId is null) return;
        await RegistrarConsumoAsync(documentoId, d.VersionActualId.Value, TipoEventoConsumo.Vista, dispositivo, ct);
        await RegistrarAuditoriaAsync(documentoId, TipoEventoDocumento.Vista, null, ct);
    }

    // ===========================================================================
    // Auditoria y estadisticas
    // ===========================================================================

    public async Task<IReadOnlyList<AuditoriaEventoDto>> ListarAuditoriaAsync(Guid documentoId, CancellationToken ct)
    {
        return await _db.DocumentoAuditorias.AsNoTracking()
            .Where(a => a.DocumentoId == documentoId)
            .OrderByDescending(a => a.OcurridoAt)
            .Select(a => new AuditoriaEventoDto(a.Id, a.TipoEvento, a.DetalleJson, a.UsuarioId, a.OcurridoAt))
            .ToListAsync(ct);
    }

    public async Task<EstadisticasDocumentoDto> GetEstadisticasAsync(Guid documentoId, CancellationToken ct)
    {
        var consumos = await _db.DocumentoConsumos.AsNoTracking()
            .Where(c => c.DocumentoId == documentoId)
            .Select(c => new { c.TipoEvento, c.UsuarioId, c.OcurridoAt })
            .ToListAsync(ct);
        var vistas = consumos.Count(c => c.TipoEvento == TipoEventoConsumo.Vista);
        var descargas = consumos.Count(c => c.TipoEvento == TipoEventoConsumo.Descarga);
        var unicos = consumos.Select(c => c.UsuarioId).Distinct().Count();
        var ultimo = consumos.Count > 0 ? consumos.Max(c => c.OcurridoAt) : (DateTimeOffset?)null;
        return new EstadisticasDocumentoDto(vistas, descargas, unicos, ultimo);
    }

    // ===========================================================================
    // Resumen
    // ===========================================================================

    public async Task<ResumenDocumentosDto> GetResumenAsync(CancellationToken ct)
    {
        var totalDocs = await _db.Documentos.CountAsync(d => d.Activo, ct);
        var totalCat = await _db.DocumentoCategorias.CountAsync(c => c.Activa, ct);
        var totalCar = await _db.DocumentoCarpetas.CountAsync(c => c.Activa, ct);
        var tamano = await _db.Documentos
            .Where(d => d.Activo && d.VersionActualId != null)
            .Join(_db.DocumentoVersiones, d => d.VersionActualId, v => v.Id, (d, v) => (long?)v.TamanoBytes)
            .SumAsync(ct) ?? 0;
        var corte = DateTimeOffset.UtcNow.AddDays(-30);
        var ultimos30 = await _db.Documentos.CountAsync(d => d.Activo && d.CreatedAt >= corte, ct);
        return new ResumenDocumentosDto(totalDocs, totalCat, totalCar, tamano, ultimos30);
    }

    // ===========================================================================
    // Helpers internos
    // ===========================================================================

    private Task RegistrarAuditoriaAsync(Guid documentoId, TipoEventoDocumento evento, string? detalle, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return Task.CompletedTask;
        _db.DocumentoAuditorias.Add(new DocumentoAuditoria
        {
            TenantId = tenantId.Value,
            DocumentoId = documentoId,
            TipoEvento = evento,
            DetalleJson = detalle,
            UsuarioId = GetUsuarioActualId(),
            OcurridoAt = DateTimeOffset.UtcNow
        });
        return _db.SaveChangesAsync(ct);
    }

    private Task RegistrarConsumoAsync(Guid documentoId, Guid versionId, TipoEventoConsumo evento, DispositivoConsumo dispositivo, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return Task.CompletedTask;
        _db.DocumentoConsumos.Add(new DocumentoConsumo
        {
            TenantId = tenantId.Value,
            DocumentoId = documentoId,
            VersionId = versionId,
            TipoEvento = evento,
            Dispositivo = dispositivo,
            UsuarioId = GetUsuarioActualId(),
            OcurridoAt = DateTimeOffset.UtcNow
        });
        return _db.SaveChangesAsync(ct);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat(name.Select(ch => invalid.Contains(ch) ? '_' : ch));
        return clean.Length > 200 ? clean.Substring(0, 200) : clean;
    }
}
