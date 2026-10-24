namespace Propia.Application.Documentos;

/// <summary>
/// Vista "Folders" (v2) del modulo 2.15 Documentos. Arbol de folders de profundidad arbitraria,
/// documentos con versiones inmutables y archivado (nunca borrado destructivo con archivo),
/// metadatos tipados por folder (heredados) y por documento. Tenant-scoped (RLS).
/// </summary>
public interface IFoldersService
{
    // ----- Folders -----
    /// <summary>Arbol completo. Siembra folders base si la copropiedad no tiene ninguno.</summary>
    Task<IReadOnlyList<FolderNodoDto>> ArbolAsync(CancellationToken ct);
    /// <summary>Folders raiz con totales recursivos (vista de tarjetas).</summary>
    Task<IReadOnlyList<FolderTarjetaDto>> TarjetasAsync(CancellationToken ct);
    Task<FolderDetalleDto?> GetFolderAsync(Guid id, CancellationToken ct);
    Task<FolderNodoDto> CrearFolderAsync(CrearFolderRequest req, CancellationToken ct);
    Task<bool> ActualizarFolderAsync(Guid id, ActualizarFolderRequest req, CancellationToken ct);
    /// <summary>Elimina el folder solo si ni el ni sus subfolders tienen documentos con archivo.</summary>
    Task<bool> EliminarFolderAsync(Guid id, CancellationToken ct);
    /// <summary>ZIP con todos los documentos del folder y sus subfolders (jerarquia como carpetas).</summary>
    Task<FolderZipDto?> DescargarZipAsync(Guid id, CancellationToken ct);

    // ----- Documentos -----
    Task<FolderDocumentoDto?> GetDocumentoAsync(Guid documentoId, CancellationToken ct);
    /// <summary>Casilla esperada sin archivo.</summary>
    Task<FolderDocumentoDto> CrearDocumentoAsync(Guid folderId, CrearDocumentoRequest req, CancellationToken ct);
    /// <summary>Crea el documento y sube la version 1 con sus metadatos.</summary>
    Task<FolderDocumentoDto> CargarDocumentoAsync(Guid folderId, CargarDocumentoRequest req, CancellationToken ct);
    /// <summary>Reemplazar: version nueva; las anteriores se conservan.</summary>
    Task<FolderDocumentoDto?> CargarVersionAsync(Guid documentoId, CargarVersionRequest req, CancellationToken ct);
    Task<bool> ActualizarDocumentoAsync(Guid documentoId, ActualizarDocumentoRequest req, CancellationToken ct);
    /// <summary>Solo casillas sin ninguna version. Con archivo: usar Archivar.</summary>
    Task<bool> EliminarDocumentoAsync(Guid documentoId, CancellationToken ct);
    Task<bool> ArchivarDocumentoAsync(Guid documentoId, CancellationToken ct);
    Task<bool> RestaurarDocumentoAsync(Guid documentoId, CancellationToken ct);
    /// <summary>Copia con metadatos propios y valores; el archivo actual queda como version 1 de la copia.</summary>
    Task<FolderDocumentoDto?> DuplicarDocumentoAsync(Guid documentoId, CancellationToken ct);

    Task<DescargaDocumentoDto?> DescargarDocumentoAsync(Guid documentoId, CancellationToken ct);
    Task<IReadOnlyList<FolderVersionDto>> ListarVersionesAsync(Guid documentoId, CancellationToken ct);
    Task<DescargaDocumentoDto?> DescargarVersionAsync(Guid documentoId, Guid versionId, CancellationToken ct);
    Task<DocumentoPreviewDto?> PreviewDocumentoAsync(Guid documentoId, CancellationToken ct);
    Task<DocumentoPreviewDto?> PreviewVersionAsync(Guid documentoId, Guid versionId, CancellationToken ct);

    /// <summary>Guarda (merge) los valores de metadatos del documento.</summary>
    Task<bool> GuardarValoresAsync(Guid documentoId, GuardarValoresRequest req, CancellationToken ct);

    /// <summary>Lista plana de todos los folders (para selectores de otros modulos: contratos, polizas).</summary>
    Task<IReadOnlyList<FolderListaDto>> ListaAsync(CancellationToken ct);

    // ----- Metadatos (definiciones) -----
    /// <summary>Esquema efectivo del documento: campos de su cadena de folders + propios.</summary>
    Task<IReadOnlyList<FolderCampoDto>> CamposEfectivosAsync(Guid documentoId, CancellationToken ct);
    /// <summary>Campos PROPIOS de un folder o de un documento (exactamente uno de los dos).</summary>
    Task<IReadOnlyList<FolderCampoDto>> ListarCamposAsync(Guid? folderId, Guid? documentoId, CancellationToken ct);
    Task<FolderCampoDto> CrearCampoAsync(CrearFolderCampoRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid campoId, ActualizarFolderCampoRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct);
}
