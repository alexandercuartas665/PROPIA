namespace Propia.Application.Catalogos;

/// <summary>
/// Metadata de las listas CONOCIDAS del catalogo global (nombre amigable para la consola A&D y si la
/// lista tiene logica atada). Es la cara "de producto" del catalogo: la UI de A&D la usa para titular
/// cada lista y para saber si debe pedir metadata de negocio al crear opciones.
///
/// Se amplia a medida que se enganchan modulos (Ola 4). Una lista presente en la tabla pero no aqui
/// se muestra igual, con su propia clave como nombre (fallback).
/// </summary>
public static class CatalogoListasRegistro
{
    /// <param name="Lista">Clave de la lista (discriminador en catalogo_opciones).</param>
    /// <param name="Nombre">Nombre amigable para la consola A&D.</param>
    /// <param name="ConLogica">true = sus opciones llevan metadata de negocio (Meta). false = lista libre.</param>
    public sealed record ListaInfo(string Lista, string Nombre, bool ConLogica);

    public static readonly IReadOnlyList<ListaInfo> Conocidas = new List<ListaInfo>
    {
        new("unidad.tipo",          "Unidades · Tipo de unidad",          false),
        new("unidad.estado",        "Unidades · Estado de la propiedad",  false),
        new("contrato.tipocontrato","Contratos · Tipo de contrato",        false),
        new("contrato.categoria",   "Contratos · Categoría",               false),
        new("vehiculo.tipovehiculo","Vehículos · Tipo de vehículo",        false),
        new("mascota.tipo",         "Mascotas · Tipo de mascota",          false),
        new("equipo.categoria",     "Equipos · Categoría",                  false),
        new("equipo.tipo",          "Equipos · Tipo (equipo/activo)",       false),
        new("equipo.estado",        "Equipos · Estado",                     false),
        new("zona.categoria",       "Zonas comunes · Categoría",            false),
        new("zona.estado",          "Zonas comunes · Estado",               false),
        new("persona.sexo",         "Personas · Sexo",                      false),
    };

    public static ListaInfo Info(string lista)
        => Conocidas.FirstOrDefault(x => string.Equals(x.Lista, lista, StringComparison.OrdinalIgnoreCase))
           ?? new ListaInfo(lista, lista, false);
}
