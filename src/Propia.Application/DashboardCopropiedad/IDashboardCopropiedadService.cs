namespace Propia.Application.DashboardCopropiedad;

/// <summary>Modulo 2.2 Dashboard de la Copropiedad - spec v1.0 MVP.</summary>
public interface IDashboardCopropiedadService
{
    /// <summary>Vista agregada del dashboard de la PH activa para el rol del usuario.</summary>
    Task<DashboardResumenDto> GetResumenAsync(CancellationToken ct);

    Task<IReadOnlyList<AlertaDashboardDto>> ListarAlertasAsync(CancellationToken ct);
    Task<AlertaDashboardDto> CrearAlertaAsync(CrearAlertaRequest req, CancellationToken ct);
    Task<bool> ResolverAlertaAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ActividadFeedDto>> ListarFeedAsync(int limit, CancellationToken ct);
    Task<ActividadFeedDto> RegistrarEventoFeedAsync(CrearEventoFeedRequest req, CancellationToken ct);
}
