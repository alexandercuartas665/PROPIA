using System.Text.Json;
using Propia.Infrastructure.InfraestructuraIa;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests PUROS (sin BD ni red) del parser de plantillas de YCloud (modulo Plantillas de WhatsApp,
/// portado de ECOREX). Verifican el mapeo de un item real de YCloud (formato Meta: components
/// HEADER/BODY/FOOTER/BUTTONS) a <see cref="Propia.Application.InfraestructuraIa.YCloudTemplateItem"/>.
/// </summary>
public class PlantillasWhatsAppParserTests
{
    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ParseItem_lee_cuerpo_header_footer_variables_y_botones()
    {
        const string json = """
        {
          "name": "recordatorio_pago",
          "language": "es",
          "status": "APPROVED",
          "id": "tmpl_123",
          "category": "UTILITY",
          "components": [
            { "type": "HEADER", "format": "TEXT", "text": "Aviso de la administracion" },
            { "type": "BODY", "text": "Hola {{1}}, tu saldo es {{2}}.",
              "example": { "body_text": [ [ "Juan", "$120.000" ] ] } },
            { "type": "FOOTER", "text": "Copropiedad PROPIA" },
            { "type": "BUTTONS", "buttons": [
              { "type": "QUICK_REPLY", "text": "Ya pague" },
              { "type": "URL", "text": "Pagar", "url": "https://pagos.propia.co/{{1}}" }
            ] }
          ]
        }
        """;

        var item = YCloudTemplateParser.ParseItem(Parse(json));

        Assert.NotNull(item);
        Assert.Equal("recordatorio_pago", item!.Name);
        Assert.Equal("es", item.Language);
        Assert.Equal("APPROVED", item.Status);
        Assert.Equal("UTILITY", item.Category);
        Assert.Equal("TEXT", item.HeaderFormat);
        Assert.Equal("Aviso de la administracion", item.HeaderText);
        Assert.Equal("Hola {{1}}, tu saldo es {{2}}.", item.BodyText);
        Assert.Equal("Copropiedad PROPIA", item.FooterText);

        Assert.NotNull(item.VariableExamples);
        Assert.Equal(new[] { "Juan", "$120.000" }, item.VariableExamples!);

        Assert.NotNull(item.Buttons);
        Assert.Equal(2, item.Buttons!.Count);
        Assert.Equal("QUICK_REPLY", item.Buttons[0].Type);
        Assert.False(item.Buttons[0].HasUrlVariable);
        Assert.Equal("URL", item.Buttons[1].Type);
        Assert.Equal("https://pagos.propia.co/{{1}}", item.Buttons[1].Url);
        Assert.True(item.Buttons[1].HasUrlVariable);
    }

    [Fact]
    public void ParseItem_devuelve_null_si_no_hay_nombre()
    {
        const string json = """{ "language": "es", "status": "APPROVED" }""";
        Assert.Null(YCloudTemplateParser.ParseItem(Parse(json)));
    }

    [Fact]
    public void ParseItem_sin_componentes_deja_contenido_nulo_pero_conserva_metadatos()
    {
        const string json = """{ "name": "saludo", "language": "en_US", "status": "PENDING" }""";
        var item = YCloudTemplateParser.ParseItem(Parse(json));

        Assert.NotNull(item);
        Assert.Equal("saludo", item!.Name);
        Assert.Equal("PENDING", item.Status);
        Assert.Null(item.BodyText);
        Assert.Null(item.HeaderFormat);
        Assert.Null(item.Buttons);
        Assert.Null(item.VariableExamples);
    }
}
