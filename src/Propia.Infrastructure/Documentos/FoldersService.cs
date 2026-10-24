using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
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
/// Vista "Folders" (v2) del modulo 2.15. Arbol de folders + documentos versionados + metadatos
/// tipados (folder heredable + documento). Tenant-scoped (RLS). Archivos en IBlobStorage bajo
/// tenants/{tenant}/folders/{folder}/docs/{doc}/v{N}/{nombre}.
/// Regla de oro: un documento con archivo NUNCA se elimina; se archiva (restaurable).
/// </summary>
public class FoldersService : IFoldersService
{
    public const long TamanoMaximoBytes = 20L * 1024 * 1024;

    /// <summary>Extensiones admitidas (PDF, imagen, Office, texto). Sin ejecutables ni scripts.</summary>
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",
        ".doc", ".docx", ".xls", ".xlsx", ".xlsm", ".ppt", ".pptx", ".odt", ".ods",
        ".txt", ".csv", ".tsv", ".md", ".rtf", ".json", ".xml"
    };

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IBlobStorage _storage;
    private readonly IHttpContextAccessor _http;

    public FoldersService(PropiaDbContext db, ITenantContext tenant, IBlobStorage storage, IHttpContextAccessor http)
    {
        _db = db;
        _tenant = tenant;
        _storage = storage;
        _http = http;
    }

    private Guid RequireTenant() => _tenant.CurrentTenantId ?? throw new InvalidOperationException("No hay copropiedad activa.");

    private Guid? UsuarioActual()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    // =====================================================================
    // Folders
    // =====================================================================

    public async Task<IReadOnlyList<FolderNodoDto>> ArbolAsync(CancellationToken ct)
    {
        var tenantId = RequireTenant();
        if (!await _db.Folders.AnyAsync(ct)) await SeedBaseAsync(tenantId, ct);

        var folders = await _db.Folders.AsNoTracking().OrderBy(f => f.Orden).ThenBy(f => f.Nombre).ToListAsync(ct);
        var campos = await _db.FolderCampoDefiniciones.AsNoTracking().Where(c => c.FolderId != null).OrderBy(c => c.Orden).ToListAsync(ct);
        var conteos = await _db.FolderDocumentos.AsNoTracking()
            .GroupBy(d => d.FolderId)
            .Select(g => new { g.Key, Activos = g.Count(d => !d.Archivado), Archivados = g.Count(d => d.Archivado) })
            .ToDictionaryAsync(x => x.Key, ct);

        var camposPorFolder = campos.GroupBy(c => c.FolderId!.Value).ToDictionary(g => g.Key, g => g.Select(c => MapCampo(c)).ToList());
        var hijosPor = folders.GroupBy(f => f.ParentId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());

        FolderNodoDto Nodo(Folder f)
        {
            var hijos = hijosPor.TryGetValue(f.Id, out var h) ? h.Select(Nodo).ToList() : new List<FolderNodoDto>();
            conteos.TryGetValue(f.Id, out var c);
            return new FolderNodoDto(f.Id, f.ParentId, f.Nombre, f.Codigo, f.Descripcion, f.Orden,
                c?.Activos ?? 0, c?.Archivados ?? 0, hijos.Count,
                camposPorFolder.TryGetValue(f.Id, out var cp) ? cp : new List<FolderCampoDto>(), hijos);
        }

        return (hijosPor.TryGetValue(Guid.Empty, out var raices) ? raices : new List<Folder>()).Select(Nodo).ToList();
    }

    public async Task<IReadOnlyList<FolderTarjetaDto>> TarjetasAsync(CancellationToken ct)
    {
        var arbol = await ArbolAsync(ct);
        var docs = await _db.FolderDocumentos.AsNoTracking()
            .Select(d => new { d.FolderId, d.Archivado, d.Obligatorio, Cargado = d.ArchivoUrl != null, d.UpdatedAt, d.CreatedAt })
            .ToListAsync(ct);
        var docsPorFolder = docs.GroupBy(d => d.FolderId).ToDictionary(g => g.Key, g => g.ToList());

        var tarjetas = new List<FolderTarjetaDto>();
        foreach (var raiz in arbol)
        {
            int subfolders = 0, total = 0, cargados = 0, pendObl = 0;
            DateTimeOffset? ultima = null;
            void Visitar(FolderNodoDto n, bool esRaiz)
            {
                if (!esRaiz) subfolders++;
                if (docsPorFolder.TryGetValue(n.Id, out var ds))
                {
                    foreach (var d in ds.Where(d => !d.Archivado))
                    {
                        total++;
                        if (d.Cargado)
                        {
                            cargados++;
                            var fecha = d.UpdatedAt ?? d.CreatedAt;
                            if (ultima is null || fecha > ultima) ultima = fecha;
                        }
                        else if (d.Obligatorio) pendObl++;
                    }
                }
                foreach (var h in n.Hijos) Visitar(h, false);
            }
            Visitar(raiz, true);
            tarjetas.Add(new FolderTarjetaDto(raiz.Id, raiz.Nombre, raiz.Codigo, raiz.Descripcion, subfolders, total, cargados, pendObl, ultima));
        }
        return tarjetas;
    }

    public async Task<FolderDetalleDto?> GetFolderAsync(Guid id, CancellationToken ct)
    {
        var f = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return null;

        var cadena = await CadenaAsync(f, ct); // raiz .. f
        var ruta = cadena.Select(x => new FolderRutaDto(x.Id, x.Nombre)).ToList();
        var subfolders = await _db.Folders.AsNoTracking().Where(x => x.ParentId == id).OrderBy(x => x.Orden).ThenBy(x => x.Nombre)
            .Select(x => new FolderRutaDto(x.Id, x.Nombre)).ToListAsync(ct);

        var idsCadena = cadena.Select(x => x.Id).ToList();
        var defsCadena = await _db.FolderCampoDefiniciones.AsNoTracking()
            .Where(c => c.FolderId != null && idsCadena.Contains(c.FolderId.Value)).OrderBy(c => c.Orden).ToListAsync(ct);
        var nombres = cadena.ToDictionary(x => x.Id, x => x.Nombre);
        var propios = defsCadena.Where(c => c.FolderId == id).Select(c => MapCampo(c)).ToList();
        var heredados = idsCadena.Where(x => x != id)
            .SelectMany(fid => defsCadena.Where(c => c.FolderId == fid).Select(c => MapCampo(c, nombres[fid])))
            .ToList();

        var docs = await _db.FolderDocumentos.AsNoTracking().Where(d => d.FolderId == id).OrderBy(d => d.Orden).ThenBy(d => d.Nombre).ToListAsync(ct);
        var docIds = docs.Select(d => d.Id).ToList();
        var defsDocs = await _db.FolderCampoDefiniciones.AsNoTracking().Where(c => c.DocumentoId != null && docIds.Contains(c.DocumentoId.Value)).OrderBy(c => c.Orden).ToListAsync(ct);
        var valores = await _db.FolderDocumentoCampoValores.AsNoTracking().Where(v => docIds.Contains(v.DocumentoId)).ToListAsync(ct);
        var ultimas = await _db.FolderDocumentoVersiones.AsNoTracking().Where(v => docIds.Contains(v.DocumentoId))
            .GroupBy(v => v.DocumentoId).Select(g => new { g.Key, Fecha = g.Max(v => v.CreatedAt) }).ToDictionaryAsync(x => x.Key, x => x.Fecha, ct);

        var esquemaBase = defsCadena.Concat(defsDocs).ToDictionary(c => c.Id);
        var docsDto = docs.Select(d => MapDocumento(d,
            defsDocs.Where(c => c.DocumentoId == d.Id).Select(c => MapCampo(c)).ToList(),
            valores.Where(v => v.DocumentoId == d.Id).ToList(), esquemaBase,
            ultimas.TryGetValue(d.Id, out var u) ? u : null)).ToList();

        return new FolderDetalleDto(f.Id, f.ParentId, f.Nombre, f.Codigo, f.Descripcion, ruta, subfolders, propios, heredados,
            docsDto.Where(d => !d.Archivado).ToList(), docsDto.Where(d => d.Archivado).ToList());
    }

    public async Task<FolderNodoDto> CrearFolderAsync(CrearFolderRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new InvalidOperationException("El nombre del folder es obligatorio.");
        if (req.ParentId.HasValue && !await _db.Folders.AnyAsync(f => f.Id == req.ParentId.Value, ct))
            throw new InvalidOperationException("El folder padre no existe.");

        var orden = (await _db.Folders.Where(f => f.ParentId == req.ParentId).Select(f => (int?)f.Orden).MaxAsync(ct) ?? -1) + 1;
        var f = new Folder
        {
            TenantId = tenantId,
            ParentId = req.ParentId,
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Orden = orden,
            Codigo = req.ParentId is null ? null : await GenerarCodigoAsync(nombre, req.ParentId.Value, ct)
        };
        _db.Folders.Add(f);
        await _db.SaveChangesAsync(ct);
        return new FolderNodoDto(f.Id, f.ParentId, f.Nombre, f.Codigo, f.Descripcion, f.Orden, 0, 0, 0, Array.Empty<FolderCampoDto>(), Array.Empty<FolderNodoDto>());
    }

    public async Task<bool> ActualizarFolderAsync(Guid id, ActualizarFolderRequest req, CancellationToken ct)
    {
        var f = await _db.Folders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return false;

        if (req.Nombre is not null)
        {
            var n = req.Nombre.Trim();
            if (n.Length == 0) throw new InvalidOperationException("El nombre del folder es obligatorio.");
            f.Nombre = n;
        }
        if (req.Descripcion is not null) f.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        if (req.Orden.HasValue) f.Orden = req.Orden.Value;

        if (req.MoverARaiz) f.ParentId = null;
        else if (req.ParentId.HasValue && req.ParentId.Value != f.ParentId)
        {
            if (req.ParentId.Value == id) throw new InvalidOperationException("Un folder no puede ser su propio padre.");
            var destino = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.ParentId.Value, ct)
                ?? throw new InvalidOperationException("El folder destino no existe.");
            var cadenaDestino = await CadenaAsync(destino, ct);
            if (cadenaDestino.Any(x => x.Id == id)) throw new InvalidOperationException("No se puede mover un folder dentro de uno de sus subfolders.");
            f.ParentId = destino.Id;
            f.Orden = (await _db.Folders.Where(x => x.ParentId == destino.Id).Select(x => (int?)x.Orden).MaxAsync(ct) ?? -1) + 1;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarFolderAsync(Guid id, CancellationToken ct)
    {
        var f = await _db.Folders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return false;
        var ids = await DescendientesAsync(id, ct); // incluye id
        if (await _db.FolderDocumentos.AnyAsync(d => ids.Contains(d.FolderId) && d.NumeroVersiones > 0, ct))
            throw new InvalidOperationException("El folder tiene documentos con archivo. Muévelos o archívalos antes de eliminarlo.");

        // Solo casillas vacias: se pueden borrar (nunca hubo archivo).
        var casillas = await _db.FolderDocumentos.Where(d => ids.Contains(d.FolderId)).ToListAsync(ct);
        _db.FolderDocumentos.RemoveRange(casillas);
        // Borrar de hojas a raiz para respetar la FK padre (Restrict).
        var folders = await _db.Folders.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var porNivel = new List<Folder>();
        var restantes = folders.ToList();
        while (restantes.Count > 0)
        {
            var hojas = restantes.Where(x => !restantes.Any(y => y.ParentId == x.Id)).ToList();
            porNivel.AddRange(hojas);
            restantes = restantes.Except(hojas).ToList();
        }
        await _db.SaveChangesAsync(ct);
        foreach (var x in porNivel) { _db.Folders.Remove(x); await _db.SaveChangesAsync(ct); }
        return true;
    }

    public async Task<FolderZipDto?> DescargarZipAsync(Guid id, CancellationToken ct)
    {
        var raiz = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (raiz is null) return null;
        var ids = await DescendientesAsync(id, ct);
        var folders = await _db.Folders.AsNoTracking().Where(f => ids.Contains(f.Id)).ToListAsync(ct);
        var porId = folders.ToDictionary(f => f.Id);
        var docs = await _db.FolderDocumentos.AsNoTracking().Where(d => ids.Contains(d.FolderId) && d.ArchivoUrl != null).OrderBy(d => d.Orden).ToListAsync(ct);

        string RutaDe(Guid folderId)
        {
            var partes = new List<string>();
            var cur = porId.GetValueOrDefault(folderId);
            while (cur is not null && cur.Id != id) { partes.Insert(0, Sanitize(cur.Nombre)); cur = cur.ParentId.HasValue ? porId.GetValueOrDefault(cur.ParentId.Value) : null; }
            partes.Insert(0, Sanitize(raiz.Nombre));
            return string.Join("/", partes);
        }

        using var ms = new MemoryStream();
        var n = 0;
        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var d in docs)
            {
                var bytes = await _storage.DownloadAsync(d.ArchivoUrl!, ct);
                if (bytes is null) continue;
                var carpeta = RutaDe(d.FolderId) + (d.Archivado ? "/Archivados" : "");
                var nombreArchivo = Sanitize(d.ArchivoNombre ?? d.Nombre);
                var entrada = $"{carpeta}/{nombreArchivo}";
                var k = 1;
                while (!usados.Add(entrada))
                {
                    var baseNombre = Path.GetFileNameWithoutExtension(nombreArchivo);
                    var ext = Path.GetExtension(nombreArchivo);
                    entrada = $"{carpeta}/{baseNombre} ({++k}){ext}";
                }
                var e = zip.CreateEntry(entrada, CompressionLevel.Optimal);
                using var s = e.Open();
                await s.WriteAsync(bytes, ct);
                n++;
            }
        }
        return new FolderZipDto(Sanitize(raiz.Nombre) + ".zip", ms.ToArray(), n);
    }

    // =====================================================================
    // Documentos
    // =====================================================================

    public async Task<FolderDocumentoDto?> GetDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        return d is null ? null : await MapDocumentoAsync(d, ct);
    }

    public async Task<FolderDocumentoDto> CrearDocumentoAsync(Guid folderId, CrearDocumentoRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        if (!await _db.Folders.AnyAsync(f => f.Id == folderId, ct)) throw new InvalidOperationException("El folder no existe.");
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new InvalidOperationException("El nombre del documento es obligatorio.");
        var d = new FolderDocumento
        {
            TenantId = tenantId,
            FolderId = folderId,
            Nombre = nombre,
            Obligatorio = req.Obligatorio,
            Orden = await SiguienteOrdenDocAsync(folderId, ct)
        };
        _db.FolderDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);
        return await MapDocumentoAsync(d, ct);
    }

    public async Task<FolderDocumentoDto> CargarDocumentoAsync(Guid folderId, CargarDocumentoRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        if (!await _db.Folders.AnyAsync(f => f.Id == folderId, ct)) throw new InvalidOperationException("El folder no existe.");
        var contenido = ValidarArchivo(req.NombreArchivo, req.ContenidoBase64, req.TamanoBytes);
        var nombre = string.IsNullOrWhiteSpace(req.Nombre) ? Path.GetFileNameWithoutExtension(req.NombreArchivo.Trim()) : req.Nombre.Trim();
        if (nombre.Length == 0) nombre = "Documento";

        // Validar los obligatorios ANTES de crear nada (el documento nuevo solo hereda los campos del folder):
        // asi una carga rechazada no deja una casilla huerfana en el folder.
        await ValidarObligatoriosDeFolderAsync(folderId, req.Valores, ct);

        var d = new FolderDocumento
        {
            TenantId = tenantId,
            FolderId = folderId,
            Nombre = nombre,
            Obligatorio = req.Obligatorio,
            Orden = await SiguienteOrdenDocAsync(folderId, ct)
        };
        _db.FolderDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);

        await SubirVersionAsync(d, contenido, req.NombreArchivo.Trim(), req.TipoMime, req.NotasCambio, ct);
        await AplicarValoresAsync(d, req.Valores, validarObligatorios: true, ct);
        await _db.SaveChangesAsync(ct);
        return await MapDocumentoAsync(d, ct);
    }

    public async Task<FolderDocumentoDto?> CargarVersionAsync(Guid documentoId, CargarVersionRequest req, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return null;
        var contenido = ValidarArchivo(req.NombreArchivo, req.ContenidoBase64, req.TamanoBytes);
        await SubirVersionAsync(d, contenido, req.NombreArchivo.Trim(), req.TipoMime, req.NotasCambio, ct);
        await AplicarValoresAsync(d, req.Valores, validarObligatorios: req.Valores is { Count: > 0 }, ct);
        await _db.SaveChangesAsync(ct);
        return await MapDocumentoAsync(d, ct);
    }

    public async Task<bool> ActualizarDocumentoAsync(Guid documentoId, ActualizarDocumentoRequest req, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return false;
        if (req.Nombre is not null)
        {
            var n = req.Nombre.Trim();
            if (n.Length == 0) throw new InvalidOperationException("El nombre del documento es obligatorio.");
            d.Nombre = n;
        }
        if (req.Obligatorio.HasValue) d.Obligatorio = req.Obligatorio.Value;
        if (req.FolderId.HasValue && req.FolderId.Value != d.FolderId)
        {
            if (!await _db.Folders.AnyAsync(f => f.Id == req.FolderId.Value, ct)) throw new InvalidOperationException("El folder destino no existe.");
            d.FolderId = req.FolderId.Value;
            d.Orden = await SiguienteOrdenDocAsync(d.FolderId, ct);
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return false;
        if (d.NumeroVersiones > 0 || d.ArchivoUrl is not null)
            throw new InvalidOperationException("El documento tiene archivo: no se elimina, se archiva.");
        _db.FolderDocumentos.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ArchivarDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return false;
        if (!d.Archivado) { d.Archivado = true; d.ArchivadoAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); }
        return true;
    }

    public async Task<bool> RestaurarDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return false;
        if (d.Archivado) { d.Archivado = false; d.ArchivadoAt = null; await _db.SaveChangesAsync(ct); }
        return true;
    }

    public async Task<FolderDocumentoDto?> DuplicarDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var src = await _db.FolderDocumentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (src is null) return null;

        var copia = new FolderDocumento
        {
            TenantId = tenantId,
            FolderId = src.FolderId,
            Nombre = src.Nombre + " (copia)",
            Obligatorio = false,
            Orden = await SiguienteOrdenDocAsync(src.FolderId, ct)
        };
        _db.FolderDocumentos.Add(copia);
        await _db.SaveChangesAsync(ct);

        // Metadatos propios: se copian las definiciones (nuevo Id) y los valores se remapean.
        var defsProp = await _db.FolderCampoDefiniciones.AsNoTracking().Where(c => c.DocumentoId == src.Id).OrderBy(c => c.Orden).ToListAsync(ct);
        var mapa = new Dictionary<Guid, Guid>();
        foreach (var c in defsProp)
        {
            var nc = new FolderCampoDefinicion
            {
                TenantId = tenantId,
                DocumentoId = copia.Id,
                Label = c.Label,
                Tipo = c.Tipo,
                Opciones = c.Opciones,
                Descripcion = c.Descripcion,
                Obligatorio = c.Obligatorio,
                Orden = c.Orden,
                Decimales = c.Decimales
            };
            _db.FolderCampoDefiniciones.Add(nc);
            mapa[c.Id] = nc.Id;
        }
        var valores = await _db.FolderDocumentoCampoValores.AsNoTracking().Where(v => v.DocumentoId == src.Id).ToListAsync(ct);
        foreach (var v in valores)
        {
            var defId = mapa.TryGetValue(v.DefinicionId, out var nuevo) ? nuevo : v.DefinicionId; // heredados conservan Id
            _db.FolderDocumentoCampoValores.Add(new FolderDocumentoCampoValor { TenantId = tenantId, DocumentoId = copia.Id, DefinicionId = defId, Valor = v.Valor });
        }

        // Archivo actual -> version 1 de la copia (copia fisica del blob).
        if (!string.IsNullOrEmpty(src.ArchivoUrl))
        {
            var bytes = await _storage.DownloadAsync(src.ArchivoUrl, ct);
            if (bytes is not null)
                await SubirVersionAsync(copia, bytes, src.ArchivoNombre ?? copia.Nombre, src.ArchivoMime ?? "application/octet-stream", "Duplicado de " + src.Nombre, ct);
        }
        await _db.SaveChangesAsync(ct);
        return await MapDocumentoAsync(copia, ct);
    }

    public async Task<DescargaDocumentoDto?> DescargarDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null || string.IsNullOrEmpty(d.ArchivoUrl)) return null;
        var bytes = await _storage.DownloadAsync(d.ArchivoUrl, ct);
        if (bytes is null) return null;
        return new DescargaDocumentoDto(d.ArchivoNombre ?? "documento", d.ArchivoMime ?? "application/octet-stream", bytes.LongLength, Convert.ToBase64String(bytes));
    }

    public async Task<IReadOnlyList<FolderVersionDto>> ListarVersionesAsync(Guid documentoId, CancellationToken ct)
        => await _db.FolderDocumentoVersiones.AsNoTracking()
            .Where(v => v.DocumentoId == documentoId)
            .OrderByDescending(v => v.Numero)
            .Select(v => new FolderVersionDto(v.Id, v.Numero, v.ArchivoNombre, v.ArchivoMime, v.ArchivoTamano, v.NotasCambio, v.CreatedAt, v.SubidoPorUsuarioId))
            .ToListAsync(ct);

    public async Task<DescargaDocumentoDto?> DescargarVersionAsync(Guid documentoId, Guid versionId, CancellationToken ct)
    {
        var v = await _db.FolderDocumentoVersiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.DocumentoId == documentoId, ct);
        if (v is null) return null;
        var bytes = await _storage.DownloadAsync(v.ArchivoUrl, ct);
        if (bytes is null) return null;
        return new DescargaDocumentoDto(v.ArchivoNombre, v.ArchivoMime, bytes.LongLength, Convert.ToBase64String(bytes));
    }

    public async Task<DocumentoPreviewDto?> PreviewDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null || string.IsNullOrEmpty(d.ArchivoUrl)) return null;
        var bytes = await _storage.DownloadAsync(d.ArchivoUrl, ct);
        return bytes is null ? null : DocumentoPreviewBuilder.Build(bytes, d.ArchivoNombre ?? "documento", d.ArchivoMime ?? "application/octet-stream");
    }

    public async Task<DocumentoPreviewDto?> PreviewVersionAsync(Guid documentoId, Guid versionId, CancellationToken ct)
    {
        var v = await _db.FolderDocumentoVersiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.DocumentoId == documentoId, ct);
        if (v is null) return null;
        var bytes = await _storage.DownloadAsync(v.ArchivoUrl, ct);
        return bytes is null ? null : DocumentoPreviewBuilder.Build(bytes, v.ArchivoNombre, v.ArchivoMime);
    }

    public async Task<bool> GuardarValoresAsync(Guid documentoId, GuardarValoresRequest req, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return false;
        await AplicarValoresAsync(d, req.Valores, req.ValidarObligatorios, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // =====================================================================
    // Metadatos (definiciones)
    // =====================================================================

    public async Task<IReadOnlyList<FolderCampoDto>> CamposEfectivosAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return Array.Empty<FolderCampoDto>();
        return await EsquemaEfectivoAsync(d, ct);
    }

    public async Task<IReadOnlyList<FolderListaDto>> ListaAsync(CancellationToken ct)
    {
        var arbol = await ArbolAsync(ct);
        var res = new List<FolderListaDto>();
        void Visitar(FolderNodoDto n, string ruta, int nivel)
        {
            var r = string.IsNullOrEmpty(ruta) ? n.Nombre : ruta + " / " + n.Nombre;
            res.Add(new FolderListaDto(n.Id, n.Nombre, n.Codigo, n.ParentId, r, nivel));
            foreach (var h in n.Hijos) Visitar(h, r, nivel + 1);
        }
        foreach (var raiz in arbol) Visitar(raiz, "", 0);
        return res;
    }

    public async Task<IReadOnlyList<FolderCampoDto>> ListarCamposAsync(Guid? folderId, Guid? documentoId, CancellationToken ct)
    {
        if (folderId.HasValue == documentoId.HasValue) return Array.Empty<FolderCampoDto>();
        return await _db.FolderCampoDefiniciones.AsNoTracking()
            .Where(c => c.FolderId == folderId && c.DocumentoId == documentoId)
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new FolderCampoDto(c.Id, c.FolderId, c.DocumentoId, c.Label, c.Tipo, c.Opciones, c.Descripcion, c.Obligatorio, c.Orden, c.Decimales, null))
            .ToListAsync(ct);
    }

    public async Task<FolderCampoDto> CrearCampoAsync(CrearFolderCampoRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        if (req.FolderId.HasValue == req.DocumentoId.HasValue)
            throw new InvalidOperationException("Indica el folder o el documento del metadato (solo uno).");
        var label = (req.Label ?? "").Trim();
        if (label.Length == 0) throw new InvalidOperationException("El nombre del metadato es obligatorio.");
        if (req.FolderId.HasValue && !await _db.Folders.AnyAsync(f => f.Id == req.FolderId.Value, ct)) throw new InvalidOperationException("El folder no existe.");
        if (req.DocumentoId.HasValue && !await _db.FolderDocumentos.AnyAsync(f => f.Id == req.DocumentoId.Value, ct)) throw new InvalidOperationException("El documento no existe.");
        ValidarTipo(req.Tipo, req.Opciones);

        var orden = (await _db.FolderCampoDefiniciones
            .Where(c => c.FolderId == req.FolderId && c.DocumentoId == req.DocumentoId)
            .Select(c => (int?)c.Orden).MaxAsync(ct) ?? -1) + 1;
        var c = new FolderCampoDefinicion
        {
            TenantId = tenantId,
            FolderId = req.FolderId,
            DocumentoId = req.DocumentoId,
            Label = label,
            Tipo = req.Tipo,
            Opciones = NormalizarOpciones(req.Opciones),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Obligatorio = req.Obligatorio,
            Orden = orden,
            Decimales = req.Decimales
        };
        _db.FolderCampoDefiniciones.Add(c);
        await _db.SaveChangesAsync(ct);
        return MapCampo(c);
    }

    public async Task<bool> ActualizarCampoAsync(Guid campoId, ActualizarFolderCampoRequest req, CancellationToken ct)
    {
        var c = await _db.FolderCampoDefiniciones.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        if (req.Label is not null)
        {
            var l = req.Label.Trim();
            if (l.Length == 0) throw new InvalidOperationException("El nombre del metadato es obligatorio.");
            c.Label = l;
        }
        if (req.Tipo.HasValue) c.Tipo = req.Tipo.Value;
        if (req.Opciones is not null) c.Opciones = NormalizarOpciones(req.Opciones);
        ValidarTipo(c.Tipo, c.Opciones);
        if (req.Descripcion is not null) c.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        if (req.Obligatorio.HasValue) c.Obligatorio = req.Obligatorio.Value;
        if (req.Orden.HasValue) c.Orden = req.Orden.Value;
        if (req.Decimales.HasValue) c.Decimales = req.Decimales.Value;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct)
    {
        var c = await _db.FolderCampoDefiniciones.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        _db.FolderCampoDefiniciones.Remove(c); // valores caen por cascade
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // =====================================================================
    // Internos
    // =====================================================================

    /// <summary>Cadena de folders desde la raiz hasta f (inclusive).</summary>
    private async Task<List<Folder>> CadenaAsync(Folder f, CancellationToken ct)
    {
        var cadena = new List<Folder> { f };
        var cur = f;
        var visitados = new HashSet<Guid> { f.Id };
        while (cur.ParentId.HasValue)
        {
            var p = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cur.ParentId.Value, ct);
            if (p is null || !visitados.Add(p.Id)) break;
            cadena.Insert(0, p);
            cur = p;
        }
        return cadena;
    }

    /// <summary>Ids del folder y de todos sus descendientes.</summary>
    private async Task<List<Guid>> DescendientesAsync(Guid id, CancellationToken ct)
    {
        var todos = await _db.Folders.AsNoTracking().Select(f => new { f.Id, f.ParentId }).ToListAsync(ct);
        var hijos = todos.GroupBy(f => f.ParentId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.Select(x => x.Id).ToList());
        var result = new List<Guid>();
        var cola = new Queue<Guid>();
        cola.Enqueue(id);
        while (cola.Count > 0)
        {
            var cur = cola.Dequeue();
            if (result.Contains(cur)) continue;
            result.Add(cur);
            if (hijos.TryGetValue(cur, out var hs)) foreach (var h in hs) cola.Enqueue(h);
        }
        return result;
    }

    /// <summary>Esquema efectivo de un documento: campos de la cadena de folders (raiz primero) + propios.</summary>
    private async Task<List<FolderCampoDto>> EsquemaEfectivoAsync(FolderDocumento d, CancellationToken ct)
    {
        var f = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == d.FolderId, ct);
        var cadena = f is null ? new List<Folder>() : await CadenaAsync(f, ct);
        var ids = cadena.Select(x => x.Id).ToList();
        var defs = await _db.FolderCampoDefiniciones.AsNoTracking()
            .Where(c => (c.FolderId != null && ids.Contains(c.FolderId.Value)) || c.DocumentoId == d.Id)
            .OrderBy(c => c.Orden).ToListAsync(ct);
        var nombres = cadena.ToDictionary(x => x.Id, x => x.Nombre);
        var res = new List<FolderCampoDto>();
        foreach (var fid in ids)
            res.AddRange(defs.Where(c => c.FolderId == fid).Select(c => MapCampo(c, nombres[fid])));
        res.AddRange(defs.Where(c => c.DocumentoId == d.Id).Select(c => MapCampo(c)));
        return res;
    }

    private async Task AplicarValoresAsync(FolderDocumento d, IReadOnlyList<CampoValorRequest>? valores, bool validarObligatorios, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var esquema = await EsquemaEfectivoAsync(d, ct);
        var porId = esquema.ToDictionary(c => c.Id);
        var actuales = await _db.FolderDocumentoCampoValores.Where(v => v.DocumentoId == d.Id).ToListAsync(ct);

        foreach (var v in valores ?? Array.Empty<CampoValorRequest>())
        {
            if (!porId.TryGetValue(v.DefinicionId, out var def))
                throw new InvalidOperationException("Uno de los metadatos enviados no pertenece al esquema del documento.");
            var valor = NormalizarValor(def, v.Valor);
            var fila = actuales.FirstOrDefault(a => a.DefinicionId == v.DefinicionId);
            if (fila is null)
            {
                if (valor is null) continue;
                fila = new FolderDocumentoCampoValor { TenantId = tenantId, DocumentoId = d.Id, DefinicionId = v.DefinicionId };
                _db.FolderDocumentoCampoValores.Add(fila);
                actuales.Add(fila);
            }
            fila.Valor = valor;
        }

        if (validarObligatorios)
        {
            var faltan = esquema.Where(c => c.Obligatorio)
                .Where(c => string.IsNullOrWhiteSpace(actuales.FirstOrDefault(a => a.DefinicionId == c.Id)?.Valor))
                .Select(c => c.Label).ToList();
            if (faltan.Count > 0)
                throw new InvalidOperationException("Faltan metadatos obligatorios: " + string.Join(", ", faltan) + ".");
        }
    }

    /// <summary>
    /// Valida los metadatos OBLIGATORIOS que un documento nuevo heredaria del folder (su cadena de folders),
    /// contra los valores entrantes, SIN crear nada. Lanza si falta alguno. Evita casillas huerfanas cuando
    /// la carga se rechaza por obligatorios.
    /// </summary>
    private async Task ValidarObligatoriosDeFolderAsync(Guid folderId, IReadOnlyList<CampoValorRequest>? valores, CancellationToken ct)
    {
        var f = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == folderId, ct);
        if (f is null) return;
        var cadena = await CadenaAsync(f, ct);
        var ids = cadena.Select(x => x.Id).ToList();
        var obligatorios = await _db.FolderCampoDefiniciones.AsNoTracking()
            .Where(c => c.FolderId != null && ids.Contains(c.FolderId.Value) && c.Obligatorio)
            .ToListAsync(ct);
        if (obligatorios.Count == 0) return;
        var conValor = (valores ?? Array.Empty<CampoValorRequest>())
            .Where(v => !string.IsNullOrWhiteSpace(v.Valor)).Select(v => v.DefinicionId).ToHashSet();
        var faltan = obligatorios.Where(c => !conValor.Contains(c.Id)).Select(c => c.Label).ToList();
        if (faltan.Count > 0)
            throw new InvalidOperationException("Faltan metadatos obligatorios: " + string.Join(", ", faltan) + ".");
    }

    private static string? NormalizarValor(FolderCampoDto def, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        switch (def.Tipo)
        {
            case TipoCampoTablero.Numero:
            case TipoCampoTablero.Moneda:
            case TipoCampoTablero.Porcentaje:
            case TipoCampoTablero.ModuloContributivo:
                if (!decimal.TryParse(NormalizarNumero(v), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                    throw new InvalidOperationException($"El metadato \"{def.Label}\" debe ser numérico.");
                return num.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case TipoCampoTablero.Fecha:
                if (!DateOnly.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, out var fecha) && !DateTime.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new InvalidOperationException($"El metadato \"{def.Label}\" debe ser una fecha (AAAA-MM-DD).");
                return v.Length >= 10 ? v[..10] : fecha.ToString("yyyy-MM-dd");
            case TipoCampoTablero.Booleano:
                return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1" || v.Equals("si", StringComparison.OrdinalIgnoreCase) || v.Equals("sí", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            case TipoCampoTablero.Seleccion:
                if (!string.IsNullOrWhiteSpace(def.Opciones))
                {
                    var ops = def.Opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var elegidas = v.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var invalidas = elegidas.Where(e => !ops.Contains(e, StringComparer.OrdinalIgnoreCase)).ToList();
                    if (invalidas.Count > 0) throw new InvalidOperationException($"El metadato \"{def.Label}\" no admite: {string.Join(", ", invalidas)}.");
                }
                return v;
            default:
                return v;
        }
    }

    /// <summary>
    /// Acepta formato colombiano y neutro: "1.500.000,50" -> "1500000.50"; "1.500.000" -> "1500000";
    /// "1500000.5" -> "1500000.5". Si hay coma, la coma es el decimal y los puntos son miles.
    /// </summary>
    private static string NormalizarNumero(string v)
    {
        v = v.Replace(" ", "").Replace("$", "").Replace("%", "");
        if (v.Contains(',')) return v.Replace(".", "").Replace(',', '.');
        var puntos = v.Count(c => c == '.');
        return puntos > 1 ? v.Replace(".", "") : v;
    }

    private static void ValidarTipo(TipoCampoTablero tipo, string? opciones)
    {
        if (tipo is TipoCampoTablero.Separador or TipoCampoTablero.Total or TipoCampoTablero.Formula or TipoCampoTablero.ModuloContributivo)
            throw new InvalidOperationException("Ese tipo de campo no aplica a metadatos de documentos.");
        if (tipo == TipoCampoTablero.Seleccion && string.IsNullOrWhiteSpace(opciones))
            throw new InvalidOperationException("Un metadato de selección necesita al menos una opción.");
    }

    private static string? NormalizarOpciones(string? opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones)) return null;
        var lineas = opciones.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        return lineas.Count == 0 ? null : string.Join("\n", lineas);
    }

    private static byte[] ValidarArchivo(string nombreArchivo, string contenidoBase64, long tamanoDeclarado)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo)) throw new InvalidOperationException("Falta el nombre del archivo.");
        if (string.IsNullOrWhiteSpace(contenidoBase64)) throw new InvalidOperationException("Falta el archivo.");
        var ext = Path.GetExtension(nombreArchivo.Trim());
        if (string.IsNullOrEmpty(ext) || !ExtensionesPermitidas.Contains(ext))
            throw new InvalidOperationException("Tipo de archivo no permitido. Usa PDF, imagen, Office o texto.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(contenidoBase64); }
        catch (FormatException) { throw new InvalidOperationException("Contenido del archivo inválido."); }
        if (bytes.Length == 0) throw new InvalidOperationException("El archivo está vacío.");
        if (bytes.LongLength > TamanoMaximoBytes || tamanoDeclarado > TamanoMaximoBytes)
            throw new InvalidOperationException("El archivo supera el máximo de 20 MB.");
        return bytes;
    }

    private async Task SubirVersionAsync(FolderDocumento d, byte[] contenido, string nombreArchivo, string? mime, string? notas, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        mime = string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime.Trim();
        var numero = d.NumeroVersiones + 1;
        var key = $"tenants/{tenantId}/folders/{d.FolderId}/docs/{d.Id}/v{numero}/{Sanitize(nombreArchivo)}";
        using (var ms = new MemoryStream(contenido)) { await _storage.UploadAsync(key, ms, mime, ct); }

        _db.FolderDocumentoVersiones.Add(new FolderDocumentoVersion
        {
            TenantId = tenantId,
            DocumentoId = d.Id,
            Numero = numero,
            ArchivoUrl = key,
            ArchivoNombre = nombreArchivo,
            ArchivoMime = mime,
            ArchivoTamano = contenido.LongLength,
            HashSha256 = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant(),
            NotasCambio = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim(),
            SubidoPorUsuarioId = UsuarioActual()
        });
        d.ArchivoUrl = key;
        d.ArchivoNombre = nombreArchivo;
        d.ArchivoMime = mime;
        d.ArchivoTamano = contenido.LongLength;
        d.NumeroVersiones = numero;
        d.SubidoPorUsuarioId = UsuarioActual();
    }

    private async Task<int> SiguienteOrdenDocAsync(Guid folderId, CancellationToken ct)
        => (await _db.FolderDocumentos.Where(x => x.FolderId == folderId).Select(x => (int?)x.Orden).MaxAsync(ct) ?? -1) + 1;

    private async Task<string> GenerarCodigoAsync(string nombre, Guid parentId, CancellationToken ct)
    {
        var padre = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == parentId, ct);
        var raiz = padre is null ? nombre : (await CadenaAsync(padre, ct)).First().Nombre;
        var pref = Prefijo(raiz);
        var n = await _db.Folders.CountAsync(f => f.Codigo != null && f.Codigo.StartsWith(pref + "-"), ct) + 1;
        return $"{pref}-{n:D3}";
    }

    private static string Prefijo(string nombre)
    {
        var letras = new string(nombre.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        letras = letras.Normalize(System.Text.NormalizationForm.FormD);
        letras = new string(letras.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return string.IsNullOrEmpty(letras) ? "FLD" : letras.PadRight(3, 'X');
    }

    private static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var s = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(s) ? "archivo" : s;
    }

    private static FolderCampoDto MapCampo(FolderCampoDefinicion c, string? heredadoDe = null)
        => new(c.Id, c.FolderId, c.DocumentoId, c.Label, c.Tipo, c.Opciones, c.Descripcion, c.Obligatorio, c.Orden, c.Decimales, heredadoDe);

    private static FolderDocumentoDto MapDocumento(FolderDocumento d, List<FolderCampoDto> propios, List<FolderDocumentoCampoValor> valores,
        Dictionary<Guid, FolderCampoDefinicion> esquema, DateTimeOffset? ultimaVersion)
    {
        var vals = valores
            .Where(v => esquema.ContainsKey(v.DefinicionId))
            .Select(v => new FolderCampoValorDto(v.DefinicionId, esquema[v.DefinicionId].Label, esquema[v.DefinicionId].Tipo, v.Valor))
            .ToList();
        return new FolderDocumentoDto(d.Id, d.FolderId, d.Nombre, d.Obligatorio, d.Archivado, d.ArchivoUrl != null,
            d.ArchivoNombre, d.ArchivoMime, d.ArchivoTamano, d.NumeroVersiones, ultimaVersion, propios, vals);
    }

    private async Task<FolderDocumentoDto> MapDocumentoAsync(FolderDocumento d, CancellationToken ct)
    {
        var esquema = await EsquemaEfectivoAsync(d, ct);
        var defs = await _db.FolderCampoDefiniciones.AsNoTracking().Where(c => esquema.Select(e => e.Id).Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var propios = esquema.Where(e => e.DocumentoId == d.Id).ToList();
        var valores = await _db.FolderDocumentoCampoValores.AsNoTracking().Where(v => v.DocumentoId == d.Id).ToListAsync(ct);
        var ultima = await _db.FolderDocumentoVersiones.AsNoTracking().Where(v => v.DocumentoId == d.Id).Select(v => (DateTimeOffset?)v.CreatedAt).MaxAsync(ct);
        return MapDocumento(d, propios, valores, defs, ultima);
    }

    /// <summary>Folders base para una copropiedad nueva (misma estructura que la TRD anterior, en lenguaje de folders).</summary>
    private async Task SeedBaseAsync(Guid tenantId, CancellationToken ct)
    {
        var data = new (string folder, (string sub, string[] docs)[] subs)[]
        {
            ("Actas y gobierno", new[]
            {
                ("Actas de Asamblea", new[] { "Convocatoria", "Acta firmada", "Listado de asistencia", "Poderes" }),
                ("Actas de Consejo", new[] { "Citación", "Acta de consejo", "Anexos" })
            }),
            ("Contratos", new[]
            {
                ("Contratos de servicios", new[] { "Contrato firmado", "Pólizas", "Cámara de comercio", "RUT" }),
                ("Contratos laborales", new[] { "Contrato laboral", "Afiliación seguridad social", "Hoja de vida" })
            }),
            ("Financieros", new[]
            {
                ("Estados financieros", new[] { "Balance general", "Estado de resultados", "Notas contables", "Dictamen revisor" }),
                ("Presupuestos", new[] { "Proyecto de presupuesto", "Presupuesto aprobado", "Ejecución" })
            }),
            ("Legal y constitución", new[]
            {
                ("Pólizas y seguros", new[] { "Póliza áreas comunes", "Póliza de manejo", "Certificado" }),
                ("Constitución", new[] { "Escritura", "Reglamento de PH", "Certificado de existencia" })
            })
        };
        var camposBase = new (string label, TipoCampoTablero tipo, string desc)[]
        {
            ("Fecha del documento", TipoCampoTablero.Fecha, "Fecha que aparece en el encabezado o la firma del documento."),
            ("Responsable", TipoCampoTablero.Texto, "Persona o entidad que firma o emite el documento."),
            ("Vigencia hasta", TipoCampoTablero.Fecha, "Fecha hasta la que el documento tiene validez, si aplica.")
        };

        var fo = 0;
        foreach (var (folderNombre, subs) in data)
        {
            var raiz = new Folder { TenantId = tenantId, Nombre = folderNombre, Orden = fo++ };
            _db.Folders.Add(raiz);
            var co = 0;
            foreach (var (label, tipo, desc) in camposBase)
                _db.FolderCampoDefiniciones.Add(new FolderCampoDefinicion { TenantId = tenantId, FolderId = raiz.Id, Label = label, Tipo = tipo, Descripcion = desc, Orden = co++ });
            var so = 0;
            foreach (var (subNombre, docs) in subs)
            {
                var sub = new Folder { TenantId = tenantId, ParentId = raiz.Id, Nombre = subNombre, Orden = so++ };
                _db.Folders.Add(sub);
                var dOrden = 0;
                foreach (var doc in docs)
                    _db.FolderDocumentos.Add(new FolderDocumento { TenantId = tenantId, FolderId = sub.Id, Nombre = doc, Obligatorio = dOrden < 2, Orden = dOrden++ });
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
