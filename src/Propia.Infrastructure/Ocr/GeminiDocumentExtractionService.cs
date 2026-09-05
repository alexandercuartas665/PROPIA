using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Application.Ocr;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Ocr;

/// <summary>
/// Arnes de extraccion con IA (Gemini). Lee el motor configurado en Super Admin (OcrProviderConfig);
/// si el habilitado es GeminiDocument, manda el PDF/imagen NATIVO al modelo con salida estructurada
/// (JSON) y devuelve los campos con su confianza. Registra cada corrida en DocumentExtractionLog para
/// afinar. No persiste el documento ni loggea la API key.
/// </summary>
public sealed class GeminiDocumentExtractionService : IAiDocumentExtractor
{
    private const int RawMaxChars = 8000;

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IAiProviderClient _ai;
    private readonly ITenantContext _tenant;

    public GeminiDocumentExtractionService(PropiaDbContext db, ISecretProtector secret, IAiProviderClient ai, ITenantContext tenant)
    {
        _db = db;
        _secret = secret;
        _ai = ai;
        _tenant = tenant;
    }

    public async Task<bool> DisponibleAsync(CancellationToken ct = default)
    {
        var cfg = await EnabledIaConfigAsync(ct);
        return cfg is not null;
    }

    public async Task<DocumentExtractionResult> ExtraerAsync(
        byte[] documento, string mimeType, string? nombreArchivo,
        IReadOnlyList<CampoObjetivo> campos, string modulo, CancellationToken ct = default)
    {
        var cfg = await EnabledIaConfigAsync(ct);
        if (cfg is null)
            return DocumentExtractionResult.Falla("El motor de extraccion configurado en Super Admin no es de IA (Gemini).");

        string apiKey;
        try { apiKey = _secret.Unprotect(cfg.ApiKeyEncrypted!); }
        catch { return DocumentExtractionResult.Falla("No se pudo descifrar la API key del proveedor de IA.", cfg.Provider.ToString()); }

        var model = string.IsNullOrWhiteSpace(cfg.ModelId) ? OcrProviderCatalog.For(cfg.Provider).DefaultModel : cfg.ModelId!;
        var instruction = BuildInstruction(campos);
        var schema = BuildSchema();

        var sw = Stopwatch.StartNew();
        var r = await _ai.ExtractFromDocumentAsync(AiProvider.Gemini, apiKey, cfg.Endpoint, model,
            instruction, documento, mimeType, schema, ct);
        sw.Stop();

        List<ExtractedField> parsed = new();
        string? parseError = null;
        if (r.Ok && !string.IsNullOrWhiteSpace(r.Text))
        {
            try { parsed = ParseCampos(r.Text!); }
            catch (Exception ex) { parseError = $"No se pudo interpretar la respuesta de la IA: {ex.Message}"; }
        }

        var ok = r.Ok && parseError is null;
        var error = r.Ok ? parseError : r.Error;

        await LogAsync(cfg.Provider, model, nombreArchivo, mimeType, documento.LongLength, modulo,
            ok, error, (int)sw.ElapsedMilliseconds, r.InputTokens, r.OutputTokens, parsed, r.Text, ct);

        if (!ok)
            return DocumentExtractionResult.Falla(error ?? "Fallo la extraccion con IA.", cfg.Provider.ToString());

        return new DocumentExtractionResult(true, null, cfg.Provider.ToString(), model, parsed, null);
    }

    // La IA se considera "el motor" solo si el proveedor habilitado es de IA.
    private async Task<OcrProviderConfig?> EnabledIaConfigAsync(CancellationToken ct)
    {
        var cfg = await _db.OcrProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (cfg is null || !OcrProviderCatalog.EsIa(cfg.Provider) || string.IsNullOrWhiteSpace(cfg.ApiKeyEncrypted))
            return null;
        return cfg;
    }

    private static string BuildInstruction(IReadOnlyList<CampoObjetivo> campos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Eres un extractor de datos de documentos. Analiza el documento adjunto y devuelve SOLO JSON que cumpla el schema.");
        sb.AppendLine("Por cada dato devuelve un objeto {nombre, valor, confianza} donde confianza es un numero 0-1.");
        sb.AppendLine("REGLAS: si un dato NO aparece en el documento, pon valor=null y confianza=0 (NUNCA inventes).");
        sb.AppendLine("Fechas en formato yyyy-MM-dd. Montos: solo el numero, sin simbolo de moneda ni separadores de miles.");
        if (campos.Count > 0)
        {
            sb.AppendLine("Extrae EXACTAMENTE estos campos (usa el 'nombre' indicado):");
            foreach (var c in campos)
                sb.AppendLine($"- {c.Nombre} ({c.Tipo}): {c.Descripcion}");
        }
        else
        {
            sb.AppendLine("Extrae todos los campos clave que identifiques (numeros, fechas, montos, entidades, partes, vigencias).");
        }
        return sb.ToString();
    }

    // Schema generico (subconjunto OpenAPI que acepta Gemini): lista de {nombre, valor, confianza}.
    private static string BuildSchema() =>
        "{\"type\":\"object\",\"properties\":{\"campos\":{\"type\":\"array\",\"items\":{\"type\":\"object\"," +
        "\"properties\":{\"nombre\":{\"type\":\"string\"},\"valor\":{\"type\":\"string\",\"nullable\":true}," +
        "\"confianza\":{\"type\":\"number\"}},\"required\":[\"nombre\",\"valor\",\"confianza\"]}}},\"required\":[\"campos\"]}";

    private static List<ExtractedField> ParseCampos(string json)
    {
        var list = new List<ExtractedField>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("campos", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            var nombre = el.TryGetProperty("nombre", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            string? valor = el.TryGetProperty("valor", out var v) && v.ValueKind != JsonValueKind.Null
                ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                : null;
            double? conf = el.TryGetProperty("confianza", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetDouble() : null;
            list.Add(new ExtractedField(nombre!, valor, conf));
        }
        return list;
    }

    private async Task LogAsync(OcrProvider provider, string model, string? archivo, string? mime, long size, string modulo,
        bool ok, string? error, int latencyMs, int inTok, int outTok, List<ExtractedField> campos, string? raw, CancellationToken ct)
    {
        try
        {
            var log = new DocumentExtractionLog
            {
                TenantId = _tenant.CurrentTenantId,
                Modulo = modulo,
                Provider = provider.ToString(),
                Model = model,
                NombreArchivo = archivo,
                MimeType = mime,
                SizeBytes = size,
                Ok = ok,
                Error = error,
                LatencyMs = latencyMs,
                InputTokens = inTok,
                OutputTokens = outTok,
                CamposJson = campos.Count > 0 ? JsonSerializer.Serialize(campos) : null,
                RawResponse = raw is null ? null : (raw.Length > RawMaxChars ? raw[..RawMaxChars] : raw),
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.DocumentExtractionLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }
        catch { /* el log no debe romper la extraccion */ }
    }
}
