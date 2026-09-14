using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Propia.Application.Documents;

namespace Propia.Infrastructure.Documents;

/// <summary>
/// HTML -> PDF con Chromium headless (PuppeteerSharp). El navegador se descarga una vez
/// (BrowserFetcher) y se reutiliza entre llamadas. El tamano/margenes los define el CSS del
/// documento (@page + @media print), por eso se usa PreferCSSPageSize y margenes en 0.
/// Registrado como Singleton para no relanzar Chromium en cada request.
///
/// Anti-fuga de Chromium (solo dev): en produccion el contenedor muere entero y se lleva sus
/// procesos, pero en dev el host .NET se mata por PID (Stop-Process -Force / down.ps1), sin shutdown
/// limpio, asi que <see cref="DisposeAsync"/> NO corre y el proceso Chromium hijo queda HUERFANO. Con
/// muchos ciclos up/down se acumulan cientos de chrome.exe y cuelgan la maquina. Solucion per-instancia
/// y auto-sanadora: se guarda el PID del Chromium lanzado en un archivo unico por instancia; al arrancar,
/// antes de lanzar, se mata el PID que quedo de la corrida anterior (si sigue vivo y es un Chromium).
/// Es per-instancia a proposito: en el equipo varias sesiones comparten el MISMO binario de Chromium,
/// asi que matar "por ruta del binario" tumbaria el navegador vivo de otro agente; matar por PID propio
/// solo elimina el huerfano de esta misma instancia.
/// </summary>
public sealed class PuppeteerHtmlToPdfService : IHtmlToPdfService, IAsyncDisposable
{
    private readonly ILogger<PuppeteerHtmlToPdfService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IBrowser? _browser;

    public PuppeteerHtmlToPdfService(ILogger<PuppeteerHtmlToPdfService> log) => _log = log;

    public async Task<byte[]> RenderAsync(string html, CancellationToken ct)
    {
        var browser = await GetBrowserAsync(ct);
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
            Timeout = 30_000
        });
        var pdf = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = true,
            MarginOptions = new MarginOptions { Top = "0", Bottom = "0", Left = "0", Right = "0" }
        });
        return pdf;
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken ct)
    {
        if (_browser is { IsConnected: true }) return _browser;
        await _gate.WaitAsync(ct);
        try
        {
            if (_browser is { IsConnected: true }) return _browser;

            // El browser anterior se desconecto (crash/timeout): disponerlo antes de relanzar para no
            // dejarlo colgado (bug secundario de la fuga).
            if (_browser is not null)
            {
                try { await _browser.DisposeAsync(); } catch { /* best-effort */ }
                _browser = null;
            }

            // Auto-sanacion: mata el Chromium huerfano que esta misma instancia dejo en una corrida
            // anterior (host matado por PID sin DisposeAsync). Solo el propio, por PID.
            MatarChromiumHuerfanoPrevio();

            var opts = new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
            };

            // En prod (contenedor) se instala Chromium por apt y se apunta con PUPPETEER_EXECUTABLE_PATH,
            // evitando descargarlo en runtime. En dev (sin la env) se descarga con BrowserFetcher.
            var exe = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
            {
                opts.ExecutablePath = exe;
            }
            else
            {
                var fetcher = new BrowserFetcher();
                await fetcher.DownloadAsync();
            }

            _browser = await Puppeteer.LaunchAsync(opts);
            GuardarPidChromium(_browser.Process);
            _log.LogInformation("Chromium headless iniciado para generacion de PDF");
            return _browser;
        }
        finally { _gate.Release(); }
    }

    // Archivo de PID unico por instancia (keyed por el directorio base de la app): dos sesiones del
    // equipo en carpetas/worktrees distintos NO se pisan; nunca se mata el Chromium de otra instancia.
    private static string PidFilePath()
    {
        var key = AppContext.BaseDirectory;
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)))[..12];
        return Path.Combine(Path.GetTempPath(), $"propia-pdf-chromium-{hash}.pid");
    }

    // Se guarda PID + StartTime (ticks): el StartTime identifica de forma UNICA al proceso, de modo que
    // si el SO recicla el PID hacia otro proceso (incluido el chrome de otro agente) no lo confundamos
    // con el nuestro.
    private void GuardarPidChromium(Process? proc)
    {
        try
        {
            if (proc is null) return;
            long ticks;
            try { ticks = proc.StartTime.Ticks; } catch { ticks = 0; }
            File.WriteAllText(PidFilePath(), $"{proc.Id}|{ticks}");
        }
        catch (Exception ex) { _log.LogDebug(ex, "No se pudo registrar el PID del Chromium"); }
    }

    private void MatarChromiumHuerfanoPrevio()
    {
        try
        {
            var f = PidFilePath();
            if (!File.Exists(f)) return;
            var parts = File.ReadAllText(f).Trim().Split('|');
            if (parts.Length >= 1 && int.TryParse(parts[0], out var pid) && pid > 0)
            {
                var savedTicks = parts.Length >= 2 && long.TryParse(parts[1], out var t) ? t : 0L;
                try
                {
                    var p = Process.GetProcessById(pid);
                    var name = p.ProcessName.ToLowerInvariant();
                    var esChromium = name.Contains("chrome") || name.Contains("chromium");
                    long liveTicks;
                    try { liveTicks = p.StartTime.Ticks; } catch { liveTicks = -1; }
                    // Matar SOLO si es exactamente el mismo proceso (mismo StartTime) y es un Chromium.
                    // Si el StartTime no coincide, el PID fue reciclado por otro proceso -> NO tocar.
                    if (esChromium && savedTicks > 0 && liveTicks == savedTicks)
                    {
                        p.Kill(entireProcessTree: true);
                        _log.LogWarning("Se elimino un Chromium huerfano de una corrida anterior (PID {Pid})", pid);
                    }
                }
                catch (ArgumentException) { /* el PID ya no existe: nada que matar */ }
            }
            File.Delete(f);
        }
        catch (Exception ex) { _log.LogDebug(ex, "No se pudo limpiar el Chromium huerfano previo"); }
    }

    public async ValueTask DisposeAsync()
    {
        try { if (_browser is not null) await _browser.DisposeAsync(); }
        catch { /* best-effort al apagar */ }
        // Shutdown limpio: el browser ya se cerro, el PID-file queda obsoleto.
        try { var f = PidFilePath(); if (File.Exists(f)) File.Delete(f); } catch { }
        _gate.Dispose();
    }
}
