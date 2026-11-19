using Propia.Application.Integraciones;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>
/// PQRSD por correo (Ola P1): la respuesta del agente "Radicador de PQRSD" se interpreta contra los tipos y categorias de
/// la copropiedad (sin inventar); la descripcion del radicado es el texto del remitente sin las citas de correos
/// anteriores y dentro de los limites del radicado; la unidad escrita se une al codigo TORRE-NUMERO solo si no hay duda.
/// </summary>
public class RadicadorPqrsdCorreoTests
{
    private static readonly Guid TQueja = Guid.NewGuid(), TPeticion = Guid.NewGuid();
    private static readonly Guid CConv = Guid.NewGuid(), CCobros = Guid.NewGuid();
    private static readonly IReadOnlyList<TipoPqrsdOpcion> Tipos = new[] { new TipoPqrsdOpcion(TQueja, "Queja"), new TipoPqrsdOpcion(TPeticion, "Petición") };
    private static readonly IReadOnlyList<CategoriaPqrsdOpcion> Cats = new[] { new CategoriaPqrsdOpcion(CConv, "Convivencia"), new CategoriaPqrsdOpcion(CCobros, "Cobros y cartera") };

    [Fact]
    public void Parsear_une_tipo_y_categoria_sin_importar_tildes_ni_mayusculas()
    {
        var p = RadicadorPqrsdCorreoAgente.Parsear(
            "```json\n{\"es_pqrsd\":true,\"tipo\":\"peticion\",\"categoria\":\"COBROS Y CARTERA\",\"unidad\":\"Torre 1 apto 202\",\"urgencia\":\"Media\",\"confianza\":93,\"motivo\":\"Pide revisar un cobro\"}\n```",
            Tipos, Cats)!;
        Assert.True(p.EsPqrsd);
        Assert.Equal(TPeticion, p.TipoId);
        Assert.Equal(CCobros, p.CategoriaId);
        Assert.Equal("Torre 1 apto 202", p.UnidadTexto);
        Assert.Equal("media", p.Urgencia);
        Assert.Equal(0.93m, p.Confianza);   // 93 -> 0.93
    }

    [Fact]
    public void Parsear_no_inventa_tipo_ni_categoria_fuera_de_la_lista()
    {
        var p = RadicadorPqrsdCorreoAgente.Parsear("{\"tipo\":\"Reclamo\",\"categoria\":\"Piscina\",\"confianza\":0.9}", Tipos, Cats)!;
        Assert.Null(p.TipoId);
        Assert.Null(p.CategoriaId);
        Assert.Equal("Reclamo", p.TipoNombre);
        Assert.Null(RadicadorPqrsdCorreoAgente.Parsear("no es json", Tipos, Cats));
    }

    [Fact]
    public void Parsear_es_pqrsd_false()
    {
        var p = RadicadorPqrsdCorreoAgente.Parsear("{\"es_pqrsd\":false,\"tipo\":\"Queja\",\"categoria\":\"Convivencia\",\"confianza\":0.8}", Tipos, Cats)!;
        Assert.False(p.EsPqrsd);
    }

    [Fact]
    public void Descripcion_quita_las_citas_del_correo_anterior()
    {
        var cuerpo = "Buenas tardes, el vecino del 502 pone musica a alto volumen todas las noches.\n\nGracias.\n\n" +
                     "El mar, 17 nov 2026 a las 10:02, Administracion <admin@conjunto.com> escribió:\n> Cordial saludo\n> texto viejo";
        var d = RadicadorPqrsdCorreoAgente.ArmarDescripcion("Ruido apto 502", cuerpo, null);
        Assert.StartsWith("Asunto: Ruido apto 502", d);
        Assert.Contains("musica a alto volumen", d);
        Assert.DoesNotContain("texto viejo", d);
        Assert.DoesNotContain("escribi", d);
    }

    [Fact]
    public void Descripcion_respeta_los_limites_del_radicado()
    {
        var largo = RadicadorPqrsdCorreoAgente.ArmarDescripcion("Asunto", new string('a', 5000), null);
        Assert.True(largo.Length <= RadicadorPqrsdCorreoAgente.MaxDescripcion);
        Assert.Contains(RadicadorPqrsdCorreoAgente.NombreAdjuntoOriginal, largo);

        var corto = RadicadorPqrsdCorreoAgente.ArmarDescripcion(null, "Hola", "El residente pide revisar una gotera.");
        Assert.True(corto.Length >= RadicadorPqrsdCorreoAgente.MinDescripcion);
        Assert.Contains("gotera", corto);

        var vacio = RadicadorPqrsdCorreoAgente.ArmarDescripcion(null, null, null);
        Assert.True(vacio.Length >= RadicadorPqrsdCorreoAgente.MinDescripcion);
    }

    [Fact]
    public void Unidad_se_une_al_codigo_solo_sin_ambiguedad()
    {
        var u1 = Guid.NewGuid(); var u2 = Guid.NewGuid(); var u3 = Guid.NewGuid();
        var unidades = new List<(Guid, string)> { (u1, "1-0202"), (u2, "2-0202"), (u3, "2-0710") };

        Assert.Equal(u1, RadicadorPqrsdCorreoAgente.ResolverUnidad("Torre 1 apto 202", unidades));
        Assert.Equal(u2, RadicadorPqrsdCorreoAgente.ResolverUnidad("2-0202", unidades));
        Assert.Equal(u3, RadicadorPqrsdCorreoAgente.ResolverUnidad("apartamento 710", unidades));
        Assert.Null(RadicadorPqrsdCorreoAgente.ResolverUnidad("apto 202", unidades));      // torre 1 o 2: no se adivina
        Assert.Null(RadicadorPqrsdCorreoAgente.ResolverUnidad("Torre 3 apto 202", unidades));
        Assert.Null(RadicadorPqrsdCorreoAgente.ResolverUnidad("", unidades));
    }
}
