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

        return cfg.Provider switch
        {
            OcrProvider.AzureComputerVision => await _computerVision.ExtraerAsync(contenido, contentType, modeloOverride, ct),
            _ => await _documentIntelligence.ExtraerAsync(contenido, contentType, modeloOverride, ct)
        };
    }
}
