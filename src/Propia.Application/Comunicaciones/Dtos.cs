using Propia.Domain.Enums;

namespace Propia.Application.Comunicaciones;

// ===========================================================================
// Plantillas
// ===========================================================================

public record PlantillaDto(
    Guid Id,
    bool EsGlobal,
    string Nombre,
    TipoComunicadoBase TipoComunicado,
    string AsuntoModelo,
    string CuerpoModelo,
    bool AcusePorDefecto,
    bool Activa);

public record CrearPlantillaRequest(
    string Nombre,
    TipoComunicadoBase TipoComunicado,
    string AsuntoModelo,
    string CuerpoModelo,
    bool AcusePorDefecto);

public record ActualizarPlantillaRequest(
    string Nombre,
    TipoComunicadoBase TipoComunicado,
    string AsuntoModelo,
    string CuerpoModelo,
    bool AcusePorDefecto);

// ===========================================================================
// Comunicado - bandeja y ficha
// ===========================================================================

public record ComunicadoListaDto(
    Guid Id,
    string Asunto,
    TipoComunicadoBase TipoComunicado,
    EstadoComunicado Estado,
    string SegmentoResumen,
    DateTimeOffset CreadoAt,
    DateTimeOffset? FechaProgramada,
    DateTimeOffset? FechaEnvio,
    bool RequiereAcuse,
    int? TotalDestinatarios,
    int? TotalAbiertos,
    decimal? TasaApertura);

public record ComunicadoDetalleDto(
    Guid Id,
    Guid? PlantillaId,
    TipoComunicadoBase TipoComunicado,
    string Asunto,
    string CuerpoHtml,
    string CuerpoTextoPlano,
    bool RequiereAcuse,
    EstadoComunicado Estado,
    DateTimeOffset? FechaProgramada,
    DateTimeOffset? FechaEnvio,
    DateTimeOffset? FechaCompletado,
    int? TotalDestinatarios,
    int? TotalEntregados,
    int? TotalFallidos,
    DateTimeOffset CreadoAt,
    Guid CreadoPorUsuarioId,
    IReadOnlyList<SegmentoDto> Segmentos,
    IReadOnlyList<AdjuntoDto> Adjuntos);

public record CrearBorradorRequest(
    Guid? PlantillaId,
    TipoComunicadoBase TipoComunicado,
    string Asunto,
    string CuerpoHtml,
    string? CuerpoTextoPlano,
    bool RequiereAcuse);

public record ActualizarBorradorRequest(
    TipoComunicadoBase TipoComunicado,
    string Asunto,
    string CuerpoHtml,
    string? CuerpoTextoPlano,
    bool RequiereAcuse);

// ===========================================================================
// Segmentacion
// ===========================================================================

public record SegmentoDto(Guid Id, TipoSegmento Tipo, string ValorJson);

public record AgregarSegmentoRequest(TipoSegmento Tipo, string ValorJson);

public record PreviewDestinatariosDto(
    int Total,
    int TotalSinWhatsapp,
    int TotalSinAutorizacion,
    IReadOnlyList<PreviewPersonaDto> Muestra);

public record PreviewPersonaDto(Guid PersonaId, string NombreCompleto, string? UnidadNumero, bool TieneWhatsapp, bool AutorizaDatos);

// ===========================================================================
// Adjuntos
// ===========================================================================

public record AdjuntoDto(Guid Id, string NombreArchivo, string TipoMime, long TamanoBytes, string UrlStorage);

public record CrearAdjuntoRequest(string NombreArchivo, string TipoMime, long TamanoBytes, string UrlStorage);

// ===========================================================================
// Envio / programacion / cancelacion
// ===========================================================================

public record EnviarRequest(DateTimeOffset? FechaProgramada);

public record CancelarRequest(string? Motivo);

// ===========================================================================
// Acuses
// ===========================================================================

public record AcuseListaDto(
    int Total,
    int Confirmados,
    int Pendientes,
    decimal TasaApertura,
    IReadOnlyList<AcuseDestinatarioDto> Confirmadas,
    IReadOnlyList<AcuseDestinatarioDto> NoAbiertos);

public record AcuseDestinatarioDto(
    Guid PersonaId,
    string NombreCompleto,
    string? UnidadNumero,
    DateTimeOffset? AbiertoAt,
    DispositivoAcuse? Dispositivo);

/// <summary>Vista publica que se sirve en GET /c/{token} sin auth.</summary>
public record VistaPublicaComunicadoDto(
    string Asunto,
    TipoComunicadoBase TipoComunicado,
    string CuerpoHtml,
    DateTimeOffset FechaEnvio,
    string CopropiedadNombre,
    IReadOnlyList<AdjuntoDto> Adjuntos);

// ===========================================================================
// Resumen
// ===========================================================================

public record ResumenComunicacionesDto(
    int Borradores,
    int Programados,
    int EnviadosMes,
    decimal? TasaAperturaPromedioMes);
