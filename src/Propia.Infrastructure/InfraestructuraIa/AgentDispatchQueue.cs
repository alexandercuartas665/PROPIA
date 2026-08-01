using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propia.Application.InfraestructuraIa;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Procesador en background del agente de IA. Resuelve los tres problemas de concurrencia del
/// flujo entrante de WhatsApp:
///
/// 1) RESPUESTA RAPIDA AL WEBHOOK: el webhook solo persiste el mensaje y llama Enqueue (instantaneo),
///    asi Evolution recibe el 2xx enseguida y no reintenta por timeout (el dispatch tarda 20-40s).
/// 2) SERIALIZACION POR CONVERSACION: _processing garantiza un unico dispatch activo por
///    conversacion. Si entra otro mensaje mientras se procesa, se reencola y corre DESPUES.
/// 3) DEBOUNCE DE RAFAGAS: cada mensaje reinicia una ventana corta; cuando expira sin mensajes
///    nuevos, se procesa la conversacion UNA vez (el dispatcher reconstruye el contexto desde la
///    BD, que ya tiene todos los mensajes de la rafaga). Evita N respuestas a "hola/buenas/quiero...".
///
/// Conversaciones DISTINTAS corren en paralelo; la MISMA conversacion, en serie.
/// El estado vive en memoria (suficiente para un host single-instance, como es hoy PROPIA en
/// Railway). Escalar a varias instancias requeriria mover el lock/debounce a un store compartido.
/// Portado de CUBOT.travels.
/// </summary>
public sealed class AgentDispatchQueue : BackgroundService, IAgentDispatchQueue
{
    // Ventana de agrupacion de mensajes seguidos del mismo contacto. Cada mensaje nuevo la reinicia.
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(8);
    // Tope maximo de espera por rafaga (cliente que escribe sin parar): responde igual pasado esto.
    private static readonly TimeSpan MaxBurstWait = TimeSpan.FromSeconds(35);
    // Cada cuanto revisa la cola el bucle de fondo.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentDispatchQueue> _logger;

    private readonly ConcurrentDictionary<Guid, Pending> _pending = new();
    private readonly ConcurrentDictionary<Guid, byte> _processing = new();

    public AgentDispatchQueue(IServiceScopeFactory scopeFactory, ILogger<AgentDispatchQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private sealed class Pending
    {
        public Guid TenantId;
        public Guid? LineId;
        public readonly List<string> Bodies = new();
        public long ReadyAtTicks;
        public long DeadlineTicks; // tope maximo: primer mensaje + MaxBurstWait (no se reinicia)
        public readonly object Gate = new();
    }

    public void Enqueue(Guid tenantId, Guid conversationId, Guid? whatsAppLineId, string inboundBody)
    {
        var now = DateTimeOffset.UtcNow;
        var readyAt = now.Add(DebounceWindow).UtcTicks;
        _pending.AddOrUpdate(
            conversationId,
            _ =>
            {
                var p = new Pending { TenantId = tenantId, LineId = whatsAppLineId, ReadyAtTicks = readyAt, DeadlineTicks = now.Add(MaxBurstWait).UtcTicks };
                p.Bodies.Add(inboundBody ?? string.Empty);
                return p;
            },
            (_, p) =>
            {
                lock (p.Gate)
                {
                    p.TenantId = tenantId;
                    if (whatsAppLineId is not null) { p.LineId = whatsAppLineId; }
                    p.Bodies.Add(inboundBody ?? string.Empty);
                    p.ReadyAtTicks = readyAt; // reinicia la ventana de debounce
                    // DeadlineTicks NO se reinicia: es el tope desde el primer mensaje de la rafaga.
                }
                return p;
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentDispatchQueue iniciado (debounce {Debounce}s).", DebounceWindow.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { PumpOnce(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "AgentDispatchQueue: error en el bucle de despacho."); }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void PumpOnce(CancellationToken stoppingToken)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;

        foreach (var kvp in _pending)
        {
            var conversationId = kvp.Key;
            var pending = kvp.Value;

            long readyAt, deadline;
            lock (pending.Gate) { readyAt = pending.ReadyAtTicks; deadline = pending.DeadlineTicks; }
            // Procesa cuando expira la ventana de debounce O cuando se alcanza el tope de rafaga.
            if (readyAt > now && deadline > now) { continue; }
            if (_processing.ContainsKey(conversationId)) { continue; } // ya hay dispatch en curso

            if (!_pending.TryRemove(conversationId, out var claimed)) { continue; }

            Guid tenantId; Guid? lineId; string body;
            lock (claimed.Gate)
            {
                tenantId = claimed.TenantId;
                lineId = claimed.LineId;
                body = string.Join("\n", claimed.Bodies);
            }

            _processing[conversationId] = 1;
            _ = Task.Run(() => ProcessAsync(tenantId, conversationId, lineId, body, stoppingToken), stoppingToken);
        }
    }

    private async Task ProcessAsync(Guid tenantId, Guid conversationId, Guid? lineId, string body, CancellationToken stoppingToken)
    {
        try
        {
            // Scope propio: el dispatcher y sus dependencias (DbContext, etc.) son Scoped.
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IAgentDispatcher>();
            await dispatcher.DispatchAsync(tenantId, conversationId, lineId, body, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AgentDispatchQueue: dispatch fallo para conv {ConvId}.", conversationId);
        }
        finally
        {
            _processing.TryRemove(conversationId, out _);
        }
    }
}
