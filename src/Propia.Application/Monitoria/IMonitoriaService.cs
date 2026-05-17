using Propia.Domain.Enums;

namespace Propia.Application.Monitoria;

/// <summary>
/// Modulo 0.3 Monitoria y Auditoria Global - servicio de aplicacion (MVP).
///
/// Accesible solo para SuperAdmin via /api/admin/monitoria/*. Maneja:
///  - Log estructurado del sistema (append-only).
///  - Incidentes operacionales (estado, asignacion, resolucion).
///  - Snapshot diario de metricas globales para dashboards de salud.
///
/// Diferido a Fase 2:
///  - Integracion con APM externo (Datadog/NewRelic/Grafana Loki).
///  - Alertas push a operadores cuando se abre un incidente Critico.
///  - Dashboards graficos historicos (consume MetricaUsoDiaria).
///  - Anomalia detection con T.1 sobre patrones del log.
/// </summary>
public interface IMonitoriaService
{
    // Logs (append-only)
    Task<Guid> RegistrarLogAsync(RegistrarLogRequest req, CancellationToken ct);
    Task<IReadOnlyList<SistemaLogDto>> ListarLogsAsync(FiltroLogsRequest filtro, CancellationToken ct);

    // Incidentes
    Task<SistemaIncidenteDto> AbrirIncidenteAsync(AbrirIncidenteRequest req, CancellationToken ct);
    Task<IReadOnlyList<SistemaIncidenteDto>> ListarIncidentesAsync(EstadoIncidente? estado, CancellationToken ct);
    Task<SistemaIncidenteDto?> GetIncidenteAsync(Guid id, CancellationToken ct);
    Task<bool> AsignarIncidenteAsync(Guid id, Guid superAdminId, CancellationToken ct);
    Task<bool> CambiarEstadoIncidenteAsync(Guid id, CambiarEstadoIncidenteRequest req, CancellationToken ct);
    Task<bool> ResolverIncidenteAsync(Guid id, ResolverIncidenteRequest req, CancellationToken ct);

    // Metricas
    Task<MetricaUsoDiariaDto> CalcularYGuardarMetricasHoyAsync(CancellationToken ct);
    Task<IReadOnlyList<MetricaUsoDiariaDto>> ListarMetricasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<MetricaUsoDiariaDto?> GetMetricaMasRecienteAsync(CancellationToken ct);

    // Resumen
    Task<ResumenMonitoriaDto> GetResumenAsync(CancellationToken ct);
}
