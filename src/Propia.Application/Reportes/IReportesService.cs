using Propia.Domain.Enums;

namespace Propia.Application.Reportes;

/// <summary>
/// Modulo 2.16 Reportes e Indicadores - servicio de aplicacion (spec v1.0 MVP).
///
/// Alcance MVP:
///  - Catalogo extensible (8 categorias base + ~25 reportes seedados via migracion).
///  - Generacion sincronas devolviendo ResultadoJson estructurado (PDF/Excel diferidos).
///  - Historial completo con regeneracion.
///  - Programaciones - solo config (RN-06: el despacho lo hara T.2 cuando se construya).
///  - Vista consejo (KPIs + reportes compartidos).
///  - Portal transparencia agregado no-nominativo (RN-04).
///  - Semaforos configurables (defaults PropIA).
///
/// Diferido a Fase 2/3:
///  - Generacion asincrona con cola (RN-02).
///  - PDF formateado con logo (RN-12) y Excel exportable.
///  - Agente IA T.1 (Origen=Ia).
///  - Despacho real via T.2 (programaciones).
///  - Cache de transparencia (TTL 1h).
/// </summary>
public interface IReportesService
{
    // ----- Catalogo -----
    Task<IReadOnlyList<CategoriaReporteDto>> ListarCategoriasAsync(AudienciaReporte? audiencia, CancellationToken ct);
    Task<IReadOnlyList<CatalogoReporteDto>> ListarCatalogoAsync(Guid? categoriaId, AudienciaReporte? audiencia, CancellationToken ct);
    Task<CatalogoReporteDto?> GetCatalogoAsync(Guid id, CancellationToken ct);

    // ----- Generacion + historial -----
    Task<ReporteGeneradoDetalleDto> GenerarAsync(GenerarReporteRequest req, CancellationToken ct);
    Task<IReadOnlyList<ReporteGeneradoListaDto>> ListarHistorialAsync(
        DateOnly? desde, DateOnly? hasta, OrigenReporte? origen,
        Guid? catalogoId, CancellationToken ct);
    Task<ReporteGeneradoDetalleDto?> GetReporteAsync(Guid id, CancellationToken ct);

    /// <summary>Regenera con los mismos parametros que el reporte original.</summary>
    Task<ReporteGeneradoDetalleDto> RegenerarAsync(Guid id, CancellationToken ct);

    /// <summary>Marca/desmarca como visible en la vista del consejo (RN-05).</summary>
    Task<bool> CompartirConsejoAsync(Guid id, bool compartir, CancellationToken ct);

    // ----- Programaciones (config; T.2 hace despacho real) -----
    Task<IReadOnlyList<ProgramacionListaDto>> ListarProgramacionesAsync(CancellationToken ct);
    Task<ProgramacionListaDto?> GetProgramacionAsync(Guid id, CancellationToken ct);
    Task<ProgramacionListaDto> CrearProgramacionAsync(CrearProgramacionRequest req, CancellationToken ct);
    Task<bool> ActualizarProgramacionAsync(Guid id, ActualizarProgramacionRequest req, CancellationToken ct);
    Task<bool> PausarProgramacionAsync(Guid id, bool pausar, CancellationToken ct);
    Task<bool> EliminarProgramacionAsync(Guid id, CancellationToken ct);

    // ----- Semaforos del consejo -----
    Task<IReadOnlyList<SemaforoConfigDto>> ListarSemaforosAsync(CancellationToken ct);
    Task<SemaforoConfigDto> GuardarSemaforoAsync(string indicadorKey, GuardarSemaforoRequest req, CancellationToken ct);

    // ----- Vista consejo -----
    Task<VistaConsejoDto> GetVistaConsejoAsync(DateOnly? periodoInicio, DateOnly? periodoFin, CancellationToken ct);

    // ----- Portal transparencia -----
    Task<TransparenciaDto> GetTransparenciaAsync(DateOnly? periodoInicio, DateOnly? periodoFin, CancellationToken ct);

    // ----- Resumen modulo -----
    Task<ResumenReportesDto> GetResumenAsync(CancellationToken ct);
}
