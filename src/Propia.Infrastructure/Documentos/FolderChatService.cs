using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.Documentos;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Application.Ocr;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Documentos;

/// <summary>
/// Chat con los documentos de un folder (Documentos v2, Ola 3). Ver <see cref="IFolderChatService"/>.
/// Indexacion bajo demanda: antes de responder se extrae el texto de los documentos del folder en contexto
/// que aun no lo tengan para su version actual (texto/Office en local; PDF/imagen via el extractor de IA
/// configurado en Super Admin). Limites de contexto para no desbordar el prompt.
/// </summary>
public class FolderChatService : IFolderChatService
{
    private const int MaxCharsPorDocumento = 4000;
    private const int MaxCharsContexto = 48_000;
    private const int MaxDocumentosIndexarPorTurno = 12;
    private const int MaxTurnosHistorial = 16;

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextAccessor _http;
    private readonly IBlobStorage _storage;
    private readonly IAiProviderClient _ai;
    private readonly IAiUsageService _usage;
    private readonly ISecretProtector _secret;
    private readonly IAiDocumentExtractor _extractor;
    private readonly ILogger<FolderChatService> _logger;

    public FolderChatService(PropiaDbContext db, ITenantContext tenant, IHttpContextAccessor http, IBlobStorage storage,
        IAiProviderClient ai, IAiUsageService usage, ISecretProtector secret, IAiDocumentExtractor extractor, ILogger<FolderChatService> logger)
    {
        _db = db; _tenant = tenant; _http = http; _storage = storage; _ai = ai; _usage = usage; _secret = secret; _extractor = extractor; _logger = logger;
    }

    private Guid RequireTenant() => _tenant.CurrentTenantId ?? throw new InvalidOperationException("No hay copropiedad activa.");

    private Guid UsuarioActual()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    // =====================================================================
    // Conversacion
    // =====================================================================

    public async Task<FolderChatDto?> ListarAsync(Guid folderRaizId, CancellationToken ct)
    {
        if (!await _db.Folders.AnyAsync(f => f.Id == folderRaizId, ct)) return null;
        var uid = UsuarioActual();
        var msgs = await _db.FolderChatMensajes.AsNoTracking()
            .Where(m => m.FolderId == folderRaizId && m.UsuarioId == uid)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new FolderChatMensajeDto(m.Id, m.Rol, m.Texto, m.CreatedAt, m.Error))
            .ToListAsync(ct);
        var prov = await ResolverProveedorAsync(ct);
        return new FolderChatDto(folderRaizId, prov is not null, prov?.provider.ToString(), prov?.model, msgs);
    }

    public async Task<bool> LimpiarAsync(Guid folderRaizId, CancellationToken ct)
    {
        var uid = UsuarioActual();
        var n = await _db.FolderChatMensajes.Where(m => m.FolderId == folderRaizId && m.UsuarioId == uid).ExecuteDeleteAsync(ct);
        return n >= 0;
    }

    public async Task<FolderChatRespuestaDto?> EnviarAsync(Guid folderRaizId, EnviarFolderChatRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var raiz = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderRaizId, ct);
        if (raiz is null) return null;
        var texto = (req.Texto ?? "").Trim();
        if (texto.Length == 0) throw new InvalidOperationException("Escribe una pregunta.");
        if (texto.Length > 4000) texto = texto[..4000];
        var uid = UsuarioActual();

        var userMsg = new FolderChatMensaje { TenantId = tenantId, FolderId = folderRaizId, UsuarioId = uid, Rol = "user", Texto = texto, FolderContextoId = req.FolderContextoId };
        _db.FolderChatMensajes.Add(userMsg);
        await _db.SaveChangesAsync(ct);

        string respuesta; var error = false; int tin = 0, tout = 0;
        var prov = await ResolverProveedorAsync(ct);
        if (prov is null)
        {
            respuesta = "La IA no está configurada en la plataforma (Super Admin > Servidores de IA). Mientras tanto puedo decirte qué hay en el folder: " + ResumenLocal(await ContextoAsync(raiz, req.FolderContextoId, ct));
            error = true;
        }
        else
        {
            var quota = await _usage.GetQuotaAsync(ct);
            if (quota.Exceeded)
            {
                respuesta = $"Alcanzaste el límite de llamadas de IA de tu plan este mes ({quota.MonthlyLimitCalls:N0}).";
                error = true;
            }
            else
            {
                var ctx = await ContextoAsync(raiz, req.FolderContextoId, ct);
                var system = SystemPrompt(raiz, ctx);
                var historial = await _db.FolderChatMensajes.AsNoTracking()
                    .Where(m => m.FolderId == folderRaizId && m.UsuarioId == uid && !m.Error && m.Id != userMsg.Id)
                    .OrderByDescending(m => m.CreatedAt).Take(MaxTurnosHistorial).ToListAsync(ct);
                historial.Reverse();
                var turns = historial.Select(m => new AiChatTurn(m.Rol == "assistant" ? "model" : "user", m.Texto)).ToList();
                turns.Add(new AiChatTurn("user", texto));

                var (provider, apiKey, baseUrl, model) = prov.Value;
                AiChatResult r;
                try { r = await _ai.CompleteAsync(provider, apiKey, baseUrl, model, system, turns, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Fallo el chat de folders"); r = new AiChatResult(false, null, ex.Message); }
                tin = r.InputTokens; tout = r.OutputTokens;
                try { await _usage.RecordAsync(null, provider, model, tin, tout, "folders-chat", r.Ok, ct); } catch { }
                if (r.Ok && !string.IsNullOrWhiteSpace(r.Text)) respuesta = r.Text!.Trim();
                else { respuesta = "No pude consultar la IA en este momento" + (string.IsNullOrWhiteSpace(r.Error) ? "." : ": " + r.Error); error = true; }
            }
        }

        var botMsg = new FolderChatMensaje { TenantId = tenantId, FolderId = folderRaizId, UsuarioId = uid, Rol = "assistant", Texto = respuesta, FolderContextoId = req.FolderContextoId, InputTokens = tin, OutputTokens = tout, Error = error };
        _db.FolderChatMensajes.Add(botMsg);
        await _db.SaveChangesAsync(ct);
        return new FolderChatRespuestaDto(
            new FolderChatMensajeDto(userMsg.Id, "user", userMsg.Texto, userMsg.CreatedAt, false),
            new FolderChatMensajeDto(botMsg.Id, "assistant", botMsg.Texto, botMsg.CreatedAt, error));
    }

    // =====================================================================
    // Proveedor
    // =====================================================================

    private async Task<(AiProvider provider, string apiKey, string? baseUrl, string model)?> ResolverProveedorAsync(CancellationToken ct)
    {
        // 1) Proveedores de agentes (Super Admin > Servidores de IA). Preferencia: Claude, Gemini, ChatGPT, DeepSeek.
        var cfgs = await _db.AiProviderConfigs.AsNoTracking().Where(c => c.IsEnabled && c.ApiKeyEncrypted != null).ToListAsync(ct);
        var orden = new[] { AiProvider.Claude, AiProvider.Gemini, AiProvider.ChatGpt, AiProvider.DeepSeek };
        foreach (var cfg in orden.Select(p => cfgs.FirstOrDefault(c => c.Provider == p)).Where(c => c is not null))
        {
            try
            {
                var apiKey = _secret.Unprotect(cfg!.ApiKeyEncrypted!);
                var model = string.IsNullOrWhiteSpace(cfg.Model) ? AiProviderCatalog.For(cfg.Provider).DefaultModel : cfg.Model!;
                return (cfg.Provider, apiKey, cfg.BaseUrl, model);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Chat de folders: no se pudo descifrar la API key del proveedor {Provider}; se prueba el siguiente.", cfg!.Provider); }
        }
        // 2) Respaldo: el motor de extraccion de documentos con IA (Super Admin > OCR, Gemini), que es el mismo
        //    que ya lee PDFs en Seguros. Usa la base por defecto de Gemini (el endpoint del OCR es de documentos).
        var ocr = await _db.OcrProviderConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.IsEnabled && c.Provider == OcrProvider.GeminiDocument && c.ApiKeyEncrypted != null, ct);
        if (ocr is not null)
        {
            try
            {
                var apiKey = _secret.Unprotect(ocr.ApiKeyEncrypted!);
                var model = string.IsNullOrWhiteSpace(ocr.ModelId) ? AiProviderCatalog.For(AiProvider.Gemini).DefaultModel : ocr.ModelId!;
                return (AiProvider.Gemini, apiKey, null, model);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Chat de folders: no se pudo descifrar la API key del motor de documentos (OCR Gemini)."); }
        }
        _logger.LogWarning("Chat de folders: ningun proveedor de IA utilizable (sin configuracion o API key no descifrable). Vuelve a guardar la clave en Super Admin.");
        return null;
    }

    // =====================================================================
    // Contexto
    // =====================================================================

    private sealed record DocCtx(FolderDocumento Doc, string Ruta, List<(string label, string? descripcion, string? valor)> Meta, string? Texto, string Fuente);
    private sealed record Ctx(Folder Raiz, Folder? Actual, List<Folder> Folders, List<DocCtx> Docs, int TotalDocs);

    private async Task<Ctx> ContextoAsync(Folder raiz, Guid? actualId, CancellationToken ct)
    {
        var todos = await _db.Folders.AsNoTracking().ToListAsync(ct);
        var porId = todos.ToDictionary(f => f.Id);
        // Subarbol del folder raiz.
        var sub = new List<Folder>();
        var cola = new Queue<Folder>(); cola.Enqueue(raiz);
        while (cola.Count > 0) { var f = cola.Dequeue(); sub.Add(f); foreach (var h in todos.Where(x => x.ParentId == f.Id)) cola.Enqueue(h); }
        var ids = sub.Select(f => f.Id).ToList();
        var actual = actualId.HasValue && porId.TryGetValue(actualId.Value, out var a) && ids.Contains(a.Id) ? a : null;

        string RutaDe(Guid fid)
        {
            var partes = new List<string>(); var cur = porId.GetValueOrDefault(fid);
            while (cur is not null) { partes.Insert(0, cur.Nombre); cur = cur.ParentId.HasValue ? porId.GetValueOrDefault(cur.ParentId.Value) : null; }
            return string.Join(" / ", partes);
        }

        var docs = await _db.FolderDocumentos.AsNoTracking().Where(d => ids.Contains(d.FolderId) && !d.Archivado).OrderBy(d => d.Orden).ToListAsync(ct);
        // Prioridad: los del folder actual (y su rama) primero.
        var ramaActual = new HashSet<Guid>();
        if (actual is not null)
        {
            var q = new Queue<Folder>(); q.Enqueue(actual);
            while (q.Count > 0) { var f = q.Dequeue(); ramaActual.Add(f.Id); foreach (var h in sub.Where(x => x.ParentId == f.Id)) q.Enqueue(h); }
            docs = docs.OrderByDescending(d => ramaActual.Contains(d.FolderId)).ThenBy(d => d.Orden).ToList();
        }

        var defs = await _db.FolderCampoDefiniciones.AsNoTracking().ToListAsync(ct);
        var docIds = docs.Select(d => d.Id).ToList();
        var valores = await _db.FolderDocumentoCampoValores.AsNoTracking().Where(v => docIds.Contains(v.DocumentoId)).ToListAsync(ct);
        var textos = await _db.FolderDocumentoTextos.AsNoTracking().Where(t => docIds.Contains(t.DocumentoId)).ToDictionaryAsync(t => t.DocumentoId, ct);

        // Indexacion bajo demanda (limitada por turno) de documentos con archivo sin texto para su version.
        var pendientes = docs.Where(d => d.ArchivoUrl != null && (!textos.TryGetValue(d.Id, out var t) || t.NumeroVersion != d.NumeroVersiones)).Take(MaxDocumentosIndexarPorTurno).ToList();
        foreach (var d in pendientes)
        {
            var t = await IndexarInternoAsync(d, ct);
            if (t is not null) textos[d.Id] = t;
        }

        var ctxDocs = new List<DocCtx>();
        foreach (var d in docs)
        {
            // Esquema efectivo: campos de la cadena de folders + propios.
            var cadena = new List<Guid>(); var cur = porId.GetValueOrDefault(d.FolderId);
            while (cur is not null) { cadena.Insert(0, cur.Id); cur = cur.ParentId.HasValue ? porId.GetValueOrDefault(cur.ParentId.Value) : null; }
            var esquema = cadena.SelectMany(fid => defs.Where(c => c.FolderId == fid).OrderBy(c => c.Orden)).Concat(defs.Where(c => c.DocumentoId == d.Id).OrderBy(c => c.Orden)).ToList();
            var vals = valores.Where(v => v.DocumentoId == d.Id).ToDictionary(v => v.DefinicionId, v => v.Valor);
            var meta = esquema.Select(c => (c.Label, c.Descripcion, vals.GetValueOrDefault(c.Id))).ToList();
            textos.TryGetValue(d.Id, out var tx);
            ctxDocs.Add(new DocCtx(d, RutaDe(d.FolderId), meta, tx?.Texto, tx?.Fuente ?? "ninguna"));
        }
        return new Ctx(raiz, actual, sub, ctxDocs, docs.Count);
    }

    private static string SystemPrompt(Folder raiz, Ctx ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Eres el asistente documental de una copropiedad (propiedad horizontal en Colombia) dentro de la plataforma PROPIA.");
        sb.AppendLine("Respondes en español, de forma breve y concreta, SOLO con base en los documentos y metadatos que te doy abajo. Si algo no está en ellos, dilo claramente.");
        sb.AppendLine("Cuando cites información, nombra el documento y el folder donde está. Las fechas, valores y nombres deben salir de los documentos o de sus metadatos, nunca inventados.");
        sb.AppendLine("Si te piden qué falta, revisa las casillas SIN archivo y las marcadas como obligatorias.");
        sb.AppendLine();
        sb.AppendLine($"FOLDER RAÍZ: {raiz.Nombre}" + (ctx.Actual is not null && ctx.Actual.Id != raiz.Id ? $"  |  FOLDER SELECCIONADO POR EL USUARIO: {ctx.Actual.Nombre}" : ""));
        sb.AppendLine("ESTRUCTURA:");
        void Arbol(Guid? parentId, int nivel)
        {
            foreach (var f in ctx.Folders.Where(x => x.ParentId == parentId).OrderBy(x => x.Orden))
            {
                var n = ctx.Docs.Count(d => d.Doc.FolderId == f.Id);
                sb.AppendLine($"{new string(' ', nivel * 2)}- {f.Nombre} ({n} documento(s))");
                Arbol(f.Id, nivel + 1);
            }
        }
        sb.AppendLine($"- {raiz.Nombre}");
        Arbol(raiz.Id, 1);
        sb.AppendLine();
        sb.AppendLine("DOCUMENTOS (nombre | folder | archivo | metadatos | texto extraído):");
        var presupuesto = MaxCharsContexto - sb.Length;
        foreach (var d in ctx.Docs)
        {
            var bloque = new StringBuilder();
            bloque.AppendLine($"### {d.Doc.Nombre}");
            bloque.AppendLine($"Folder: {d.Ruta}");
            bloque.AppendLine(d.Doc.ArchivoUrl is null
                ? $"Archivo: SIN ARCHIVO (casilla vacía{(d.Doc.Obligatorio ? ", OBLIGATORIA" : "")})"
                : $"Archivo: {d.Doc.ArchivoNombre} (versión {d.Doc.NumeroVersiones}, {d.Doc.ArchivoTamano / 1024} KB)");
            if (d.Meta.Count > 0)
            {
                bloque.AppendLine("Metadatos:");
                foreach (var (label, desc, valor) in d.Meta)
                    bloque.AppendLine($"- {label}{(string.IsNullOrWhiteSpace(desc) ? "" : $" ({desc})")}: {(string.IsNullOrWhiteSpace(valor) ? "(sin valor)" : valor)}");
            }
            if (!string.IsNullOrWhiteSpace(d.Texto))
            {
                var t = d.Texto.Length > MaxCharsPorDocumento ? d.Texto[..MaxCharsPorDocumento] + " [...]" : d.Texto;
                bloque.AppendLine("Texto:");
                bloque.AppendLine(t);
            }
            else if (d.Doc.ArchivoUrl is not null) bloque.AppendLine($"Texto: (no disponible, fuente {d.Fuente})");
            bloque.AppendLine();
            if (bloque.Length > presupuesto) { sb.AppendLine($"[... {ctx.Docs.Count - ctx.Docs.IndexOf(d)} documento(s) más no incluidos por tamaño]"); break; }
            sb.Append(bloque); presupuesto -= bloque.Length;
        }
        return sb.ToString();
    }

    private static string ResumenLocal(Ctx ctx)
    {
        var con = ctx.Docs.Count(d => d.Doc.ArchivoUrl != null);
        var sin = ctx.Docs.Where(d => d.Doc.ArchivoUrl == null).Select(d => d.Doc.Nombre).ToList();
        return $"{ctx.TotalDocs} documento(s), {con} con archivo" + (sin.Count > 0 ? $"; sin archivo: {string.Join(", ", sin.Take(10))}." : ".");
    }

    // =====================================================================
    // Indexacion (texto extraido)
    // =====================================================================

    public async Task<FolderDocumentoTextoDto?> IndexarDocumentoAsync(Guid documentoId, bool forzar, CancellationToken ct)
    {
        var d = await _db.FolderDocumentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return null;
        var existente = await _db.FolderDocumentoTextos.AsNoTracking().FirstOrDefaultAsync(t => t.DocumentoId == documentoId, ct);
        if (!forzar && existente is not null && existente.NumeroVersion == d.NumeroVersiones) return Map(existente);
        var t2 = await IndexarInternoAsync(d, ct);
        return t2 is null ? null : Map(t2);
    }

    private static FolderDocumentoTextoDto Map(FolderDocumentoTexto t)
        => new(t.DocumentoId, t.NumeroVersion, t.Fuente, t.Caracteres, t.Error, t.Texto is null ? null : (t.Texto.Length > 400 ? t.Texto[..400] + "..." : t.Texto));

    private async Task<FolderDocumentoTexto?> IndexarInternoAsync(FolderDocumento d, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var fila = await _db.FolderDocumentoTextos.FirstOrDefaultAsync(t => t.DocumentoId == d.Id, ct)
                   ?? new FolderDocumentoTexto { TenantId = tenantId, DocumentoId = d.Id };
        fila.NumeroVersion = d.NumeroVersiones; fila.Texto = null; fila.Error = null; fila.Caracteres = 0; fila.Fuente = "ninguna";
        if (string.IsNullOrEmpty(d.ArchivoUrl)) { fila.Error = "Sin archivo."; }
        else
        {
            try
            {
                var bytes = await _storage.DownloadAsync(d.ArchivoUrl, ct);
                if (bytes is null) fila.Error = "Archivo no encontrado en el almacenamiento.";
                else
                {
                    var (texto, fuente, error) = await ExtraerTextoAsync(bytes, d.ArchivoNombre ?? "", d.ArchivoMime ?? "", ct);
                    fila.Texto = string.IsNullOrWhiteSpace(texto) ? null : Normalizar(texto!);
                    fila.Caracteres = fila.Texto?.Length ?? 0; fila.Fuente = fuente; fila.Error = error;
                }
            }
            catch (Exception ex) { fila.Error = ex.Message; }
        }
        if (fila.Id == Guid.Empty || !await _db.FolderDocumentoTextos.AnyAsync(t => t.Id == fila.Id, ct)) _db.FolderDocumentoTextos.Add(fila);
        await _db.SaveChangesAsync(ct);
        return fila;
    }

    private async Task<(string? texto, string fuente, string? error)> ExtraerTextoAsync(byte[] bytes, string nombre, string mime, CancellationToken ct)
    {
        var ext = (Path.GetExtension(nombre) ?? "").TrimStart('.').ToLowerInvariant();
        if (ext is "txt" or "csv" or "tsv" or "md" or "json" or "xml" or "log" or "rtf" || mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return (DecodificarTexto(bytes), "texto", null);
        if (ext == "docx")
        {
            try { return (WordATexto(bytes), "office", null); } catch (Exception ex) { return (null, "office", "Word ilegible: " + ex.Message); }
        }
        if (ext is "xlsx" or "xlsm")
        {
            try { return (ExcelATexto(bytes), "office", null); } catch (Exception ex) { return (null, "office", "Excel ilegible: " + ex.Message); }
        }
        var esPdf = ext == "pdf" || mime.Contains("pdf", StringComparison.OrdinalIgnoreCase);
        var esImg = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || ext is "png" or "jpg" or "jpeg" or "webp" or "gif" or "bmp";
        if (esPdf || esImg)
        {
            if (!await _extractor.DisponibleAsync(ct)) return (null, "ia", "El motor de extracción con IA no está configurado (Super Admin).");
            var campos = new[] { new CampoObjetivo("texto_completo", "Transcripción literal y completa de todo el texto legible del documento, en orden de lectura, sin resumir.", "texto") };
            var r = await _extractor.ExtraerAsync(bytes, string.IsNullOrWhiteSpace(mime) ? (esPdf ? "application/pdf" : "image/png") : mime, nombre, campos, "folders", ct);
            if (!r.Ok) return (null, "ia", r.Error);
            var t = r.Campo("texto_completo") ?? r.TextoCompleto;
            return (t, "ia", string.IsNullOrWhiteSpace(t) ? "La IA no devolvió texto." : null);
        }
        return (null, "ninguna", "Tipo de archivo sin extracción de texto.");
    }

    private static string DecodificarTexto(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) bytes = bytes[3..];
        try { return new UTF8Encoding(false, true).GetString(bytes); } catch { return Encoding.Latin1.GetString(bytes); }
    }

    private static string WordATexto(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return "";
        var sb = new StringBuilder();
        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            var t = string.Concat(p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(x => x.Text));
            if (!string.IsNullOrWhiteSpace(t)) sb.AppendLine(t);
        }
        return sb.ToString();
    }

    private static string ExcelATexto(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        var sb = new StringBuilder();
        foreach (var ws in wb.Worksheets)
        {
            sb.AppendLine($"[Hoja {ws.Name}]");
            var range = ws.RangeUsed(); if (range is null) continue;
            var rows = Math.Min(range.RowCount(), 400);
            for (var i = 1; i <= rows; i++)
            {
                var celdas = range.Row(i).Cells().Select(c => c.GetFormattedString()).Where(s => !string.IsNullOrWhiteSpace(s));
                var linea = string.Join(" | ", celdas);
                if (linea.Length > 0) sb.AppendLine(linea);
            }
        }
        return sb.ToString();
    }

    private static string Normalizar(string s)
    {
        var t = s.Replace("\r\n", "\n").Replace('\r', '\n');
        while (t.Contains("\n\n\n")) t = t.Replace("\n\n\n", "\n\n");
        return t.Length > 200_000 ? t[..200_000] : t;
    }
}
