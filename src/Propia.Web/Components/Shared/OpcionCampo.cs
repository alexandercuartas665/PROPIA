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
                var arr = JsonSerializer.Deserialize<List<OpcionCampo>>(raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                foreach (var o in arr ?? new())
                    if (!string.IsNullOrWhiteSpace(o.K))
                        res.Add(o with { K = o.K.Trim(), Color = string.IsNullOrWhiteSpace(o.Color) ? null : o.Color });
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
