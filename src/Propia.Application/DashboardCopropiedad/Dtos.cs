using Propia.Domain.Enums;

namespace Propia.Application.DashboardCopropiedad;

public record AlertaDashboardDto(
    Guid Id, TipoAlertaDashboard Tipo, SeveridadAlerta Severidad,
    string Titulo, string Descripcion, string? UrlAccion, DateTimeOffset CreatedAt);

public record ActividadFeedDto(
    Guid Id, TipoEventoActividad Tipo, string? ActorNombre, string Descripcion,
    string? ModuloCodigo, string? UrlItem, DateTimeOffset OcurridoAt);

public record TareaResumenDto(Guid Id, string NumeroTarea, string Titulo, string Estado, string Prioridad, DateOnly? FechaVencimiento, bool Vencida);

public record DashboardResumenDto(
    // Banda alertas criticas
    IReadOnlyList<AlertaDashboardDto> Alertas,
    // Bloque financiero
    decimal? RecaudoMesPorcentaje,
    int UnidadesEnMora,
    decimal? PresupuestoEjecutadoPorcentaje,
    // Bloque operativo
    int TareasTotalActivas,
    int TareasVencidas,
    IReadOnlyList<TareaResumenDto> TareasUrgentes,
    // Resumen copropiedad
    int TotalUnidades,
    int TorresTotal,
    int ZonasComunesTotal,
    // Feed
    IReadOnlyList<ActividadFeedDto> Feed,
    // Acciones rapidas - flags de modulos activos
    bool ModuloPresupuestoConfigurado);

public record CrearAlertaRequest(TipoAlertaDashboard Tipo, SeveridadAlerta Severidad, string Titulo, string Descripcion, string? UrlAccion);
public record CrearEventoFeedRequest(TipoEventoActividad Tipo, string Descripcion, string? ModuloCodigo, string? UrlItem);
