using Propia.Domain.Enums;

namespace Propia.Application.PanelConsolidado;

public record PanelTarjetaDto(
    Guid TenantId,
    string CopropiedadNombre,
    string? Ciudad,
    string? Tipo,
    string? FotoFachadaUrl,
    string? LogoUrl,
    int TotalUnidades,
    EstadoSaludCopropiedad EstadoSalud,
    int AlertasCriticas,
    int TareasVencidas,
    int PqrsdSinResponder,
    decimal? RecaudoMesPorcentaje,
    decimal? CarteraVencidaCop,
    DateOnly? ProximoEventoFecha,
    string? ProximoEventoLabel,
    DateTimeOffset CalculadoAt);

public record PanelKpisGlobalesDto(
    int TotalCopropiedadesActivas,
    int CopropiedadesEnCritico,
    int TotalTareasVencidas,
    int TotalPqrsdSinResponder,
    decimal RecaudoPromedioMes,
    decimal CarteraVencidaTotal);

public record PanelFeedEventoDto(
    Guid Id,
    Guid TenantId,
    string CopropiedadNombre,
    TipoEventoPanel TipoEvento,
    string Descripcion,
    string? UrlAccion,
    DateTimeOffset OcurridoAt);

public record PanelAlertaCruzadaDto(
    string Tipo,
    int Cantidad,
    string Resumen,
    IReadOnlyList<string> Copropiedades);

public record PanelResumenDto(
    PanelKpisGlobalesDto Kpis,
    IReadOnlyList<PanelTarjetaDto> Tarjetas,
    IReadOnlyList<PanelAlertaCruzadaDto> AlertasCruzadas);
