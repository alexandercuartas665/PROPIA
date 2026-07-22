using Microsoft.EntityFrameworkCore;
using Propia.Application.Programaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Programaciones;

/// <summary>
/// CRUD de programaciones de tareas. La materializacion la hace ProgramacionTareasJob.
/// TenantId se asigna automaticamente en SaveChanges (TenantEntity) + RLS.
/// </summary>
public class ProgramacionTareasService : IProgramacionTareasService
{
    private readonly PropiaDbContext _db;

    public ProgramacionTareasService(PropiaDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProgramacionTareaDto>> ListarAsync(string? moduloOrigen, Guid? entidadOrigenId, CancellationToken ct)
    {
        var q = _db.ProgramacionTareas.AsNoTracking().Include(p => p.Responsables).AsQueryable();
        if (!string.IsNullOrWhiteSpace(moduloOrigen)) q = q.Where(p => p.ModuloOrigenCodigo == moduloOrigen);
        if (entidadOrigenId.HasValue) q = q.Where(p => p.EntidadOrigenId == entidadOrigenId.Value);
        var progs = await q.OrderByDescending(p => p.Activa).ThenBy(p => p.FechaProximaEjecucion).ToListAsync(ct);
        var tableros = await TableroNombresAsync(ct);
        return progs.Select(p => ToDto(p, tableros)).ToList();
    }

    public async Task<ProgramacionTareaDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.ProgramacionTareas.AsNoTracking().Include(x => x.Responsables)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var tableros = await TableroNombresAsync(ct);
        return ToDto(p, tableros);
    }

    public async Task<ProgramacionTareaDto> CrearAsync(CrearProgramacionRequest req, Guid usuarioId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("El titulo de la programacion es obligatorio.");

        var zona = NormalizarZona(req.ZonaHoraria);
        var cron = NormalizarCron(req.Tipo, req.CronExpresion);

        var p = new ProgramacionTarea
        {
            Titulo = req.Titulo.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Prioridad = req.Prioridad,
            TableroId = req.TableroId,
            Tipo = req.Tipo,
            Periodicidad = req.Periodicidad,
            CronExpresion = cron,
            ZonaHoraria = zona,
            NotificarPorCorreo = req.NotificarPorCorreo,
            FechaProximaEjecucion = req.FechaProximaEjecucion,
            FechaFin = req.FechaFin,
            Activa = true,
            ModuloOrigenCodigo = string.IsNullOrWhiteSpace(req.ModuloOrigenCodigo) ? null : req.ModuloOrigenCodigo.Trim(),
            EntidadOrigenId = req.EntidadOrigenId,
            OrigenReferencia = string.IsNullOrWhiteSpace(req.OrigenReferencia) ? null : req.OrigenReferencia.Trim(),
            CreadoPorUsuarioId = usuarioId
        };
        // En modo cron la proxima corrida se calcula aqui: el job solo la consume y la avanza.
        p.ProximaEjecucionUtc = req.Tipo == TipoProgramacion.Cron
            ? CronHelper.ProximaEjecucion(cron, zona, DateTimeOffset.UtcNow)
            : null;
        AplicarResponsables(p, req.Responsables);
        _db.ProgramacionTareas.Add(p);
        await _db.SaveChangesAsync(ct);
        var tableros = await TableroNombresAsync(ct);
        return ToDto(p, tableros);
    }

    public async Task<bool> ActualizarAsync(Guid id, ActualizarProgramacionRequest req, CancellationToken ct)
    {
        var p = await _db.ProgramacionTareas.Include(x => x.Responsables).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("El titulo de la programacion es obligatorio.");

        var zona = NormalizarZona(req.ZonaHoraria);
        var cron = NormalizarCron(req.Tipo, req.CronExpresion);
        // Si cambio la regla de disparo (o la zona), hay que recalcular la proxima corrida:
        // dejar la vieja haria que el cron nuevo no se respete hasta pasada una ejecucion.
        var reglaCambio = p.Tipo != req.Tipo || p.CronExpresion != cron || p.ZonaHoraria != zona;

        p.Titulo = req.Titulo.Trim();
        p.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        p.Prioridad = req.Prioridad;
        p.TableroId = req.TableroId;
        p.Tipo = req.Tipo;
        p.Periodicidad = req.Periodicidad;
        p.CronExpresion = cron;
        p.ZonaHoraria = zona;
        p.NotificarPorCorreo = req.NotificarPorCorreo;
        p.FechaProximaEjecucion = req.FechaProximaEjecucion;
        p.FechaFin = req.FechaFin;
        p.Activa = req.Activa;

        if (req.Tipo != TipoProgramacion.Cron) p.ProximaEjecucionUtc = null;
        else if (reglaCambio || p.ProximaEjecucionUtc is null)
            p.ProximaEjecucionUtc = CronHelper.ProximaEjecucion(cron, zona, DateTimeOffset.UtcNow);

        _db.ProgramacionTareaResponsables.RemoveRange(p.Responsables);
        p.Responsables.Clear();
        AplicarResponsables(p, req.Responsables);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ToggleActivaAsync(Guid id, bool activa, CancellationToken ct)
    {
        var p = await _db.ProgramacionTareas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        p.Activa = activa;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.ProgramacionTareas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        _db.ProgramacionTareas.Remove(p); // cascade borra responsables
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public CronPreviewResult PreviewCron(CronPreviewRequest req)
    {
        if (!CronHelper.EsValida(req.CronExpresion))
            return new CronPreviewResult(false, "Expresion no valida. Formato: minuto hora dia-del-mes mes dia-de-semana (ej. 0 8 * * 1).", Array.Empty<DateTimeOffset>());

        var cuantas = Math.Clamp(req.Cuantas, 1, 20);
        var proximas = CronHelper.Proximas(req.CronExpresion, req.ZonaHoraria, DateTimeOffset.UtcNow, cuantas);
        return proximas.Count == 0
            ? new CronPreviewResult(false, "La expresion es valida pero no vuelve a ocurrir.", proximas)
            : new CronPreviewResult(true, null, proximas);
    }

    // ----------------------------- Helpers -----------------------------

    private static string NormalizarZona(string? zona)
        => string.IsNullOrWhiteSpace(zona) ? CronHelper.ZonaHorariaPorDefecto : zona.Trim();

    /// <summary>
    /// Valida el cron al guardar (no al ejecutar): una expresion mala debe fallar en la cara
    /// del usuario, no en silencio dentro del job seis horas despues.
    /// </summary>
    private static string? NormalizarCron(TipoProgramacion tipo, string? cron)
    {
        if (tipo != TipoProgramacion.Cron) return null;
        if (string.IsNullOrWhiteSpace(cron))
            throw new InvalidOperationException("La expresion cron es obligatoria cuando la programacion es de tipo Cron.");
        var limpia = cron.Trim();
        if (!CronHelper.EsValida(limpia))
            throw new InvalidOperationException($"La expresion cron '{limpia}' no es valida. Formato: minuto hora dia-del-mes mes dia-de-semana (ej. 0 8 * * 1).");
        return limpia;
    }

    private static void AplicarResponsables(ProgramacionTarea p, IReadOnlyList<ResponsableProgramacionDto>? responsables)
    {
        if (responsables is null) return;
        foreach (var r in responsables.Where(x => x.PersonaId != Guid.Empty).DistinctBy(x => x.PersonaId))
            p.Responsables.Add(new ProgramacionTareaResponsable
            {
                PersonaId = r.PersonaId,
                NombreSnapshot = string.IsNullOrWhiteSpace(r.Nombre) ? null : r.Nombre.Trim()
            });
    }

    private async Task<Dictionary<Guid, string>> TableroNombresAsync(CancellationToken ct)
        => await _db.Tableros.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

    private static ProgramacionTareaDto ToDto(ProgramacionTarea p, Dictionary<Guid, string> tableros)
        => new(
            p.Id, p.Titulo, p.Descripcion, p.Prioridad,
            p.TableroId, p.TableroId is { } tid && tableros.TryGetValue(tid, out var n) ? n : null,
            p.Periodicidad, p.FechaProximaEjecucion, p.FechaFin, p.Activa,
            p.ModuloOrigenCodigo, p.EntidadOrigenId, p.OrigenReferencia,
            p.TareasGeneradas, p.UltimaEjecucion,
            p.Responsables.Select(r => new ResponsableProgramacionDto(r.PersonaId, r.NombreSnapshot)).ToList(),
            p.Tipo, p.CronExpresion, p.ZonaHoraria, p.ProximaEjecucionUtc, p.NotificarPorCorreo);
}
