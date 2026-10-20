using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>Store CRUD de las "Vistas del sistema" (compartidas por copropiedad via RLS). Ver IVistaSistemaService.</summary>
public class VistaSistemaService : IVistaSistemaService
{
    private readonly PropiaDbContext _db;
    public VistaSistemaService(PropiaDbContext db) => _db = db;

    private static VistaSistemaDto Map(VistaSistema v) => new(v.Id, v.Nombre, v.Orden, v.Definicion);

    public async Task<IReadOnlyList<VistaSistemaDto>> ListarAsync(CancellationToken ct) =>
        await _db.VistasSistema.AsNoTracking().OrderBy(v => v.Orden).ThenBy(v => v.CreatedAt)
            .Select(v => new VistaSistemaDto(v.Id, v.Nombre, v.Orden, v.Definicion)).ToListAsync(ct);

    public async Task<VistaSistemaDto> CrearAsync(GuardarVistaRequest req, CancellationToken ct)
    {
        var v = new VistaSistema { Nombre = (req.Nombre ?? "").Trim(), Orden = req.Orden, Definicion = string.IsNullOrWhiteSpace(req.Definicion) ? "{}" : req.Definicion };
        _db.VistasSistema.Add(v);
        await _db.SaveChangesAsync(ct);
        return Map(v);
    }

    public async Task<bool> ActualizarAsync(Guid id, GuardarVistaRequest req, CancellationToken ct)
    {
        var v = await _db.VistasSistema.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return false;
        v.Nombre = (req.Nombre ?? "").Trim();
        v.Orden = req.Orden;
        v.Definicion = string.IsNullOrWhiteSpace(req.Definicion) ? "{}" : req.Definicion;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct)
    {
        var v = await _db.VistasSistema.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return false;
        _db.VistasSistema.Remove(v);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReordenarAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var all = await _db.VistasSistema.ToListAsync(ct);
        for (var i = 0; i < ids.Count; i++)
        {
            var v = all.FirstOrDefault(x => x.Id == ids[i]);
            if (v is not null) v.Orden = i;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<VistaSistemaDto>> SembrarAsync(IReadOnlyList<GuardarVistaRequest> semillas, bool reemplazar, CancellationToken ct)
    {
        var existentes = await _db.VistasSistema.OrderBy(v => v.Orden).ToListAsync(ct);
        if (existentes.Count > 0 && !reemplazar)
            return existentes.Select(Map).ToList();   // ya hay vistas de la copropiedad: no re-sembrar (primer arranque atomico)
        if (existentes.Count > 0) _db.VistasSistema.RemoveRange(existentes);
        var orden = 0;
        foreach (var s in semillas)
            _db.VistasSistema.Add(new VistaSistema { Nombre = (s.Nombre ?? "").Trim(), Orden = orden++, Definicion = string.IsNullOrWhiteSpace(s.Definicion) ? "{}" : s.Definicion });
        await _db.SaveChangesAsync(ct);
        return await ListarAsync(ct);
    }
}
