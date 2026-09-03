namespace Propia.Application.Navegacion;

// =====================================================================================
// Contratos del menu de navegacion configurable (GLOBAL, plataforma). Concepto portado de
// ECOREX.tareas (menu data-driven con Name/SortOrder/ParentId), adaptado a PROPIA como
// "override sobre base": el menu base se define en codigo (MenuCatalog) y una capa global de
// overrides (MenuOverride) cambia nombre/orden/ubicacion. Asi los modulos nuevos que agrega un
// dev aparecen solos, y el Super Admin reordena/renombra/mueve items sin tocar codigo.
// =====================================================================================

/// <summary>Definicion base de una seccion del rail de iconos (grupo del menu).</summary>
public sealed record MenuSectionDef(string Key, string Label, string Icon, string TooltipDesc, int Order, bool SeparatorBefore = false);

/// <summary>Definicion base de un item (enlace) del menu.</summary>
public sealed record MenuItemDef(string Key, string SectionKey, string Label, string Href, string Icon, int Order,
    string? Subheading = null, bool DividerBefore = false);

/// <summary>Override guardado de un nodo (seccion o item). Null en un campo = usar el valor base.
/// Si IsCustom=true es un nodo NUEVO (no existe en el base): sus datos viven todos aqui.</summary>
public sealed record MenuOverrideData(string NodeKey, string? Label, string? ParentKey, int? SortOrder,
    bool IsCustom = false, string? NodeType = null, string? Icon = null, string? Href = null, bool Hidden = false);

// ---------- Resuelto (base + overrides) para render y editor ----------

public sealed record ResolvedMenuItem(string Key, string SectionKey, string Label, string Href, string Icon, int Order,
    string? Subheading, bool DividerBefore, bool IsCustom = false, bool Hidden = false,
    IReadOnlyList<ResolvedMenuItem>? Children = null);

/// <summary>Items de una seccion agrupados por subheading (el grupo con Heading=null va bajo el titulo de la seccion).</summary>
public sealed record ResolvedMenuItemGroup(string? Heading, bool DividerBefore, IReadOnlyList<ResolvedMenuItem> Items);

public sealed record ResolvedMenuSection(string Key, string Label, string Icon, string TooltipDesc, int Order,
    bool SeparatorBefore, IReadOnlyList<ResolvedMenuItem> Items, IReadOnlyList<ResolvedMenuItemGroup> Groups, bool IsCustom = false, bool Hidden = false);

public sealed record ResolvedMenu(IReadOnlyList<ResolvedMenuSection> Sections);

// ---------- Guardar arreglo (desde el editor) ----------

public sealed record MenuItemArrangement(string Key, string? Label, int Order,
    bool IsCustom = false, string? Icon = null, string? Href = null, bool Hidden = false,
    IReadOnlyList<MenuItemArrangement>? Children = null);
public sealed record MenuSectionArrangement(string Key, string? Label, int Order, IReadOnlyList<MenuItemArrangement> Items,
    bool IsCustom = false, string? Icon = null, bool Hidden = false);

/// <summary>El editor envia el arreglo completo (secciones en orden, cada una con sus items en orden).
/// El servicio lo compara con el base y persiste solo los deltas.</summary>
public sealed record SaveMenuArrangementRequest(IReadOnlyList<MenuSectionArrangement> Sections);
