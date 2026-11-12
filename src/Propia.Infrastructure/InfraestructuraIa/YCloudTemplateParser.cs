using System.Text.Json;
using Propia.Application.InfraestructuraIa;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Parseo PURO (sin red) de un item de plantilla como lo devuelve YCloud en items[] (formato Meta:
/// name/language/status/category + components HEADER/BODY/FOOTER/BUTTONS). Aislado del cliente HTTP
/// para poder testearlo con payloads reales. Las variables del cuerpo son POSICIONALES ({{1}}...) y sus
/// ejemplos se leen de example.body_text[0]. Portado de ECOREX (YCloudTemplateParser).
/// </summary>
public static class YCloudTemplateParser
{
    /// <summary>Parsea un item. Devuelve null si no trae name (item invalido).</summary>
    public static YCloudTemplateItem? ParseItem(JsonElement it)
    {
        var name = Str(it, "name");
        if (string.IsNullOrWhiteSpace(name)) { return null; }

        var (headerFormat, headerText, bodyText, footerText, varExamples, buttons) = ParseComponents(it);
        return new YCloudTemplateItem(
            name!,
            Str(it, "language"),
            Str(it, "status") ?? "UNKNOWN",
            Str(it, "id"),
            Str(it, "rejectedReason") ?? Str(it, "qualityScore"),
            Str(it, "category"),
            headerFormat, headerText, bodyText, footerText, varExamples, buttons);
    }

    private static (string? headerFormat, string? headerText, string? bodyText, string? footerText,
        IReadOnlyList<string>? varExamples, IReadOnlyList<YCloudTemplateButton>? buttons)
        ParseComponents(JsonElement it)
    {
        string? headerFormat = null, headerText = null, bodyText = null, footerText = null;
        List<string>? varExamples = null;
        List<YCloudTemplateButton>? buttons = null;
        if (!it.TryGetProperty("components", out var comps) || comps.ValueKind != JsonValueKind.Array)
        {
            return (null, null, null, null, null, null);
        }
        foreach (var c in comps.EnumerateArray())
        {
            switch (Str(c, "type")?.ToUpperInvariant())
            {
                case "HEADER":
                    headerFormat = Str(c, "format")?.ToUpperInvariant();
                    if (string.Equals(headerFormat, "TEXT", StringComparison.Ordinal)) { headerText = Str(c, "text"); }
                    break;
                case "BODY":
                    bodyText = Str(c, "text");
                    if (c.TryGetProperty("example", out var ex) && ex.ValueKind == JsonValueKind.Object
                        && ex.TryGetProperty("body_text", out var bt) && bt.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var group in bt.EnumerateArray())
                        {
                            if (group.ValueKind != JsonValueKind.Array) { continue; }
                            varExamples = new List<string>();
                            foreach (var v in group.EnumerateArray())
                            {
                                varExamples.Add(v.ValueKind == JsonValueKind.String ? (v.GetString() ?? string.Empty) : v.ToString());
                            }
                            break; // solo el primer grupo de ejemplos
                        }
                    }
                    break;
                case "FOOTER":
                    footerText = Str(c, "text");
                    break;
                case "BUTTONS":
                    buttons = ParseButtons(c);
                    break;
            }
        }
        return (headerFormat, headerText, bodyText, footerText, varExamples, buttons);
    }

    /// <summary>Lee el arreglo buttons[] de un componente BUTTONS (formato Meta). Un boton URL con "{{" en la url
    /// es DINAMICO (sufijo variable por envio). Devuelve null si no hay botones.</summary>
    private static List<YCloudTemplateButton>? ParseButtons(JsonElement comp)
    {
        if (!comp.TryGetProperty("buttons", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var list = new List<YCloudTemplateButton>();
        foreach (var b in arr.EnumerateArray())
        {
            if (b.ValueKind != JsonValueKind.Object) { continue; }
            var type = Str(b, "type")?.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(type)) { continue; }
            var url = Str(b, "url");
            var hasVar = !string.IsNullOrWhiteSpace(url) && url!.Contains("{{", StringComparison.Ordinal);
            list.Add(new YCloudTemplateButton(type!, Str(b, "text"), url, Str(b, "phone_number"), hasVar));
        }
        return list.Count > 0 ? list : null;
    }

    private static string? Str(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
