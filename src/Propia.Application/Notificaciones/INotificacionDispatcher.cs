using Propia.Domain.Enums;

namespace Propia.Application.Notificaciones;

/// <summary>
/// Punto de entrada unico para enviar notificaciones desde cualquier modulo (T.2).
///
/// Patron:
///  - Persistencia atomica: el dispatcher graba la Notificacion en BD ANTES de invocar
///    al canal real (asi siempre queda rastro auditable, incluso si el canal cae).
///  - Provider selector: segun "Notificaciones:Provider" (Stub | Sendgrid | WhatsAppCloud)
///    se invoca el adapter apropiado. Stub es default - reemplaza los "simulados"
///    diseminados en 9 modulos por un unico punto centralizado.
///  - Idempotente: si EntidadOrigenId + ModuloOrigenCodigo + UsuarioDestinatarioId
///    ya existen como notificacion Enviado/Encolada, devuelve la existente.
///
/// MVP: ejecucion sincrona. Fase 2: cola Redis + worker async + reintentos con backoff.
/// </summary>
public interface INotificacionDispatcher
{
    Task<ResultadoEnvioNotificacion> EnviarAsync(EnviarNotificacionRequest req, CancellationToken ct);

    /// <summary>Envio en lote - util para broadcasts grandes (2.14, 1.5).</summary>
    Task<IReadOnlyList<ResultadoEnvioNotificacion>> EnviarLoteAsync(
        IEnumerable<EnviarNotificacionRequest> requests, CancellationToken ct);

    /// <summary>
    /// Notifica un evento a un usuario (persona) por TODOS sus canales: InApp (inbox) +
    /// cada contacto activo que configuro en Mi Perfil (correos/telefonos). Si no configuro
    /// contactos, cae al correo de la Persona. No notifica al propio actor de la accion.
    /// Punto unico para "me asignaron / me mencionaron / me crearon algo".
    /// </summary>
    Task EnviarEventoUsuarioAsync(
        Guid personaId, string asunto, string cuerpo, string moduloOrigen, Guid? entidadOrigenId,
        Guid? tenantId = null, PrioridadNotificacion prioridad = PrioridadNotificacion.Normal,
        CancellationToken ct = default);
}

/// <summary>
/// Servicio de lectura del inbox y operaciones sobre notificaciones ya despachadas.
/// Separado del dispatcher para mantener responsabilidades claras (CQRS-light).
/// </summary>
public interface INotificacionesService
{
    Task<IReadOnlyList<NotificacionDto>> ListarAsync(FiltroNotificacionesRequest filtro, CancellationToken ct);
    Task<NotificacionDto?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Marca una notificacion InApp como Leido por el destinatario actual.</summary>
    Task<bool> MarcarLeidoAsync(Guid id, CancellationToken ct);

    /// <summary>Reintenta una notificacion Fallida (resetea contadores y vuelve a Encolada).</summary>
    Task<ResultadoEnvioNotificacion> ReintentarAsync(Guid id, CancellationToken ct);

    Task<ResumenNotificacionesDto> GetResumenAsync(CancellationToken ct);
}
