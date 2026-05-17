using Propia.Domain.Enums;

namespace Propia.Application.Monitoria;

// ===========================================================================
// Logs
// ===========================================================================

public record RegistrarLogRequest(
    TipoEventoSistema TipoEvento,
    string Mensaje,
    SeveridadIncidente Severidad = SeveridadIncidente.Info,
    Guid? TenantId = null,
    Guid? ActorUsuarioId = null,
    string? ModuloOrigenCodigo = null,
    string? DetalleJson = null,
    string? Ip = null,
    string? UserAgent = null);

public record SistemaLogDto(
    Guid Id,
    TipoEventoSistema TipoEvento,
    SeveridadIncidente Severidad,
    Guid? TenantId,
    Guid? ActorUsuarioId,
    string Mensaje,
    string? ModuloOrigenCodigo,
    string? DetalleJson,
    string? Ip,
    DateTimeOffset CreadoAt);

public record FiltroLogsRequest(
    SeveridadIncidente? Severidad = null,
    TipoEventoSistema? TipoEvento = null,
    Guid? TenantId = null,
    string? ModuloOrigenCodigo = null,
    DateTimeOffset? DesdeUtc = null,
    DateTimeOffset? HastaUtc = null,
    int Limite = 200);

// ===========================================================================
// Incidentes
// ===========================================================================

public record AbrirIncidenteRequest(
    SeveridadIncidente Severidad,
    string Titulo,
    string? Descripcion = null,
    string? ServicioAfectado = null,
    Guid? TenantImpactadoId = null);

public record CambiarEstadoIncidenteRequest(
    EstadoIncidente NuevoEstado,
    string? Nota = null);

public record ResolverIncidenteRequest(
    string CausaRaiz,
    string SolucionAplicada);

public record SistemaIncidenteDto(
    Guid Id,
    SeveridadIncidente Severidad,
    EstadoIncidente Estado,
    string Titulo,
    string? Descripcion,
    string? ServicioAfectado,
    Guid? TenantImpactadoId,
    Guid? AsignadoSuperAdminId,
    DateTimeOffset DetectadoAt,
    DateTimeOffset? ResueltoAt,
    string? CausaRaiz,
    string? SolucionAplicada,
    DateTimeOffset CreadoAt);

// ===========================================================================
// Metricas
// ===========================================================================

public record MetricaUsoDiariaDto(
    DateOnly Fecha,
    int TotalTenants,
    int TenantsActivos,
    int TotalOrganizaciones,
    int TotalUsuarios,
    int TotalSuperAdmins,
    int TareasCreadas24h,
    int PqrsdsRadicadas24h,
    int ComunicadosEnviados24h,
    int NotificacionesDespachadas24h,
    int IncidentesAbiertos,
    int IncidentesCriticos);

// ===========================================================================
// Resumen ejecutivo
// ===========================================================================

public record ResumenMonitoriaDto(
    int LogsUlt24h,
    int LogsErrorUlt24h,
    int IncidentesAbiertos,
    int IncidentesCriticosAbiertos,
    DateTimeOffset? UltimaMetricaAt);
