using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Catalogos;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Catalogos;

/// <summary>
/// Lee el catalogo global de listas (<c>catalogo_opcion</c>) con un cache de proceso de corta vida.
/// La tabla es GLOBAL (sin RLS/tenant), asi que la lectura es la misma para todos los tenants.
/// Cache: por lista, con TTL corto para auto-sanarse entre procesos (Web y Api) sin coordinacion;
/// ademas <see cref="InvalidarCache"/> lo limpia en el proceso que escribe (consola A&D, Ola 2).
/// </summary>
public sealed class CatalogoListasService : ICatalogoListas
{
    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly PropiaDbContext _db;
    public CatalogoListasService(PropiaDbContext db) => _db = db;

    private sealed record CacheEntry(DateTime LoadedUtc, IReadOnlyList<CatalogoOpcionDto> Opciones);

    public async Task<IReadOnlyList<CatalogoOpcionDto>> OpcionesAsync(
        string lista, bool incluirInactivas = false, CancellationToken ct = default)
    {
        var todas = await TodasAsync(lista, ct);
        return incluirInactivas ? todas : todas.Where(o => o.Activo).ToList();
    }

    public async Task<string?> LabelAsync(string lista, string clave, CancellationToken ct = default)
        => (await TodasAsync(lista, ct))
            .FirstOrDefault(o => string.Equals(o.Clave, clave, StringComparison.OrdinalIgnoreCase))?.Label;

    public void InvalidarCache(string? lista = null)
    {
        if (lista is null) _cache.Clear();
        else _cache.TryRemove(lista, out _);
    }

    private async Task<IReadOnlyList<CatalogoOpcionDto>> TodasAsync(string lista, CancellationToken ct)
    {
        if (_cache.TryGetValue(lista, out var e) && DateTime.UtcNow - e.LoadedUtc < Ttl)
            return e.Opciones;

        var filas = await _db.CatalogoOpciones.AsNoTracking()
            .Where(x => x.Lista == lista)
            .OrderBy(x => x.Orden).ThenBy(x => x.Label)
            .Select(x => new CatalogoOpcionDto(x.Clave, x.Label, x.Orden, x.Color, x.Activo, x.EsSemilla, x.Meta))
            .ToListAsync(ct);

        _cache[lista] = new CacheEntry(DateTime.UtcNow, filas);
        return filas;
    }
}
