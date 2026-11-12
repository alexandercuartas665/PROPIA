using System.Globalization;

namespace Propia.Web.Components.Pages.Capa2.Spx;

/// <summary>Ayudas de formato y errores del modulo Servicios publicos (replica: numOrNull y el patron K-09).</summary>
public static class SpxFormato
{
    /// <summary>Numero escrito por el usuario (o vacio -> null). Como numOrNull de la replica: ignora lo que no es
    /// digito, punto o signo; acepta coma decimal.</summary>
    public static decimal? NumOrNull(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var s = new string(v.Trim().Where(c => char.IsAsciiDigit(c) || c is '.' or '-' or ',').ToArray()).Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    /// <summary>Mensaje {"error": "..."} del API (o el texto por defecto).</summary>
    public static async Task<string> ErrorDeAsync(HttpResponseMessage resp, string porDefecto)
    {
        if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden) return "No tienes permiso para esta acción.";
        try
        {
            var txt = await resp.Content.ReadAsStringAsync();
            if (System.Text.Json.JsonDocument.Parse(txt).RootElement.TryGetProperty("error", out var e) && e.GetString() is { Length: > 0 } m) return m;
        }
        catch { }
        return porDefecto;
    }
}
