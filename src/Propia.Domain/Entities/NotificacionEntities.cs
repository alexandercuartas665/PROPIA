using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Notificacion persistente del motor T.2. Entidad global (TenantId nullable porque
/// notificaciones de Capa 0 / Capa 1 no pertenecen a una copropiedad).
///
/// MVP T.2:
///  - Dispatcher unico (NotificacionDispatcher) registra la notificacion en BD y la
///    despacha sincronamente por canal. Real WhatsApp/Email queda detras de flag de
///    configuracion (Notificaciones:Provider = "Stub" | "Sendgrid" | "WhatsAppCloud").
///  - Stub marca Enviado inmediatamente (reemplaza el "simulado" de 2.14).
///  - Reintentos manuales via endpoint (cron job diferido a Fase 2).
///
/// Append-only: una vez Enviado o Fallido, no se modifica salvo Leido (InApp).
/// </summary>
public class Notificacion : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? UsuarioDestinatarioId { get; set; }
    public Guid? PersonaDestinatariaId { get; set; }

    public CanalNotificacion Canal { get; set; }
    public PrioridadNotificacion Prioridad { get; set; } = PrioridadNotificacion.Normal;
    public EstadoNotificacion Estado { get; set; } = EstadoNotificacion.Encolada;

    /// <summary>Email o telefono o user_id segun canal. Validado en dispatcher.</summary>
    public string Destino { get; set; } = string.Empty;

    /// <summary>Asunto corto (email/in-app). Para WhatsApp se usa solo cuerpo.</summary>
    public string? Asunto { get; set; }

    /// <summary>Cuerpo plano. HTML solo permitido para email (campo CuerpoHtml).</summary>
    public string Cuerpo { get; set; } = string.Empty;

    /// <summary>Cuerpo HTML opcional solo para email.</summary>
    public string? CuerpoHtml { get; set; }

    /// <summary>JSON con metadata adicional - usado por consumidores para correlacionar.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>Codigo del modulo origen (ej. "2.14", "1.5", "2.9").</summary>
    public string? ModuloOrigenCodigo { get; set; }

    /// <summary>ID de la entidad origen (ComunicadoId, TareaId, PqrsdId, etc.).</summary>
    public Guid? EntidadOrigenId { get; set; }

    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTimeOffset? FechaProximoIntento { get; set; }
    public DateTimeOffset? FechaEnviado { get; set; }
    public DateTimeOffset? FechaLeido { get; set; }
}
