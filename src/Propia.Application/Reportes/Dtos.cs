using Propia.Domain.Enums;

namespace Propia.Application.Reportes;

// ===========================================================================
// Catalogo
// ===========================================================================

public record CategoriaReporteDto(
    Guid Id,
    string Nombre,
    string? Icono,
    string? Color,
    string ModuloOrigen,
    int Orden,
    bool EsActiva,
    int NumeroReportes);

public record CatalogoReporteDto(
    Guid Id,
    Guid CategoriaId,
    string CategoriaNombre,
    string Nombre,
    string? Descripcion,
    string ModuloOrigen,
    string Clave,
    IReadOnlyList<AudienciaReporte> Audiencias,
    bool EsActivo,
    bool EsSistema,
    int Orden);

// ===========================================================================
// Generacion
// ===========================================================================

public record GenerarReporteRequest(
    Guid CatalogoId,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    string? FiltrosAplicadosJson);

public record ReporteGeneradoListaDto(
    Guid Id,
    Guid? CatalogoId,
    string Nombre,
    string Categoria,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    OrigenReporte Origen,
    EstadoReporteGenerado Estado,
    bool CompartidoConsejo,
    DateTimeOffset? CompartidoAt,
    Guid? GeneradoPorUsuarioId,
    DateTimeOffset CreadoAt);

public record ReporteGeneradoDetalleDto(
    Guid Id,
    Guid? CatalogoId,
    string Nombre,
    string Categoria,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    OrigenReporte Origen,
    EstadoReporteGenerado Estado,
    string? ErrorMensaje,
    bool CompartidoConsejo,
    DateTimeOffset? CompartidoAt,
    string? FiltrosAplicadosJson,
    string? ResultadoJson,
    string? UrlPdf,
    string? UrlExcel,
    DateTimeOffset? UrlExpiracion,
    DateTimeOffset CreadoAt,
    Guid? GeneradoPorUsuarioId);

// ===========================================================================
// Programaciones
// ===========================================================================

public record ProgramacionListaDto(
    Guid Id,
    Guid CatalogoId,
    string CatalogoNombre,
    string? Nombre,
    FrecuenciaProgramacion Frecuencia,
    int DiaEnvio,
    PeriodoQueCubre PeriodoQueCubre,
    FormatoReporte Formato,
    IReadOnlyList<string> Canales,
    EstadoProgramacion Estado,
    DateOnly? ProximoEnvio,
    DateTimeOffset? UltimoEnvio,
    bool? UltimoEnvioExitoso,
    int NumeroDestinatarios);

public record DestinatarioDto(Guid Id, Guid? PersonaId, string? PersonaNombre, string? EmailExterno, string? WhatsappExterno);

public record CrearProgramacionRequest(
    Guid CatalogoId,
    string? Nombre,
    FrecuenciaProgramacion Frecuencia,
    int DiaEnvio,
    PeriodoQueCubre PeriodoQueCubre,
    FormatoReporte Formato,
    IReadOnlyList<string> Canales,
    string? FiltrosAplicadosJson,
    IReadOnlyList<DestinatarioInput> Destinatarios);

public record DestinatarioInput(Guid? PersonaId, string? EmailExterno, string? WhatsappExterno);

public record ActualizarProgramacionRequest(
    string? Nombre,
    FrecuenciaProgramacion Frecuencia,
    int DiaEnvio,
    PeriodoQueCubre PeriodoQueCubre,
    FormatoReporte Formato,
    IReadOnlyList<string> Canales,
    string? FiltrosAplicadosJson,
    IReadOnlyList<DestinatarioInput> Destinatarios);

// ===========================================================================
// Semaforos
// ===========================================================================

public record SemaforoConfigDto(
    string IndicadorKey,
    decimal UmbralAmarillo,
    decimal UmbralRojo,
    bool EsAscendente);

public record GuardarSemaforoRequest(decimal UmbralAmarillo, decimal UmbralRojo, bool EsAscendente);

// ===========================================================================
// Vista consejo + portal transparencia (pasan los indicadores ya empaquetados)
// ===========================================================================

public record VistaConsejoDto(
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    KpisConsejoDto Kpis,
    IReadOnlyList<ReporteGeneradoListaDto> ReportesCompartidos);

// ===========================================================================
// Resumen modulo
// ===========================================================================

public record ResumenReportesDto(
    int TotalCatalogo,
    int CategoriasActivas,
    int GeneradosUltimoMes,
    int CompartidosConsejoUltimoMes,
    int ProgramacionesActivas);
