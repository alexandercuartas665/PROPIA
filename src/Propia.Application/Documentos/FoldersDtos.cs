using Propia.Domain.Enums;

namespace Propia.Application.Documentos;

// ===========================================================================
// Vista "Folders" (v2) del modulo 2.15. DTOs.
// ===========================================================================

/// <summary>Metadato (definicion) de un folder o documento.</summary>
public record FolderCampoDto(
    Guid Id, Guid? FolderId, Guid? DocumentoId, string Label, TipoCampoTablero Tipo,
    string? Opciones, string? Descripcion, bool Obligatorio, int Orden, int? Decimales,
    /// <summary>Nombre del folder del que se hereda (null = propio del documento o del folder consultado).</summary>
    string? HeredadoDe = null);

public record FolderCampoValorDto(Guid DefinicionId, string Label, TipoCampoTablero Tipo, string? Valor);

public record FolderNodoDto(
    Guid Id, Guid? ParentId, string Nombre, string? Codigo, string? Descripcion, int Orden,
    int Documentos, int Archivados, int Subfolders,
    IReadOnlyList<FolderCampoDto> Campos,
    IReadOnlyList<FolderNodoDto> Hijos);

/// <summary>Tarjeta de la vista Folders (folders raiz con totales recursivos).</summary>
public record FolderTarjetaDto(
    Guid Id, string Nombre, string? Codigo, string? Descripcion,
    int Subfolders, int Total, int Cargados, int PendientesObligatorios, DateTimeOffset? UltimaCarga);

public record FolderRutaDto(Guid Id, string Nombre);

/// <summary>Fila de la lista plana de folders. Ruta = "Raiz / Sub / Folder".</summary>
public record FolderListaDto(Guid Id, string Nombre, string? Codigo, Guid? ParentId, string Ruta, int Nivel);

public record FolderDocumentoDto(
    Guid Id, Guid FolderId, string Nombre, bool Obligatorio, bool Archivado, bool Cargado,
    string? ArchivoNombre, string? ArchivoMime, long ArchivoTamano, int NumeroVersiones,
    DateTimeOffset? UltimaVersionAt,
    IReadOnlyList<FolderCampoDto> CamposPropios,
    IReadOnlyList<FolderCampoValorDto> Valores);

public record FolderDetalleDto(
    Guid Id, Guid? ParentId, string Nombre, string? Codigo, string? Descripcion,
    IReadOnlyList<FolderRutaDto> Ruta,
    IReadOnlyList<FolderRutaDto> Subfolders,
    IReadOnlyList<FolderCampoDto> CamposPropios,
    IReadOnlyList<FolderCampoDto> CamposHeredados,
    IReadOnlyList<FolderDocumentoDto> Documentos,
    IReadOnlyList<FolderDocumentoDto> Archivados);

public record FolderVersionDto(
    Guid Id, int Numero, string ArchivoNombre, string ArchivoMime, long ArchivoTamano,
    string? NotasCambio, DateTimeOffset CreadoAt, Guid? SubidoPorUsuarioId);

/// <summary>
/// Vista previa renderizable. Kind: "image"/"pdf" -> DataUri; "text" -> Texto; "html" -> Html; "none" -> solo descarga.
/// </summary>
public record DocumentoPreviewDto(
    string Kind, string Nombre, string Mime,
    string? DataUri = null, string? Html = null, string? Texto = null);

public record FolderZipDto(string NombreArchivo, byte[] Contenido, int Archivos);

// ----- Requests -----
public record CrearFolderRequest(string Nombre, Guid? ParentId = null, string? Descripcion = null);

/// <summary>MERGE: solo se aplican los campos enviados. MoverARaiz=true deja ParentId en null.</summary>
public record ActualizarFolderRequest(
    string? Nombre = null, Guid? ParentId = null, bool MoverARaiz = false,
    string? Descripcion = null, int? Orden = null);

public record CrearDocumentoRequest(string Nombre, bool Obligatorio = false);

public record CampoValorRequest(Guid DefinicionId, string? Valor);

/// <summary>Crea el documento y sube su primera version. Nombre null = nombre del archivo sin extension.</summary>
public record CargarDocumentoRequest(
    string NombreArchivo, string TipoMime, long TamanoBytes, string ContenidoBase64,
    string? Nombre = null, bool Obligatorio = false,
    IReadOnlyList<CampoValorRequest>? Valores = null, string? NotasCambio = null);

/// <summary>Sube una version nueva (o la primera, si el documento era una casilla vacia).</summary>
public record CargarVersionRequest(
    string NombreArchivo, string TipoMime, long TamanoBytes, string ContenidoBase64,
    IReadOnlyList<CampoValorRequest>? Valores = null, string? NotasCambio = null);

/// <summary>MERGE. FolderId mueve el documento a otro folder.</summary>
public record ActualizarDocumentoRequest(string? Nombre = null, bool? Obligatorio = null, Guid? FolderId = null);

/// <summary>MERGE de valores. ValidarObligatorios: falla si algun obligatorio del esquema efectivo queda vacio.</summary>
public record GuardarValoresRequest(IReadOnlyList<CampoValorRequest> Valores, bool ValidarObligatorios = true);

/// <summary>Exactamente uno de FolderId / DocumentoId.</summary>
public record CrearFolderCampoRequest(
    Guid? FolderId, Guid? DocumentoId, string Label, TipoCampoTablero Tipo = TipoCampoTablero.Texto,
    string? Opciones = null, string? Descripcion = null, bool Obligatorio = false, int? Decimales = null);

/// <summary>MERGE.</summary>
public record ActualizarFolderCampoRequest(
    string? Label = null, TipoCampoTablero? Tipo = null, string? Opciones = null, string? Descripcion = null,
    bool? Obligatorio = null, int? Orden = null, int? Decimales = null);
