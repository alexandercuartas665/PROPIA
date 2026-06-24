using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Ocr;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Ocr;

/// <summary>
/// Extraccion de documentos via Azure AI Document Intelligence (Form Recognizer), api 2024-11-30.
/// Lee la config maestra (OcrProviderConfig) de Super Admin; si no hay proveedor habilitado
/// devuelve Ok=false. Flujo REST: POST :analyze -> 202 + Operation-Location -> poll hasta succeeded.
/// </summary>
public sealed class AzureDocumentExtractionService : IDocumentExtractionService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly IHttpClientFactory _httpFactory;

    public AzureDocumentExtractionService(PropiaDbContext db, ISecretProtector secretProtector, IHttpClientFactory httpFactory)
    {
        _db = db;
        _secretProtector = secretProtector;
        _httpFactory = httpFactory;
    }

    public async Task<DocumentExtractionResult> ExtraerAsync(Stream contenido, string contentType, string? modeloOverride, CancellationToken ct = default)
    {
        var cfg = await _db.OcrProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Provider == OcrProvider.AzureDocumentIntelligence, ct);
        if (cfg is null || !cfg.IsEnabled || string.IsNullOrWhiteSpace(cfg.Endpoint) || cfg.ApiKeyEncrypted is null)
            return DocumentExtractionResult.Falla("OCR no configurado o deshabilitado en Super Admin.", "azure");

        string apiKey;
        try { apiKey = _secretProtector.Unprotect(cfg.ApiKeyEncrypted); }
        catch { return DocumentExtractionResult.Falla("Credencial de OCR invalida; re-ingresala en Super Admin.", "azure"); }

        var modelo = !string.IsNullOrWhiteSpace(modeloOverride) ? modeloOverride!.Trim()
            : (string.IsNullOrWhiteSpace(cfg.ModelId) ? "prebuilt-invoice" : cfg.ModelId!.Trim());

        using var ms = new MemoryStream();
        await contenido.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(60);
        var baseUrl = cfg.Endpoint!.TrimEnd('/');
        var analyzeUrl = $"{baseUrl}/documentintelligence/documentModels/{modelo}:analyze?api-version=2024-11-30";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, analyzeUrl);
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            req.Content = new ByteArrayContent(bytes);
            req.Content.Headers.TryAddWithoutValidation("Content-Type",
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                return DocumentExtractionResult.Falla($"Azure respondio {(int)resp.StatusCode}: {Trunc(body)}", "azure");
            }
            if (!resp.Headers.TryGetValues("Operation-Location", out var locs))
                return DocumentExtractionResult.Falla("Azure no devolvio Operation-Location.", "azure");
            var pollUrl = locs.First();

            // Poll hasta succeeded/failed (max ~40s).
            for (int i = 0; i < 27; i++)
            {
                await Task.Delay(1500, ct);
                using var pollReq = new HttpRequestMessage(HttpMethod.Get, pollUrl);
                pollReq.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
                var pollResp = await http.SendAsync(pollReq, ct);
                var json = await pollResp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var status = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
                if (status == "succeeded")
                    return Parse(doc.RootElement, modelo);
                if (status == "failed")
                    return DocumentExtractionResult.Falla("Azure no pudo analizar el documento.", "azure");
            }
            return DocumentExtractionResult.Falla("Tiempo de espera agotado analizando el documento.", "azure");
        }
        catch (Exception ex)
        {
            return DocumentExtractionResult.Falla($"Error llamando a Azure OCR: {ex.Message}", "azure");
        }
    }

    private static DocumentExtractionResult Parse(JsonElement root, string modelo)
    {
        var campos = new List<ExtractedField>();
        string? texto = null;
        if (root.TryGetProperty("analyzeResult", out var ar))
        {
            if (ar.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                texto = content.GetString();

            if (ar.TryGetProperty("documents", out var docs) && docs.ValueKind == JsonValueKind.Array && docs.GetArrayLength() > 0)
            {
                var first = docs[0];
                if (first.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
                {
                    foreach (var f in fields.EnumerateObject())
                    {
                        var (valor, conf) = ValorDeCampo(f.Value);
                        if (valor is not null) campos.Add(new ExtractedField(f.Name, valor, conf));
                    }
                }
            }
        }
        return new DocumentExtractionResult(true, null, "azure", modelo, campos, texto);
    }

    private static (string? valor, double? conf) ValorDeCampo(JsonElement field)
    {
        double? conf = field.TryGetProperty("confidence", out var cf) && cf.TryGetDouble(out var c) ? c : null;
        // Preferimos un valor tipado; si no, el "content" textual.
        if (field.TryGetProperty("valueCurrency", out var cur) && cur.TryGetProperty("amount", out var amt) && amt.TryGetDouble(out var a))
            return (a.ToString(System.Globalization.CultureInfo.InvariantCulture), conf);
        if (field.TryGetProperty("valueNumber", out var num) && num.TryGetDouble(out var n))
            return (n.ToString(System.Globalization.CultureInfo.InvariantCulture), conf);
        if (field.TryGetProperty("valueDate", out var vd) && vd.ValueKind == JsonValueKind.String)
            return (vd.GetString(), conf);
        if (field.TryGetProperty("valueString", out var vs) && vs.ValueKind == JsonValueKind.String)
            return (vs.GetString(), conf);
        if (field.TryGetProperty("content", out var ct2) && ct2.ValueKind == JsonValueKind.String)
            return (ct2.GetString(), conf);
        return (null, conf);
    }

    private static string Trunc(string s) => s.Length <= 300 ? s : s[..300];
}
