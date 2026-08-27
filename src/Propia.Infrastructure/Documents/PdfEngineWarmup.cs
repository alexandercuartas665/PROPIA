using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propia.Application.Documents;

namespace Propia.Infrastructure.Documents;

/// <summary>
/// Precalienta el motor HTML->PDF al arrancar: descarga Chromium (una vez) y lanza el navegador
/// en segundo plano, para que la primera generacion de PDF de un usuario no espere la descarga.
/// No bloquea el arranque; si falla, se reintenta al primer uso real.
/// </summary>
public sealed class PdfEngineWarmup : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<PdfEngineWarmup> _log;

    public PdfEngineWarmup(IServiceProvider sp, ILogger<PdfEngineWarmup> log)
    {
        _sp = sp;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var svc = _sp.GetRequiredService<IHtmlToPdfService>();
                await svc.RenderAsync("<!doctype html><html><body>warmup</body></html>", CancellationToken.None);
                _log.LogInformation("Motor PDF (Chromium) precalentado y listo");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo precalentar el motor PDF; se reintentara en el primer uso");
            }
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
