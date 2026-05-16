using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Categoria del documento. Spec 2.15 seccion 5.1 y 7.
///
/// - TenantId NULL + EsBase=true -> categoria global PropIA (9 base seedadas).
/// - TenantId con valor -> categoria custom de la copropiedad.
/// RN-12: las categorias base no son editables ni eliminables. Las custom si.
/// </summary>
public class DocumentoCategoria : BaseEntity
{
    /// <summary>NULL si es categoria base PropIA, valor si es custom del tenant.</summary>
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Icono visual sugerido (flaticon o lucide). Spec seccion 5.1.</summary>
    public string? Icono { get; set; }

    /// <summary>Color hex sugerido (paleta tema). Spec seccion 5.1.</summary>
    public string? Color { get; set; }

    /// <summary>True para las 9 categorias base PropIA. RN-12: no editable.</summary>
    public bool EsBase { get; set; }

    /// <summary>Soft delete - no se borra fisicamente (RN-01).</summary>
    public bool Activa { get; set; } = true;

    public int Orden { get; set; }
}

/// <summary>
/// Carpeta dentro de una categoria. Spec 2.15 seccion 5.2 y 7.
/// Estructura jerarquica via PadreId (autorreferencial).
/// </summary>
public class DocumentoCarpeta : TenantEntity
{
    public Guid CategoriaId { get; set; }
    public DocumentoCategoria? Categoria { get; set; }

    /// <summary>Carpeta padre. NULL = carpeta raiz de la categoria.</summary>
    public Guid? PadreId { get; set; }
    public DocumentoCarpeta? Padre { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Soft delete.</summary>
    public bool Activa { get; set; } = true;

    public int Orden { get; set; }
    public Guid? CreadoPorUsuarioId { get; set; }
}

/// <summary>
/// Documento - entidad central. Spec 2.15 seccion 4 y 7.
/// El archivo fisico vive en blob storage (R2 o local) y aqui solo metadatos.
/// Una version "actual" siempre referencia el blob mas reciente (ver DocumentoVersion).
/// RN-01: no eliminacion fisica - se marca Activo=false y Estado=Archivado.
/// </summary>
public class Documento : TenantEntity
{
    public Guid CategoriaId { get; set; }
    public DocumentoCategoria? Categoria { get; set; }

    public Guid? CarpetaId { get; set; }
    public DocumentoCarpeta? Carpeta { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Nombre original del archivo (incluye extension).</summary>
    public string NombreArchivoOriginal { get; set; } = string.Empty;

    public OrigenDocumento Origen { get; set; } = OrigenDocumento.Manual;

    /// <summary>FK al modulo de origen (ej: id de la asamblea, pqrsd, tarea). Nullable.</summary>
    public Guid? OrigenEntidadId { get; set; }

    public EstadoDocumento Estado { get; set; } = EstadoDocumento.Vigente;

    /// <summary>
    /// Visibilidad simplificada MVP:
    /// "PRIVADO" (solo creador y equipo admin),
    /// "EQUIPO" (todo el equipo de la copropiedad),
    /// "PUBLICO" (residentes con accesos al modulo).
    /// </summary>
    public string Visibilidad { get; set; } = "EQUIPO";

    public Guid? VersionActualId { get; set; }
    public DocumentoVersion? VersionActual { get; set; }

    /// <summary>Cantidad de versiones - se incrementa con NuevaVersion (RN-13).</summary>
    public int NumeroVersiones { get; set; } = 0;

    /// <summary>True si el usuario lo marca destacado a nivel copropiedad. Spec seccion 11.</summary>
    public bool Destacado { get; set; }

    /// <summary>Soft delete.</summary>
    public bool Activo { get; set; } = true;

    public Guid SubidoPorUsuarioId { get; set; }

    public ICollection<DocumentoVersion> Versiones { get; set; } = new List<DocumentoVersion>();
    public ICollection<DocumentoEtiqueta> Etiquetas { get; set; } = new List<DocumentoEtiqueta>();
}

/// <summary>
/// Version inmutable del documento. Cada subida nueva crea un registro.
/// El blob storage referenciado por UrlStorage NO se borra (RN-13).
/// </summary>
public class DocumentoVersion : TenantEntity
{
    public Guid DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    /// <summary>Numero de version secuencial dentro del documento (1, 2, 3...).</summary>
    public int Numero { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }

    /// <summary>Key/path en el blob storage (R2 o local).</summary>
    public string UrlStorage { get; set; } = string.Empty;

    /// <summary>SHA-256 hex del contenido. Garantiza inmutabilidad e integridad.</summary>
    public string HashSha256 { get; set; } = string.Empty;

    /// <summary>Notas opcionales del cambio entre versiones.</summary>
    public string? NotasCambio { get; set; }

    public Guid SubidoPorUsuarioId { get; set; }
}

/// <summary>
/// Etiqueta del catalogo - configurable por copropiedad. Spec 2.15 seccion 5.3.
///
/// - TenantId NULL + EsBase=true -> etiqueta global PropIA (7 base seedadas).
/// - TenantId con valor -> etiqueta custom del tenant.
/// </summary>
public class DocumentoEtiquetaCatalogo : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Color { get; set; }

    public bool EsBase { get; set; }
    public bool Activa { get; set; } = true;
}

/// <summary>Join entre Documento y DocumentoEtiquetaCatalogo. M:N.</summary>
public class DocumentoEtiqueta : TenantEntity
{
    public Guid DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    public Guid EtiquetaCatalogoId { get; set; }
    public DocumentoEtiquetaCatalogo? EtiquetaCatalogo { get; set; }
}

/// <summary>
/// "Pin" personal de un usuario sobre un documento. Spec 2.15 seccion 11.
/// Distinto de Documento.Destacado (que es a nivel copropiedad).
/// </summary>
public class DocumentoDestacadoPersonal : TenantEntity
{
    public Guid DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    public Guid UsuarioId { get; set; }
}

/// <summary>
/// Bitacora append-only del documento. Spec 2.15 seccion 15.1.
/// Trigger SQL BEFORE UPDATE/DELETE bloquea modificaciones (RN-15).
/// </summary>
public class DocumentoAuditoria : TenantEntity
{
    public Guid DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    public TipoEventoDocumento TipoEvento { get; set; }

    /// <summary>Snapshot opcional con el cambio (ej: {"versionAnterior":1,"versionNueva":2}).</summary>
    public string? DetalleJson { get; set; }

    public Guid UsuarioId { get; set; }
    public DateTimeOffset OcurridoAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Consumo del documento (vista/descarga). Spec 2.15 seccion 15.2.
/// Append-only - el motor de reportes 2.16 lo consumira para estadisticas.
/// Retencion 12 meses (por job nocturno futuro).
/// </summary>
public class DocumentoConsumo : TenantEntity
{
    public Guid DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    /// <summary>Version concreta accedida. Util para auditoria forense.</summary>
    public Guid VersionId { get; set; }
    public DocumentoVersion? Version { get; set; }

    public TipoEventoConsumo TipoEvento { get; set; }
    public DispositivoConsumo Dispositivo { get; set; } = DispositivoConsumo.Unknown;

    public Guid UsuarioId { get; set; }
    public DateTimeOffset OcurridoAt { get; set; } = DateTimeOffset.UtcNow;
}
