using System.Text.Json;

namespace Propia.Web.Components.Shared;

/// <summary>
/// Una opcion de una lista de seleccion (campo del sistema o campo propio). Formato UNICO y compartido
/// por el gestor de campos, el render de valores y el panel de Unidades.
///
/// K = texto visible. Oculta = la opcion no se ofrece pero las filas que ya la usan la conservan.
/// Color = valor de color del chip (de <see cref="PaletaChips"/>); null = chip neutro.
///
/// Se serializa como JSON `[{"K":"...","Oculta":false,"Color":"#6D4FE3"}]`. Se PARSEA tolerando el
/// formato legado (una opcion por linea, sin color/oculta) que usaban los campos propios, para no
/// romper datos ya guardados. Los lectores de la plantilla/import solo miran K y Oculta e ignoran
/// props desconocidas, asi que agregar Color no los afecta.
/// </summary>
public sealed record OpcionCampo(string K, bool Oculta = false, string? Color = null)
{
    /// <summary>Parsea el string de opciones (JSON nuevo o texto-por-linea legado).</summary>
    public static List<OpcionCampo> Parse(string? raw)
    {
        var res = new List<OpcionCampo>();
        if (string.IsNullOrWhiteSpace(raw)) return res;
        var t = raw.TrimStart();
        if (t.StartsWith('['))
        {
            try
            {
                // Array JSON: cada elemento puede ser un OBJETO {K,Oculta,Color} (formato nuevo) o un STRING
                // (formato legado de algunos modulos, p.ej. etiquetas del Directorio = ["Proveedor","Apoderado"]).
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) res.Add(new OpcionCampo(s!.Trim()));
                        }
                        else if (el.ValueKind == JsonValueKind.Object)
                        {
                            string? k = (el.TryGetProperty("K", out var kp) || el.TryGetProperty("k", out kp)) && kp.ValueKind == JsonValueKind.String ? kp.GetString() : null;
                            if (string.IsNullOrWhiteSpace(k)) continue;
                            var oculta = (el.TryGetProperty("Oculta", out var op) || el.TryGetProperty("oculta", out op)) && op.ValueKind == JsonValueKind.True;
                            string? color = (el.TryGetProperty("Color", out var cp) || el.TryGetProperty("color", out cp)) && cp.ValueKind == JsonValueKind.String ? cp.GetString() : null;
                            res.Add(new OpcionCampo(k!.Trim(), oculta, string.IsNullOrWhiteSpace(color) ? null : color));
                        }
                    }
                }
            }
            catch { /* JSON corrupto: se cae a lista vacia */ }
            return res;
        }
        // Legado: una opcion por linea, sin color ni oculta.
        foreach (var l in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            res.Add(new OpcionCampo(l));
        return res;
    }

    /// <summary>Serializa a JSON para guardar en la columna Opciones.</summary>
    public static string Serializar(IEnumerable<OpcionCampo> opciones)
        => JsonSerializer.Serialize(opciones.ToList());

    /// <summary>Solo los textos visibles (para dropdowns de captura de valor).</summary>
    public static string[] VisiblesK(string? raw)
        => Parse(raw).Where(o => !o.Oculta).Select(o => o.K).ToArray();
}
