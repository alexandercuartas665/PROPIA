using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

// ===========================================================================
// Modulo 2.15 Documentos - vista "Folders" (evolucion v2 de Expedientes/TRD).
// Modelo comun para el usuario: FOLDERS (arbol de profundidad arbitraria) que contienen
// subfolders y DOCUMENTOS. Folder y documento son entidades con METADATOS tipados propios
// (patron <X>CampoDefinicion + <X>CampoValor; el documento hereda los campos de su cadena de
// folders). Los archivos viven en IBlobStorage con versiones inmutables. Un documento NUNCA se
// borra una vez tiene archivo: se ARCHIVA (restaurable).
// Reemplaza a SerieDocumental/SubserieDocumental/Expediente/* (migracion AddFoldersDocumentos
// copia los datos conservando los Ids, por eso ContratoExpediente/Poliza.ExpedienteId siguen
// apuntando al folder con el mismo Id).
// Independiente del "Archivo central" (DocumentoCategoria/Carpeta/Documento).
// ===========================================================================

/// <summary>Folder de documentos de la copropiedad. ParentId null = folder raiz.</summary>
public class Folder : TenantEntity
{
    public Guid? ParentId { get; set; }
    public Folder? Parent { get; set; }

    public string Nombre { get; set; } = null!;

    /// <summary>Codigo corto opcional (ej. ACT-001). Se conserva el de los expedientes migrados.</summary>
    public string? Codigo { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Orden entre hermanos.</summary>
    public int Orden { get; set; }

    public ICollection<Folder> Hijos { get; set; } = new List<Folder>();
    public ICollection<FolderDocumento> Documentos { get; set; } = new List<FolderDocumento>();
    public ICollection<FolderCampoDefinicion> Campos { get; set; } = new List<FolderCampoDefinicion>();
}

/// <summary>
/// Documento dentro de un folder. Puede existir SIN archivo (casilla esperada / plantilla) y luego
/// cargarse. El archivo actual es la ultima version; el historial vive en <see cref="FolderDocumentoVersion"/>.
/// </summary>
public class FolderDocumento : TenantEntity
{
    public Guid FolderId { get; set; }
    public Folder? Folder { get; set; }

    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }

    /// <summary>Si es obligatorio en el folder (bloquea "folder completo" mientras no se cargue).</summary>
    public bool Obligatorio { get; set; }

    /// <summary>Archivado = oculto de la lista activa, restaurable. Nunca se elimina si tiene archivo.</summary>
    public bool Archivado { get; set; }
    public DateTimeOffset? ArchivadoAt { get; set; }

    /// <summary>Key del blob de la version ACTUAL. Null = sin cargar.</summary>
    public string? ArchivoUrl { get; set; }
    public string? ArchivoNombre { get; set; }
    public string? ArchivoMime { get; set; }
    public long ArchivoTamano { get; set; }

    /// <summary>Numero de versiones cargadas (contador denormalizado). 0 = sin cargar.</summary>
    public int NumeroVersiones { get; set; }

    public Guid? SubidoPorUsuarioId { get; set; }

    public ICollection<FolderDocumentoVersion> Versiones { get; set; } = new List<FolderDocumentoVersion>();
    public ICollection<FolderCampoDefinicion> Campos { get; set; } = new List<FolderCampoDefinicion>();
    public ICollection<FolderDocumentoCampoValor> Valores { get; set; } = new List<FolderDocumentoCampoValor>();
}

/// <summary>Version inmutable del archivo de un documento (RN-13): reemplazar agrega, nunca borra.</summary>
public class FolderDocumentoVersion : TenantEntity
{
    public Guid DocumentoId { get; set; }
    public FolderDocumento? Documento { get; set; }

    public int Numero { get; set; }
    public string ArchivoUrl { get; set; } = null!;
    public string ArchivoNombre { get; set; } = null!;
    public string ArchivoMime { get; set; } = null!;
    public long ArchivoTamano { get; set; }
    public string? HashSha256 { get; set; }
    public string? NotasCambio { get; set; }
    public Guid? SubidoPorUsuarioId { get; set; }
}

/// <summary>
/// Definicion de un metadato (campo tipado) de un FOLDER o de un DOCUMENTO (exactamente uno de los
/// dos). Los campos del folder aplican a todos los documentos de ese folder y de sus subfolders.
/// La descripcion ademas de ayuda en UI sirve para que una IA ubique el dato dentro del documento.
/// </summary>
public class FolderCampoDefinicion : TenantEntity
{
    public Guid? FolderId { get; set; }
    public Folder? Folder { get; set; }

    public Guid? DocumentoId { get; set; }
    public FolderDocumento? Documento { get; set; }

    public string Label { get; set; } = null!;

    /// <summary>Reutiliza el enum transversal de campos dinamicos (homogeneo con Unidades/Tareas/PQRSD).</summary>
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;

    /// <summary>Opciones (una por linea) para Seleccion. Null para los demas tipos.</summary>
    public string? Opciones { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Obligatorio: bloquea el guardado de valores del documento si queda vacio.</summary>
    public bool Obligatorio { get; set; }

    public int Orden { get; set; }

    /// <summary>Decimales para Numero/Moneda/Porcentaje. Null = por defecto del tipo.</summary>
    public int? Decimales { get; set; }
}

/// <summary>Valor de un metadato para un documento concreto (EAV).</summary>
public class FolderDocumentoCampoValor : TenantEntity
{
    public Guid DocumentoId { get; set; }
    public FolderDocumento? Documento { get; set; }

    public Guid DefinicionId { get; set; }
    public FolderCampoDefinicion? Definicion { get; set; }

    public string? Valor { get; set; }
}

/// <summary>
/// Texto extraido del archivo ACTUAL de un documento (indexacion para el chat con IA). Una fila por
/// documento; se recalcula cuando cambia la version. Fuente: "texto" (archivo de texto), "office"
/// (Word/Excel leidos localmente), "ia" (PDF/imagen transcritos por el extractor de IA), "ninguna".
/// </summary>
public class FolderDocumentoTexto : TenantEntity
{
    public Guid DocumentoId { get; set; }
    public FolderDocumento? Documento { get; set; }

    /// <summary>Numero de version del archivo del que se extrajo el texto.</summary>
    public int NumeroVersion { get; set; }
    public string Fuente { get; set; } = "ninguna";
    public string? Texto { get; set; }
    public int Caracteres { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Mensaje del chat "Conversar con los documentos" (explorador de folders). La conversacion es por
/// copropiedad + folder RAIZ + usuario, y persiste entre aperturas del explorador.
/// </summary>
public class FolderChatMensaje : TenantEntity
{
    /// <summary>Folder raiz de la conversacion (el de la tarjeta con la que se abrio el explorador).</summary>
    public Guid FolderId { get; set; }
    public Folder? Folder { get; set; }

    public Guid UsuarioId { get; set; }

    /// <summary>"user" | "assistant".</summary>
    public string Rol { get; set; } = "user";
    public string Texto { get; set; } = null!;

    /// <summary>Folder que estaba seleccionado al preguntar (contexto), si aplica.</summary>
    public Guid? FolderContextoId { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public bool Error { get; set; }
}
