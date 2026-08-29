using Propia.Domain.Enums;

namespace Propia.Application.DashboardCopropiedad;

public record AlertaDashboardDto(
    Guid Id, TipoAlertaDashboard Tipo, SeveridadAlerta Severidad,
    string Titulo, string Descripcion, string? UrlAccion, DateTimeOffset CreatedAt);

public record ActividadFeedDto(
    Guid Id, TipoEventoActividad Tipo, string? ActorNombre, string Descripcion,
    string? ModuloCodigo, string? UrlItem, DateTimeOffset OcurridoAt);

public record TareaResumenDto(Guid Id, string NumeroTarea, string Titulo, string Estado, string Prioridad, DateOnly? FechaVencimiento, bool Vencida);

/// <summary>Contrato/poliza proximo a vencer para el widget del Dashboard (Ola 3).</summary>
public record ContratoPorVencerDto(Guid Id, string Nombre, DateOnly? FechaFin, int? DiasParaVencer, SemaforoContrato Semaforo);

/// <summary>Distribucion de tareas activas por etapa (grafica de barras del Dashboard).</summary>
public record TareasPorEtapaDto(string Nombre, string? Color, int Cantidad);

/// <summary>PQR en gestion para el widget del Dashboard. Semaforo: "verde" | "amarillo" | "rojo".</summary>
public record PqrDashboardDto(Guid Id, string NumeroRadicado, string Tipo, string Descripcion, DateOnly FechaVencimiento, int DiasHastaVencimiento, string Semaforo);

/// <summary>Novedad de porteria reciente para el widget del Dashboard.</summary>
public record NovedadPorteriaDashboardDto(Guid Id, string Tipo, string Descripcion, string? GuardaNombre, DateTimeOffset OcurridoAt, bool GeneroTarea);

/// <summary>Punto de la serie mensual de actividad (grafica de lineas del Dashboard).</summary>
public record SerieMensualDto(int Anio, int Mes, int Tareas, int Pqrs, int Novedades);

/// <summary>Distribucion de PQRSD por tipo (dona con porcentajes del Dashboard).</summary>
public record PqrsPorTipoDto(string Tipo, int Cantidad);

/// <summary>Actividad combinada de un dia (heatmap "dias mas activos" del Dashboard).</summary>
public record ActividadDiaDto(DateOnly Dia, int Cantidad);

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
    bool ModuloPresupuestoConfigurado,
    // Contratos proximos a vencer (Ola 3). Default para no romper construcciones existentes.
    IReadOnlyList<ContratoPorVencerDto>? ContratosPorVencer = null,
    // Polizas proximas a vencer (Ola 4c). Reusa el mismo DTO (Nombre = aseguradora).
    IReadOnlyList<ContratoPorVencerDto>? PolizasPorVencer = null,
    // ----- Dashboard v2 (rediseno): tareas por etapa + PQRSD + novedades de porteria -----
    IReadOnlyList<TareasPorEtapaDto>? TareasPorEtapa = null,
    int PqrsAbiertas = 0,
    int PqrsPorVencer = 0,
    int PqrsVencidas = 0,
    IReadOnlyList<PqrDashboardDto>? PqrsProximas = null,
    int NovedadesHoy = 0,
    IReadOnlyList<NovedadPorteriaDashboardDto>? NovedadesPorteria = null,
    // ----- Dashboard v3: graficas (linea mensual, dona por tipo, heatmap diario) -----
    IReadOnlyList<SerieMensualDto>? SerieMensual = null,
    IReadOnlyList<PqrsPorTipoDto>? PqrsPorTipo = null,
    IReadOnlyList<ActividadDiaDto>? ActividadDiaria = null);

public record CrearAlertaRequest(TipoAlertaDashboard Tipo, SeveridadAlerta Severidad, string Titulo, string Descripcion, string? UrlAccion);
public record CrearEventoFeedRequest(TipoEventoActividad Tipo, string Descripcion, string? ModuloCodigo, string? UrlItem);
