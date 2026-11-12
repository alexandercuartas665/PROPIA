using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Asambleas;

namespace Propia.Api.Controllers;

/// <summary>
/// Webhook de LiveKit (ola 5 de Asambleas). Sin login: la autenticidad la da el JWT del header Authorization, firmado con
/// el ApiSecret y con el hash del cuerpo (IVideoSalaProvider.LeerWebhook). La copropiedad sale del nombre de la sala
/// ("asm-{tenant}-{sesion}") y se fija antes de tocar datos (RLS). Configurar en LiveKit: {API}/api/livekit/webhook.
/// </summary>
[ApiController]
[Route("api/livekit/webhook")]
[AllowAnonymous]
public class LiveKitWebhookController : ControllerBase
{
    private readonly IVideoSalaProvider _video;
    private readonly IAsambleaService _svc;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LiveKitWebhookController> _log;

    public LiveKitWebhookController(IVideoSalaProvider video, IAsambleaService svc, IServiceScopeFactory scopes, ILogger<LiveKitWebhookController> log)
    { _video = video; _svc = svc; _scopes = scopes; _log = log; }

    [HttpPost]
    public async Task<IActionResult> Recibir(CancellationToken ct)
    {
        string cuerpo;
        using (var r = new StreamReader(Request.Body, Encoding.UTF8)) cuerpo = await r.ReadToEndAsync(ct);
        var ev = _video.LeerWebhook(cuerpo, Request.Headers.Authorization.ToString());
        if (ev is null) return Unauthorized();
        if (!_video.TryLeerSala(ev.Sala, out var tenantId, out var sesionId)) return Ok();   // sala ajena a PROPIA
        if (!await _svc.ActivarTenantPublicoAsync(tenantId, ct)) return Ok();

        try
        {
            await _svc.ConfirmarSalidasPendientesAsync(sesionId, ct);   // barrido perezoso de gracias vencidas
            if (await _svc.ProcesarEventoSalaAsync(sesionId, ev, ct) is { } vence)
                ProgramarConfirmacion(tenantId, sesionId, vence);
        }
        catch (InvalidOperationException ex) { _log.LogWarning(ex, "Webhook LiveKit {Evento} sesion {Sesion}", ev.Evento, sesionId); }
        return Ok();
    }

    /// <summary>Confirma la salida cuando vence la gracia de reconexion (fuera del request: scope propio + tenant fijado).</summary>
    private void ProgramarConfirmacion(Guid tenantId, Guid sesionId, DateTimeOffset vence)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var espera = vence - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
                if (espera > TimeSpan.Zero) await Task.Delay(espera);
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IAsambleaService>();
                if (!await svc.ActivarTenantPublicoAsync(tenantId, CancellationToken.None)) return;
                await svc.ConfirmarSalidasPendientesAsync(sesionId, CancellationToken.None);
            }
            catch (Exception ex) { _log.LogWarning(ex, "No se pudo confirmar la salida de la sala (sesion {Sesion})", sesionId); }
        });
    }
}
