using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Log de cada corrida de extraccion de documentos con IA (arnes IAiDocumentExtractor). GLOBAL (sin
/// RLS): lo consulta Super Admin para AFINAR prompt/schema. Guarda entrada minima + la respuesta
/// cruda del modelo y los campos parseados con su confianza. NO guarda el documento en si.
/// </summary>
public class DocumentExtractionLog : BaseEntity
{
    /// <summary>Tenant que origino la extraccion (referencia; la tabla es global, sin policy RLS).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Modulo consumidor (ej. "seguros", "contratos", "directorio").</summary>
    public string Modulo { get; set; } = string.Empty;

    /// <summary>Proveedor usado (ej. "GeminiDocument").</summary>
    public string Provider { get; set; } = string.Empty;
    public string? Model { get; set; }

    public string? NombreArchivo { get; set; }
    public string? MimeType { get; set; }
    public long SizeBytes { get; set; }

    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int LatencyMs { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>Campos parseados con confianza, en JSON (para revision).</summary>
    public string? CamposJson { get; set; }

    /// <summary>Respuesta cruda del modelo (truncada). Para afinar el prompt/schema.</summary>
    public string? RawResponse { get; set; }
}
