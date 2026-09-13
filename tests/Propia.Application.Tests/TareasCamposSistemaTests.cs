using Propia.Application.Tareas;
using Propia.Domain.Enums;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>
/// PREPARACION Fase 1 (Selector de Campos): fija el catalogo de campos del sistema de la tarea contra las
/// columnas que HOY muestra la tabla del tablero (TableroTareas.razor, cabecera tb-list-hdr). Si alguien
/// agrega/quita/reordena una columna del sistema sin actualizar el catalogo, este test lo caza.
/// </summary>
public class TareasCamposSistemaTests
{
    // Orden EXACTO de las columnas del sistema en la cabecera de la tabla (lineas 559-569 del razor).
    private static readonly string[] OrdenEsperado =
    {
        "titulo", "descripcion", "codigo", "estado", "asignado",
        "progreso", "etiquetas", "prioridad", "vence", "valor", "origen",
    };

    // Columnas que se muestran SIEMPRE y no tienen toggle (estructurales).
    private static readonly string[] Estructurales = { "titulo", "estado", "progreso" };

    [Fact]
    public void El_catalogo_enumera_exactamente_las_columnas_de_hoy_en_orden()
    {
        var claves = TareaCamposSistema.Todos.Select(c => c.Clave).ToArray();
        Assert.Equal(OrdenEsperado, claves);
    }

    [Fact]
    public void No_hay_claves_duplicadas()
    {
        var claves = TareaCamposSistema.Todos.Select(c => c.Clave).ToList();
        Assert.Equal(claves.Count, claves.Distinct().Count());
    }

    [Fact]
    public void Todas_las_columnas_son_visibles_por_defecto()
    {
        // Hoy todos los toggles (_colDesc, _colResp, ...) nacen en true y titulo/etapa/progreso se ven siempre.
        Assert.All(TareaCamposSistema.Todos, c => Assert.True(c.VisiblePorDefecto, $"'{c.Clave}' deberia ser visible por defecto"));
        Assert.Equal(OrdenEsperado.ToHashSet(), TareaCamposSistema.ClavesVisiblesPorDefecto.ToHashSet());
    }

    [Fact]
    public void Solo_titulo_etapa_y_progreso_son_estructurales()
    {
        var estructurales = TareaCamposSistema.Todos.Where(c => c.SiempreEnPlantilla).Select(c => c.Clave).OrderBy(c => c);
        Assert.Equal(Estructurales.OrderBy(c => c), estructurales);
    }

    [Fact]
    public void Cada_campo_tiene_label_y_tipo_definido()
    {
        Assert.All(TareaCamposSistema.Todos, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Label), $"'{c.Clave}' sin label");
            Assert.True(Enum.IsDefined(typeof(TipoCampoTablero), c.Tipo), $"'{c.Clave}' con tipo invalido");
        });
    }

    [Fact]
    public void Por_encuentra_por_clave_sin_distinguir_mayusculas()
    {
        Assert.NotNull(TareaCamposSistema.Por("ESTADO"));
        Assert.Null(TareaCamposSistema.Por("no-existe"));
    }
}
