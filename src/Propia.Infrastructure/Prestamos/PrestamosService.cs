using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Prestamos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Prestamos;

/// <summary>Prestamos de equipos + trazabilidad de entrega/devolucion con fotos (tambien para reservas de zona).</summary>
public class PrestamosService : IPrestamosService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IBlobStorage _storage;

    public PrestamosService(PropiaDbContext db, ITenantContext tenant, IBlobStorage storage)
    {
        _db = db; _tenant = tenant; _storage = storage;
    }

    private Guid RequireTenant() => _tenant.CurrentTenantId ?? throw new InvalidOperationException("No hay copropiedad activa.");

    // ===================== Prestamos de equipo =====================

    public async Task<IReadOnlyList<PrestamoEquipoDto>> ListarAsync(Guid? equipoId, CancellationToken ct)
    {
        var q = _db.PrestamosEquipo.AsNoTracking().AsQueryable();
        if (equipoId.HasValue) q = q.Where(p => p.EquipoActivoId == equipoId.Value);
        var rows = await q.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.CreatedAt).ToListAsync(ct);
        return await MapAsync(rows, ct);
    }

    public async Task<PrestamoEquipoDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.PrestamosEquipo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        return (await MapAsync(new List<PrestamoEquipo> { p }, ct)).FirstOrDefault();
    }

    public async Task<PrestamoEquipoDto> CrearAsync(CrearPrestamoRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var eq = await _db.EquiposActivos.FirstOrDefaultAsync(e => e.Id == req.EquipoActivoId, ct)
            ?? throw new InvalidOperationException("Equipo no encontrado.");
        if (!eq.EsReservable) throw new InvalidOperationException("Este equipo/activo no es reservable.");
        if (req.PersonaId == Guid.Empty) throw new InvalidOperationException("Indica quien toma el prestamo.");
        if (req.HoraInicio.HasValue && req.HoraFin.HasValue && req.HoraFin <= req.HoraInicio)
            throw new InvalidOperationException("La hora fin debe ser posterior a la de inicio.");

        var n = await _db.PrestamosEquipo.CountAsync(p => p.Codigo.StartsWith($"PRE-{req.Fecha.Year}-"), ct) + 1;
        var p = new PrestamoEquipo
        {
            TenantId = tenantId,
            Codigo = $"PRE-{req.Fecha.Year}-{n:D4}",
            EquipoActivoId = req.EquipoActivoId,
            PersonaId = req.PersonaId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            Fecha = req.Fecha,
            HoraInicio = req.HoraInicio,
            HoraFin = req.HoraFin,
            Estado = EstadoPrestamoEquipo.Reservado,
            Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones!.Trim()
        };
        _db.PrestamosEquipo.Add(p);
        await _db.SaveChangesAsync(ct);
        return (await MapAsync(new List<PrestamoEquipo> { p }, ct)).First();
    }

    public async Task<bool> RegistrarEntregaAsync(Guid id, RegistrarEntregaRequest req, CancellationToken ct)
    {
        var p = await _db.PrestamosEquipo.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (p.Estado == EstadoPrestamoEquipo.Cancelado) throw new InvalidOperationException("El prestamo esta cancelado.");
        p.EntregadoAt = DateTimeOffset.UtcNow;
        p.EntregaObservacion = Limpiar(req.Observacion);
        p.Estado = EstadoPrestamoEquipo.Entregado;
        await GuardarFotoAsync("prestamo", p.Id, MomentoEntrega.Entrega, req, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RegistrarDevolucionAsync(Guid id, RegistrarEntregaRequest req, CancellationToken ct)
    {
        var p = await _db.PrestamosEquipo.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (p.Estado == EstadoPrestamoEquipo.Cancelado) throw new InvalidOperationException("El prestamo esta cancelado.");
        p.DevueltoAt = DateTimeOffset.UtcNow;
        p.DevolucionObservacion = Limpiar(req.Observacion);
        p.Estado = EstadoPrestamoEquipo.Devuelto;
        await GuardarFotoAsync("prestamo", p.Id, MomentoEntrega.Devolucion, req, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelarAsync(Guid id, string? motivo, CancellationToken ct)
    {
        var p = await _db.PrestamosEquipo.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        p.Estado = EstadoPrestamoEquipo.Cancelado;
        p.MotivoCancelacion = Limpiar(motivo);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Entrega/devolucion de reserva de zona =====================

    public async Task<bool> RegistrarEntregaReservaAsync(Guid reservaId, RegistrarEntregaRequest req, CancellationToken ct)
    {
        var r = await _db.Reservas.FirstOrDefaultAsync(x => x.Id == reservaId, ct);
        if (r is null) return false;
        r.EntregadaAt = DateTimeOffset.UtcNow;
        r.EntregaObservacion = Limpiar(req.Observacion);
        await GuardarFotoAsync("reserva", r.Id, MomentoEntrega.Entrega, req, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RegistrarDevolucionReservaAsync(Guid reservaId, RegistrarEntregaRequest req, CancellationToken ct)
    {
        var r = await _db.Reservas.FirstOrDefaultAsync(x => x.Id == reservaId, ct);
        if (r is null) return false;
        r.DevueltaAt = DateTimeOffset.UtcNow;
        r.DevolucionObservacion = Limpiar(req.Observacion);
        r.Estado = EstadoReserva.Completada;
        await GuardarFotoAsync("reserva", r.Id, MomentoEntrega.Devolucion, req, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Fotos =====================

    public async Task<IReadOnlyList<EntregaFotoDto>> ListarFotosAsync(string origenTipo, Guid origenId, CancellationToken ct)
    {
        var rows = await _db.EntregaFotos.AsNoTracking()
            .Where(f => f.OrigenTipo == origenTipo && f.OrigenId == origenId)
            .OrderBy(f => f.Momento).ThenBy(f => f.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(f => new EntregaFotoDto(f.Id, _storage.ResolveUrl(f.Url), f.Momento, f.CreatedAt)).ToList();
    }

    // ===================== Helpers =====================

    private async Task GuardarFotoAsync(string origenTipo, Guid origenId, MomentoEntrega momento, RegistrarEntregaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FotoBase64)) return;
        var tenantId = RequireTenant();
        byte[] bytes;
        try { bytes = Convert.FromBase64String(req.FotoBase64); } catch { throw new InvalidOperationException("Foto invalida (base64)."); }
        var mime = string.IsNullOrWhiteSpace(req.FotoTipoMime) ? "image/jpeg" : req.FotoTipoMime!;
        var ext = mime.Contains("png") ? ".png" : mime.Contains("webp") ? ".webp" : ".jpg";
        var key = $"tenants/{tenantId:N}/entregas/{origenTipo}/{origenId:N}/{Guid.NewGuid():N}{ext}";
        using (var ms = new MemoryStream(bytes)) { await _storage.UploadAsync(key, ms, mime, ct); }
        _db.EntregaFotos.Add(new EntregaFoto { TenantId = tenantId, OrigenTipo = origenTipo, OrigenId = origenId, Url = key, Momento = momento });
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<List<PrestamoEquipoDto>> MapAsync(List<PrestamoEquipo> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return new();
        var eqIds = rows.Select(r => r.EquipoActivoId).Distinct().ToList();
        var pIds = rows.Select(r => r.PersonaId).Distinct().ToList();
        var uIds = rows.Where(r => r.UnidadPrivadaId.HasValue).Select(r => r.UnidadPrivadaId!.Value).Distinct().ToList();
        var ids = rows.Select(r => r.Id).ToList();

        var eqs = await _db.EquiposActivos.AsNoTracking().Where(e => eqIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nombre, ct);
        var pers = await _db.Personas.AsNoTracking().Where(p => pIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => (p.Nombres + " " + p.Apellidos).Trim(), ct);
        var unis = await _db.UnidadesPrivadas.AsNoTracking().Where(u => uIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Numero, ct);
        var fotos = await _db.EntregaFotos.AsNoTracking()
            .Where(f => f.OrigenTipo == "prestamo" && ids.Contains(f.OrigenId))
            .GroupBy(f => new { f.OrigenId, f.Momento })
            .Select(g => new { g.Key.OrigenId, g.Key.Momento, C = g.Count() })
            .ToListAsync(ct);

        return rows.Select(p => new PrestamoEquipoDto(
            p.Id, p.Codigo, p.EquipoActivoId, eqs.GetValueOrDefault(p.EquipoActivoId, "Equipo"),
            p.PersonaId, pers.GetValueOrDefault(p.PersonaId, "Persona"),
            p.UnidadPrivadaId, p.UnidadPrivadaId.HasValue ? unis.GetValueOrDefault(p.UnidadPrivadaId.Value) : null,
            p.Fecha, p.HoraInicio, p.HoraFin, p.Estado, p.Observaciones,
            p.EntregadoAt, p.EntregaObservacion, p.DevueltoAt, p.DevolucionObservacion,
            fotos.Where(f => f.OrigenId == p.Id && f.Momento == MomentoEntrega.Entrega).Sum(f => f.C),
            fotos.Where(f => f.OrigenId == p.Id && f.Momento == MomentoEntrega.Devolucion).Sum(f => f.C)
        )).ToList();
    }
}
