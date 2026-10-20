namespace Propia.Application.MiCopropiedad;

/// <summary>
/// CRUD de las "Vistas del sistema" (explorador de datos de Mi Copropiedad). Store dataset-agnostico:
/// guarda/lee la DEFINICION JSON de cada vista, compartida por copropiedad. No calcula datos (eso va en el
/// frontend, en vivo, sobre los endpoints de cada modulo).
/// </summary>
public interface IVistaSistemaService
{
    Task<IReadOnlyList<VistaSistemaDto>> ListarAsync(CancellationToken ct);
    Task<VistaSistemaDto> CrearAsync(GuardarVistaRequest req, CancellationToken ct);
    Task<bool> ActualizarAsync(Guid id, GuardarVistaRequest req, CancellationToken ct);
    Task<bool> EliminarAsync(Guid id, CancellationToken ct);
    Task ReordenarAsync(IReadOnlyList<Guid> ids, CancellationToken ct);

    /// <summary>Siembra las vistas de ejemplo. reemplazar=false solo inserta si la copropiedad no tiene
    /// vistas (primer arranque, atomico); reemplazar=true borra las actuales y crea las semillas
    /// ("Restablecer ejemplos"). Devuelve la lista final ordenada.</summary>
    Task<IReadOnlyList<VistaSistemaDto>> SembrarAsync(IReadOnlyList<GuardarVistaRequest> semillas, bool reemplazar, CancellationToken ct);
}
