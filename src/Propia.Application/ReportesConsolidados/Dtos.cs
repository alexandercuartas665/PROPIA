using Propia.Domain.Enums;

namespace Propia.Application.ReportesConsolidados;

// ===========================================================================
// Plantillas base + reportes guardados
// ===========================================================================

public record PlantillaBaseDto(
    string Codigo,         // "salud_portafolio" | "financiero" | "operativo" | "convivencia_pqrsd" | "desempeno_equipo"
    CategoriaReporteConsolidado Categoria,
    string Nombre,
    string Descripcion,
    bool TieneDatosNominativos);

public record OrgReporteDto(
    Guid Id,
    Guid OrganizacionId,
    string Nombre,
    CategoriaReporteConsolidado Categoria,
    bool EsPlantillaBase,
    bool TieneDatosNominativos,
    Guid CreadoPorUsuarioId,
    DateTimeOffset CreadoAt,
    DateTimeOffset? UpdatedAt,
    int NumeroGeneraciones);

public record CrearReporteRequest(
    string Nombre,
    CategoriaReporteConsolidado Categoria,
    string? PlantillaBase,
    string ConfiguracionJson);

public record ActualizarReporteRequest(
    string Nombre,
    string ConfiguracionJson);

// ===========================================================================
// Generaciones (historial)
// ===========================================================================

public record GenerarReporteRequest(
    Guid ReporteId,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta);

public record GeneracionListaDto(
    Guid Id,
    Guid ReporteId,
    string ReporteNombre,
    CategoriaReporteConsolidado Categoria,
    OrigenGeneracionConsolidada Origen,
    EstadoGeneracionConsolidada Estado,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta,
    DateTimeOffset CreadoAt,
    DateTimeOffset? GeneradoAt,
    int Intentos);

public record GeneracionDetalleDto(
    Guid Id,
    Guid ReporteId,
    string ReporteNombre,
    CategoriaReporteConsolidado Categoria,
    OrigenGeneracionConsolidada Origen,
    EstadoGeneracionConsolidada Estado,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta,
    string? ResultadoJson,
    string? UrlPdf,
    string? UrlExcel,
    DateTimeOffset? UrlExpiracion,
    string? ErrorDetalle,
    int Intentos,
    DateTimeOffset CreadoAt,
    DateTimeOffset? GeneradoAt);

// ===========================================================================
// Indicadores consolidados cross-tenant
// ===========================================================================

public record IndicadoresPortafolioDto(
    int TotalCopropiedades,
    int Verdes,
    int Amarillas,
    int Rojas,
    int AlertasActivas,
    int TareasVencidasTotal);

public record IndicadoresFinancieroConsolidadoDto(
    decimal RecaudoTotal,
    decimal MoraTotal,
    decimal RecaudoPctPromedio,
    IReadOnlyList<RecaudoPorCopropiedadDto> PorCopropiedad);

public record RecaudoPorCopropiedadDto(Guid TenantId, string CopropiedadNombre, decimal Recaudado, decimal Esperado, decimal? PctRecaudo);

public record IndicadoresOperativoConsolidadoDto(
    int TareasAbiertas,
    int TareasVencidas,
    int TareasCompletadas30d,
    IReadOnlyList<TareasPorCopropiedadDto> PorCopropiedad);

public record TareasPorCopropiedadDto(Guid TenantId, string CopropiedadNombre, int Activas, int Vencidas);

public record IndicadoresPqrsdConsolidadoDto(
    int TotalActivas,
    int Vencidas,
    int Felicitaciones,
    decimal? TiempoPromedioRespuestaDias,
    IReadOnlyList<PqrsdPorCopropiedadDto> PorCopropiedad);

public record PqrsdPorCopropiedadDto(Guid TenantId, string CopropiedadNombre, int Activas, int Vencidas);

public record IndicadoresEquipoDto(
    IReadOnlyList<DesempenoColaboradorDto> Colaboradores);

public record DesempenoColaboradorDto(
    Guid ColaboradorId,
    string Nombre,
    int TareasAsignadas,
    int TareasCompletadas,
    int TareasVencidas,
    int CopropiedadesAsignadas);

// ===========================================================================
// Resumen
// ===========================================================================

public record ResumenReportesConsolidadosDto(
    int TotalReportesGuardados,
    int Plantillas,
    int GeneracionesUlt30Dias,
    int ProgramacionesActivas);
