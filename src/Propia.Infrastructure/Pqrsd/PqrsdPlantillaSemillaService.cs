using Microsoft.EntityFrameworkCore;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Pqrsd;

/// <summary>
/// CRUD del catalogo GLOBAL de plantillas semilla PQRSD (Super Admin, Capa 0). Tabla global sin RLS.
/// El sembrado en cada copropiedad lo hace PqrsdService en el contexto del propio tenant.
/// </summary>
public sealed class PqrsdPlantillaSemillaService : IPqrsdPlantillaSemillaService
{
    private readonly PropiaDbContext _db;

    public PqrsdPlantillaSemillaService(PropiaDbContext db) => _db = db;

    public async Task<IReadOnlyList<PqrsdPlantillaSemillaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct)
    {
        var q = _db.PqrsdPlantillasSemilla.AsNoTracking().AsQueryable();
        if (!incluirInactivas) q = q.Where(p => p.Activa);
        return await q.OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new PqrsdPlantillaSemillaDto(p.Id, p.Nombre, p.CuerpoHtml, p.Activa, p.Orden))
            .ToListAsync(ct);
    }

    public async Task<PqrsdPlantillaSemillaDto> CrearAsync(GuardarPlantillaSemillaRequest req, CancellationToken ct)
    {
        var orden = req.Orden;
        if (orden <= 0)
            orden = (await _db.PqrsdPlantillasSemilla.CountAsync(ct));
        var p = new PqrsdPlantillaSemilla
        {
            Nombre = (req.Nombre ?? "Plantilla").Trim(),
            CuerpoHtml = req.CuerpoHtml ?? "",
            Activa = req.Activa,
            Orden = orden
        };
        _db.PqrsdPlantillasSemilla.Add(p);
        await _db.SaveChangesAsync(ct);
        return new PqrsdPlantillaSemillaDto(p.Id, p.Nombre, p.CuerpoHtml, p.Activa, p.Orden);
    }

    public async Task<PqrsdPlantillaSemillaDto?> ActualizarAsync(Guid id, GuardarPlantillaSemillaRequest req, CancellationToken ct)
    {
        var p = await _db.PqrsdPlantillasSemilla.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        p.Nombre = (req.Nombre ?? p.Nombre).Trim();
        p.CuerpoHtml = req.CuerpoHtml ?? "";
        p.Activa = req.Activa;
        p.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return new PqrsdPlantillaSemillaDto(p.Id, p.Nombre, p.CuerpoHtml, p.Activa, p.Orden);
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.PqrsdPlantillasSemilla.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        _db.PqrsdPlantillasSemilla.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
