using Propia.Application.Notificaciones;
using Propia.Domain.Enums;

namespace Propia.Integration.Tests;

/// <summary>
/// Dispatcher stub para tests - marca todo Enviado sin tocar BD ni canales reales.
/// Cualquier test cuyo Service consuma INotificacionDispatcher debe registrarlo via:
///     sc.AddSingleton&lt;INotificacionDispatcher, FakeNotificacionDispatcher&gt;();
///
/// Justificacion: el dispatcher real necesita IConfiguration + ILogger + DbSet
/// Notificaciones funcional; en tests de cada modulo consumidor nos importa el
/// comportamiento del SERVICE consumidor, no la persistencia de T.2 (eso lo
/// validan los NotificacionesFlowTests).
/// </summary>
public sealed class FakeNotificacionDispatcher : INotificacionDispatcher
{
    public List<EnviarNotificacionRequest> Enviadas { get; } = new();

    public Task<ResultadoEnvioNotificacion> EnviarAsync(
        EnviarNotificacionRequest req, CancellationToken ct)
    {
        Enviadas.Add(req);
        return Task.FromResult(new ResultadoEnvioNotificacion(
            Guid.NewGuid(), EstadoNotificacion.Enviado, null));
    }

    public async Task<IReadOnlyList<ResultadoEnvioNotificacion>> EnviarLoteAsync(
        IEnumerable<EnviarNotificacionRequest> requests, CancellationToken ct)
    {
        var list = new List<ResultadoEnvioNotificacion>();
        foreach (var r in requests) list.Add(await EnviarAsync(r, ct));
        return list;
    }
}
