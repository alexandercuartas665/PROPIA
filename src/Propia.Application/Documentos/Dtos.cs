using Propia.Domain.Enums;

namespace Propia.Application.Documentos;

// ===========================================================================
// Categorias
// ===========================================================================

public record CategoriaDto(
    Guid Id,
    bool EsBase,
    string Nombre,
    string? Descripcion,
    string? Icono,
    string? Color,
    bool Activa,
    int Orden,
    int NumeroDocumentos);

public record CrearCategoriaRequest(
    string Nombre,
    string? Descripcion,
    string? Icono,
    string? Color);

public record ActualizarCategoriaRequest(
    string Nombre,
    string? Descripcion,
    string? Icono,
    string? Color,
    int Orden);

// ===========================================================================
// Carpetas
// ===========================================================================

public record CarpetaDto(
    Guid Id,
    Guid CategoriaId,
    Guid? PadreId,
    string Nombre,
    string? Descripcion,
    int Orden,
    bool Activa,
    int NumeroDocumentos,
    IReadOnlyList<CarpetaDto> Subcarpetas);

public record CrearCarpetaRequest(
    Guid CategoriaId,
    Guid? PadreId,
    string Nombre,
    string? Descripcion);

public record RenombrarCarpetaRequest(string Nombre, string? Descripcion);

// ===========================================================================
// Documento - listado y ficha
// ===========================================================================

public record DocumentoListaDto(
    Guid Id,
    string Titulo,
    string NombreArchivoOriginal,
    Guid CategoriaId,
    string CategoriaNombre,
    Guid? CarpetaId,
    string? CarpetaNombre,
    EstadoDocumento Estado,
    OrigenDocumento Origen,
    string Visibilidad,
    int NumeroVersiones,
    long TamanoBytes,
    string TipoMime,
    bool Destacado,
    bool DestacadoPersonal,
    DateTimeOffset CreadoAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<EtiquetaResumenDto> Etiquetas);

public record DocumentoDetalleDto(
    Guid Id,
    string Titulo,
    string? Descripcion,
    string NombreArchivoOriginal,
    Guid CategoriaId,
    string CategoriaNombre,
    Guid? CarpetaId,
    string? CarpetaNombre,
    EstadoDocumento Estado,
    OrigenDocumento Origen,
    Guid? OrigenEntidadId,
    string Visibilidad,
    int NumeroVersiones,
    bool Destacado,
    bool DestacadoPersonal,
    DateTimeOffset CreadoAt,
    Guid SubidoPorUsuarioId,
    VersionDto VersionActual,
    IReadOnlyList<VersionDto> Historial,
    IReadOnlyList<EtiquetaResumenDto> Etiquetas);

public record VersionDto(
    Guid Id,
    int Numero,
    string NombreArchivo,
    string TipoMime,
    long TamanoBytes,
    string HashSha256,
    string UrlStorage,
    string? NotasCambio,
    Guid SubidoPorUsuarioId,
    DateTimeOffset CreadoAt);

/// <summary>Filtros para la bandeja de documentos. Spec 2.15 seccion 11.</summary>
public record DocumentosFiltro(
    Guid? CategoriaId,
    Guid? CarpetaId,
    OrigenDocumento? Origen,
    EstadoDocumento? Estado,
    string? Visibilidad,
    string? TextoBusqueda,
    Guid? EtiquetaId,
    bool? SoloDestacados,
    int Page = 1,
    int PageSize = 30);

public record DocumentosPageDto(IReadOnlyList<DocumentoListaDto> Items, int Total, int Page, int PageSize);

public record SubirDocumentoRequest(
    Guid CategoriaId,
    Guid? CarpetaId,
    string Titulo,
    string? Descripcion,
    string NombreArchivo,
    string TipoMime,
    long TamanoBytes,
    /// <summary>Contenido en base64. MVP - en Fase 2 se usa upload directo a R2.</summary>
    string ContenidoBase64,
    string Visibilidad,
    IReadOnlyList<Guid>? EtiquetaIds,
    OrigenDocumento Origen = OrigenDocumento.Manual,
    Guid? OrigenEntidadId = null);

public record NuevaVersionRequest(
    string NombreArchivo,
    string TipoMime,
    long TamanoBytes,
    string ContenidoBase64,
    string? NotasCambio);

public record ActualizarMetadatosRequest(
    string Titulo,
    string? Descripcion,
    Guid CategoriaId,
    Guid? CarpetaId,
    string Visibilidad,
    IReadOnlyList<Guid>? EtiquetaIds);

public record CambiarEstadoRequest(EstadoDocumento NuevoEstado);

// ===========================================================================
// Etiquetas
// ===========================================================================

public record EtiquetaDto(
    Guid Id,
    bool EsBase,
    string Nombre,
    string? Color,
    bool Activa,
    int NumeroDocumentos);

public record EtiquetaResumenDto(Guid Id, string Nombre, string? Color);

public record CrearEtiquetaRequest(string Nombre, string? Color);

public record ActualizarEtiquetaRequest(string Nombre, string? Color);

// ===========================================================================
// Auditoria y consumo
// ===========================================================================

public record AuditoriaEventoDto(
    Guid Id,
    TipoEventoDocumento TipoEvento,
    string? DetalleJson,
    Guid UsuarioId,
    DateTimeOffset OcurridoAt);

public record ConsumoEventoDto(
    TipoEventoConsumo TipoEvento,
    DispositivoConsumo Dispositivo,
    Guid UsuarioId,
    DateTimeOffset OcurridoAt);

public record EstadisticasDocumentoDto(
    int TotalVistas,
    int TotalDescargas,
    int UsuariosUnicos,
    DateTimeOffset? UltimoAcceso);

// ===========================================================================
// Resumen modulo
// ===========================================================================

public record ResumenDocumentosDto(
    int TotalDocumentos,
    int TotalCategorias,
    int TotalCarpetas,
    long TamanoTotalBytes,
    int DocumentosUltimos30Dias);

// ===========================================================================
// Descarga / vista
// ===========================================================================

public record DescargaDocumentoDto(
    string NombreArchivo,
    string TipoMime,
    long TamanoBytes,
    /// <summary>Contenido en base64 - MVP. Fase 2: URL firmada R2.</summary>
    string ContenidoBase64);
