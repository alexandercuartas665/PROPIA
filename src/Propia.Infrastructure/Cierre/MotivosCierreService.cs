using Microsoft.EntityFrameworkCore;
using Propia.Application.Cierre;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Cierre;

/// <summary>
/// CRUD + siembra perezosa de motivos de cierre por modulo. Tenant-scoped (RLS).
/// </summary>
public class MotivosCierreService : IMotivosCierreService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public MotivosCierreService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // Defaults por modulo: cubren las 3 clasificaciones (correcta / via interna agotada / perdida).
    private static readonly Dictionary<string, (string Nombre, ClasificacionCierre Clas)[]> Base = new()
    {
        [ModuloCierre.Pqrsd] = new[]
        {
            ("Resuelto y respondido", ClasificacionCierre.CierreCorrecto),
            ("Acuerdo con el solicitante", ClasificacionCierre.CierreCorrecto),
            ("Via interna agotada", ClasificacionCierre.ViaInternaAgotada),
            ("Desistido / sin respuesta del solicitante", ClasificacionCierre.Perdida),
        },
        [ModuloCierre.Tareas] = new[]
        {
            ("Resuelta satisfactoriamente", ClasificacionCierre.CierreCorrecto),
            ("Completada con observaciones", ClasificacionCierre.CierreCorrecto),
            ("Via interna agotada", ClasificacionCierre.ViaInternaAgotada),
            ("Descartada / no aplica", ClasificacionCierre.Perdida),
        },
    };

    private static string Norm(string modulo) =>
        string.Equals(modulo, ModuloCierre.Pqrsd, StringComparison.OrdinalIgnoreCase) ? ModuloCierre.Pqrsd : ModuloCierre.Tareas;

    private async Task AsegurarBaseAsync(string modulo, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return;
        if (await _db.MotivosCierre.AnyAsync(m => m.Modulo == modulo, ct)) return;
        if (!Base.TryGetValue(modulo, out var defs)) return;
        var orden = 0;
        foreach (var (nombre, clas) in defs)
        {
            _db.MotivosCierre.Add(new MotivoCierre
            {
                TenantId = tid,
                Modulo = modulo,
                Nombre = nombre,
                Clasificacion = clas,
                EsBase = true,
                Activo = true,
                Orden = orden++
            });
        }
        try { await _db.SaveChangesAsync(ct); } catch { _db.ChangeTracker.Clear(); }  // carrera concurrente -> ignorar
    }

    public async Task<IReadOnlyList<MotivoCierreDto>> ListarAsync(string modulo, bool incluirInactivos, CancellationToken ct = default)
    {
        modulo = Norm(modulo);
        await AsegurarBaseAsync(modulo, ct);
        var q = _db.MotivosCierre.AsNoTracking().Where(m => m.Modulo == modulo);
        if (!incluirInactivos) q = q.Where(m => m.Activo);
        return await q.OrderBy(m => m.Orden).ThenBy(m => m.Nombre)
            .Select(m => new MotivoCierreDto(m.Id, m.Modulo, m.Nombre, m.Clasificacion, m.EsBase, m.Activo, m.Orden))
            .ToListAsync(ct);
    }

    public async Task<MotivoCierreDto> CrearAsync(string modulo, GuardarMotivoCierreRequest req, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("No hay copropiedad activa.");
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre del motivo es obligatorio.");
        modulo = Norm(modulo);
        var maxOrden = await _db.MotivosCierre.Where(m => m.Modulo == modulo).Select(m => (int?)m.Orden).MaxAsync(ct) ?? -1;
        var m = new MotivoCierre
        {
            TenantId = tid,
            Modulo = modulo,
            Nombre = req.Nombre.Trim(),
            Clasificacion = req.Clasificacion,
            EsBase = false,
            Activo = req.Activo ?? true,
            Orden = req.Orden ?? (maxOrden + 1)
        };
        _db.MotivosCierre.Add(m);
        await _db.SaveChangesAsync(ct);
        return new MotivoCierreDto(m.Id, m.Modulo, m.Nombre, m.Clasificacion, m.EsBase, m.Activo, m.Orden);
    }

    public async Task<MotivoCierreDto?> ActualizarAsync(Guid id, GuardarMotivoCierreRequest req, CancellationToken ct = default)
    {
        var m = await _db.MotivosCierre.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return null;
        if (!string.IsNullOrWhiteSpace(req.Nombre)) m.Nombre = req.Nombre.Trim();
        m.Clasificacion = req.Clasificacion;
        if (req.Orden is int o) m.Orden = o;
        if (req.Activo is bool a) m.Activo = a;
        await _db.SaveChangesAsync(ct);
        return new MotivoCierreDto(m.Id, m.Modulo, m.Nombre, m.Clasificacion, m.EsBase, m.Activo, m.Orden);
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _db.MotivosCierre.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return false;
        // Los base no se borran (se pueden desactivar) para no romper referencias/reportes.
        if (m.EsBase) { m.Activo = false; }
        else _db.MotivosCierre.Remove(m);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
