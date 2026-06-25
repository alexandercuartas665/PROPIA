namespace Propia.Domain.Enums;

/// <summary>Proveedor de OCR / extraccion de documentos (config maestra Capa 0).</summary>
public enum OcrProvider
{
    /// <summary>Azure AI Document Intelligence (prebuilt-invoice/receipt): devuelve campos estructurados.</summary>
    AzureDocumentIntelligence = 1,

    /// <summary>Azure AI Vision (Computer Vision 4.0, feature "read"): devuelve el texto crudo del documento.</summary>
    AzureComputerVision = 2
}
