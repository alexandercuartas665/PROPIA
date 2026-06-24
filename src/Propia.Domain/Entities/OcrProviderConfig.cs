using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Cuenta maestra de un proveedor de OCR / extraccion de documentos (Super Admin, Capa 0). GLOBAL:
/// un registro por proveedor. La API key se guarda cifrada (ISecretProtector) y nunca se expone
/// completa ni se loggea. La consumen los modulos que leen documentos: recibos de servicios
/// publicos (2.17), RUT/Camara de Comercio (Directorio 2.4) y contratos (2.6).
/// </summary>
public class OcrProviderConfig : BaseEntity
{
    public OcrProvider Provider { get; set; }

    /// <summary>Endpoint del recurso (ej. https://miorg-di.cognitiveservices.azure.com).</summary>
    public string? Endpoint { get; set; }

    /// <summary>API key del proveedor cifrada en reposo.</summary>
    public string? ApiKeyEncrypted { get; set; }

    /// <summary>Modelo a usar (Azure: prebuilt-invoice, prebuilt-receipt, prebuilt-document...).</summary>
    public string? ModelId { get; set; }

    /// <summary>Si esta habilitado para uso de la plataforma.</summary>
    public bool IsEnabled { get; set; }
}
