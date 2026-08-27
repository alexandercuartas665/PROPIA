using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Documentos;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Documentos;

/// <summary>
/// Vista "Expedientes" del modulo 2.15 (TRD). Configuracion serie/subserie/tipologia/campo +
/// expedientes con checklist de documentos esperados. Tenant-scoped (RLS). Archivo en IBlobStorage.
/// La TRD base se siembra de forma perezosa la primera vez que la copropiedad entra al modulo.
/// </summary>
public class ExpedientesService : IExpedientesService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IBlobStorage _storage;

    public ExpedientesService(PropiaDbContext db, ITenantContext tenant, IBlobStorage storage)
    {
        _db = db;
        _tenant = tenant;
        _storage = storage;
    }

    private Guid RequireTenant() => _tenant.CurrentTenantId ?? throw new InvalidOperationException("No hay copropiedad activa.");

    // ===================== TRD (configuracion documental) =====================

    public async Task<IReadOnlyList<TrdSerieDto>> ListarTrdAsync(CancellationToken ct)
    {
        var tenantId = RequireTenant();
        if (!await _db.SeriesDocumentales.AnyAsync(ct))
        {
            await SeedTrdAsync(tenantId, ct);
        }

        var series = await _db.SeriesDocumentales.AsNoTracking().OrderBy(s => s.Orden).ThenBy(s => s.Nombre).ToListAsync(ct);
        var subseries = await _db.SubseriesDocumentales.AsNoTracking().OrderBy(s => s.Orden).ThenBy(s => s.Nombre).ToListAsync(ct);
        var tipologias = await _db.SubserieTipologias.AsNoTracking().OrderBy(t => t.Orden).ToListAsync(ct);
        var campos = await _db.SubserieCampos.AsNoTracking().OrderBy(c => c.Orden).ToListAsync(ct);

        return series.Select(s => new TrdSerieDto(s.Id, s.Nombre, s.Orden,
            subseries.Where(ss => ss.SerieId == s.Id).Select(ss => new TrdSubserieDto(ss.Id, ss.Nombre, ss.Orden,
                tipologias.Where(t => t.SubserieId == ss.Id).Select(t => new TrdTipologiaDto(t.Id, t.Nombre, t.Orden)).ToList(),
                campos.Where(c => c.SubserieId == ss.Id).Select(c => new TrdCampoDto(c.Id, c.Clave, c.Label, c.Orden)).ToList()
            )).ToList()
        )).ToList();
    }

    public async Task<TrdSerieDto> CrearSerieAsync(CrearSerieRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre de la serie es obligatorio.");
        var orden = (await _db.SeriesDocumentales.Select(s => (int?)s.Orden).MaxAsync(ct) ?? -1) + 1;
        var serie = new SerieDocumental { TenantId = tenantId, Nombre = req.Nombre.Trim(), Orden = orden };
        _db.SeriesDocumentales.Add(serie);
        await _db.SaveChangesAsync(ct);
        return new TrdSerieDto(serie.Id, serie.Nombre, serie.Orden, new List<TrdSubserieDto>());
    }

    public async Task<bool> EliminarSerieAsync(Guid id, CancellationToken ct)
    {
        var serie = await _db.SeriesDocumentales.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (serie is null) return false;
        _db.SeriesDocumentales.Remove(serie); // cascade -> subseries/tipologias/campos
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TrdSubserieDto> CrearSubserieAsync(CrearSubserieRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var serie = await _db.SeriesDocumentales.FirstOrDefaultAsync(s => s.Id == req.SerieId, ct)
            ?? throw new InvalidOperationException("La serie no existe.");
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre de la subserie es obligatorio.");
        var orden = (await _db.SubseriesDocumentales.Where(s => s.SerieId == serie.Id).Select(s => (int?)s.Orden).MaxAsync(ct) ?? -1) + 1;
        var sub = new SubserieDocumental { TenantId = tenantId, SerieId = serie.Id, Nombre = req.Nombre.Trim(), Orden = orden };
        _db.SubseriesDocumentales.Add(sub);
        // Campos por defecto (mismos del prototipo).
        var co = 0;
        foreach (var (clave, label) in DefaultCampos())
            _db.SubserieCampos.Add(new SubserieCampo { TenantId = tenantId, SubserieId = sub.Id, Clave = clave, Label = label, Orden = co++ });
        await _db.SaveChangesAsync(ct);
        return new TrdSubserieDto(sub.Id, sub.Nombre, sub.Orden, new List<TrdTipologiaDto>(),
            DefaultCampos().Select((c, i) => new TrdCampoDto(Guid.Empty, c.clave, c.label, i)).ToList());
    }

    public async Task<bool> EliminarSubserieAsync(Guid id, CancellationToken ct)
    {
        var sub = await _db.SubseriesDocumentales.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null) return false;
        _db.SubseriesDocumentales.Remove(sub);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TrdTipologiaDto> CrearTipologiaCfgAsync(CrearTipologiaCfgRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var sub = await _db.SubseriesDocumentales.FirstOrDefaultAsync(s => s.Id == req.SubserieId, ct)
            ?? throw new InvalidOperationException("La subserie no existe.");
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre de la tipologia es obligatorio.");
        var orden = (await _db.SubserieTipologias.Where(t => t.SubserieId == sub.Id).Select(t => (int?)t.Orden).MaxAsync(ct) ?? -1) + 1;
        var tip = new SubserieTipologia { TenantId = tenantId, SubserieId = sub.Id, Nombre = req.Nombre.Trim(), Orden = orden };
        _db.SubserieTipologias.Add(tip);
        await _db.SaveChangesAsync(ct);
        return new TrdTipologiaDto(tip.Id, tip.Nombre, tip.Orden);
    }

    public async Task<bool> EliminarTipologiaCfgAsync(Guid id, CancellationToken ct)
    {
        var tip = await _db.SubserieTipologias.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tip is null) return false;
        _db.SubserieTipologias.Remove(tip);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TrdCampoDto> CrearCampoCfgAsync(CrearCampoCfgRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var sub = await _db.SubseriesDocumentales.FirstOrDefaultAsync(s => s.Id == req.SubserieId, ct)
            ?? throw new InvalidOperationException("La subserie no existe.");
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("El nombre del metadato es obligatorio.");
        var orden = (await _db.SubserieCampos.Where(c => c.SubserieId == sub.Id).Select(c => (int?)c.Orden).MaxAsync(ct) ?? -1) + 1;
        var campo = new SubserieCampo { TenantId = tenantId, SubserieId = sub.Id, Clave = Slug(req.Label), Label = req.Label.Trim(), Orden = orden };
        _db.SubserieCampos.Add(campo);
        await _db.SaveChangesAsync(ct);
        return new TrdCampoDto(campo.Id, campo.Clave, campo.Label, campo.Orden);
    }

    public async Task<bool> EliminarCampoCfgAsync(Guid id, CancellationToken ct)
    {
        var campo = await _db.SubserieCampos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campo is null) return false;
        _db.SubserieCampos.Remove(campo);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Expedientes =====================

    public async Task<IReadOnlyList<ExpedienteListaDto>> ListarExpedientesAsync(CancellationToken ct)
    {
        var exps = await _db.Expedientes.AsNoTracking().OrderByDescending(e => e.CreatedAt).ToListAsync(ct);
        var tips = await _db.ExpedienteTipologias.AsNoTracking().ToListAsync(ct);
        return exps.Select(e =>
        {
            var et = tips.Where(t => t.ExpedienteId == e.Id).ToList();
            var cargadas = et.Count(t => !string.IsNullOrEmpty(t.ArchivoUrl));
            var pend = et.Count(t => t.Obligatoria && string.IsNullOrEmpty(t.ArchivoUrl));
            return new ExpedienteListaDto(e.Id, e.Codigo, e.Nombre, e.Serie, e.Subserie, et.Count, cargadas, pend);
        }).ToList();
    }

    public async Task<ExpedienteDetalleDto?> GetExpedienteAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.Expedientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;
        var campos = await _db.ExpedienteCampos.AsNoTracking().Where(c => c.ExpedienteId == id).OrderBy(c => c.Orden).ToListAsync(ct);
        var tips = await _db.ExpedienteTipologias.AsNoTracking().Where(t => t.ExpedienteId == id).OrderBy(t => t.Orden).ToListAsync(ct);
        return MapDetalle(e, campos, tips);
    }

    public async Task<ExpedienteDetalleDto> CrearExpedienteAsync(CrearExpedienteRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre del expediente es obligatorio.");
        var sub = await _db.SubseriesDocumentales.Include(s => s.Serie).FirstOrDefaultAsync(s => s.Id == req.SubserieId, ct)
            ?? throw new InvalidOperationException("Selecciona la serie y la subserie.");
        var serieNombre = sub.Serie?.Nombre ?? "";
        var tipsCfg = await _db.SubserieTipologias.AsNoTracking().Where(t => t.SubserieId == sub.Id).OrderBy(t => t.Orden).ToListAsync(ct);
        var camposCfg = await _db.SubserieCampos.AsNoTracking().Where(c => c.SubserieId == sub.Id).OrderBy(c => c.Orden).ToListAsync(ct);

        var pref = Prefijo(serieNombre);
        var n = await _db.Expedientes.CountAsync(e => e.Codigo.StartsWith("EXP-" + pref + "-"), ct) + 1;

        var exp = new Expediente
        {
            TenantId = tenantId,
            Codigo = $"EXP-{pref}-{n:D3}",
            Nombre = req.Nombre.Trim(),
            Serie = serieNombre,
            Subserie = sub.Nombre
        };
        _db.Expedientes.Add(exp);

        var orden = 0;
        foreach (var t in tipsCfg)
        {
            _db.ExpedienteTipologias.Add(new ExpedienteTipologia
            {
                TenantId = tenantId,
                ExpedienteId = exp.Id,
                Nombre = t.Nombre,
                Obligatoria = orden < 3, // las primeras 3 obligatorias por defecto (como el prototipo)
                Orden = orden++
            });
        }
        var oc = 0;
        foreach (var c in camposCfg)
            _db.ExpedienteCampos.Add(new ExpedienteCampo { TenantId = tenantId, ExpedienteId = exp.Id, Clave = c.Clave, Label = c.Label, Orden = oc++ });

        await _db.SaveChangesAsync(ct);
        return (await GetExpedienteAsync(exp.Id, ct))!;
    }

    public async Task<bool> AgregarTipologiaAsync(Guid expedienteId, AgregarTipologiaExpRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var exp = await _db.Expedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return false;
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre de la tipologia es obligatorio.");
        var orden = (await _db.ExpedienteTipologias.Where(t => t.ExpedienteId == expedienteId).Select(t => (int?)t.Orden).MaxAsync(ct) ?? -1) + 1;
        _db.ExpedienteTipologias.Add(new ExpedienteTipologia { TenantId = tenantId, ExpedienteId = expedienteId, Nombre = req.Nombre.Trim(), Obligatoria = req.Obligatoria, Orden = orden });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarTipologiaAsync(Guid tipologiaId, CancellationToken ct)
    {
        var t = await _db.ExpedienteTipologias.FirstOrDefaultAsync(x => x.Id == tipologiaId, ct);
        if (t is null) return false;
        // Borra los blobs de todas las versiones (las filas caen por FK cascade al remover la tipologia).
        var versiones = await _db.ExpedienteTipologiaVersiones.AsNoTracking().Where(v => v.ExpedienteTipologiaId == tipologiaId).ToListAsync(ct);
        foreach (var v in versiones) { if (!string.IsNullOrEmpty(v.ArchivoUrl)) { try { await _storage.DeleteAsync(v.ArchivoUrl, ct); } catch { } } }
        if (!string.IsNullOrEmpty(t.ArchivoUrl) && versiones.All(v => v.ArchivoUrl != t.ArchivoUrl)) { try { await _storage.DeleteAsync(t.ArchivoUrl, ct); } catch { } }
        _db.ExpedienteTipologias.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AgregarCampoAsync(Guid expedienteId, AgregarCampoExpRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var exp = await _db.Expedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return false;
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("El nombre del metadato es obligatorio.");
        var orden = (await _db.ExpedienteCampos.Where(c => c.ExpedienteId == expedienteId).Select(c => (int?)c.Orden).MaxAsync(ct) ?? -1) + 1;
        _db.ExpedienteCampos.Add(new ExpedienteCampo { TenantId = tenantId, ExpedienteId = expedienteId, Clave = Slug(req.Label) + "_" + orden, Label = req.Label.Trim(), Orden = orden });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct)
    {
        var c = await _db.ExpedienteCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        _db.ExpedienteCampos.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CargarTipologiaAsync(Guid tipologiaId, CargarTipologiaRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var t = await _db.ExpedienteTipologias.FirstOrDefaultAsync(x => x.Id == tipologiaId, ct);
        if (t is null) return false;
        if (string.IsNullOrWhiteSpace(req.ContenidoBase64)) throw new InvalidOperationException("Falta el archivo.");

        var contenido = Convert.FromBase64String(req.ContenidoBase64);
        var nombre = string.IsNullOrWhiteSpace(req.NombreArchivo) ? (t.Nombre.Replace(' ', '_') + ".pdf") : req.NombreArchivo.Trim();
        var mime = string.IsNullOrWhiteSpace(req.TipoMime) ? "application/octet-stream" : req.TipoMime;
        var tamano = req.TamanoBytes > 0 ? req.TamanoBytes : contenido.Length;

        // Versionado (RN-13): cada carga -> nueva version en carpeta v{N}; NO se borra el blob anterior.
        var nuevoNumero = t.NumeroVersiones + 1;
        var key = $"tenants/{tenantId}/expedientes/{t.ExpedienteId}/tipologias/{t.Id}/v{nuevoNumero}/{Sanitize(nombre)}";
        using (var ms = new MemoryStream(contenido)) { await _storage.UploadAsync(key, ms, mime, ct); }

        _db.ExpedienteTipologiaVersiones.Add(new ExpedienteTipologiaVersion
        {
            TenantId = tenantId,
            ExpedienteTipologiaId = t.Id,
            Numero = nuevoNumero,
            ArchivoUrl = key,
            ArchivoNombre = nombre,
            ArchivoMime = mime,
            ArchivoTamano = tamano,
            NotasCambio = string.IsNullOrWhiteSpace(req.NotasCambio) ? null : req.NotasCambio!.Trim()
        });

        // La tipologia apunta a la version ACTUAL (la recien cargada).
        t.ArchivoUrl = key;
        t.ArchivoNombre = nombre;
        t.ArchivoMime = mime;
        t.ArchivoTamano = tamano;
        t.NumeroVersiones = nuevoNumero;

        var meta = (req.Meta ?? new List<ExpedienteMetaValorDto>())
            .Where(m => !string.IsNullOrWhiteSpace(m.Valor))
            .ToDictionary(m => m.Clave, m => m.Valor!.Trim());
        t.MetaJson = meta.Count > 0 ? JsonSerializer.Serialize(meta) : null;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<TipologiaVersionDto>> ListarVersionesTipologiaAsync(Guid tipologiaId, CancellationToken ct)
    {
        return await _db.ExpedienteTipologiaVersiones.AsNoTracking()
            .Where(v => v.ExpedienteTipologiaId == tipologiaId)
            .OrderByDescending(v => v.Numero)
            .Select(v => new TipologiaVersionDto(v.Id, v.Numero, v.ArchivoNombre, v.ArchivoMime, v.ArchivoTamano, v.NotasCambio, v.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<DescargaDocumentoDto?> DescargarVersionTipologiaAsync(Guid tipologiaId, Guid versionId, CancellationToken ct)
    {
        var v = await _db.ExpedienteTipologiaVersiones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == versionId && x.ExpedienteTipologiaId == tipologiaId, ct);
        if (v is null || string.IsNullOrEmpty(v.ArchivoUrl)) return null;
        var bytes = await _storage.DownloadAsync(v.ArchivoUrl, ct);
        if (bytes is null) return null;
        return new DescargaDocumentoDto(v.ArchivoNombre, v.ArchivoMime, bytes.LongLength, Convert.ToBase64String(bytes));
    }

    public async Task<bool> QuitarArchivoAsync(Guid tipologiaId, CancellationToken ct)
    {
        var t = await _db.ExpedienteTipologias.FirstOrDefaultAsync(x => x.Id == tipologiaId, ct);
        if (t is null) return false;
        // Quitar = vaciar la casilla por completo: borra todas las versiones (blobs + filas) y su historial.
        var versiones = await _db.ExpedienteTipologiaVersiones.Where(v => v.ExpedienteTipologiaId == tipologiaId).ToListAsync(ct);
        foreach (var v in versiones) { if (!string.IsNullOrEmpty(v.ArchivoUrl)) { try { await _storage.DeleteAsync(v.ArchivoUrl, ct); } catch { } } }
        _db.ExpedienteTipologiaVersiones.RemoveRange(versiones);
        if (!string.IsNullOrEmpty(t.ArchivoUrl) && versiones.All(v => v.ArchivoUrl != t.ArchivoUrl)) { try { await _storage.DeleteAsync(t.ArchivoUrl, ct); } catch { } }
        t.ArchivoUrl = null; t.ArchivoNombre = null; t.ArchivoMime = null; t.ArchivoTamano = 0; t.MetaJson = null; t.NumeroVersiones = 0;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DescargaDocumentoDto?> DescargarTipologiaAsync(Guid tipologiaId, CancellationToken ct)
    {
        var t = await _db.ExpedienteTipologias.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tipologiaId, ct);
        if (t is null || string.IsNullOrEmpty(t.ArchivoUrl)) return null;
        var bytes = await _storage.DownloadAsync(t.ArchivoUrl, ct);
        if (bytes is null) return null;
        return new DescargaDocumentoDto(t.ArchivoNombre ?? "documento", t.ArchivoMime ?? "application/octet-stream", bytes.LongLength, Convert.ToBase64String(bytes));
    }

    public async Task<TipologiaPreviewDto?> PreviewTipologiaAsync(Guid tipologiaId, CancellationToken ct)
    {
        var t = await _db.ExpedienteTipologias.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tipologiaId, ct);
        if (t is null || string.IsNullOrEmpty(t.ArchivoUrl)) return null;
        var bytes = await _storage.DownloadAsync(t.ArchivoUrl, ct);
        if (bytes is null) return null;
        return BuildPreview(bytes, t.ArchivoNombre ?? "documento", t.ArchivoMime ?? "application/octet-stream");
    }

    public async Task<TipologiaPreviewDto?> PreviewVersionTipologiaAsync(Guid tipologiaId, Guid versionId, CancellationToken ct)
    {
        var v = await _db.ExpedienteTipologiaVersiones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == versionId && x.ExpedienteTipologiaId == tipologiaId, ct);
        if (v is null || string.IsNullOrEmpty(v.ArchivoUrl)) return null;
        var bytes = await _storage.DownloadAsync(v.ArchivoUrl, ct);
        if (bytes is null) return null;
        return BuildPreview(bytes, v.ArchivoNombre, v.ArchivoMime);
    }

    public async Task<bool> DuplicarTipologiaAsync(Guid tipologiaId, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var t = await _db.ExpedienteTipologias.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tipologiaId, ct);
        if (t is null) return false;
        // Casilla nueva del mismo tipo (misma etiqueta), vacia y no obligatoria: para cargar otro documento igual.
        var orden = (await _db.ExpedienteTipologias.Where(x => x.ExpedienteId == t.ExpedienteId).Select(x => (int?)x.Orden).MaxAsync(ct) ?? -1) + 1;
        _db.ExpedienteTipologias.Add(new ExpedienteTipologia { TenantId = tenantId, ExpedienteId = t.ExpedienteId, Nombre = t.Nombre, Obligatoria = false, Orden = orden });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Preview (render de txt/csv/imagenes/excel/word) =====================

    private static TipologiaPreviewDto BuildPreview(byte[] bytes, string nombre, string mime)
    {
        var ext = (Path.GetExtension(nombre) ?? "").TrimStart('.').ToLowerInvariant();
        mime = string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime;

        bool esImagen = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || ext is "png" or "jpg" or "jpeg" or "gif" or "webp" or "bmp" or "svg";
        bool esPdf = mime.Contains("pdf", StringComparison.OrdinalIgnoreCase) || ext == "pdf";
        bool esTexto = mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || ext is "txt" or "csv" or "tsv" or "md" or "log" or "json" or "xml" or "yml" or "yaml" or "ini";
        bool esExcel = ext is "xlsx" or "xlsm" or "xltx" || mime.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase);
        bool esWord = ext == "docx" || mime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase);

        if (esImagen)
            return new TipologiaPreviewDto("image", nombre, mime, DataUri: $"data:{mime};base64,{Convert.ToBase64String(bytes)}");
        if (esPdf)
            return new TipologiaPreviewDto("pdf", nombre, "application/pdf", DataUri: $"data:application/pdf;base64,{Convert.ToBase64String(bytes)}");
        if (esTexto)
            return new TipologiaPreviewDto("text", nombre, mime, Texto: Truncar(DecodificarTexto(bytes), 300_000));
        if (esExcel)
        {
            try { return new TipologiaPreviewDto("html", nombre, mime, Html: ExcelAHtml(bytes)); }
            catch { return new TipologiaPreviewDto("none", nombre, mime); }
        }
        if (esWord)
        {
            try { return new TipologiaPreviewDto("html", nombre, mime, Html: WordAHtml(bytes)); }
            catch { return new TipologiaPreviewDto("none", nombre, mime); }
        }
        return new TipologiaPreviewDto("none", nombre, mime);
    }

    private static string DecodificarTexto(byte[] bytes)
    {
        // Salta BOM UTF-8 si existe.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            bytes = bytes[3..];
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch { return Encoding.Latin1.GetString(bytes); } // fallback: archivos legados en ANSI/Latin1
    }

    private static string Truncar(string s, int max)
        => s.Length <= max ? s : s[..max] + "\n\n... (contenido truncado para la vista previa)";

    private static string ExcelAHtml(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        var sb = new StringBuilder();
        foreach (var ws in wb.Worksheets)
        {
            sb.Append("<div class=\"prev-sheet\"><div class=\"prev-sheet-name\">").Append(Html(ws.Name)).Append("</div>");
            var range = ws.RangeUsed();
            if (range is null) { sb.Append("<div class=\"prev-empty\">Hoja vacia</div></div>"); continue; }
            int firstRow = range.RangeAddress.FirstAddress.RowNumber;
            int firstCol = range.RangeAddress.FirstAddress.ColumnNumber;
            int rows = Math.Min(range.RowCount(), 250);
            int cols = Math.Min(range.ColumnCount(), 40);
            sb.Append("<table class=\"prev-table\">");
            for (int i = 0; i < rows; i++)
            {
                sb.Append("<tr>");
                for (int j = 0; j < cols; j++)
                    sb.Append("<td>").Append(Html(ws.Cell(firstRow + i, firstCol + j).GetFormattedString())).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table>");
            if (range.RowCount() > rows) sb.Append("<div class=\"prev-more\">... ").Append(range.RowCount() - rows).Append(" fila(s) mas</div>");
            sb.Append("</div>");
        }
        return sb.ToString();
    }

    private static string WordAHtml(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return "<div class=\"prev-empty\">Documento vacio</div>";
        var sb = new StringBuilder("<div class=\"prev-doc\">");
        foreach (var el in body.Elements())
        {
            if (el is DocumentFormat.OpenXml.Wordprocessing.Paragraph p)
            {
                var texto = string.Concat(p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
                var estilo = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                if (string.IsNullOrWhiteSpace(texto)) { sb.Append("<p>&nbsp;</p>"); continue; }
                if (estilo.Equals("Title", StringComparison.OrdinalIgnoreCase) || estilo.StartsWith("Heading1", StringComparison.OrdinalIgnoreCase))
                    sb.Append("<h2>").Append(Html(texto)).Append("</h2>");
                else if (estilo.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                    sb.Append("<h3>").Append(Html(texto)).Append("</h3>");
                else
                    sb.Append("<p>").Append(Html(texto)).Append("</p>");
            }
            else if (el is DocumentFormat.OpenXml.Wordprocessing.Table tbl)
            {
                sb.Append("<table class=\"prev-table\">");
                foreach (var tr in tbl.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                {
                    sb.Append("<tr>");
                    foreach (var tc in tr.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                    {
                        var ctexto = string.Concat(tc.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
                        sb.Append("<td>").Append(Html(ctexto)).Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Html(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    // ===================== Helpers =====================

    private static ExpedienteDetalleDto MapDetalle(Expediente e, List<ExpedienteCampo> campos, List<ExpedienteTipologia> tips)
    {
        var cargadas = tips.Count(t => !string.IsNullOrEmpty(t.ArchivoUrl));
        var camposDto = campos.Select(c => new ExpedienteCampoDto(c.Id, c.Clave, c.Label)).ToList();
        var tipsDto = tips.Select(t =>
        {
            var meta = ParseMeta(t.MetaJson);
            var pairs = campos
                .Where(c => meta.ContainsKey(c.Clave) && !string.IsNullOrWhiteSpace(meta[c.Clave]))
                .Select(c => new ExpedienteMetaPairDto(c.Clave, c.Label, meta[c.Clave]))
                .ToList();
            return new ExpedienteTipologiaDto(t.Id, t.Nombre, t.Obligatoria, !string.IsNullOrEmpty(t.ArchivoUrl),
                t.ArchivoNombre, t.ArchivoMime, t.ArchivoTamano, pairs, t.NumeroVersiones);
        }).ToList();
        return new ExpedienteDetalleDto(e.Id, e.Codigo, e.Nombre, e.Serie, e.Subserie, tips.Count, cargadas, camposDto, tipsDto);
    }

    private static Dictionary<string, string> ParseMeta(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static (string clave, string label)[] DefaultCampos() => new[]
    {
        ("fecha", "Fecha del documento"),
        ("tercero", "Tercero / responsable"),
        ("vigencia", "Vigencia / vence"),
        ("folios", "N de folios")
    };

    private static string Prefijo(string serie)
    {
        if (serie.StartsWith("Actas", StringComparison.OrdinalIgnoreCase)) return "ACT";
        if (serie.StartsWith("Contratos", StringComparison.OrdinalIgnoreCase)) return "CON";
        if (serie.StartsWith("Financ", StringComparison.OrdinalIgnoreCase)) return "FIN";
        if (serie.StartsWith("Legal", StringComparison.OrdinalIgnoreCase)) return "LEG";
        var letras = new string(serie.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        return string.IsNullOrEmpty(letras) ? "EXP" : letras.PadRight(3, 'X');
    }

    private static string Slug(string s)
    {
        var lower = s.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        var slug = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(slug) ? "campo" : slug;
    }

    private static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    // TRD base (igual al prototipo): 4 series, 8 subseries, campos por defecto.
    private async Task SeedTrdAsync(Guid tenantId, CancellationToken ct)
    {
        var data = new (string serie, (string sub, string[] tips)[] subs)[]
        {
            ("Actas y gobierno", new[]
            {
                ("Actas de Asamblea", new[] { "Convocatoria", "Acta firmada", "Listado de asistencia", "Poderes" }),
                ("Actas de Consejo", new[] { "Citacion", "Acta de consejo", "Anexos" })
            }),
            ("Contratos", new[]
            {
                ("Contratos de servicios", new[] { "Contrato firmado", "Polizas", "Camara de comercio", "RUT" }),
                ("Contratos laborales", new[] { "Contrato laboral", "Afiliacion seguridad social", "Hoja de vida" })
            }),
            ("Financieros", new[]
            {
                ("Estados financieros", new[] { "Balance general", "Estado de resultados", "Notas contables", "Dictamen revisor" }),
                ("Presupuestos", new[] { "Proyecto de presupuesto", "Presupuesto aprobado", "Ejecucion" })
            }),
            ("Legal y constitucion", new[]
            {
                ("Polizas y seguros", new[] { "Poliza areas comunes", "Poliza de manejo", "Certificado" }),
                ("Constitucion", new[] { "Escritura", "Reglamento de PH", "Certificado de existencia" })
            })
        };

        var so = 0;
        foreach (var (serieNombre, subs) in data)
        {
            var serie = new SerieDocumental { TenantId = tenantId, Nombre = serieNombre, Orden = so++ };
            _db.SeriesDocumentales.Add(serie);
            var sso = 0;
            foreach (var (subNombre, tips) in subs)
            {
                var sub = new SubserieDocumental { TenantId = tenantId, SerieId = serie.Id, Nombre = subNombre, Orden = sso++ };
                _db.SubseriesDocumentales.Add(sub);
                var to = 0;
                foreach (var tip in tips)
                    _db.SubserieTipologias.Add(new SubserieTipologia { TenantId = tenantId, SubserieId = sub.Id, Nombre = tip, Orden = to++ });
                var co = 0;
                foreach (var (clave, label) in DefaultCampos())
                    _db.SubserieCampos.Add(new SubserieCampo { TenantId = tenantId, SubserieId = sub.Id, Clave = clave, Label = label, Orden = co++ });
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
