namespace Propia.Application.Documents;

/// <summary>
/// Renderiza un documento HTML (standalone, con su CSS y @page) a PDF con fidelidad WYSIWYG.
/// La implementacion usa un motor Chromium headless (PuppeteerSharp).
/// </summary>
public interface IHtmlToPdfService
{
    Task<byte[]> RenderAsync(string html, CancellationToken ct);
}
