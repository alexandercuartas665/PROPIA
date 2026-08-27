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
            _log.LogInformation("Chromium headless iniciado para generacion de PDF");
            return _browser;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        try { if (_browser is not null) await _browser.DisposeAsync(); }
        catch { /* best-effort al apagar */ }
        _gate.Dispose();
    }
}
