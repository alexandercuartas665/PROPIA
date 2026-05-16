namespace Propia.Application.ReportesConsolidados;

/// <summary>
/// Modulo 1.4 Reportes Consolidados - servicio de aplicacion (spec v1.0 MVP).
///
/// Alcance MVP:
///  - 5 plantillas base PropIA (SaludPortafolio, FinancieroConsolidado, OperativoConsolidado,
///    ConvivenciaPqrsd, DesempenoEquipo).
///  - Indicadores consolidados cross-tenant del portafolio de una organizacion
///    (RN-01: nunca consulta tablas operativas directo).
///  - CRUD de reportes guardados.
///  - Generacion sincrona devolviendo ResultadoJson estructurado.
///  - Historial regenerable con expiracion 30 dias para archivos.
///  - RN-04 + RN-06: detecta automaticamente si tiene datos nominativos
///    (DesempenoEquipo siempre los tiene).
///
/// Diferido a Fase 2:
///  - Constructor drag & drop completo (en MVP usar plantillas predefinidas).
///  - Link publico temporal con token JWT.
///  - Programaciones automaticas (consume T.2 no construido).
///  - Agente IA T.1 para generacion en lenguaje natural.
///  - Exportacion PDF/Excel real con QuestPDF/ClosedXML.
///  - Generacion asincrona con cola (RN-08).
///  - Nivel ASIGNADO con filtro automatico segun 1.3 (RN-03).
/// </summary>
public interface IReportesConsolidadosService
{
    // ----- Plantillas base + reportes guardados -----

    Task<IReadOnlyList<PlantillaBaseDto>> ListarPlantillasBaseAsync(CancellationToken ct);

    Task<IReadOnlyList<OrgReporteDto>> ListarReportesAsync(CancellationToken ct);
    Task<OrgReporteDto?> GetReporteAsync(Guid id, CancellationToken ct);
    Task<OrgReporteDto> CrearReporteAsync(CrearReporteRequest req, CancellationToken ct);
    Task<bool> ActualizarReporteAsync(Guid id, ActualizarReporteRequest req, CancellationToken ct);
    Task<bool> EliminarReporteAsync(Guid id, CancellationToken ct);

    // ----- Generacion + historial -----

    Task<GeneracionDetalleDto> GenerarAsync(GenerarReporteRequest req, CancellationToken ct);
    Task<IReadOnlyList<GeneracionListaDto>> ListarHistorialAsync(Guid? reporteId, CancellationToken ct);
    Task<GeneracionDetalleDto?> GetGeneracionAsync(Guid id, CancellationToken ct);
    Task<GeneracionDetalleDto> RegenerarAsync(Guid generacionId, CancellationToken ct);

    // ----- Indicadores consolidados cross-tenant -----

    Task<IndicadoresPortafolioDto> GetIndicadoresPortafolioAsync(CancellationToken ct);
    Task<IndicadoresFinancieroConsolidadoDto> GetFinancieroConsolidadoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<IndicadoresOperativoConsolidadoDto> GetOperativoConsolidadoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<IndicadoresPqrsdConsolidadoDto> GetPqrsdConsolidadoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<IndicadoresEquipoDto> GetIndicadoresEquipoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    // ----- Resumen -----

    Task<ResumenReportesConsolidadosDto> GetResumenAsync(CancellationToken ct);
}
