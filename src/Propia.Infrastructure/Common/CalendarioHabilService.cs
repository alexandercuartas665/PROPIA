using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Common;

/// <summary>
/// Implementacion del calendario habil colombiano.
/// Cachea los festivos en un HashSet estatico (compartido por todas las instancias
/// del proceso) para evitar hits a BD repetidos en hot loops como el calculo de
/// vencimientos PQRSD. Se refresca lazy en el primer uso o al llamar InvalidarCache().
/// </summary>
public class CalendarioHabilService : ICalendarioHabilService
{
    // Cache de proceso. Volatil para visibilidad cross-thread (Blazor/ASP.NET concurrentes).
    private static volatile HashSet<DateOnly>? _festivosCache;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private readonly PropiaDbContext _db;
    public CalendarioHabilService(PropiaDbContext db) => _db = db;

    public void InvalidarCache() => _festivosCache = null;

    private async Task<HashSet<DateOnly>> GetFestivosAsync(CancellationToken ct)
    {
        if (_festivosCache is { } cached) return cached;
        await _lock.WaitAsync(ct);
        try
        {
            if (_festivosCache is { } c2) return c2;
            var lista = await _db.FestivosColombianos.AsNoTracking()
                .Select(f => f.Fecha).ToListAsync(ct);
            _festivosCache = new HashSet<DateOnly>(lista);
            return _festivosCache;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> EsHabilAsync(DateOnly fecha, CancellationToken ct)
    {
        if (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday) return false;
        var festivos = await GetFestivosAsync(ct);
        return !festivos.Contains(fecha);
    }

    public async Task<DateOnly> SumarDiasHabilesAsync(DateOnly desde, int dias, CancellationToken ct)
    {
        if (dias <= 0) return desde;
        var festivos = await GetFestivosAsync(ct);
        var d = desde;
        var anadidos = 0;
        while (anadidos < dias)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek != DayOfWeek.Saturday
                && d.DayOfWeek != DayOfWeek.Sunday
                && !festivos.Contains(d))
                anadidos++;
        }
        return d;
    }

    public async Task<int> ContarDiasHabilesAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde) return -await ContarDiasHabilesAsync(hasta, desde, ct);
        var festivos = await GetFestivosAsync(ct);
        var count = 0;
        var d = desde;
        while (d < hasta)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek != DayOfWeek.Saturday
                && d.DayOfWeek != DayOfWeek.Sunday
                && !festivos.Contains(d))
                count++;
        }
        return count;
    }
}
