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
            CreadoPorUsuarioId = usuarioId,
            HorizonteDias = Math.Clamp(req.HorizonteDias, 0, MaxHorizonteDias)
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

        // Cambios que invalidan las tareas ya adelantadas: cambia CUANDO ocurren o QUE dicen.
        // No entran aqui NotificarPorCorreo ni Activa (no alteran las ocurrencias ya escritas).
        var horizonte = Math.Clamp(req.HorizonteDias, 0, MaxHorizonteDias);
        var regenerar = reglaCambio
            || p.Periodicidad != req.Periodicidad
            || p.FechaProximaEjecucion != req.FechaProximaEjecucion
            || p.FechaFin != req.FechaFin
            || p.HorizonteDias != horizonte
            || p.TableroId != req.TableroId
            || p.Titulo != req.Titulo.Trim()
            || p.Prioridad != req.Prioridad;

        // Si el usuario no movio la fecha de arranque, el puntero que trae el request es el que
        // el job dejo tras adelantar el horizonte (ej. ya en 2027). Hay que saberlo para poder
        // rebobinarlo despues: si no, retirar las tareas futuras las borraria sin rehacerlas.
        var movioElInicio = p.FechaProximaEjecucion != req.FechaProximaEjecucion;

        p.Titulo = req.Titulo.Trim();
        p.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        p.Prioridad = req.Prioridad;
        p.TableroId = req.TableroId;
        p.Tipo = req.Tipo;
        p.Periodicidad = req.Periodicidad;
        p.CronExpresion = cron;
        p.ZonaHoraria = zona;
        p.NotificarPorCorreo = req.NotificarPorCorreo;
        p.HorizonteDias = horizonte;
        p.FechaProximaEjecucion = req.FechaProximaEjecucion;
        p.FechaFin = req.FechaFin;
        p.Activa = req.Activa;

        if (req.Tipo != TipoProgramacion.Cron) p.ProximaEjecucionUtc = null;
        else if (reglaCambio || p.ProximaEjecucionUtc is null)
            p.ProximaEjecucionUtc = CronHelper.ProximaEjecucion(cron, zona, DateTimeOffset.UtcNow);

        var responsablesCambiaron = ResponsablesCambiaron(p, req.Responsables);
        _db.ProgramacionTareaResponsables.RemoveRange(p.Responsables);
        p.Responsables.Clear();
        AplicarResponsables(p, req.Responsables);

        if (regenerar || responsablesCambiaron)
        {
            var primeraRetirada = await LimpiarFuturoAsync(p.Id, ct);

            // Rebobinar el puntero a la ocurrencia mas temprana que se retiro, para que el job
            // vuelva a materializarlas. Solo si el usuario no fijo el arranque el mismo: en ese
            // caso su fecha manda y lo anterior simplemente deja de existir.
            if (primeraRetirada.HasValue && !movioElInicio && primeraRetirada.Value < p.FechaProximaEjecucion)
            {
                p.FechaProximaEjecucion = primeraRetirada.Value;
                if (p.Tipo == TipoProgramacion.Cron)
                    p.ProximaEjecucionUtc = PlanificadorOcurrencias.Instante(primeraRetirada.Value);
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Retira las tareas FUTURAS que genero esta regla y que nadie ha tocado, para que el job las
    /// vuelva a crear con los datos nuevos. Es el precio de adelantar el calendario: si se corrige
    /// la regla en marzo, las visitas de agosto que ya estaban puestas deben reflejar la correccion.
    ///
    /// "No tocada" se interpreta de forma conservadora: sigue en el estado inicial del tablero, sin
    /// progreso, sin fecha de completada y sin comentarios. Si alguien la empezo, la movio, la
    /// comento o la cerro, es trabajo real y se respeta aunque quede desalineada de la regla.
    /// Lo pasado nunca se toca: es historia.
    ///
    /// Se marca Eliminada en vez de borrar: es el soft-delete que usa todo el modulo de Tareas, y
    /// ademas tarea_historial es append-only por trigger, asi que un DELETE real revienta en BD.
    /// </summary>
    /// <returns>Fecha de la ocurrencia mas temprana retirada, o null si no se retiro ninguna.</returns>
    private async Task<DateOnly?> LimpiarFuturoAsync(Guid programacionId, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;

        var candidatas = await _db.Tareas
            .Where(t => t.ProgramacionTareaId == programacionId
                     && !t.Eliminada
                     && t.OcurrenciaUtc != null && t.OcurrenciaUtc > ahora
                     && t.Progreso == 0
                     && t.FechaCompletada == null)
            .ToListAsync(ct);
        if (candidatas.Count == 0) return null;

        var ids = candidatas.Select(t => t.Id).ToList();

        // Estados base del tablero: si la tarea ya no esta en el primero, alguien la movio.
        var estadosBase = await _db.TareasEstados.AsNoTracking()
            .Where(e => e.EsBase)
            .Select(e => e.Id)
            .ToListAsync(ct);

        var conComentario = await _db.TareaComentarios.AsNoTracking()
            .Where(c => ids.Contains(c.TareaId))
            .Select(c => c.TareaId)
            .Distinct()
            .ToListAsync(ct);

        var intactas = candidatas
            .Where(t => estadosBase.Contains(t.EstadoId) && !conComentario.Contains(t.Id))
            .ToList();
        if (intactas.Count == 0) return null;

        foreach (var t in intactas) t.Eliminada = true;
        return intactas.Min(t => DateOnly.FromDateTime(t.OcurrenciaUtc!.Value.UtcDateTime));
    }

    private static bool ResponsablesCambiaron(ProgramacionTarea p, IReadOnlyList<ResponsableProgramacionDto>? nuevos)
    {
        var antes = p.Responsables.Select(r => r.PersonaId).OrderBy(x => x).ToList();
        var ahora = (nuevos ?? Array.Empty<ResponsableProgramacionDto>())
            .Select(r => r.PersonaId).Distinct().OrderBy(x => x).ToList();
        return !antes.SequenceEqual(ahora);
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
            p.Tipo, p.CronExpresion, p.ZonaHoraria, p.ProximaEjecucionUtc, p.NotificarPorCorreo,
            p.HorizonteDias);

    /// <summary>
    /// Tope del horizonte de generacion anticipada: 2 anios. Evita que un dedo de mas convierta
    /// una regla diaria en decenas de miles de tareas.
    /// </summary>
    private const int MaxHorizonteDias = 730;
}
