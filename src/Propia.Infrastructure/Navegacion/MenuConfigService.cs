using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Propia.Application.Navegacion;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Navegacion;

/// <summary>
/// Menu de navegacion configurable GLOBAL: resuelve el base (MenuCatalog) con los overrides guardados y
/// persiste el arreglo del editor como DELTAS vs el base (solo se guarda lo que cambia). Cachea el menu
/// resuelto (una sola entrada, es global) con invalidacion al guardar + TTL de respaldo. La tabla
/// menu_overrides es global (sin RLS), asi que se lee/escribe igual desde un contexto de tenant o de
/// Super Admin. Concepto portado de ECOREX.tareas (menu data-driven).
/// </summary>
public sealed class MenuConfigService : IMenuConfigService
{
    private const string CacheKey = "menu:resolved:v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly PropiaDbContext _db;
    private readonly IMemoryCache _cache;

    public MenuConfigService(PropiaDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<ResolvedMenu> GetResolvedMenuAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out ResolvedMenu? cached) && cached is not null)
        {
            return cached;
        }

        var overrides = await _db.MenuOverrides.AsNoTracking()
            .Select(o => new MenuOverrideData(o.NodeKey, o.Label, o.ParentKey, o.SortOrder, o.IsCustom, o.NodeType, o.Icon, o.Href, o.Hidden))
            .ToListAsync(ct);
        var resolved = MenuCatalog.Resolve(overrides);
        _cache.Set(CacheKey, resolved, CacheTtl);
        return resolved;
    }

    public async Task SaveArrangementAsync(SaveMenuArrangementRequest request, CancellationToken ct = default)
    {
        var sectionBase = MenuCatalog.Sections.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var itemBase = MenuCatalog.Items.ToDictionary(i => i.Key, StringComparer.Ordinal);

        var rows = new List<MenuOverride>();
        foreach (var s in request.Sections)
        {
            if (s.IsCustom)
            {
                // Seccion NUEVA: se guarda completa (no existe en el base).
                rows.Add(new MenuOverride
                {
                    NodeKey = s.Key,
                    IsCustom = true,
                    NodeType = "section",
                    Label = Clean(s.Label) ?? "Seccion",
                    SortOrder = s.Order,
                    Icon = Clean(s.Icon) ?? "fi-rr-apps",
                    Hidden = s.Hidden
                });
            }
            else if (sectionBase.TryGetValue(s.Key, out var sb))
            {
                var label = Clean(s.Label);
                var labelDelta = label is not null && label != sb.Label ? label : null;
                int? orderDelta = s.Order != sb.Order ? s.Order : null;
                // El icono de las secciones base tambien se puede cambiar (delta vs el base).
                var icon = Clean(s.Icon);
                var iconDelta = icon is not null && icon != sb.Icon ? icon : null;
                if (labelDelta is not null || orderDelta is not null || iconDelta is not null || s.Hidden)
                {
                    rows.Add(new MenuOverride { NodeKey = s.Key, Label = labelDelta, ParentKey = null, SortOrder = orderDelta, Icon = iconDelta, Hidden = s.Hidden });
                }
            }
            else { continue; } // seccion base desconocida (stale): se ignora

            foreach (var it in s.Items)
            {
                if (it.IsCustom)
                {
                    // Item NUEVO: se guarda completo. Sin funcion todavia -> Href /proximamente.
                    rows.Add(new MenuOverride
                    {
                        NodeKey = it.Key,
                        IsCustom = true,
                        NodeType = "item",
                        Label = Clean(it.Label) ?? "Item",
                        ParentKey = s.Key,
                        SortOrder = it.Order,
                        Icon = Clean(it.Icon) ?? "fi-rr-clock-three",
                        Href = Clean(it.Href) ?? "/proximamente",
                        Hidden = it.Hidden
                    });
                }
                else if (itemBase.TryGetValue(it.Key, out var ib))
                {
                    var label = Clean(it.Label);
                    var labelDelta = label is not null && label != ib.Label ? label : null;
                    var parentDelta = !string.Equals(s.Key, ib.SectionKey, StringComparison.Ordinal) ? s.Key : null;
                    int? orderDelta = it.Order != ib.Order ? it.Order : null;
                    // El icono de los items base tambien se puede cambiar (delta vs el base).
                    var icon = Clean(it.Icon);
                    var iconDelta = icon is not null && icon != ib.Icon ? icon : null;
                    if (labelDelta is not null || parentDelta is not null || orderDelta is not null || iconDelta is not null || it.Hidden)
                    {
                        rows.Add(new MenuOverride { NodeKey = it.Key, Label = labelDelta, ParentKey = parentDelta, SortOrder = orderDelta, Icon = iconDelta, Hidden = it.Hidden });
                    }
                }
            }
        }

        // Reemplazo total: borra los overrides actuales e inserta los nuevos (deltas de base + nodos custom).
        var current = await _db.MenuOverrides.ToListAsync(ct);
        _db.MenuOverrides.RemoveRange(current);
        if (rows.Count > 0) { _db.MenuOverrides.AddRange(rows); }
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        var current = await _db.MenuOverrides.ToListAsync(ct);
        if (current.Count > 0)
        {
            _db.MenuOverrides.RemoveRange(current);
            await _db.SaveChangesAsync(ct);
        }
        _cache.Remove(CacheKey);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
