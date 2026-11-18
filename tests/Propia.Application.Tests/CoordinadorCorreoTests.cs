using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>
/// Lector de buzon: la respuesta del agente "Coordinador de correo" se interpreta de forma tolerante (bloques de codigo,
/// texto alrededor, confianza en %) CONTRA los temas permitidos del buzon (lista por copropiedad "correo.tema"). Lo que
/// no trae un tema de esa lista se rechaza (queda como error y se puede reclasificar) en vez de inventar un tema.
/// </summary>
public class CoordinadorCorreoTests
{
    private static readonly IReadOnlyList<TemaCorreoDef> Base = CoordinadorCorreoAgente.TemasBase;

    private static readonly IReadOnlyList<TemaCorreoDef> ConPropio = Base
        .Append(new TemaCorreoDef("c:asc", "Mantenimiento de ascensores", "Reportes y cotizaciones de la empresa de ascensores."))
        .ToList();

    [Fact]
    public void Parsear_json_limpio()
    {
        var r = CoordinadorCorreoAgente.Parsear(
            "{\"tema\":\"recibo_servicio\",\"confianza\":0.92,\"resumen\":\"Factura EPM octubre\",\"accion_sugerida\":\"Registrar en Servicios\",\"datos\":{\"servicio\":\"energia\",\"valor\":\"350000\"}}", Base);
        Assert.NotNull(r);
        Assert.Equal(TemasCorreoBase.ReciboServicio, r!.TemaClave);
        Assert.Equal(0.92m, r.Confianza);
        Assert.Equal("Factura EPM octubre", r.Resumen);
        Assert.Equal("Registrar en Servicios", r.AccionSugerida);
        Assert.Contains("energia", r.DatosJson);
    }

    [Fact]
    public void Parsear_tolera_bloque_de_codigo_y_texto_alrededor()
    {
        var r = CoordinadorCorreoAgente.Parsear("Claro, aqui va:\n```json\n{\"tema\":\"PQRSD\",\"confianza\":\"85\",\"datos\":{}}\n```", Base);
        Assert.NotNull(r);
        Assert.Equal(TemasCorreoBase.Pqrsd, r!.TemaClave);
        Assert.Equal(0.85m, r.Confianza);   // 85 (porcentaje) -> 0.85
        Assert.Null(r.DatosJson);           // datos vacio no se guarda
    }

    [Theory]
    [InlineData("poliza_contrato", TemasCorreoBase.PolizaContrato)]
    [InlineData("Poliza Contrato", TemasCorreoBase.PolizaContrato)]
    [InlineData("P\u00f3liza o contrato", TemasCorreoBase.PolizaContrato)]
    [InlineData("otro", TemasCorreoBase.Otro)]
    [InlineData("spam", TemasCorreoBase.Irrelevante)]
    [InlineData("recibo-servicio", TemasCorreoBase.ReciboServicio)]
    [InlineData("c:asc", "c:asc")]
    [InlineData("C:ASC", "c:asc")]
    [InlineData("Mantenimiento de ascensores", "c:asc")]
    public void ResolverTema_reconoce_clave_nombre_y_variantes(string valor, string esperado)
        => Assert.Equal(esperado, CoordinadorCorreoAgente.ResolverTema(valor, ConPropio));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no se que es")]
    [InlineData("{\"tema\":\"factura_compra\"}")]
    [InlineData("{\"tema\":")]
    public void Parsear_rechaza_sin_tema_conocido(string? texto)
        => Assert.Null(CoordinadorCorreoAgente.Parsear(texto, Base));

    [Fact]
    public void TemasDelBuzon_limita_a_los_marcados_mas_otro_e_irrelevante()
    {
        var temas = CoordinadorCorreoAgente.TemasDelBuzon(ConPropio, new[] { "c:asc" });
        Assert.Equal(new[] { TemasCorreoBase.Otro, TemasCorreoBase.Irrelevante, "c:asc" }, temas.Select(t => t.Clave).ToArray());

        // Un tema que el buzon no atiende se rechaza aunque exista en la copropiedad.
        Assert.Null(CoordinadorCorreoAgente.Parsear("{\"tema\":\"recibo_servicio\"}", temas));
        Assert.Equal("c:asc", CoordinadorCorreoAgente.Parsear("{\"tema\":\"c:asc\"}", temas)!.TemaClave);

        // Sin seleccion = todos los temas activos.
        Assert.Equal(ConPropio.Count, CoordinadorCorreoAgente.TemasDelBuzon(ConPropio, null).Count);
    }

    [Fact]
    public void ArmarPrompt_lista_temas_recorta_el_cuerpo_y_lista_adjuntos()
    {
        var cuerpo = new string('x', CoordinadorCorreoAgente.MaxCuerpoPrompt + 500);
        var p = CoordinadorCorreoAgente.ArmarPrompt(ConPropio, "EPM", "facturas@epm.com.co", "Su factura",
            new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero), new[] { "factura.pdf" }, cuerpo);
        Assert.Contains("TEMAS PERMITIDOS", p);
        Assert.Contains("- \"c:asc\": Mantenimiento de ascensores. Reportes y cotizaciones", p);
        Assert.Contains("De: EPM <facturas@epm.com.co>", p);
        Assert.Contains("Documentos adjuntos: factura.pdf", p);
        Assert.Contains("[...cuerpo recortado...]", p);
        Assert.True(p.Length < CoordinadorCorreoAgente.MaxCuerpoPrompt + 1800);
    }

    [Fact]
    public void DescripcionDeMeta_lee_la_descripcion_del_json()
    {
        Assert.Equal("Hola", CoordinadorCorreoAgente.DescripcionDeMeta("{\"descripcion\":\"Hola\"}"));
        Assert.Null(CoordinadorCorreoAgente.DescripcionDeMeta(null));
        Assert.Null(CoordinadorCorreoAgente.DescripcionDeMeta("{roto"));
    }
}
