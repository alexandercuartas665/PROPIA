namespace Propia.Domain.Enums;

/// <summary>
/// Canal por el cual se entrega una notificacion. T.2 motor de notificaciones (MVP).
/// </summary>
public enum CanalNotificacion
{
    InApp = 1,
    Email = 2,
    WhatsApp = 3,
    Push = 4
}

/// <summary>
/// Estado del ciclo de vida de una notificacion en T.2.
/// Encolada -> Enviando -> Enviado | Fallido (con reintentos automaticos)
/// InApp adicionalmente: Enviado -> Leido cuando el usuario abre la notificacion.
/// </summary>
public enum EstadoNotificacion
{
    Encolada = 1,
    Enviando = 2,
    Enviado = 3,
    Fallido = 4,
    Leido = 5,        // solo InApp
    Cancelado = 6
}

/// <summary>
/// Prioridad - influye en SLA de entrega y orden de reintentos.
/// </summary>
public enum PrioridadNotificacion
{
    Baja = 1,
    Normal = 2,
    Alta = 3,
    Critica = 4
}
