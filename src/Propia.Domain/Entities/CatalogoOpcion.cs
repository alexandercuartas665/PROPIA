using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Opcion de un catalogo de lista GLOBAL de la plataforma (editable desde la consola A&D).
/// Es una entidad GLOBAL (hereda de <see cref="BaseEntity"/>, SIN tenant_id): la fuente de verdad
/// de "fabrica" de las listas de seleccion del producto (Tipo de unidad, Estado, etc.). Cada
/// copropiedad puede, por encima de esto, ocultar/renombrar/reordenar/agregar en su propia config
/// (unidad_campos_config, tipos_unidad_custom, ...). Sin RLS: lectura para todos los tenants; la
/// escritura se gatea a nivel de app (endpoints A&D), como las demas tablas de plataforma.
/// </summary>
public class CatalogoOpcion : BaseEntity
{
    /// <summary>Discriminador de la lista, ej. "unidad.tipo", "unidad.estado", "pqrsd.tipo".</summary>
    public string Lista { get; set; } = string.Empty;

    /// <summary>Clave ESTABLE a la que apuntan los datos. Para listas atadas a enum: "e:&lt;Enum&gt;".
    /// Para listas libres y opciones nuevas creadas por A&D: un string normalizado. NUNCA se edita.</summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Texto visible (editable por A&D).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Orden en el selector.</summary>
    public int Orden { get; set; }

    /// <summary>Color opcional (chip), por token o hex, segun el campo.</summary>
    public string? Color { get; set; }

    /// <summary>Si la opcion esta disponible. Ocultar en vez de borrar cuando hay datos en uso.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>true = vino de un enum/semilla del codigo (se puede ocultar pero avisa); false = creada por A&D.</summary>
    public bool EsSemilla { get; set; }

    /// <summary>Metadata de negocio en JSON para las listas con-logica (ej. {"plazoDias":15} en pqrsd.tipo,
    /// {"terminal":true,"cuentaSla":false} en una etapa). Vacio en listas libres.</summary>
    public string? Meta { get; set; }
}
