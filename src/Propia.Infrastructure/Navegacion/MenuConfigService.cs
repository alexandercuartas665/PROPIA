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
            .Select(o => new MenuOverrideData(o.NodeKey, o.Label, o.ParentKey, o.SortOrder))
            .ToListAsync(ct);
        var resolved = MenuCatalog.Resolve(overrides);
        _cache.Set(CacheKey, resolved, CacheTtl);
        return resolved;
    }

    public async Task SaveArrangementAsync(SaveMenuArrangementRequest request, CancellationToken ct = default)
    {
        var sectionBase = MenuCatalog.Sections.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var itemBase = MenuCatalog.Items.ToDictionary(i => i.Key, StringComparer.Ordinal);

        var deltas = new List<MenuOverride>();
        foreach (var s in request.Sections)
        {
            if (sectionBase.TryGetValue(s.Key, out var sb))
            {
                var label = Clean(s.Label);
                var labelDelta = label is not null && label != sb.Label ? label : null;
                int? orderDelta = s.Order != sb.Order ? s.Order : null;
                if (labelDelta is not null || orderDelta is not null)
                {
                    deltas.Add(new MenuOverride { NodeKey = s.Key, Label = labelDelta, ParentKey = null, SortOrder = orderDelta });
                }
            }

            foreach (var it in s.Items)
            {
                if (!itemBase.TryGetValue(it.Key, out var ib)) { continue; }
                var label = Clean(it.Label);
                var labelDelta = label is not null && label != ib.Label ? label : null;
                var parentDelta = !string.Equals(s.Key, ib.SectionKey, StringComparison.Ordinal) ? s.Key : null;
                int? orderDelta = it.Order != ib.Order ? it.Order : null;
                if (labelDelta is not null || parentDelta is not null || orderDelta is not null)
                {
                    deltas.Add(new MenuOverride { NodeKey = it.Key, Label = labelDelta, ParentKey = parentDelta, SortOrder = orderDelta });
                }
            }
        }

        // Reemplazo total: borra los overrides actuales e inserta los deltas nuevos.
        var current = await _db.MenuOverrides.ToListAsync(ct);
        _db.MenuOverrides.RemoveRange(current);
        if (deltas.Count > 0) { _db.MenuOverrides.AddRange(deltas); }
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
