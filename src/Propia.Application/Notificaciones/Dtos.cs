using Propia.Domain.Enums;

namespace Propia.Application.Notificaciones;

/// <summary>
/// Payload que cualquier modulo envia a T.2 para despachar una notificacion.
///
/// Reglas:
///  - Si UsuarioDestinatarioId tiene valor, el dispatcher resuelve Destino segun canal:
///    InApp -> usuario, Email -> persona.email, WhatsApp -> persona.celular.
///  - Si Destino esta seteado directamente (ej. email externo), se usa tal cual.
///  - Para multiples destinatarios, el llamador debe iterar (T.2 maneja N notificaciones
///    individuales para tener trazabilidad por destinatario - patron usado en 2.14).
/// </summary>
public record EnviarNotificacionRequest(
    CanalNotificacion Canal,
    string Cuerpo,
    Guid? TenantId = null,
    Guid? UsuarioDestinatarioId = null,
    Guid? PersonaDestinatariaId = null,
    string? Destino = null,
    string? Asunto = null,
    string? CuerpoHtml = null,
    PrioridadNotificacion Prioridad = PrioridadNotificacion.Normal,
    string? ModuloOrigenCodigo = null,
    Guid? EntidadOrigenId = null,
    string? MetadataJson = null);

/// <summary>Resultado del envio (sincrono para stub, async-ready para providers reales).</summary>
public record ResultadoEnvioNotificacion(
    Guid NotificacionId,
    EstadoNotificacion Estado,
    string? Error);

public record NotificacionDto(
    Guid Id,
    Guid? TenantId,
    Guid? UsuarioDestinatarioId,
    Guid? PersonaDestinatariaId,
    CanalNotificacion Canal,
    PrioridadNotificacion Prioridad,
    EstadoNotificacion Estado,
    string Destino,
    string? Asunto,
    string Cuerpo,
    string? ModuloOrigenCodigo,
    Guid? EntidadOrigenId,
    int Intentos,
    string? UltimoError,
    DateTimeOffset? FechaEnviado,
    DateTimeOffset? FechaLeido,
    DateTimeOffset CreadoAt);

public record FiltroNotificacionesRequest(
    EstadoNotificacion? Estado = null,
    CanalNotificacion? Canal = null,
    string? ModuloOrigenCodigo = null,
    int Limite = 100);

public record ResumenNotificacionesDto(
    int Encoladas,
    int Enviadas,
    int Fallidas,
    int InAppNoLeidas);
