using Propia.Domain.Enums;

namespace Propia.Application.Documentos;

/// <summary>
/// Modulo 2.15 Documentos y Archivo Digital - servicio de aplicacion (spec v1.0 MVP).
/// Gestiona el repositorio documental de la copropiedad: subida, versionado,
/// categorias/carpetas, etiquetas, auditoria append-only y consumo (vistas/descargas).
/// La firma electronica (estados EnFirma/Firmado) queda fuera del MVP.
/// El blob fisico vive en IBlobStorage (R2 en produccion, filesystem local en dev).
/// </summary>
public interface IDocumentosService
{
    // ----- Categorias -----
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct);
    Task<CategoriaDto?> GetCategoriaAsync(Guid id, CancellationToken ct);
    Task<CategoriaDto> CrearCategoriaAsync(CrearCategoriaRequest req, CancellationToken ct);
    /// <summary>RN-12: solo categorias custom del tenant son editables.</summary>
    Task<bool> ActualizarCategoriaAsync(Guid id, ActualizarCategoriaRequest req, CancellationToken ct);
    /// <summary>Soft delete - RN-12 bloquea base, RN-01 prohibe borrado fisico.</summary>
    Task<bool> DesactivarCategoriaAsync(Guid id, CancellationToken ct);

    // ----- Carpetas -----
    /// <summary>Arbol completo de carpetas de la categoria.</summary>
    Task<IReadOnlyList<CarpetaDto>> ListarCarpetasAsync(Guid categoriaId, CancellationToken ct);
    Task<CarpetaDto> CrearCarpetaAsync(CrearCarpetaRequest req, CancellationToken ct);
    Task<bool> RenombrarCarpetaAsync(Guid id, RenombrarCarpetaRequest req, CancellationToken ct);
    /// <summary>Soft delete. Falla si tiene documentos activos.</summary>
    Task<bool> DesactivarCarpetaAsync(Guid id, CancellationToken ct);

    // ----- Etiquetas -----
    Task<IReadOnlyList<EtiquetaDto>> ListarEtiquetasAsync(CancellationToken ct);
    Task<EtiquetaDto> CrearEtiquetaAsync(CrearEtiquetaRequest req, CancellationToken ct);
    Task<bool> ActualizarEtiquetaAsync(Guid id, ActualizarEtiquetaRequest req, CancellationToken ct);
    Task<bool> DesactivarEtiquetaAsync(Guid id, CancellationToken ct);

    // ----- Documentos: ciclo de vida -----
    Task<DocumentosPageDto> ListarDocumentosAsync(DocumentosFiltro filtro, CancellationToken ct);
    Task<DocumentoDetalleDto?> GetDocumentoAsync(Guid id, CancellationToken ct);

    /// <summary>Sube el documento + crea v1 + emite evento Subida + indexa metadatos.</summary>
    Task<DocumentoDetalleDto> SubirDocumentoAsync(SubirDocumentoRequest req, CancellationToken ct);

    /// <summary>RN-13: incrementa numero y conserva las versiones anteriores.</summary>
    Task<VersionDto> SubirNuevaVersionAsync(Guid documentoId, NuevaVersionRequest req, CancellationToken ct);

    Task<bool> ActualizarMetadatosAsync(Guid id, ActualizarMetadatosRequest req, CancellationToken ct);

    /// <summary>Transicion explicita Borrador->Vigente o Vigente->Archivado.</summary>
    Task<bool> CambiarEstadoAsync(Guid id, CambiarEstadoRequest req, CancellationToken ct);

    /// <summary>Soft delete - RN-01: no hay eliminacion fisica. Documento queda Activo=false y Estado=Archivado.</summary>
    Task<bool> EliminarDocumentoAsync(Guid id, CancellationToken ct);

    // ----- Destacados -----
    /// <summary>Pin personal del usuario actual.</summary>
    Task<bool> MarcarDestacadoPersonalAsync(Guid documentoId, CancellationToken ct);
    Task<bool> QuitarDestacadoPersonalAsync(Guid documentoId, CancellationToken ct);
    /// <summary>Pin a nivel copropiedad (solo admin).</summary>
    Task<bool> MarcarDestacadoCopropiedadAsync(Guid documentoId, bool destacar, CancellationToken ct);

    // ----- Descarga / visualizacion -----
    /// <summary>Descarga la version actual (o la version solicitada si version != null). Emite evento Descarga.</summary>
    Task<DescargaDocumentoDto?> DescargarAsync(Guid documentoId, Guid? versionId, CancellationToken ct);

    /// <summary>Registra evento Vista (sin descarga). Spec seccion 15.2.</summary>
    Task RegistrarVistaAsync(Guid documentoId, DispositivoConsumo dispositivo, CancellationToken ct);

    // ----- Auditoria y estadisticas -----
    Task<IReadOnlyList<AuditoriaEventoDto>> ListarAuditoriaAsync(Guid documentoId, CancellationToken ct);
    Task<EstadisticasDocumentoDto> GetEstadisticasAsync(Guid documentoId, CancellationToken ct);

    // ----- Resumen modulo -----
    Task<ResumenDocumentosDto> GetResumenAsync(CancellationToken ct);
}
