using System.Net;
using System.Text.Json;

namespace Propia.Web.Services;

/// <summary>
/// Reemplazo de HttpResponseMessage.EnsureSuccessStatusCode (Alex 2026-11-24): aquel lanzaba
/// "Response status code does not indicate success: 403 (Forbidden)." y la pantalla mostraba ese texto tecnico.
/// Este lanza HttpRequestException con el MENSAJE que manda el API en "error" (p.ej. "No tienes permiso para crear
/// en Tareas y Proyectos...") o, si no lo hay, uno claro segun el codigo (401 sesion vencida, 403 sin permiso).
/// Los catch existentes que muestran ex.Message pasan a mostrar ese mensaje sin otro cambio.
/// </summary>
public static class HttpRespuestas
{
    public static async Task AsegurarOkAsync(this HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        throw new HttpRequestException(await MensajeErrorAsync(resp), null, resp.StatusCode);
    }

    /// <summary>Mensaje para el usuario de una respuesta fallida: el "error" del API o uno claro segun el codigo.</summary>
    public static async Task<string> MensajeErrorAsync(HttpResponseMessage resp)
    {
        string? error = null;
        string cuerpo = "";
        try { cuerpo = (await resp.Content.ReadAsStringAsync()).Trim(); } catch { }
        if (cuerpo.Length > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(cuerpo);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String) error = e.GetString();
                    // ProblemDetails / validacion de ASP.NET: "title" o "detail".
                    else if (doc.RootElement.TryGetProperty("detail", out var dt) && dt.ValueKind == JsonValueKind.String) error = dt.GetString();
                    else if (doc.RootElement.TryGetProperty("title", out var tt) && tt.ValueKind == JsonValueKind.String) error = tt.GetString();
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.String) error = doc.RootElement.GetString();
            }
            catch
            {
                // Texto plano (BadRequest("...")): se muestra tal cual si es corto y no es una pagina HTML.
                if (cuerpo.Length <= 400 && !cuerpo.StartsWith('<')) error = cuerpo;
            }
        }

        // "forbidden" pelado (respuestas viejas) no le dice nada al usuario.
        if (!string.IsNullOrWhiteSpace(error) && !string.Equals(error, "forbidden", StringComparison.OrdinalIgnoreCase))
            return error!;
        return resp.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Tu sesi\u00f3n expir\u00f3. Recarga la p\u00e1gina (F5) o vuelve a iniciar sesi\u00f3n.",
            HttpStatusCode.Forbidden => "No tienes permiso para esta acci\u00f3n. P\u00eddele a un Administrador que lo habilite en "
                                        + "Usuarios y Accesos > Roles y permisos.",
            HttpStatusCode.NotFound => "No se encontr\u00f3 el registro (puede que otra persona lo haya eliminado).",
            _ => $"Ocurri\u00f3 un error ({(int)resp.StatusCode}). Intenta de nuevo."
        };
    }
}
