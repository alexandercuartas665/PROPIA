namespace Propia.Application.Ocr;

/// <summary>Un campo extraido de un documento, con su nivel de confianza (0-1).</summary>
public sealed record ExtractedField(string Nombre, string? Valor, double? Confianza);

/// <summary>Resultado de extraer datos de un documento (recibo, factura, RUT, etc.).</summary>
public sealed record DocumentExtractionResult(
    bool Ok,
    string? Error,
    string Proveedor,
    string? Modelo,
    IReadOnlyList<ExtractedField> Campos,
    string? TextoCompleto)
{
    public static DocumentExtractionResult Falla(string error, string proveedor = "ninguno")
        => new(false, error, proveedor, null, Array.Empty<ExtractedField>(), null);

    /// <summary>Busca el primer campo cuyo nombre coincida (case-insensitive) con alguno de los alias.</summary>
    public string? Campo(params string[] alias)
        => Campos.FirstOrDefault(c => alias.Any(a => string.Equals(a, c.Nombre, StringComparison.OrdinalIgnoreCase)))?.Valor;
}

/// <summary>
/// Extrae datos estructurados de un documento usando el proveedor de OCR configurado en Super Admin
/// (config maestra). Si no hay proveedor habilitado, devuelve un resultado con Ok=false.
/// </summary>
public interface IDocumentExtractionService
{
    Task<DocumentExtractionResult> ExtraerAsync(Stream contenido, string contentType, string? modeloOverride, CancellationToken ct = default);
}
