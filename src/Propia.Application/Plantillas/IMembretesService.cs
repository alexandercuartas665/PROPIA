namespace Propia.Application.Plantillas;

/// <summary>
/// Encabezados y pies de pagina (membretes) de los documentos de la copropiedad activa. La copropiedad nace
/// con "Institucional" (encabezado y pie, predeterminados) la primera vez que se consultan.
/// </summary>
public interface IMembretesService
{
    /// <summary>Biblioteca (encabezados y pies) con sus usos + logo y ejemplo de {documento.datos} para la Web.</summary>
    Task<MembretesDto> ListarAsync(CancellationToken ct);
    /// <summary>Crea uno. InvalidOperationException si falta el tipo o el nombre.</summary>
    Task<MembreteDto> CrearAsync(GuardarMembreteRequest req, CancellationToken ct);
    /// <summary>PUT MERGE. null si no existe.</summary>
    Task<MembreteDto?> ActualizarAsync(Guid id, GuardarMembreteRequest req, CancellationToken ct);
    /// <summary>Elimina uno que no sea el predeterminado; las plantillas que lo tenian fijo pasan a Predeterminado.
    /// false si no existe; InvalidOperationException si es el predeterminado.</summary>
    Task<bool> EliminarAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Encabezado y pie YA RESUELTOS con los datos de la copropiedad (y los de <paramref name="datos"/>) para
    /// componer un documento: los de la plantilla si se indica, si no los predeterminados. {documento.datos} queda
    /// literal: lo pone el armador del documento (tipo, radicado y fecha). null = sin encabezado / sin pie.
    /// </summary>
    Task<(string? Encabezado, string? Pie)> ParaDocumentoAsync(Guid? plantillaId, IReadOnlyDictionary<string, string>? datos, CancellationToken ct);
}
