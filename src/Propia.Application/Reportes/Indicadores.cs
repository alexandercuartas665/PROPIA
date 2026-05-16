namespace Propia.Application.Reportes;

/// <summary>
/// Contrato de la capa de indicadores cross-modulo. Spec 2.16 seccion 16 + RN-01:
/// 2.16 NO ejecuta queries directas sobre tablas operativas; consume agregados
/// expuestos aqui. Cada modulo productor implementa su seccion (en Fase 2/3 se
/// migraran a servicios separados; en MVP todos viven en un solo agregador).
/// </summary>
public interface IIndicadoresService
{
    // 2.6 Financiero
    Task<IndicadoresFinancieroDto> GetFinancieroAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // 2.7 Cartera
    Task<IndicadoresCarteraDto> GetCarteraAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // 2.9 PQRSD
    Task<IndicadoresPqrsdDto> GetPqrsdAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // 2.10 Tareas y Proyectos
    Task<IndicadoresOperativoDto> GetOperativoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // 2.11 Mantenimiento
    Task<IndicadoresMantenimientoDto> GetMantenimientoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // 2.14 Comunicaciones
    Task<IndicadoresComunicacionesDto> GetComunicacionesAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // 2.15 Documentos (uso interno - audit del repositorio)
    Task<IndicadoresDocumentosDto> GetDocumentosAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // KPI ejecutivos consolidados para el consejo (spec seccion 7)
    Task<KpisConsejoDto> GetKpisConsejoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // Resumen agregado no nominativo para el portal de transparencia (RN-04)
    Task<TransparenciaDto> GetTransparenciaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}

// ===========================================================================
// DTOs por modulo - publicos para que el motor de templates y la UI los usen.
// ===========================================================================

public record IndicadoresFinancieroDto(
    decimal RecaudoEsperado,
    decimal RecaudoRecibido,
    decimal? RecaudoPct,
    int CuotasVencidas,
    int PresupuestosActivos,
    IReadOnlyList<RubroEjecucionDto> EjecucionPorRubro,
    /// <summary>Ingresos por cuotas extraordinarias del periodo (4.2 Financiero).</summary>
    IReadOnlyList<CuotaExtraordinariaResumenDto> CuotasExtraordinarias);

public record RubroEjecucionDto(string Rubro, decimal Presupuestado, decimal Ejecutado, decimal? Porcentaje);
public record CuotaExtraordinariaResumenDto(string Concepto, decimal Meta, decimal Recaudado);

public record IndicadoresCarteraDto(
    decimal TotalMora,
    int UnidadesEnMora,
    int AcuerdosVigentes,
    int PazSalvosPeriodo,
    AgingDto Aging,
    IReadOnlyList<TopUnidadMoraDto> Top5Mora);

public record AgingDto(decimal D0a30, decimal D31a60, decimal D61a90, decimal Mas90);
public record TopUnidadMoraDto(string Unidad, decimal Saldo, int DiasMora);

public record IndicadoresPqrsdDto(
    int Total,
    int Resueltas,
    int Vencidas,
    int EnTramite,
    int Felicitaciones,
    decimal? TiempoPromedioRespuestaDias,
    IReadOnlyList<PqrsdPorTipoDto> PorTipo);

public record PqrsdPorTipoDto(string Tipo, int Total, int Resueltas, int Vencidas);

public record IndicadoresOperativoDto(
    int TotalTareas,
    int Completadas,
    int Vencidas,
    int EnProgreso,
    int ProyectosActivos,
    decimal? TiempoPromedioCierreDias,
    IReadOnlyList<CargaResponsableDto> CargaPorResponsable);

public record CargaResponsableDto(string Persona, int Activas, int Completadas);

public record IndicadoresMantenimientoDto(
    int Intervenciones,
    int Preventivos,
    int Correctivos,
    int ActivosVencidos,
    int ActivosOk,
    IReadOnlyList<MantenimientoPorActivoDto> PorActivo);

public record MantenimientoPorActivoDto(string Activo, int Intervenciones, decimal? MttrHoras);

public record IndicadoresComunicacionesDto(
    int Enviados,
    int Programados,
    int Cancelados,
    decimal? TasaAperturaPromedio,
    IReadOnlyList<ComunicadosPorTipoDto> PorTipo);

public record ComunicadosPorTipoDto(string Tipo, int Total);

public record IndicadoresDocumentosDto(
    int TotalDocumentos,
    int NuevosEnPeriodo,
    int NuevasVersionesEnPeriodo,
    int CompartidosConsejo,  // proxy: cuantos estan en visibilidad EQUIPO o PUBLICO
    long TamanoTotalBytes);

/// <summary>KPIs del consejo (4-6 cards segun spec seccion 7).</summary>
public record KpisConsejoDto(
    KpiDto Recaudo,
    KpiDto Mora,
    KpiDto PqrsdVencidas,
    KpiDto TareasActivas,
    KpiDto MantenimientoPendiente,
    KpiDto ComunicacionesEnviadas);

/// <summary>
/// Valor + semaforo de un indicador. Semaforo se calcula contra reporte_semaforo_config
/// del tenant (con defaults PropIA si no hay configuracion).
/// </summary>
public record KpiDto(
    string Key,
    string Etiqueta,
    decimal Valor,
    string Unidad,
    /// <summary>"verde" | "amarillo" | "rojo" | "neutro".</summary>
    string Semaforo,
    string? Contexto);

/// <summary>Indicadores no-nominativos para propietarios. RN-04.</summary>
public record TransparenciaDto(
    decimal? RecaudoPct,
    decimal? RecaudoMes,
    decimal? RecaudoMeta,
    decimal EjecucionPresupuestalPct,
    int MesPresupuestoActual,
    int PqrsdRadicadas,
    int PqrsdResueltas,
    int PqrsdEnTramite,
    int TareasCompletadas,
    int ProyectosActivos);
