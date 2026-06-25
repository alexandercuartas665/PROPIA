using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Ocr;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Ocr;

/// <summary>
/// Extraccion de texto via Azure AI Vision (Computer Vision 4.0 Image Analysis, feature "read").
/// Portado de la logica del core legacy (modulo Gastos / ComputerVision.vb): una sola llamada
/// sincrona POST :analyze que devuelve readResult.content (texto crudo del documento).
/// A diferencia de Document Intelligence NO devuelve campos estructurados, solo el texto completo.
/// Solo procesa imagenes (JPG/PNG/BMP); el PDF no lo soporta este endpoint.
/// Lee la config maestra (OcrProviderConfig, Provider=AzureComputerVision) de Super Admin.
/// </summary>
public sealed class AzureComputerVisionExtractionService : IDocumentExtractionService
{
    private const string ApiVersion = "2024-02-01"; // Image Analysis 4.0 GA (el 2023-02-01-preview fue deprecado)

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly IHttpClientFactory _httpFactory;

    public AzureComputerVisionExtractionService(PropiaDbContext db, ISecretProtector secretProtector, IHttpClientFactory httpFactory)
    {
        _db = db;
        _secretProtector = secretProtector;
        _httpFactory = httpFactory;
    }

    public async Task<DocumentExtractionResult> ExtraerAsync(Stream contenido, string contentType, string? modeloOverride, CancellationToken ct = default)
    {
        var cfg = await _db.OcrProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Provider == OcrProvider.AzureComputerVision, ct);
        if (cfg is null || !cfg.IsEnabled || string.IsNullOrWhiteSpace(cfg.Endpoint) || cfg.ApiKeyEncrypted is null)
            return DocumentExtractionResult.Falla("OCR (Computer Vision) no configurado o deshabilitado en Super Admin.", "azure-vision");

        string apiKey;
        try { apiKey = _secretProtector.Unprotect(cfg.ApiKeyEncrypted); }
        catch { return DocumentExtractionResult.Falla("Credencial de OCR invalida; re-ingresala en Super Admin.", "azure-vision"); }

        // "Modelo" para Computer Vision = features de Image Analysis (default "read").
        var features = !string.IsNullOrWhiteSpace(modeloOverride) ? modeloOverride!.Trim()
            : (string.IsNullOrWhiteSpace(cfg.ModelId) ? "read" : cfg.ModelId!.Trim());

        using var ms = new MemoryStream();
        await contenido.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(60);
        var baseUrl = cfg.Endpoint!.TrimEnd('/');
        var url = $"{baseUrl}/computervision/imageanalysis:analyze?api-version={ApiVersion}&features={features}&language=es";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            req.Content = new ByteArrayContent(bytes);
            req.Content.Headers.TryAddWithoutValidation("Content-Type",
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            var resp = await http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return DocumentExtractionResult.Falla($"Azure Vision respondio {(int)resp.StatusCode}: {Trunc(json)}", "azure-vision");

            using var doc = JsonDocument.Parse(json);
            var texto = ExtraerContenido(doc.RootElement);
            if (string.IsNullOrWhiteSpace(texto))
                return DocumentExtractionResult.Falla("Azure Vision no devolvio texto (readResult.content vacio).", "azure-vision");

            return new DocumentExtractionResult(true, null, "azure-vision", features, Array.Empty<ExtractedField>(), texto);
        }
        catch (Exception ex)
        {
            return DocumentExtractionResult.Falla($"Error llamando a Azure Vision OCR: {ex.Message}", "azure-vision");
        }
    }

    private static string? ExtraerContenido(JsonElement root)
    {
        // Computer Vision 4.0: { "readResult": { "content": "...", "blocks": [...] } }
        if (root.TryGetProperty("readResult", out var rr) &&
            rr.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            return content.GetString();

        // Fallback defensivo: arma el texto desde los bloques/lineas si no vino "content".
        if (root.TryGetProperty("readResult", out var rr2) &&
            rr2.TryGetProperty("blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var b in blocks.EnumerateArray())
                if (b.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
                    foreach (var l in lines.EnumerateArray())
                        if (l.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String)
                            sb.AppendLine(txt.GetString());
            return sb.Length > 0 ? sb.ToString() : null;
        }
        return null;
    }

    private static string Trunc(string s) => s.Length <= 300 ? s : s[..300];
}
