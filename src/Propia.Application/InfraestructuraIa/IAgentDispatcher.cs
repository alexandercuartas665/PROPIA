namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Despachador del agente de IA: por cada mensaje ENTRANTE decide si un agente debe responder
/// automaticamente y, si aplica, arma el contexto, corre la inferencia y envia la respuesta por
/// la linea. Es el corazon de "poner operativo" el modulo: sin esto los entrantes se guardan y se
/// ven en la bandeja, pero ningun agente contesta. Portado de CUBOT.travels (sin el acoplamiento
/// CRM: no crea leads ni pedidos de ventas).
///
/// Corre SIN JWT de usuario (lo dispara el webhook), asi que fija el tenant en contexto con
/// ITenantContext.SetTenant antes de consultar (habilita el query filter + la RLS de Postgres).
/// </summary>
public interface IAgentDispatcher
{
    /// <summary>
    /// Atiende una conversacion tras uno o varios entrantes agrupados. inboundBody es el texto
    /// combinado de la rafaga (defensa; el contexto real se reconstruye desde la BD).
    /// </summary>
    Task DispatchAsync(Guid tenantId, Guid conversationId, Guid? whatsAppLineId, string inboundBody, CancellationToken ct = default);
}

/// <summary>
/// Cola en background del despacho del agente. El webhook solo persiste el entrante y llama
/// Enqueue (instantaneo) para responder rapido; la cola agrupa rafagas (debounce) y serializa por
/// conversacion, y luego corre el IAgentDispatcher en un scope propio. Portado de CUBOT.travels.
/// </summary>
public interface IAgentDispatchQueue
{
    /// <summary>Encola un entrante para despacho. Reinicia la ventana de agrupacion de la conversacion.</summary>
    void Enqueue(Guid tenantId, Guid conversationId, Guid? whatsAppLineId, string inboundBody);
}
