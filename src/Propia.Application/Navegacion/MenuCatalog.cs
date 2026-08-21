namespace Propia.Application.Navegacion;

/// <summary>
/// Menu BASE del Web cliente (fuente de verdad en codigo). Espeja el NavMenu.razor original: 8 secciones
/// del rail de iconos + sus items. El orden/nombre/ubicacion se puede sobrescribir por la capa global de
/// overrides (ver MenuOverride + IMenuConfigService); esta clase solo define el arbol por defecto y la
/// funcion pura Resolve(base + overrides) que consumen tanto el render (NavMenu) como el editor.
///
/// Al agregar un modulo nuevo: agrega aqui su MenuItemDef con un Key estable; aparecera automaticamente
/// en el menu (y sera reordenable/renombrable desde el editor). El Key NUNCA debe cambiar (es la llave
/// de los overrides guardados).
/// </summary>
public static class MenuCatalog
{
    public static readonly IReadOnlyList<MenuSectionDef> Sections = new[]
    {
        new MenuSectionDef("tabInicio", "Inicio", "fi-rr-house-blank",
            "Tablero de arranque con los indicadores de la copropiedad y los accesos rapidos del dia.", 1),
        new MenuSectionDef("tabMiPH", "Mi copropiedad", "fi-rr-building",
            "La ficha viva de la PH: identidad, distribucion de unidades, directorio y usuarios con sus roles.", 2),
        new MenuSectionDef("tabFinanzas", "Finanzas", "fi-rr-usd-circle",
            "Presupuesto y cuotas, cartera y estado de cuenta, servicios y contratos, y numeracion de documentos.", 3),
        new MenuSectionDef("tabGobierno", "Gobierno y convivencia", "fi-rr-podium-star",
            "Asambleas y organos de la copropiedad: consejo, comites, convocatorias y decisiones.", 4),
        new MenuSectionDef("tabOperacion", "Operacion", "fi-rr-list-check",
            "El dia a dia: tareas y proyectos, mantenimiento de activos, porteria y accesos, y reservas de zonas comunes.", 5),
        new MenuSectionDef("tabComunicacion", "Comunicacion y documentos", "fi-rr-megaphone",
            "Comunicados, documentos, reportes e indicadores, y el stack de WhatsApp con los agentes de IA.", 6),
        new MenuSectionDef("tabOrg", "Organizacion (Capa 1)", "fi-rr-network",
            "Vista de todas tus copropiedades: panel consolidado, calendario multi-PH, equipo de trabajo y reportes cruzados.", 7, SeparatorBefore: true),
        new MenuSectionDef("tabCuenta", "Mi cuenta", "fi-rr-user",
            "Tu perfil, la sesion activa y las preferencias personales de la plataforma.", 8),
    };

    public static readonly IReadOnlyList<MenuItemDef> Items = new[]
    {
        // Inicio
        new MenuItemDef("mi-dashboard", "tabInicio", "Dashboard", "/dashboard-ph", "fi-rr-dashboard", 1),

        // Mi copropiedad
        new MenuItemDef("mi-copropiedad", "tabMiPH", "Mi copropiedad", "/mi-copropiedad", "fi-rr-building", 1),
        new MenuItemDef("mi-distribucion", "tabMiPH", "Unidades Privadas", "/distribucion", "fi-rr-apps", 2),
        new MenuItemDef("mi-zonas", "tabMiPH", "Zonas Comunes", "/zonas-comunes", "fi-rr-trees", 3),
        new MenuItemDef("mi-equipos", "tabMiPH", "Equipos y Activos", "/equipos-activos", "fi-rr-settings", 4),
        new MenuItemDef("mi-directorio", "tabMiPH", "Directorio", "/directorio", "fi-rr-address-book", 5),
        new MenuItemDef("mi-usuarios", "tabMiPH", "Usuarios y roles", "/usuarios", "fi-rr-users-alt", 6),

        // Finanzas
        new MenuItemDef("fin-presupuesto", "tabFinanzas", "Presupuesto y cuotas", "/presupuesto", "fi-rr-money-bill-wave", 1),
        new MenuItemDef("fin-cartera", "tabFinanzas", "Cartera y estado de cuenta", "/cartera", "fi-rr-credit-card", 2),
        new MenuItemDef("fin-servicios", "tabFinanzas", "Servicios", "/servicios", "fi-rr-briefcase", 3),
        new MenuItemDef("fin-contratos", "tabFinanzas", "Contratos", "/contratos", "fi-rr-file-signature", 4),
        new MenuItemDef("fin-numeracion", "tabFinanzas", "Numeracion de documentos", "/finanzas/numeracion", "fi-rr-hashtag", 5),

        // Gobierno y convivencia
        new MenuItemDef("gob-asambleas", "tabGobierno", "Asambleas y organos", "/asambleas", "fi-rr-podium-star", 1),
        new MenuItemDef("gob-pqrs", "tabGobierno", "PQRSD y convivencia", "/pqrs", "fi-rr-comments-question", 2),

        // Operacion
        new MenuItemDef("op-tareas", "tabOperacion", "Tareas y proyectos", "/tareas", "fi-rr-list-check", 1),
        new MenuItemDef("op-mantenimiento", "tabOperacion", "Mantenimiento y activos", "/mantenimiento", "fi-rr-screwdriver", 2),
        new MenuItemDef("op-porteria", "tabOperacion", "Porteria y accesos", "/porteria", "fi-rr-shield-check", 3),
        new MenuItemDef("op-reservas", "tabOperacion", "Reservas zonas comunes", "/reservas", "fi-rr-calendar-check", 4),

        // Comunicacion y documentos
        new MenuItemDef("com-comunicaciones", "tabComunicacion", "Comunicaciones", "/comunicaciones", "fi-rr-megaphone", 1),
        new MenuItemDef("com-documentos", "tabComunicacion", "Documentos", "/documentos", "fi-rr-folder-open", 2),
        new MenuItemDef("com-reportes", "tabComunicacion", "Reportes e indicadores", "/reportes", "fi-rr-chart-line-up", 3),
        new MenuItemDef("com-informes", "tabComunicacion", "Informes de gestion", "/informes", "fi-rr-document", 4),
        // Subgrupo "Infraestructura & IA"
        new MenuItemDef("ia-lineas", "tabComunicacion", "Lineas WhatsApp", "/ia/lineas", "fi-rr-comment-alt", 5, Subheading: "Infraestructura & IA"),
        new MenuItemDef("ia-agentes", "tabComunicacion", "Agentes de IA", "/ia/agentes", "fi-rr-robot", 6, Subheading: "Infraestructura & IA"),
        new MenuItemDef("ia-conversaciones", "tabComunicacion", "Conversaciones", "/ia/conversaciones", "fi-rr-comments", 7, Subheading: "Infraestructura & IA"),
        new MenuItemDef("ia-bitacora", "tabComunicacion", "Bitacora del agente", "/ia/bitacora", "fi-rr-time-past", 8, Subheading: "Infraestructura & IA"),
        new MenuItemDef("ia-automatizaciones", "tabComunicacion", "Automatizaciones", "/ia/automatizaciones", "fi-rr-bolt", 9, Subheading: "Infraestructura & IA"),
        new MenuItemDef("ia-lista-negra", "tabComunicacion", "Lista negra", "/ia/lista-negra", "fi-rr-ban", 10, Subheading: "Infraestructura & IA"),

        // Organizacion (Capa 1)
        new MenuItemDef("org-panel", "tabOrg", "Panel consolidado", "/org/panel-consolidado", "fi-rr-dashboard", 1),
        new MenuItemDef("org-calendario", "tabOrg", "Calendario multi-PH", "/org/calendario", "fi-rr-calendar", 2),
        new MenuItemDef("org-equipo", "tabOrg", "Equipo de trabajo", "/org/equipo", "fi-rr-users-medical", 3),
        new MenuItemDef("org-reportes", "tabOrg", "Reportes cruzados", "/org/reportes-cruzados", "fi-rr-chart-pie", 4),
        new MenuItemDef("org-transferencia", "tabOrg", "Transferencia custodia", "/org/transferencia-custodia", "fi-rr-arrows-repeat", 5),
        // Subgrupo "Operador A&D (Capa 0)" (con divisor antes)
        new MenuItemDef("op0-info", "tabOrg", "Acerca del operador", "/operador/info", "fi-rr-shield", 6, Subheading: "Operador A&D (Capa 0)", DividerBefore: true),

        // Mi cuenta
        new MenuItemDef("cta-perfil", "tabCuenta", "Perfil", "/cuenta/perfil", "fi-rr-user", 1),
        new MenuItemDef("cta-login", "tabCuenta", "Iniciar sesion", "/login", "fi-rr-sign-in-alt", 2),
    };

    /// <summary>Aplica los overrides globales sobre el arbol base y devuelve el menu resuelto (para render + editor).</summary>
    public static ResolvedMenu Resolve(IEnumerable<MenuOverrideData> overrides)
    {
        var all = overrides.ToList();
        var ov = new Dictionary<string, MenuOverrideData>(StringComparer.Ordinal);
        foreach (var o in all) { ov[o.NodeKey] = o; }

        // Secciones: base (con override de nombre/orden) + custom (nodos nuevos del Super Admin).
        var baseSections = Sections.Select(s =>
        {
            ov.TryGetValue(s.Key, out var o);
            var label = string.IsNullOrWhiteSpace(o?.Label) ? s.Label : o!.Label!;
            var order = o?.SortOrder ?? s.Order;
            return new SectionShape(s.Key, label, s.Icon, s.TooltipDesc, order, s.SeparatorBefore, false);
        });
        var customSections = all
            .Where(o => o.IsCustom && string.Equals(o.NodeType, "section", StringComparison.OrdinalIgnoreCase))
            .Select(o => new SectionShape(
                o.NodeKey,
                string.IsNullOrWhiteSpace(o.Label) ? "Seccion" : o.Label!,
                string.IsNullOrWhiteSpace(o.Icon) ? "fi-rr-apps" : o.Icon!,
                string.Empty, o.SortOrder ?? 999, false, true));

        var sections = baseSections.Concat(customSections)
            .OrderBy(x => x.Order).ThenBy(x => x.Label, StringComparer.Ordinal)
            .ToList();
        var sectionKeys = sections.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);

        // Items: base (con override de nombre/orden/ubicacion) + custom.
        var baseItems = Items.Select(i =>
        {
            ov.TryGetValue(i.Key, out var o);
            var section = !string.IsNullOrWhiteSpace(o?.ParentKey) && sectionKeys.Contains(o!.ParentKey!)
                ? o.ParentKey! : i.SectionKey;
            var label = string.IsNullOrWhiteSpace(o?.Label) ? i.Label : o!.Label!;
            var order = o?.SortOrder ?? i.Order;
            return new ResolvedMenuItem(i.Key, section, label, i.Href, i.Icon, order, i.Subheading, i.DividerBefore, false);
        });
        var customItems = all
            .Where(o => o.IsCustom && string.Equals(o.NodeType, "item", StringComparison.OrdinalIgnoreCase))
            .Select(o =>
            {
                var section = !string.IsNullOrWhiteSpace(o.ParentKey) && sectionKeys.Contains(o.ParentKey!)
                    ? o.ParentKey! : (sections.Count > 0 ? sections[0].Key : string.Empty);
                return new ResolvedMenuItem(
                    o.NodeKey, section,
                    string.IsNullOrWhiteSpace(o.Label) ? "Item" : o.Label!,
                    string.IsNullOrWhiteSpace(o.Href) ? "/proximamente" : o.Href!,
                    string.IsNullOrWhiteSpace(o.Icon) ? "fi-rr-clock-three" : o.Icon!,
                    o.SortOrder ?? 999, null, false, true);
            });

        var items = baseItems.Concat(customItems).ToList();

        var resolved = new List<ResolvedMenuSection>(sections.Count);
        foreach (var s in sections)
        {
            var secItems = items
                .Where(i => string.Equals(i.SectionKey, s.Key, StringComparison.Ordinal))
                .OrderBy(i => i.Order).ThenBy(i => i.Label, StringComparer.Ordinal)
                .ToList();
            resolved.Add(new ResolvedMenuSection(s.Key, s.Label, s.Icon, s.TooltipDesc, s.Order,
                s.SeparatorBefore, secItems, BuildGroups(secItems), s.IsCustom));
        }
        return new ResolvedMenu(resolved);
    }

    private sealed record SectionShape(string Key, string Label, string Icon, string TooltipDesc, int Order, bool SeparatorBefore, bool IsCustom);

    /// <summary>Agrupa los items de una seccion por subheading (corridas consecutivas). El primer grupo
    /// sin heading (null) se pinta bajo el titulo de la seccion.</summary>
    private static IReadOnlyList<ResolvedMenuItemGroup> BuildGroups(IReadOnlyList<ResolvedMenuItem> items)
    {
        var acc = new List<(string? Heading, bool Divider, List<ResolvedMenuItem> Items)>();
        foreach (var it in items)
        {
            if (acc.Count > 0 && string.Equals(acc[^1].Heading, it.Subheading, StringComparison.Ordinal))
            {
                acc[^1].Items.Add(it);
            }
            else
            {
                acc.Add((it.Subheading, it.DividerBefore, new List<ResolvedMenuItem> { it }));
            }
        }
        return acc.Select(g => new ResolvedMenuItemGroup(g.Heading, g.Divider, g.Items)).ToList();
    }
}
