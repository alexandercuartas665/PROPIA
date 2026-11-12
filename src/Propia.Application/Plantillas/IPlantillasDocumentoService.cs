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

    // ---- Campos propios (columna "+" de la vista tabla) ----
    Task<IReadOnlyList<PlantillaCampoDefinicionDto>> ListarCamposAsync(CancellationToken ct);
    /// <summary>Crea el campo; si ya existe uno con el mismo nombre lo devuelve. InvalidOperationException si el nombre esta vacio.</summary>
    Task<PlantillaCampoDefinicionDto> CrearCampoAsync(Propia.Application.MiCopropiedad.CrearCampoDefinicionRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid definicionId, Propia.Application.MiCopropiedad.ActualizarCampoDefinicionRequest req, CancellationToken ct);
    /// <summary>Borra la definicion y sus valores (cascada).</summary>
    Task<bool> EliminarCampoAsync(Guid definicionId, CancellationToken ct);
    Task<IReadOnlyList<PlantillaCampoValorFlatDto>> ListarValoresAsync(CancellationToken ct);
    /// <summary>Fija (o limpia con null/vacio) el valor. false si la plantilla o el campo no son del tenant.</summary>
    Task<bool> SetValorAsync(Guid plantillaId, Guid definicionId, string? valor, CancellationToken ct);
}
