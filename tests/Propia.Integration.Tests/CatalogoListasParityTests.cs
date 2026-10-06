using Propia.Application.MiCopropiedad;
using Propia.Domain.Enums;
using Propia.Infrastructure.Catalogos;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Paridad del catalogo global (Ola 1): el registro de semillas de <see cref="CatalogoListasSeeder"/>
/// debe reflejar EXACTAMENTE las fuentes de "fabrica" que hoy muestran los modulos, para que al sembrar
/// el catalogo nada cambie en pantalla. Son tests PUROS (no tocan la BD / Testcontainers): validan la
/// funcion de registro, asi corren rapido y cazan cualquier divergencia de clave/label/orden.
/// </summary>
public class CatalogoListasParityTests
{
    private static IReadOnlyList<CatalogoListasSeeder.SemillaOpcion> Lista(string lista)
        => CatalogoListasSeeder.Registro().Single(x => x.Lista == lista).Opciones;

    [Fact]
    public void Unidad_tipo_cubre_todo_el_enum_con_clave_y_label_de_fabrica()
    {
        var ops = Lista("unidad.tipo");

        // Una opcion por cada valor del enum TipoUnidad (ninguno sin catalogo).
        var enumVals = Enum.GetValues<TipoUnidad>();
        Assert.Equal(enumVals.Length, ops.Count);

        foreach (var t in enumVals)
        {
            var clave = "e:" + t;
            var op = Assert.Single(ops.Where(o => o.Clave == clave));
            Assert.Equal(TiposUnidadCatalogo.Etiqueta(t), op.Label);   // label de fabrica exacto
            Assert.False(string.IsNullOrWhiteSpace(op.Label));
            Assert.Null(op.Meta);                                      // unidad.tipo es lista libre (sin metadata)
        }

        // Claves unicas.
        Assert.Equal(ops.Count, ops.Select(o => o.Clave).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Unidad_estado_es_la_semilla_de_fabrica()
    {
        var ops = Lista("unidad.estado");
        var semilla = UnidadCamposSistema.EstadosSemilla;

        Assert.Equal(semilla.Length, ops.Count);
        for (var i = 0; i < semilla.Length; i++)
        {
            Assert.Equal(semilla[i], ops[i].Clave);   // en listas libres la clave ES el texto
            Assert.Equal(semilla[i], ops[i].Label);
            Assert.Equal(i, ops[i].Orden);
        }
    }

    [Fact]
    public void No_hay_listas_duplicadas_en_el_registro()
    {
        var listas = CatalogoListasSeeder.Registro().Select(x => x.Lista).ToList();
        Assert.Equal(listas.Count, listas.Distinct(StringComparer.Ordinal).Count());
    }
}
