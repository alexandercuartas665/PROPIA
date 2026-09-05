namespace Propia.Application.Ocr;

/// <summary>Un campo objetivo a extraer de un documento (define el schema que se le pide a la IA).</summary>
/// <param name="Nombre">Clave del campo (ej. "numero_poliza"). Es la que se devuelve en el resultado.</param>
/// <param name="Descripcion">Que es el campo, para guiar a la IA (ej. "Numero de la poliza tal cual figura").</param>
/// <param name="Tipo">Sugerencia de formato: "texto" | "numero" | "moneda" | "fecha" (yyyy-MM-dd).</param>
public sealed record CampoObjetivo(string Nombre, string Descripcion, string Tipo = "texto");

/// <summary>
/// Arnes de extraccion de documentos con IA: manda el PDF/imagen NATIVO al proveedor configurado en
/// Super Admin (si es un motor de IA, p.ej. Gemini) y pide salida estructurada segun los campos objetivo.
/// Cada corrida se registra en el log de extraccion (para afinar prompt/schema). No auto-guarda nada:
/// el consumidor (p.ej. Seguros) usa el resultado para PRE-LLENAR un formulario que el humano confirma.
/// </summary>
public interface IAiDocumentExtractor
{
    /// <summary>True si el motor configurado en Super Admin es de IA (soporta este arnes).</summary>
    Task<bool> DisponibleAsync(CancellationToken ct = default);

    /// <summary>
    /// Extrae los <paramref name="campos"/> del documento. Si la lista viene vacia, hace extraccion
    /// abierta (todos los campos clave que encuentre). <paramref name="modulo"/> es solo para el log.
    /// </summary>
    Task<DocumentExtractionResult> ExtraerAsync(
        byte[] documento, string mimeType, string? nombreArchivo,
        IReadOnlyList<CampoObjetivo> campos, string modulo, CancellationToken ct = default);
}
