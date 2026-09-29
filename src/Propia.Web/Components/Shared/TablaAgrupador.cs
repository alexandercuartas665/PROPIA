using System.Collections.Generic;
using System.Linq;

namespace Propia.Web.Components.Shared;

/// <summary>
/// Valor de una dimension de agrupacion para un item: clave estable (para juntar) + titulo visible,
/// con color/icono opcionales (util para dimensiones con chip, p.ej. Etiqueta). Un item puede
/// devolver VARIOS valores en un nivel (multi-membership, p.ej. varias etiquetas); los modulos
/// single-valued devuelven exactamente uno.
/// </summary>
public readonly record struct ValorGrupo(string Clave, string Titulo, string? Color = null, string? Icono = null);

/// <summary>
/// Nodo del arbol de agrupacion anidada. En cada nivel: un grupo con su titulo/valor y su conteo;
/// si quedan mas niveles tiene <see cref="Subgrupos"/>, si es el ultimo nivel tiene <see cref="Filas"/>.
/// </summary>
public sealed class NodoGrupo<T>
{
    public string Clave { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string? Color { get; init; }
    public string? Icono { get; init; }
    /// <summary>Profundidad 0-based (0 = primer nivel de agrupacion).</summary>
    public int Nivel { get; init; }
    /// <summary>Total de filas hoja bajo este nodo (con multi-membership puede solaparse entre hermanos).</summary>
    public int Conteo { get; init; }
    public IReadOnlyList<NodoGrupo<T>> Subgrupos { get; init; } = System.Array.Empty<NodoGrupo<T>>();
    /// <summary>Filas del nodo, solo en el ultimo nivel de agrupacion.</summary>
    public IReadOnlyList<T> Filas { get; init; } = System.Array.Empty<T>();
    public bool EsHoja => Subgrupos.Count == 0;
}

/// <summary>
/// Motor UNICO de agrupacion anidada N-niveles de la vista tabla (gemelo conceptual del componente
/// de menu <c>TablaAgruparMenu</c> y del render <c>TablaGruposRender</c>). Dado la lista, los niveles
/// elegidos en el menu (claves de columna, en orden de jerarquia) y un selector de valor por
/// (item, clave), construye el arbol de grupos. No sabe de UI: solo datos.
/// </summary>
public static class TablaAgrupador
{
    /// <param name="items">Filas ya filtradas (con agrupacion NO se pagina: van completas).</param>
    /// <param name="niveles">Claves de las dimensiones, en orden (1er nivel, 2do nivel, ...).</param>
    /// <param name="valorDe">Dado (item, clave) devuelve uno o mas <see cref="ValorGrupo"/>. Devuelve
    /// vacio si el item no tiene valor en esa dimension (cae en un grupo "(Sin ...)" al final).</param>
    /// <param name="tituloSinValor">Titulo del grupo para los items sin valor en una dimension.</param>
    public static IReadOnlyList<NodoGrupo<T>> Agrupar<T>(
        IEnumerable<T> items,
        IReadOnlyList<string> niveles,
        System.Func<T, string, IEnumerable<ValorGrupo>> valorDe,
        string tituloSinValor = "(Sin dato)")
        => Construir(items as ICollection<T> ?? items.ToList(), niveles, valorDe, tituloSinValor, 0);

    private static IReadOnlyList<NodoGrupo<T>> Construir<T>(
        ICollection<T> items, IReadOnlyList<string> niveles,
        System.Func<T, string, IEnumerable<ValorGrupo>> valorDe, string tituloSinValor, int nivel)
    {
        if (nivel >= niveles.Count) return System.Array.Empty<NodoGrupo<T>>();
        var clave = niveles[nivel];

        // Reparte los items por valor de esta dimension, preservando el orden de aparicion del valor.
        var buckets = new Dictionary<string, (ValorGrupo v, List<T> items)>(System.StringComparer.Ordinal);
        var orden = new List<string>();
        var sinValor = new List<T>();
        foreach (var it in items)
        {
            var vals = valorDe(it, clave);
            var any = false;
            if (vals is not null)
            {
                foreach (var v in vals)
                {
                    any = true;
                    if (!buckets.TryGetValue(v.Clave, out var b)) { b = (v, new List<T>()); buckets[v.Clave] = b; orden.Add(v.Clave); }
                    b.items.Add(it);
                }
            }
            if (!any) sinValor.Add(it);
        }

        var nodos = new List<NodoGrupo<T>>(buckets.Count + 1);
        foreach (var k in orden.OrderBy(x => buckets[x].v.Titulo, System.StringComparer.CurrentCultureIgnoreCase))
        {
            var (v, its) = buckets[k];
            nodos.Add(Nodo(v, its, niveles, valorDe, tituloSinValor, nivel));
        }
        if (sinValor.Count > 0)
            nodos.Add(Nodo(new ValorGrupo(string.Empty, tituloSinValor), sinValor, niveles, valorDe, tituloSinValor, nivel));
        return nodos;
    }

    private static NodoGrupo<T> Nodo<T>(
        ValorGrupo v, List<T> its, IReadOnlyList<string> niveles,
        System.Func<T, string, IEnumerable<ValorGrupo>> valorDe, string tituloSinValor, int nivel)
    {
        var hoja = nivel + 1 >= niveles.Count;
        return new NodoGrupo<T>
        {
            Clave = v.Clave,
            Titulo = v.Titulo,
            Color = v.Color,
            Icono = v.Icono,
            Nivel = nivel,
            Conteo = its.Count,
            Subgrupos = hoja ? System.Array.Empty<NodoGrupo<T>>() : Construir(its, niveles, valorDe, tituloSinValor, nivel + 1),
            Filas = hoja ? its : System.Array.Empty<T>(),
        };
    }
}
