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
    /// <param name="MetaSchema">Ola 4c: campos de metadata que acarrea cada opcion de una lista con-logica. El
    /// editor por-tenant renderiza un input por campo; el consumidor lee el valor por Clave del JSON Meta. null
    /// en listas libres o con-logica cuya Meta no edita la copropiedad (p.ej. plazos legales de PQRSD).</param>
    public sealed record ListaInfo(string Lista, string Nombre, bool ConLogica, IReadOnlyList<MetaCampoDef>? MetaSchema = null);

    /// <summary>Tipo de dato de un campo de metadata editable (Ola 4c).</summary>
    public enum MetaTipo { Bool, Entero, Seleccion }

    /// <summary>Un campo de la <see cref="ListaInfo.MetaSchema"/>: la clave en el JSON Meta, su etiqueta, el tipo
    /// de input y, para <see cref="MetaTipo.Seleccion"/>, las opciones disponibles.</summary>
    public sealed record MetaCampoDef(string Clave, string Label, MetaTipo Tipo,
        IReadOnlyList<MetaOpcionDef>? Opciones = null, string? Ayuda = null);

    /// <summary>Una opcion de un campo Meta de tipo <see cref="MetaTipo.Seleccion"/> (valor guardado + etiqueta).</summary>
    public sealed record MetaOpcionDef(string Valor, string Label);

    public static readonly IReadOnlyList<ListaInfo> Conocidas = new List<ListaInfo>
    {
        new("unidad.tipo",          "Unidades · Tipo de unidad",          false),
        new("unidad.estado",        "Unidades · Estado de la propiedad",  false),
        new("contrato.tipocontrato","Contratos · Tipo de contrato",        false),
        new("contrato.categoria",   "Contratos · Categoría",               false),
        new("contrato.formapago",   "Contratos · Forma de pago",           false),
        new("contrato.tiposervicio","Contratos · Tipo de servicio",        false),
        new("vehiculo.tipovehiculo","Vehículos · Tipo de vehículo",        false),
        new("mascota.tipo",         "Mascotas · Tipo de mascota",          false),
        new("equipo.categoria",     "Equipos · Categoría",                  false),
        new("equipo.tipo",          "Equipos · Tipo (equipo/activo)",       false),
        new("equipo.estado",        "Equipos · Estado",                     false),
        new("zona.categoria",       "Zonas comunes · Categoría",            false),
        new("zona.estado",          "Zonas comunes · Estado",               false),
        new("persona.sexo",         "Personas · Sexo",                      false),
        new("directorio.etiqueta",  "Directorio · Etiquetas",               false),
        new("usuario.cargo",        "Usuarios · Cargo (descriptivo)",       false),
        // Ola 4b-2: listas libres (enum-universo) nuevas, editables por A&D sin deploy.
        new("comunicacion.tipocomunicado", "Comunicaciones · Tipo de comunicado", false),
        new("porteria.tiponovedad",        "Portería · Tipo de novedad",          false),
        new("porteria.tipocorrespondencia","Portería · Tipo de correspondencia",   false),
        new("reserva.etiquetabloqueo",     "Reservas · Etiqueta de bloqueo",       false),
        new("pqrsd.mediorecepcion",        "PQRSD · Medio de recepción",           false),
        new("equipotrabajo.rol",           "Equipo de trabajo · Rol operativo",    false),
        new("gobierno.cargoconsejo",       "Consejo · Cargo",                      false),
        new("pqrsd.tipo",           "PQRSD · Tipo (plazo legal Ley 1755)",  true),
        new("pqrsd.estado",         "PQRSD · Etapa (columnas del tablero)", true),
        new("mantenimiento.periodicidad",   "Mantenimiento · Periodicidad",        true),
        new("mantenimiento.prioridad",       "Mantenimiento · Prioridad (programación)", false),
        new("mantenimiento.intervtipo",      "Mantenimiento · Tipo de intervención", false),
        new("mantenimiento.intervestado",    "Mantenimiento · Estado de intervención", true),
        new("mantenimiento.intervprioridad", "Mantenimiento · Prioridad (intervención)", false),
        // Ola 4c: listas con-logica editables por la copropiedad (la opcion acarrea comportamiento via Meta).
        new("porteria.tipovisitante", "Portería · Tipo de visitante", true, new List<MetaCampoDef>
        {
            new("columnaTablero", "Columna del tablero \"Adentro ahora\"", MetaTipo.Seleccion, new List<MetaOpcionDef>
            {
                new("0", "Visitantes"),
                new("1", "Proveedores y personal"),
                new("2", "Domicilios y otros"),
            }, "En cuál de las 3 columnas del tablero aparece este tipo de visitante."),
        }),
    };

    public static ListaInfo Info(string lista)
        => Conocidas.FirstOrDefault(x => string.Equals(x.Lista, lista, StringComparison.OrdinalIgnoreCase))
           ?? new ListaInfo(lista, lista, false);
}
