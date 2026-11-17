using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>
/// Lector de buzon (Ola 1): la respuesta del agente "Coordinador de correo" se interpreta de forma tolerante (bloques
/// de codigo, texto alrededor, confianza en %), y lo que no trae un tema conocido se rechaza (queda como error y se
/// puede reclasificar) en vez de inventar un tema.
/// </summary>
public class CoordinadorCorreoTests
{
    [Fact]
    public void Parsear_json_limpio()
    {
        var r = CoordinadorCorreoAgente.Parsear(
            "{\"tema\":\"recibo_servicio\",\"confianza\":0.92,\"resumen\":\"Factura EPM octubre\",\"accion_sugerida\":\"Registrar en Servicios\",\"datos\":{\"servicio\":\"energia\",\"valor\":\"350000\"}}");
        Assert.NotNull(r);
        Assert.Equal(TemaCorreo.ReciboServicio, r!.Tema);
        Assert.Equal(0.92m, r.Confianza);
        Assert.Equal("Factura EPM octubre", r.Resumen);
        Assert.Equal("Registrar en Servicios", r.AccionSugerida);
        Assert.Contains("energia", r.DatosJson);
    }

    [Fact]
    public void Parsear_tolera_bloque_de_codigo_y_texto_alrededor()
    {
        var r = CoordinadorCorreoAgente.Parsear("Claro, aqui va:\n```json\n{\"tema\":\"PQRSD\",\"confianza\":\"85\",\"datos\":{}}\n```");
        Assert.NotNull(r);
        Assert.Equal(TemaCorreo.Pqrsd, r!.Tema);
        Assert.Equal(0.85m, r.Confianza);   // 85 (porcentaje) -> 0.85
        Assert.Null(r.DatosJson);           // datos vacio no se guarda
    }

    [Theory]
    [InlineData("poliza_contrato", TemaCorreo.PolizaContrato)]
    [InlineData("Poliza Contrato", TemaCorreo.PolizaContrato)]
    [InlineData("otro", TemaCorreo.Otro)]
    [InlineData("irrelevante", TemaCorreo.Irrelevante)]
    [InlineData("recibo-servicio", TemaCorreo.ReciboServicio)]
    public void TemaDesdeClave_reconoce_variantes(string clave, TemaCorreo esperado)
        => Assert.Equal(esperado, CoordinadorCorreoAgente.TemaDesdeClave(clave));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no se que es")]
    [InlineData("{\"tema\":\"factura_compra\"}")]
    [InlineData("{\"tema\":")]
    public void Parsear_rechaza_sin_tema_conocido(string? texto)
        => Assert.Null(CoordinadorCorreoAgente.Parsear(texto));

    [Fact]
    public void ArmarPrompt_recorta_el_cuerpo_y_lista_adjuntos()
    {
        var cuerpo = new string('x', CoordinadorCorreoAgente.MaxCuerpoPrompt + 500);
        var p = CoordinadorCorreoAgente.ArmarPrompt("EPM", "facturas@epm.com.co", "Su factura",
            new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero), new[] { "factura.pdf" }, cuerpo);
        Assert.Contains("De: EPM <facturas@epm.com.co>", p);
        Assert.Contains("Documentos adjuntos: factura.pdf", p);
        Assert.Contains("[...cuerpo recortado...]", p);
        Assert.True(p.Length < CoordinadorCorreoAgente.MaxCuerpoPrompt + 400);
    }
}
