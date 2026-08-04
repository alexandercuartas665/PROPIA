using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Override GLOBAL de un nodo del menu de navegacion (una seccion o un item). El menu base vive en
/// codigo (MenuCatalog); aqui se guardan solo los cambios del Super Admin: nombre a mostrar, orden y
/// ubicacion (seccion destino de un item). Entidad GLOBAL de plataforma (hereda BaseEntity, SIN
/// tenant_id ni RLS): el mismo menu lo ven todas las copropiedades. Concepto portado de ECOREX.tareas
/// (MenuNode.Name/SortOrder/ParentId), en forma de override-sobre-base.
/// </summary>
public class MenuOverride : BaseEntity
{
    /// <summary>Llave estable del nodo (Key de la seccion o del item en MenuCatalog). Unica.</summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>Nombre a mostrar custom. Null = usar el label base.</summary>
    public string? Label { get; set; }

    /// <summary>Solo items: seccion destino (mover de ubicacion). Null = seccion base.</summary>
    public string? ParentKey { get; set; }

    /// <summary>Orden custom entre hermanos. Null = orden base.</summary>
    public int? SortOrder { get; set; }
}
