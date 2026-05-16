using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Plantilla reutilizable de comunicado. Spec 2.14 v1.0 seccion 9.
///
/// - TenantId nullable: si NULL -> plantilla global PropIA (es_global=true).
///   Estas se siembran via migracion y no se editan/borran (RN-06) - solo se clonan.
/// - TenantId con valor -> plantilla de la copropiedad creada por el admin.
/// </summary>
public class ComunicadoPlantilla : BaseEntity
{
    /// <summary>NULL si es global, valor si es de la copropiedad.</summary>
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public TipoComunicadoBase TipoComunicado { get; set; }

    /// <summary>Asunto modelo con variables tipo "{{fecha}}". Plain text.</summary>
    public string AsuntoModelo { get; set; } = string.Empty;

    /// <summary>Cuerpo modelo HTML con variables sustituibles.</summary>
    public string CuerpoModelo { get; set; } = string.Empty;

    public bool AcusePorDefecto { get; set; }

    /// <summary>True = plantilla PropIA. Se siembra via migracion y es inmutable.</summary>
    public bool EsGlobal { get; set; }

    /// <summary>Soft delete (RN-11) - plantilla con comunicados activos no se elimina.</summary>
    public bool Activa { get; set; } = true;

    public Guid? CreadoPorUsuarioId { get; set; }
}

/// <summary>
/// Comunicado - entidad central del modulo. Spec 2.14 v1.0 seccion 4 y 21.
/// Estados: Borrador -> Programado/Enviando -> Enviado/Cancelado (terminales).
/// Inmutable post-envio (RN-04) - validado por trigger SQL.
/// </summary>
public class Comunicado : TenantEntity
{
    public Guid? PlantillaId { get; set; }
    public ComunicadoPlantilla? Plantilla { get; set; }

    public TipoComunicadoBase TipoComunicado { get; set; }

    public string Asunto { get; set; } = string.Empty;

    /// <summary>Contenido enriquecido (HTML).</summary>
    public string CuerpoHtml { get; set; } = string.Empty;

    /// <summary>Version texto plano para WhatsApp (resumen max 500 chars).</summary>
    public string CuerpoTextoPlano { get; set; } = string.Empty;

    public bool RequiereAcuse { get; set; }

    public EstadoComunicado Estado { get; set; } = EstadoComunicado.Borrador;

    /// <summary>NULL = envio inmediato; con valor = programado.</summary>
    public DateTimeOffset? FechaProgramada { get; set; }

    /// <summary>Cuando inicio el envio real.</summary>
    public DateTimeOffset? FechaEnvio { get; set; }

    /// <summary>Cuando termino la cola.</summary>
    public DateTimeOffset? FechaCompletado { get; set; }

    public int? TotalDestinatarios { get; set; }
    public int? TotalEntregados { get; set; }
    public int? TotalFallidos { get; set; }

    /// <summary>FK a 2.15 Documentos si fue archivado (modulo no construido aun).</summary>
    public Guid? ArchivadoDocId { get; set; }

    public Guid CreadoPorUsuarioId { get; set; }
    public Guid? CanceladoPorUsuarioId { get; set; }
    public DateTimeOffset? CanceladoAt { get; set; }

    public ICollection<ComunicadoSegmento> Segmentos { get; set; } = new List<ComunicadoSegmento>();
    public ICollection<ComunicadoAdjunto> Adjuntos { get; set; } = new List<ComunicadoAdjunto>();
    public ICollection<ComunicadoDestinatario> Destinatarios { get; set; } = new List<ComunicadoDestinatario>();
}

/// <summary>Capa de segmentacion aplicada a un comunicado. Spec seccion 6 y 21.</summary>
public class ComunicadoSegmento : TenantEntity
{
    public Guid ComunicadoId { get; set; }
    public Comunicado? Comunicado { get; set; }

    public TipoSegmento TipoSegmento { get; set; }

    /// <summary>Valor JSON segun TipoSegmento (ej {"tipo":"Propietarios"} o {"torreId":"..."} ).</summary>
    public string ValorJson { get; set; } = "{}";
}

/// <summary>Adjunto del comunicado. Max 5 archivos x 10 MB (spec seccion 16).</summary>
public class ComunicadoAdjunto : TenantEntity
{
    public Guid ComunicadoId { get; set; }
    public Comunicado? Comunicado { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public string UrlStorage { get; set; } = string.Empty;
    public Guid SubidoPorUsuarioId { get; set; }
}

/// <summary>
/// Destinatario individual del comunicado. Generado al enviar.
/// Token UUID v4 unico es la clave del acuse (RN-08). Spec seccion 10.2.
/// </summary>
public class ComunicadoDestinatario : TenantEntity
{
    public Guid ComunicadoId { get; set; }
    public Comunicado? Comunicado { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    /// <summary>Token unico, presente en la URL https://app.propia.co/c/{token}.</summary>
    public Guid Token { get; set; } = Guid.NewGuid();

    public DateTimeOffset TokenExpiraAt { get; set; }

    public EstadoEntregaDestinatario EstadoEntrega { get; set; } = EstadoEntregaDestinatario.Pendiente;
    public DateTimeOffset? FechaEntrega { get; set; }

    public ICollection<ComunicadoAcuse> Acuses { get; set; } = new List<ComunicadoAcuse>();
}

/// <summary>
/// Acuse de recibo - append-only. Trigger SQL bloquea UPDATE y DELETE.
/// Spec seccion 11. Se registra en el GET /c/{token} antes de servir el contenido.
/// </summary>
public class ComunicadoAcuse : TenantEntity
{
    public Guid ComunicadoId { get; set; }
    public Comunicado? Comunicado { get; set; }

    public Guid DestinatarioId { get; set; }
    public ComunicadoDestinatario? Destinatario { get; set; }

    public Guid PersonaId { get; set; }

    public DateTimeOffset AbiertoAt { get; set; } = DateTimeOffset.UtcNow;

    public DispositivoAcuse Dispositivo { get; set; } = DispositivoAcuse.Unknown;
}
