using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace Propia.Web.Components.Shared.IaIngesta;

/// <summary>Campo devuelto por /api/ocr/extraer-agente (ya normalizado en el servidor).</summary>
public sealed record IaCampo(string Nombre, string? Valor, double? Confianza);

/// <summary>Resultado de una lectura: claves del perfil que se aplicaron al formulario y las dudosas (confianza baja),
/// tokens gastados y la key del PDF origen (si se pidio guardarlo).</summary>
public sealed record IaResultado(IReadOnlyList<string> Aplicadas, IReadOnlyList<string> Dudosas, int Tokens, string? PdfOrigenKey);

/// <summary>
/// Proceso UNICO de lectura con el Agente Documental para Nuevo contrato y Nueva poliza (y "Actualizar desde un PDF").
/// Avanza la transicion (IaLectura) con los eventos REALES:
///  paso 0 Subiendo el archivo -> paso 1 Leyendo el texto del PDF (pdf.js; si el texto es pobre o no hay, se
///  rasterizan las paginas para vision) -> paso 2 Encontrando los datos (respuesta del agente; los chips salen de
///  los campos devueltos) -> paso 3 Llenando el formulario (aplicar) -> resumen.
/// El modulo solo aporta el perfil, como aplicar los campos a SU formulario y la etiqueta de cada clave.
/// </summary>
public static class IaIngestaLector
{
    private const double ConfianzaDudosa = 0.6;
    private const int PalabrasMinPorPagina = 40;   // mismo umbral que el servidor (OcrController.EsTextoPobre)

    private sealed record Info(string? Texto, int Paginas);
    private sealed record Raster(string[] Paginas, int Total);
    private sealed record Resp(List<IaCampo>? Campos, string? PdfOrigenKey, int InputTokens = 0, int OutputTokens = 0);
    private sealed record ErrorResp(string? Error);

    public static async Task<IaResultado?> LeerAsync(
        IaLectura ia, byte[] bytes, string mime, string fileName, string perfil,
        IJSRuntime js, Func<HttpClient> http, Func<HttpClient> httpLargo,
        Func<IReadOnlyList<IaCampo>, IReadOnlyList<string>> aplicar, Func<string, string?> etiqueta,
        string msgNoPdf, bool guardarPdfOrigen = false)
    {
        if (bytes.Length == 0 || !mime.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            ia.Fallo(msgNoPdf, "Solo PDF");
            return null;
        }
        try
        {
            // 0) Subiendo el archivo (el archivo ya esta en memoria: el paso dura su minimo de 450 ms).
            ia.Iniciar();
            var b64 = Convert.ToBase64String(bytes);

            // 1) Leyendo el texto del PDF (pdf.js). Texto pobre o vacio -> imagenes de las paginas (vision).
            await ia.PasoAsync(1, 38);
            var info = await js.InvokeAsync<Info?>("propiaPdfThumb.extraerTextoInfo", b64, mime);
            var texto = info?.Texto;
            var paginas = Math.Max(1, info?.Paginas ?? 1);
            string[]? imgs = null;
            var palabras = string.IsNullOrWhiteSpace(texto) ? 0 : texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            if (palabras / (double)Math.Min(paginas, 15) < PalabrasMinPorPagina)
            {
                var ras = await js.InvokeAsync<Raster?>("propiaPdfThumb.rasterizarPaginas", b64, 12);
                if (ras is { Paginas.Length: > 0 }) { imgs = ras.Paginas; paginas = Math.Max(paginas, ras.Total); }
                else if (palabras == 0) { ia.Fallo("No se pudo leer el PDF: no tiene texto ni paginas legibles."); return null; }
            }

            // 2) Encontrando los datos: el Agente Documental (1 reintento si no devuelve nada; la vision a veces falla).
            await ia.PasoAsync(2, 40);
            object body = new
            {
                Perfil = perfil,
                Texto = string.IsNullOrWhiteSpace(texto) ? null : texto,
                Imagenes = imgs,
                Paginas = paginas,
                PdfBase64 = guardarPdfOrigen ? b64 : null,
                Mime = guardarPdfOrigen ? mime : null,
                FileName = guardarPdfOrigen ? fileName : null,
            };
            Resp? r = null;
            for (var intento = 1; intento <= 2 && r?.Campos is not { Count: > 0 }; intento++)
            {
                var llamada = (imgs is not null ? httpLargo() : http()).PostAsJsonAsync("/api/ocr/extraer-agente", body);
                await ia.EsperarAsync(llamada);
                var resp = await llamada;
                if (!resp.IsSuccessStatusCode) { ia.Fallo("No se pudo leer el documento " + await DetalleErrorAsync(resp), "Error IA"); return null; }
                r = await resp.Content.ReadFromJsonAsync<Resp>();
            }
            var campos = (r?.Campos ?? new()).Where(c => !string.IsNullOrWhiteSpace(c.Valor)).ToList();
            var tokens = (r?.InputTokens ?? 0) + (r?.OutputTokens ?? 0);
            await ia.ChipsAsync(campos.Select(c => etiqueta(c.Nombre)).OfType<string>().Distinct().ToList());

            // 3) Llenando el formulario.
            await ia.PasoAsync(3, 96);
            var aplicadas = aplicar(campos);
            var dudosas = campos.Where(c => c.Confianza is double cf && cf < ConfianzaDudosa && aplicadas.Contains(c.Nombre)).Select(c => c.Nombre).Distinct().ToList();
            await ia.TerminarAsync(
                aplicadas.Select(etiqueta).OfType<string>().Distinct().ToList(),
                dudosas.Select(etiqueta).OfType<string>().Distinct().ToList(),
                tokens);
            return new IaResultado(aplicadas, dudosas, tokens, r?.PdfOrigenKey);
        }
        catch (TaskCanceledException) { ia.Fallo("La lectura tardó demasiado (tiempo agotado). Intenta de nuevo.", "Tiempo agotado"); return null; }
        catch (Exception ex) { ia.Fallo("No se pudo leer el documento: " + ex.Message, "Error IA"); return null; }
    }

    // Detalle legible del fallo ({ error = "..." }) en vez de un "IA no disponible" generico.
    private static async Task<string> DetalleErrorAsync(HttpResponseMessage resp)
    {
        string? det = null;
        try { det = (await resp.Content.ReadFromJsonAsync<ErrorResp>())?.Error; } catch { /* cuerpo no JSON (502/504) */ }
        det = det switch
        {
            "agente_documental_no_disponible" => "el Agente Documental no está activo en esta copropiedad (IA > Agentes).",
            "texto_o_imagenes_requerido" => "el PDF no tiene texto ni páginas legibles.",
            null or "" => resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "la sesión venció; recarga la página." : (resp.ReasonPhrase ?? "sin detalle del servidor."),
            _ => det
        };
        return $"({(int)resp.StatusCode}): {det}";
    }
}
