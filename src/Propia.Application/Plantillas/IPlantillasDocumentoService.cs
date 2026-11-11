namespace Propia.Application.Plantillas;

/// <summary>Catalogo central de plantillas de documentos de la copropiedad activa (tenant del request).</summary>
public interface IPlantillasDocumentoService
{
    /// <summary>Plantillas del tenant (opcionalmente de una categoria). La primera vez siembra las de PQRSD.</summary>
    Task<IReadOnlyList<PlantillaDocumentoDto>> ListarAsync(string? categoria, CancellationToken ct);
    Task<PlantillaDocumentoDto?> ObtenerAsync(Guid id, CancellationToken ct);
    /// <summary>Crea la plantilla. InvalidOperationException si el nombre esta vacio o la categoria no existe.</summary>
    Task<PlantillaDocumentoDto> CrearAsync(GuardarPlantillaDocumentoRequest req, CancellationToken ct);
    /// <summary>PUT MERGE. null si no existe; InvalidOperationException si deja el nombre vacio o la categoria no existe.</summary>
    Task<PlantillaDocumentoDto?> ActualizarAsync(Guid id, GuardarPlantillaDocumentoRequest req, CancellationToken ct);
    Task<bool> EliminarAsync(Guid id, CancellationToken ct);
}
