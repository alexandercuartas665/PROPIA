using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Mantenimiento;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Mantenimiento;

/// <summary>
/// Modulo 2.11 Mantenimiento y Activos (spec v1.0 MVP).
/// Gestiona planes preventivos, intervenciones, bitacora append-only,
/// cambio de estado del activo y vinculo bidireccional con 2.10 Tareas.
/// </summary>
public class MantenimientoService : IMantenimientoService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    public MantenimientoService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    // ===========================================================================
    // Helpers de calculo
    // ===========================================================================

    /// <summary>Equivalencia en dias para frecuencias estandar (spec 22 notas).</summary>
    private static int FrecuenciaEnDias(FrecuenciaMantenimiento f, int? personalizada) => f switch
    {
        FrecuenciaMantenimiento.Semanal => 7,
        FrecuenciaMantenimiento.Quincenal => 15,
        FrecuenciaMantenimiento.Mensual => 30,
        FrecuenciaMantenimiento.Trimestral => 90,
        FrecuenciaMantenimiento.Semestral => 180,
        FrecuenciaMantenimiento.Anual => 365,
        FrecuenciaMantenimiento.Personalizada => personalizada ?? 30,
        _ => 30
    };

    /// <summary>Semaforo del plan (spec seccion 8). NEGRO solo aplica al activo sin ningun plan.</summary>
    private static SemaforoMantenimiento CalcularSemaforoPlan(MantenimientoPlan plan, DateOnly hoy)
    {
        if (!plan.Activo) return SemaforoMantenimiento.Negro;
        var limiteAmarillo = hoy.AddDays(plan.DiasAlertaPrevio);
        if (plan.ProximaEjecucion < hoy) return SemaforoMantenimiento.Rojo;
        if (plan.ProximaEjecucion <= limiteAmarillo) return SemaforoMantenimiento.Amarillo;
        return SemaforoMantenimiento.Verde;
    }

    /// <summary>Combina semaforos de varios planes para mostrar el "peor" del activo.</summary>
    private static SemaforoMantenimiento CombinarSemaforos(IEnumerable<SemaforoMantenimiento> values)
    {
        var lista = values.ToList();
        if (lista.Count == 0) return SemaforoMantenimiento.Negro;
        if (lista.Any(s => s == SemaforoMantenimiento.Rojo)) return SemaforoMantenimiento.Rojo;
        if (lista.Any(s => s == SemaforoMantenimiento.Amarillo)) return SemaforoMantenimiento.Amarillo;
        if (lista.Any(s => s == SemaforoMantenimiento.Verde)) return SemaforoMantenimiento.Verde;
        return SemaforoMantenimiento.Negro;
    }

    // ===========================================================================
    // Panel y resumen
    // ===========================================================================

    public async Task<IReadOnlyList<ActivoPanelDto>> ListarActivosPanelAsync(
        TipoActivoMantenimiento? activoTipo,
        SemaforoMantenimiento? semaforo,
        string? query,
        CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var resultado = new List<ActivoPanelDto>();

        var equipos = activoTipo is not null && activoTipo != TipoActivoMantenimiento.Equipo
            ? new List<EquipoActivo>()
            : await _db.EquiposActivos.AsNoTracking().ToListAsync(ct);

        var zonas = activoTipo is not null && activoTipo != TipoActivoMantenimiento.ZonaComun
            ? new List<ZonaComun>()
            : await _db.ZonasComunes.AsNoTracking().ToListAsync(ct);

        var planes = await _db.MantenimientoPlanes.AsNoTracking()
            .Include(p => p.ProveedorPreferido)
            .ToListAsync(ct);

        var intervenciones = await _db.MantenimientoIntervenciones.AsNoTracking().ToListAsync(ct);

        ActivoPanelDto BuildDto(TipoActivoMantenimiento tipo, Guid id, string nombre, string? categoria, string estado)
        {
            var planesActivo = planes.Where(p => p.ActivoTipo == tipo && p.ActivoId == id && p.Activo).ToList();
            var intervencionesActivo = intervenciones.Where(i => i.ActivoTipo == tipo && i.ActivoId == id).ToList();

            SemaforoMantenimiento sem = SemaforoMantenimiento.Negro;
            MantenimientoPlan? planProximo = null;
            int? diasParaVencer = null;
            DateOnly? proxima = null;

            if (planesActivo.Count > 0)
            {
                var semaforos = planesActivo.Select(p => CalcularSemaforoPlan(p, hoy)).ToList();
                sem = CombinarSemaforos(semaforos);
                planProximo = planesActivo.OrderBy(p => p.ProximaEjecucion).First();
                proxima = planProximo.ProximaEjecucion;
                diasParaVencer = planProximo.ProximaEjecucion.DayNumber - hoy.DayNumber;
            }

            var ultimo = intervencionesActivo
                .Where(i => i.Estado == EstadoIntervencion.Completada && i.FechaCierre is not null)
                .OrderByDescending(i => i.FechaCierre)
                .FirstOrDefault();

            var abiertas = intervencionesActivo.Count(i =>
                i.Estado != EstadoIntervencion.Completada && i.Estado != EstadoIntervencion.Cancelada);

            return new ActivoPanelDto(
                tipo, id, nombre, categoria, estado, sem,
                planProximo?.Nombre, proxima, diasParaVencer,
                planProximo?.ProveedorPreferido is null
                    ? null
                    : $"{planProximo.ProveedorPreferido!.Nombres} {planProximo.ProveedorPreferido!.Apellidos}".Trim(),
                ultimo?.FechaCierre,
                planesActivo.Count,
                abiertas);
        }

        foreach (var e in equipos)
        {
            var dto = BuildDto(TipoActivoMantenimiento.Equipo, e.Id, e.Nombre, e.Categoria.ToString(), e.Estado.ToString());
            resultado.Add(dto);
        }
        foreach (var z in zonas)
        {
            var dto = BuildDto(TipoActivoMantenimiento.ZonaComun, z.Id, z.Nombre, z.Categoria.ToString(), z.Estado.ToString());
            resultado.Add(dto);
        }

        if (semaforo is not null)
            resultado = resultado.Where(a => a.Semaforo == semaforo).ToList();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var qn = query.Trim().ToLowerInvariant();
            resultado = resultado.Where(a => a.Nombre.ToLowerInvariant().Contains(qn)).ToList();
        }

        return resultado
            .OrderBy(a => a.Semaforo == SemaforoMantenimiento.Rojo ? 0
                : a.Semaforo == SemaforoMantenimiento.Amarillo ? 1
                : a.Semaforo == SemaforoMantenimiento.Negro ? 2 : 3)
            .ThenBy(a => a.Nombre)
            .ToList();
    }

    public async Task<ResumenMantenimientoDto> GetResumenAsync(CancellationToken ct)
    {
        var panel = await ListarActivosPanelAsync(null, null, null, ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);

        var verde = panel.Count(a => a.Semaforo == SemaforoMantenimiento.Verde);
        var amarillo = panel.Count(a => a.Semaforo == SemaforoMantenimiento.Amarillo);
        var rojo = panel.Count(a => a.Semaforo == SemaforoMantenimiento.Rojo);
        var negro = panel.Count(a => a.Semaforo == SemaforoMantenimiento.Negro);

        var intervenciones = await _db.MantenimientoIntervenciones.AsNoTracking().ToListAsync(ct);
        var abiertas = intervenciones.Count(i =>
            i.Estado != EstadoIntervencion.Completada && i.Estado != EstadoIntervencion.Cancelada);
        var vencidas = intervenciones.Count(i =>
            i.Estado != EstadoIntervencion.Completada && i.Estado != EstadoIntervencion.Cancelada
            && i.FechaProgramada is not null && i.FechaProgramada < hoy);

        var preventivosMes = intervenciones.Count(i =>
            i.Tipo == TipoIntervencionMantenimiento.Preventivo
            && i.Estado == EstadoIntervencion.Completada
            && i.FechaCierre is not null && i.FechaCierre >= inicioMes);
        var correctivosMes = intervenciones.Count(i =>
            i.Tipo == TipoIntervencionMantenimiento.Correctivo
            && i.Estado == EstadoIntervencion.Completada
            && i.FechaCierre is not null && i.FechaCierre >= inicioMes);

        return new ResumenMantenimientoDto(verde, amarillo, rojo, negro, abiertas, vencidas, preventivosMes, correctivosMes);
    }

    // ===========================================================================
    // Planes preventivos
    // ===========================================================================

    public async Task<IReadOnlyList<PlanDto>> ListarPlanesAsync(
        TipoActivoMantenimiento? activoTipo,
        Guid? activoId,
        bool? activos,
        CancellationToken ct)
    {
        var q = _db.MantenimientoPlanes.AsNoTracking()
            .Include(p => p.ProveedorPreferido)
            .AsQueryable();

        if (activoTipo is not null) q = q.Where(p => p.ActivoTipo == activoTipo);
        if (activoId is not null) q = q.Where(p => p.ActivoId == activoId);
        if (activos is not null) q = q.Where(p => p.Activo == activos);

        var lista = await q.OrderByDescending(p => p.Activo).ThenBy(p => p.ProximaEjecucion).ToListAsync(ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var equiposNombres = await _db.EquiposActivos.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
        var zonasNombres = await _db.ZonasComunes.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);

        return lista.Select(p => new PlanDto(
            p.Id, p.ActivoTipo, p.ActivoId,
            p.ActivoTipo == TipoActivoMantenimiento.Equipo
                ? equiposNombres.GetValueOrDefault(p.ActivoId, "(equipo no encontrado)")
                : zonasNombres.GetValueOrDefault(p.ActivoId, "(zona no encontrada)"),
            p.Nombre, p.Descripcion, p.Frecuencia, p.FrecuenciaDias,
            p.FechaInicio, p.ProximaEjecucion,
            p.ProveedorPreferidoId,
            p.ProveedorPreferido is null ? null : $"{p.ProveedorPreferido.Nombres} {p.ProveedorPreferido.Apellidos}".Trim(),
            p.Disparo, p.DiasAlertaPrevio, p.GeneraNotifResidentes, p.Activo,
            CalcularSemaforoPlan(p, hoy),
            p.ProximaEjecucion.DayNumber - hoy.DayNumber)).ToList();
    }

    public async Task<PlanDto?> GetPlanAsync(Guid id, CancellationToken ct)
    {
        var planes = await ListarPlanesAsync(null, null, null, ct);
        return planes.FirstOrDefault(p => p.Id == id);
    }

    public async Task<PlanDto> CrearPlanAsync(CrearPlanRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        if (req.FechaInicio < hoy) throw new InvalidOperationException("RN-02: La fecha de inicio no puede estar en el pasado.");

        await ValidarActivoAsync(req.ActivoTipo, req.ActivoId, ct);

        if (req.Frecuencia == FrecuenciaMantenimiento.Personalizada)
        {
            if (req.FrecuenciaDias is not int dias || dias < 1 || dias > 365)
                throw new InvalidOperationException("Frecuencia personalizada: dias entre 1 y 365.");
        }

        if (req.DiasAlertaPrevio < 0 || req.DiasAlertaPrevio > 90)
            throw new InvalidOperationException("DiasAlertaPrevio entre 0 y 90.");

        var plan = new MantenimientoPlan
        {
            ActivoTipo = req.ActivoTipo,
            ActivoId = req.ActivoId,
            Nombre = req.Nombre.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Frecuencia = req.Frecuencia,
            FrecuenciaDias = req.Frecuencia == FrecuenciaMantenimiento.Personalizada ? req.FrecuenciaDias : null,
            FechaInicio = req.FechaInicio,
            ProximaEjecucion = req.FechaInicio,
            ProveedorPreferidoId = req.ProveedorPreferidoId,
            Disparo = req.Disparo,
            DiasAlertaPrevio = req.DiasAlertaPrevio,
            GeneraNotifResidentes = req.GeneraNotifResidentes,
            Activo = true,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };

        _db.MantenimientoPlanes.Add(plan);
        await _db.SaveChangesAsync(ct);

        return (await GetPlanAsync(plan.Id, ct))!;
    }

    public async Task<bool> ActualizarPlanAsync(Guid id, ActualizarPlanRequest req, CancellationToken ct)
    {
        var plan = await _db.MantenimientoPlanes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return false;
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");

        if (req.Frecuencia == FrecuenciaMantenimiento.Personalizada)
        {
            if (req.FrecuenciaDias is not int dias || dias < 1 || dias > 365)
                throw new InvalidOperationException("Frecuencia personalizada: dias entre 1 y 365.");
        }

        plan.Nombre = req.Nombre.Trim();
        plan.Descripcion = req.Descripcion?.Trim();
        plan.Frecuencia = req.Frecuencia;
        plan.FrecuenciaDias = req.Frecuencia == FrecuenciaMantenimiento.Personalizada ? req.FrecuenciaDias : null;
        plan.ProveedorPreferidoId = req.ProveedorPreferidoId;
        plan.Disparo = req.Disparo;
        plan.DiasAlertaPrevio = req.DiasAlertaPrevio;
        plan.GeneraNotifResidentes = req.GeneraNotifResidentes;
        plan.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesactivarPlanAsync(Guid id, CancellationToken ct)
    {
        var plan = await _db.MantenimientoPlanes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return false;
        plan.Activo = false;
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReactivarPlanAsync(Guid id, CancellationToken ct)
    {
        var plan = await _db.MantenimientoPlanes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return false;
        plan.Activo = true;
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ValidarActivoAsync(TipoActivoMantenimiento tipo, Guid activoId, CancellationToken ct)
    {
        var existe = tipo == TipoActivoMantenimiento.Equipo
            ? await _db.EquiposActivos.AnyAsync(e => e.Id == activoId, ct)
            : await _db.ZonasComunes.AnyAsync(z => z.Id == activoId, ct);
        if (!existe) throw new InvalidOperationException("Activo no encontrado.");
    }

    // ===========================================================================
    // Intervenciones
    // ===========================================================================

    public async Task<IReadOnlyList<IntervencionListaDto>> ListarIntervencionesAsync(
        EstadoIntervencion? estado,
        TipoIntervencionMantenimiento? tipo,
        TipoActivoMantenimiento? activoTipo,
        Guid? activoId,
        Guid? proveedorId,
        string? query,
        CancellationToken ct)
    {
        var q = _db.MantenimientoIntervenciones.AsNoTracking()
            .Include(i => i.Proveedor)
            .Include(i => i.Tarea)
            .AsQueryable();

        if (estado is not null) q = q.Where(i => i.Estado == estado);
        if (tipo is not null) q = q.Where(i => i.Tipo == tipo);
        if (activoTipo is not null) q = q.Where(i => i.ActivoTipo == activoTipo);
        if (activoId is not null) q = q.Where(i => i.ActivoId == activoId);
        if (proveedorId is not null) q = q.Where(i => i.ProveedorId == proveedorId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var qn = query.Trim().ToLowerInvariant();
            q = q.Where(i => i.Titulo.ToLower().Contains(qn) || i.Codigo.ToLower().Contains(qn));
        }

        var lista = await q.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var equiposNombres = await _db.EquiposActivos.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
        var zonasNombres = await _db.ZonasComunes.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);

        return lista.Select(i => new IntervencionListaDto(
            i.Id, i.Codigo, i.Tipo, i.ActivoTipo, i.ActivoId,
            i.ActivoTipo == TipoActivoMantenimiento.Equipo
                ? equiposNombres.GetValueOrDefault(i.ActivoId, "(equipo)")
                : zonasNombres.GetValueOrDefault(i.ActivoId, "(zona)"),
            i.Titulo, i.Estado, i.Prioridad, i.Origen,
            i.Proveedor is null ? null : $"{i.Proveedor.Nombres} {i.Proveedor.Apellidos}".Trim(),
            i.FechaProgramada, i.FechaCierre,
            i.TareaId, i.Tarea?.NumeroTarea,
            i.FechaProgramada is not null && i.FechaProgramada < hoy
                && i.Estado != EstadoIntervencion.Completada
                && i.Estado != EstadoIntervencion.Cancelada
        )).ToList();
    }

    public async Task<IntervencionDetalleDto?> GetIntervencionAsync(Guid id, CancellationToken ct)
    {
        var i = await _db.MantenimientoIntervenciones.AsNoTracking()
            .Include(x => x.Proveedor)
            .Include(x => x.ResponsableInterno)
            .Include(x => x.Plan)
            .Include(x => x.Tarea)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return null;

        var equiposNombres = await _db.EquiposActivos.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
        var zonasNombres = await _db.ZonasComunes.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);

        var bitacora = await ListarBitacoraAsync(id, ct);

        return new IntervencionDetalleDto(
            i.Id, i.Codigo, i.Tipo, i.ActivoTipo, i.ActivoId,
            i.ActivoTipo == TipoActivoMantenimiento.Equipo
                ? equiposNombres.GetValueOrDefault(i.ActivoId, "(equipo)")
                : zonasNombres.GetValueOrDefault(i.ActivoId, "(zona)"),
            i.PlanId, i.Plan?.Nombre,
            i.Origen, i.OrigenReferenciaId,
            i.Titulo, i.Descripcion, i.Estado, i.Prioridad,
            i.ProveedorId, i.Proveedor is null ? null : $"{i.Proveedor.Nombres} {i.Proveedor.Apellidos}".Trim(),
            i.ResponsableInternoId, i.ResponsableInterno is null ? null : $"{i.ResponsableInterno.Nombres} {i.ResponsableInterno.Apellidos}".Trim(),
            i.FechaProgramada, i.FechaInicioReal, i.FechaCierre,
            i.TareaId, i.Tarea?.NumeroTarea,
            i.CambioEstadoActivo, i.EstadoActivoNuevo, i.NotificarResidentes, i.MotivoCancelacion,
            i.CreatedAt, bitacora);
    }

    public async Task<IntervencionDetalleDto> CrearIntervencionAsync(CrearIntervencionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo obligatorio.");
        await ValidarActivoAsync(req.ActivoTipo, req.ActivoId, ct);

        if (req.PlanId is not null)
        {
            var plan = await _db.MantenimientoPlanes.FirstOrDefaultAsync(p => p.Id == req.PlanId, ct);
            if (plan is null) throw new InvalidOperationException("Plan no encontrado.");
            if (plan.ActivoTipo != req.ActivoTipo || plan.ActivoId != req.ActivoId)
                throw new InvalidOperationException("El plan no corresponde al activo indicado.");
        }

        var codigo = await GenerarCodigoIntervencionAsync(ct);

        var intervencion = new MantenimientoIntervencion
        {
            Codigo = codigo,
            Tipo = req.Tipo,
            ActivoTipo = req.ActivoTipo,
            ActivoId = req.ActivoId,
            PlanId = req.PlanId,
            Origen = req.Origen,
            OrigenReferenciaId = req.OrigenReferenciaId,
            Titulo = req.Titulo.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Estado = EstadoIntervencion.Programada,
            Prioridad = req.Prioridad,
            ProveedorId = req.ProveedorId,
            ResponsableInternoId = req.ResponsableInternoId,
            FechaProgramada = req.FechaProgramada,
            NotificarResidentes = req.NotificarResidentes,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };

        _db.MantenimientoIntervenciones.Add(intervencion);
        await _db.SaveChangesAsync(ct);

        // RN-03: toda intervencion genera una tarea en 2.10 (atomico - si falla, rollback)
        var tareaId = await CrearTareaVinculadaAsync(intervencion, ct);
        intervencion.TareaId = tareaId;

        // Bitacora inicial del sistema
        _db.MantenimientoBitacora.Add(new MantenimientoBitacora
        {
            IntervencionId = intervencion.Id,
            AutorUsuarioId = GetUsuarioActualId(),
            TipoAutor = TipoAutorBitacoraMantenimiento.Sistema,
            Contenido = $"Intervencion {codigo} creada. Origen: {req.Origen}. Tarea vinculada generada."
        });
        await _db.SaveChangesAsync(ct);

        return (await GetIntervencionAsync(intervencion.Id, ct))!;
    }

    public async Task<bool> ActualizarIntervencionAsync(Guid id, ActualizarIntervencionRequest req, CancellationToken ct)
    {
        var i = await _db.MantenimientoIntervenciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return false;
        if (i.Estado == EstadoIntervencion.Completada || i.Estado == EstadoIntervencion.Cancelada)
            throw new InvalidOperationException("No se puede editar una intervencion terminal.");

        i.Titulo = req.Titulo.Trim();
        i.Descripcion = req.Descripcion?.Trim();
        i.Prioridad = req.Prioridad;
        i.ProveedorId = req.ProveedorId;
        i.ResponsableInternoId = req.ResponsableInternoId;
        i.FechaProgramada = req.FechaProgramada;
        i.NotificarResidentes = req.NotificarResidentes;
        i.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CambiarEstadoIntervencionAsync(Guid id, CambiarEstadoIntervencionRequest req, CancellationToken ct)
    {
        var i = await _db.MantenimientoIntervenciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return false;
        if (i.Estado == EstadoIntervencion.Completada || i.Estado == EstadoIntervencion.Cancelada)
            throw new InvalidOperationException("No se puede cambiar el estado de una intervencion terminal.");

        if (req.NuevoEstado == EstadoIntervencion.Completada)
            throw new InvalidOperationException("Use CerrarIntervencion para cerrar (requiere bitacora).");
        if (req.NuevoEstado == EstadoIntervencion.Cancelada)
            throw new InvalidOperationException("Use CancelarIntervencion para cancelar (requiere motivo).");

        var anterior = i.Estado;
        i.Estado = req.NuevoEstado;
        if (req.NuevoEstado == EstadoIntervencion.EnEjecucion && i.FechaInicioReal is null)
            i.FechaInicioReal = DateOnly.FromDateTime(DateTime.UtcNow);
        i.UpdatedAt = DateTimeOffset.UtcNow;

        _db.MantenimientoBitacora.Add(new MantenimientoBitacora
        {
            IntervencionId = i.Id,
            AutorUsuarioId = GetUsuarioActualId(),
            TipoAutor = TipoAutorBitacoraMantenimiento.Sistema,
            Contenido = string.IsNullOrWhiteSpace(req.ContenidoBitacora)
                ? $"Cambio de estado: {anterior} -> {req.NuevoEstado}. {req.Motivo ?? ""}".Trim()
                : req.ContenidoBitacora.Trim()
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CerrarIntervencionAsync(Guid id, CerrarIntervencionRequest req, CancellationToken ct)
    {
        var i = await _db.MantenimientoIntervenciones.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return false;
        if (i.Estado == EstadoIntervencion.Completada || i.Estado == EstadoIntervencion.Cancelada)
            throw new InvalidOperationException("Intervencion ya esta cerrada.");

        if (string.IsNullOrWhiteSpace(req.ContenidoBitacora))
            throw new InvalidOperationException("Cerrar requiere una entrada de bitacora (validacion seccion 16).");

        // Bitacora final
        _db.MantenimientoBitacora.Add(new MantenimientoBitacora
        {
            IntervencionId = i.Id,
            AutorUsuarioId = GetUsuarioActualId(),
            TipoAutor = TipoAutorBitacoraMantenimiento.Administrador,
            Contenido = req.ContenidoBitacora.Trim()
        });

        i.Estado = EstadoIntervencion.Completada;
        i.FechaCierre = req.FechaCierre;
        i.CambioEstadoActivo = req.CambiarEstadoActivo;
        i.EstadoActivoNuevo = req.EstadoActivoNuevo;
        i.UpdatedAt = DateTimeOffset.UtcNow;

        // Cambio de estado del activo (si aplica). RN-07 manual y deliberado.
        if (req.CambiarEstadoActivo && !string.IsNullOrWhiteSpace(req.EstadoActivoNuevo))
        {
            await EjecutarCambioEstadoActivoAsync(
                i.ActivoTipo, i.ActivoId, req.EstadoActivoNuevo!,
                req.MotivoCambioEstado ?? $"Cierre intervencion {i.Codigo}",
                req.NotificarResidentes, i.Id, ct);
        }

        // Si es preventivo con plan, recalcular proxima_ejecucion
        if (i.Tipo == TipoIntervencionMantenimiento.Preventivo && i.Plan is not null && i.Plan.Activo)
        {
            var dias = FrecuenciaEnDias(i.Plan.Frecuencia, i.Plan.FrecuenciaDias);
            i.Plan.ProximaEjecucion = req.FechaCierre.AddDays(dias);
            i.Plan.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelarIntervencionAsync(Guid id, CancelarIntervencionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.MotivoCancelacion))
            throw new InvalidOperationException("Motivo de cancelacion obligatorio.");

        var i = await _db.MantenimientoIntervenciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return false;
        if (i.Estado == EstadoIntervencion.Completada || i.Estado == EstadoIntervencion.Cancelada)
            throw new InvalidOperationException("Intervencion ya esta cerrada.");

        i.Estado = EstadoIntervencion.Cancelada;
        i.MotivoCancelacion = req.MotivoCancelacion.Trim();
        i.UpdatedAt = DateTimeOffset.UtcNow;

        _db.MantenimientoBitacora.Add(new MantenimientoBitacora
        {
            IntervencionId = i.Id,
            AutorUsuarioId = GetUsuarioActualId(),
            TipoAutor = TipoAutorBitacoraMantenimiento.Administrador,
            Contenido = $"Intervencion cancelada. Motivo: {req.MotivoCancelacion.Trim()}"
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerarCodigoIntervencionAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefijo = $"MNT-{year}-";
        var ultimos = await _db.MantenimientoIntervenciones.AsNoTracking()
            .Where(x => x.Codigo.StartsWith(prefijo))
            .Select(x => x.Codigo)
            .ToListAsync(ct);
        int max = 0;
        foreach (var c in ultimos)
        {
            if (int.TryParse(c.Substring(prefijo.Length), out var s) && s > max) max = s;
        }
        return $"{prefijo}{(max + 1):D4}";
    }

    private async Task<Guid> CrearTareaVinculadaAsync(MantenimientoIntervencion i, CancellationToken ct)
    {
        // Asegurar estados base de Tareas (idempotente)
        if (!await _db.TareasEstados.AnyAsync(ct))
        {
            foreach (var (nombre, orden, esTerminal) in EstadoTareaBase.Base)
            {
                _db.TareasEstados.Add(new TareaEstado
                {
                    Nombre = nombre,
                    Orden = orden,
                    EsTerminal = esTerminal,
                    EsBase = true,
                    Activo = true,
                    Color = nombre switch
                    {
                        EstadoTareaBase.Pendiente => "#94a3b8",
                        EstadoTareaBase.EnProgreso => "#3b82f6",
                        EstadoTareaBase.EnRevision => "#f59e0b",
                        EstadoTareaBase.Bloqueada => "#ef4444",
                        EstadoTareaBase.Completada => "#22c55e",
                        EstadoTareaBase.Cancelada => "#6b7280",
                        _ => null
                    }
                });
            }
            await _db.SaveChangesAsync(ct);
        }
        var estadoPendienteId = await _db.TareasEstados.Where(e => e.Nombre == EstadoTareaBase.Pendiente)
            .Select(e => e.Id).FirstAsync(ct);

        // Numero secuencial T-{ANO}-{SEQ}
        var year = DateTime.UtcNow.Year;
        var prefijo = $"T-{year}-";
        var ultimos = await _db.Tareas.AsNoTracking()
            .Where(t => t.NumeroTarea.StartsWith(prefijo))
            .Select(t => t.NumeroTarea)
            .ToListAsync(ct);
        int max = 0;
        foreach (var n in ultimos)
        {
            if (int.TryParse(n.Substring(prefijo.Length), out var s) && s > max) max = s;
        }
        var numero = $"{prefijo}{(max + 1):D4}";

        // Nombre del activo para el titulo de la tarea
        var nombreActivo = i.ActivoTipo == TipoActivoMantenimiento.Equipo
            ? await _db.EquiposActivos.Where(e => e.Id == i.ActivoId).Select(e => e.Nombre).FirstOrDefaultAsync(ct)
            : await _db.ZonasComunes.Where(z => z.Id == i.ActivoId).Select(z => z.Nombre).FirstOrDefaultAsync(ct);

        var prioridadTarea = i.Prioridad switch
        {
            PrioridadIntervencion.Urgente => PrioridadTarea.Urgente,
            PrioridadIntervencion.Alta => PrioridadTarea.Alta,
            PrioridadIntervencion.Baja => PrioridadTarea.Baja,
            _ => PrioridadTarea.Normal
        };

        var tarea = new Tarea
        {
            NumeroTarea = numero,
            Titulo = $"[{i.Tipo.ToString().ToUpperInvariant()}] {nombreActivo ?? "Activo"} - {i.Titulo}",
            Descripcion = i.Descripcion,
            EstadoId = estadoPendienteId,
            Prioridad = prioridadTarea,
            AsignadoPersonaId = i.ProveedorId ?? i.ResponsableInternoId,
            FechaVencimiento = i.FechaProgramada,
            Origen = OrigenTarea.ModuloExterno,
            ModuloOrigenCodigo = "2.11",
            ModuloOrigenEntidadId = i.Id,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.Tareas.Add(tarea);

        _db.TareaHistorial.Add(new TareaHistorial
        {
            Tarea = tarea,
            TipoEvento = TipoEventoTarea.Creada,
            Descripcion = $"Tarea generada desde mantenimiento {i.Codigo}",
            RealizadoPorUsuarioId = GetUsuarioActualId(),
            OcurridoAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return tarea.Id;
    }

    // ===========================================================================
    // Bitacora
    // ===========================================================================

    public async Task<BitacoraEntradaDto> AgregarEntradaBitacoraAsync(Guid intervencionId, AgregarBitacoraRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Contenido))
            throw new InvalidOperationException("Contenido obligatorio.");

        var existe = await _db.MantenimientoIntervenciones.AnyAsync(i => i.Id == intervencionId, ct);
        if (!existe) throw new InvalidOperationException("Intervencion no encontrada.");

        var entrada = new MantenimientoBitacora
        {
            IntervencionId = intervencionId,
            AutorUsuarioId = GetUsuarioActualId(),
            TipoAutor = req.TipoAutor,
            Contenido = req.Contenido.Trim()
        };
        _db.MantenimientoBitacora.Add(entrada);
        await _db.SaveChangesAsync(ct);

        return new BitacoraEntradaDto(entrada.Id, entrada.AutorUsuarioId, "(usuario)",
            entrada.TipoAutor, entrada.Contenido, entrada.CreatedAt, new List<AdjuntoBitacoraDto>());
    }

    public async Task<IReadOnlyList<BitacoraEntradaDto>> ListarBitacoraAsync(Guid intervencionId, CancellationToken ct)
    {
        var entradas = await _db.MantenimientoBitacora.AsNoTracking()
            .Where(b => b.IntervencionId == intervencionId)
            .Include(b => b.Adjuntos)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

        // Resolver nombres de autores (best-effort)
        var autorIds = entradas.Select(e => e.AutorUsuarioId).Distinct().ToList();
        var usuariosPorId = await _db.Users.AsNoTracking()
            .Where(u => autorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.PersonaId })
            .ToListAsync(ct);
        var personaIds = usuariosPorId.Select(u => u.PersonaId).Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();
        var personasPorId = await _db.Personas.AsNoTracking()
            .Where(p => personaIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);

        string ResolverNombre(Guid uid)
        {
            var u = usuariosPorId.FirstOrDefault(x => x.Id == uid);
            if (u is null) return "(usuario)";
            if (u.PersonaId is Guid pid && personasPorId.TryGetValue(pid, out var n)) return n;
            return "(usuario)";
        }

        return entradas.Select(b => new BitacoraEntradaDto(
            b.Id, b.AutorUsuarioId, ResolverNombre(b.AutorUsuarioId), b.TipoAutor, b.Contenido, b.CreatedAt,
            b.Adjuntos.Select(a => new AdjuntoBitacoraDto(
                a.Id, a.NombreArchivo, a.TipoMime, a.TamanoBytes, a.UrlStorage, a.CreatedAt)).ToList())).ToList();
    }

    // ===========================================================================
    // Cambio de estado del activo
    // ===========================================================================

    public Task<bool> CambiarEstadoActivoAsync(CambioEstadoActivoRequest req, CancellationToken ct) =>
        EjecutarCambioEstadoActivoAsync(req.ActivoTipo, req.ActivoId, req.EstadoNuevo,
            req.Motivo, req.NotificarResidentes, req.IntervencionId, ct);

    private async Task<bool> EjecutarCambioEstadoActivoAsync(
        TipoActivoMantenimiento tipo, Guid activoId, string estadoNuevoStr,
        string? motivo, bool notificarResidentes, Guid? intervencionId, CancellationToken ct)
    {
        if (tipo == TipoActivoMantenimiento.Equipo)
        {
            var equipo = await _db.EquiposActivos.FirstOrDefaultAsync(e => e.Id == activoId, ct);
            if (equipo is null) throw new InvalidOperationException("Equipo no encontrado.");
            if (!Enum.TryParse<EstadoEquipoActivo>(estadoNuevoStr, true, out var nuevo))
                throw new InvalidOperationException($"Estado invalido: {estadoNuevoStr}");
            if (equipo.Estado == nuevo)
                throw new InvalidOperationException("El nuevo estado debe ser diferente al actual.");
            var anterior = equipo.Estado.ToString();
            equipo.Estado = nuevo;
            equipo.UpdatedAt = DateTimeOffset.UtcNow;
            _db.MantenimientoHistorialEstados.Add(new MantenimientoHistorialEstado
            {
                ActivoTipo = tipo,
                ActivoId = activoId,
                IntervencionId = intervencionId,
                EstadoAnterior = anterior,
                EstadoNuevo = nuevo.ToString(),
                Motivo = motivo,
                NotificadoResidentes = notificarResidentes,
                ActorUsuarioId = GetUsuarioActualId()
            });
        }
        else
        {
            var zona = await _db.ZonasComunes.FirstOrDefaultAsync(z => z.Id == activoId, ct);
            if (zona is null) throw new InvalidOperationException("Zona no encontrada.");
            if (!Enum.TryParse<EstadoZonaComunMantenimiento>(estadoNuevoStr, true, out var nuevo))
                throw new InvalidOperationException($"Estado invalido: {estadoNuevoStr}");
            if (zona.Estado == nuevo)
                throw new InvalidOperationException("El nuevo estado debe ser diferente al actual.");
            var anterior = zona.Estado.ToString();
            zona.Estado = nuevo;
            zona.UpdatedAt = DateTimeOffset.UtcNow;
            _db.MantenimientoHistorialEstados.Add(new MantenimientoHistorialEstado
            {
                ActivoTipo = tipo,
                ActivoId = activoId,
                IntervencionId = intervencionId,
                EstadoAnterior = anterior,
                EstadoNuevo = nuevo.ToString(),
                Motivo = motivo,
                NotificadoResidentes = notificarResidentes,
                ActorUsuarioId = GetUsuarioActualId()
            });
            // RN-08: Si zona pasa a EnMantenimiento, 2.13 debe bloquear reservas.
            // Modulo 2.13 aun no construido - cuando se materialice, debera suscribirse al cambio.
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<HistorialEstadoActivoDto>> ListarHistorialEstadoAsync(
        TipoActivoMantenimiento activoTipo, Guid activoId, CancellationToken ct)
    {
        var lista = await _db.MantenimientoHistorialEstados.AsNoTracking()
            .Where(h => h.ActivoTipo == activoTipo && h.ActivoId == activoId)
            .Include(h => h.Intervencion)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        return lista.Select(h => new HistorialEstadoActivoDto(
            h.Id, h.EstadoAnterior, h.EstadoNuevo, h.Motivo, h.NotificadoResidentes,
            h.ActorUsuarioId, h.IntervencionId, h.Intervencion?.Codigo, h.CreatedAt)).ToList();
    }
}
