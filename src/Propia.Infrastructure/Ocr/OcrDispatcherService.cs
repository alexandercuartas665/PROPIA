using System.Text;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Ocr;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Ocr;

/// <summary>
/// Enruta la extraccion de documentos al proveedor OCR habilitado en Super Admin (config maestra).
/// Solo un proveedor esta activo a la vez (al habilitar uno se deshabilitan los demas). Si ninguno
/// esta habilitado, devuelve Ok=false. Es el IDocumentExtractionService que consume OcrController.
/// </summary>
public sealed class OcrDispatcherService : IDocumentExtractionService
{
    private readonly PropiaDbContext _db;
    private readonly AzureDocumentExtractionService _documentIntelligence;
    private readonly AzureComputerVisionExtractionService _computerVision;

    public OcrDispatcherService(
        PropiaDbContext db,
        AzureDocumentExtractionService documentIntelligence,
        AzureComputerVisionExtractionService computerVision)
    {
        _db = db;
        _documentIntelligence = documentIntelligence;
        _computerVision = computerVision;
    }

    public async Task<DocumentExtractionResult> ExtraerAsync(Stream contenido, string contentType, string? modeloOverride, CancellationToken ct = default)
    {
        var cfg = await _db.OcrProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (cfg is null)
            return DocumentExtractionResult.Falla("OCR no configurado o deshabilitado en Super Admin.");

        // Materializamos el contenido una vez: lo necesitamos para detectar PDF y, si aplica, rasterizarlo.
        using var ms = new MemoryStream();
        await contenido.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        // Computer Vision (Image Analysis) NO procesa PDF -> si llega un PDF, lo rasterizamos a imagenes
        // y hacemos OCR por pagina, concatenando el texto. Document Intelligence si maneja PDF nativo.
        if (cfg.Provider == OcrProvider.AzureComputerVision && PdfRasterizer.EsPdf(contentType, bytes))
            return await ExtraerPdfConComputerVisionAsync(bytes, modeloOverride, ct);

        return cfg.Provider switch
        {
            OcrProvider.AzureComputerVision => await _computerVision.ExtraerAsync(new MemoryStream(bytes), contentType, modeloOverride, ct),
            _ => await _documentIntelligence.ExtraerAsync(new MemoryStream(bytes), contentType, modeloOverride, ct)
        };
    }

    private async Task<DocumentExtractionResult> ExtraerPdfConComputerVisionAsync(byte[] pdf, string? modelo, CancellationToken ct)
    {
        List<byte[]> paginas;
        try { paginas = PdfRasterizer.RasterizarPaginas(pdf); }
        catch (Exception ex) { return DocumentExtractionResult.Falla($"No se pudo convertir el PDF a imagen: {ex.Message}", "azure-vision"); }
        if (paginas.Count == 0)
            return DocumentExtractionResult.Falla("El PDF no tiene paginas legibles.", "azure-vision");

        var sb = new StringBuilder();
        string? modeloUsado = null;
        for (var i = 0; i < paginas.Count; i++)
        {
            var r = await _computerVision.ExtraerAsync(new MemoryStream(paginas[i]), "image/png", modelo, ct);
            if (r.Ok && !string.IsNullOrWhiteSpace(r.TextoCompleto))
            {
                if (sb.Length > 0) sb.AppendLine();
                if (paginas.Count > 1) sb.AppendLine($"--- Pagina {i + 1} ---");
                sb.AppendLine(r.TextoCompleto);
                modeloUsado = r.Modelo;
            }
        }
        var texto = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return DocumentExtractionResult.Falla("El OCR no extrajo texto del PDF (paginas en blanco o escaneo ilegible).", "azure-vision");

        return new DocumentExtractionResult(true, null, "azure-vision (pdf)", modeloUsado, Array.Empty<ExtractedField>(), texto);
    }
}
