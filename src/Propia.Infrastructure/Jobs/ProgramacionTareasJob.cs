using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Tareas;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Materializa las programaciones de tareas vencidas en tareas reales (modulo 2.10).
/// Como corre sin contexto de request, itera los tenants (tabla global Tenants) y para cada
/// uno setea ITenantContext + reabre la conexion (el TenantConnectionInterceptor aplica
/// app.tenant_id) para que RLS permita leer/crear. Crea la tarea via ITareasService (reusa la
/// generacion de numero/estado/responsables) y avanza FechaProximaEjecucion segun periodicidad
/// (Unica = se desactiva). Idempotente por fecha: corre varias veces al dia sin duplicar de mas.
/// </summary>
public class ProgramacionTareasJob : IBackgroundJob
{
    public string Nombre => "ProgramacionTareas";
    public int FrecuenciaMinutos => 60 * 6; // cada 6 horas

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ITareasService _tareas;

    public ProgramacionTareasJob(PropiaDbContext db, ITenantContext tenant, ITareasService tareas)
    {
        _db = db;
        _tenant = tenant;
        _tareas = tareas;
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Tenants es global (sin RLS); nos da la lista para iterar.
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);

        int tareasCreadas = 0, programacionesProcesadas = 0, desactivadas = 0, tenantsConTrabajo = 0;

        foreach (var tid in tenantIds)
        {
            _tenant.SetTenant(tid);
            await _db.Database.CloseConnectionAsync(); // el interceptor aplica app.tenant_id al reabrir

            var due = await _db.ProgramacionTareas
                .Include(p => p.Responsables)
                .Where(p => p.Activa && p.FechaProximaEjecucion <= hoy)
                .OrderBy(p => p.FechaProximaEjecucion)
                .Take(500)
                .ToListAsync(ct);
            if (due.Count == 0) continue;
            tenantsConTrabajo++;

            foreach (var prog in due)
            {
                // Vencio la ventana de vigencia: desactivar sin crear.
                if (prog.FechaFin.HasValue && prog.FechaProximaEjecucion > prog.FechaFin.Value)
                {
                    prog.Activa = false;
                    desactivadas++;
                    continue;
                }

                var responsables = prog.Responsables.Select(r => r.PersonaId).Distinct().ToList();
                Guid? asignado = responsables.Count > 0 ? responsables[0] : null;
                var colaboradores = responsables.Skip(1).ToList();

                var req = new CrearTareaRequest(
                    prog.Titulo,
                    prog.Descripcion,
                    prog.Prioridad,
                    null,                       // EstadoId -> primer estado por defecto
                    asignado,
                    null,                       // FechaInicio
                    prog.FechaProximaEjecucion, // FechaVencimiento = fecha de ejecucion programada
                    null,                       // PadreId
                    null,                       // EtiquetaIds
                    TableroId: prog.TableroId,
                    OrigenTipo: prog.ModuloOrigenCodigo,
                    OrigenReferencia: prog.OrigenReferencia,
                    ResponsablePersonaIds: colaboradores.Count > 0 ? colaboradores : null);

                await _tareas.CrearTareaAsync(req, ct);
                tareasCreadas++;
                programacionesProcesadas++;

                prog.TareasGeneradas += 1;
                prog.UltimaEjecucion = DateTimeOffset.UtcNow;

                if (prog.Periodicidad == PeriodicidadProgramacion.Unica)
                {
                    prog.Activa = false;
                    desactivadas++;
                }
                else
                {
                    var next = Avanzar(prog.FechaProximaEjecucion, prog.Periodicidad);
                    prog.FechaProximaEjecucion = next;
                    if (prog.FechaFin.HasValue && next > prog.FechaFin.Value)
                    {
                        prog.Activa = false;
                        desactivadas++;
                    }
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        _tenant.Clear();
        return new
        {
            tenants = tenantIds.Count,
            tenantsConTrabajo,
            programacionesProcesadas,
            tareasCreadas,
            desactivadas,
            fecha = hoy.ToString("yyyy-MM-dd")
        };
    }

    private static DateOnly Avanzar(DateOnly d, PeriodicidadProgramacion p) => p switch
    {
        PeriodicidadProgramacion.Diaria => d.AddDays(1),
        PeriodicidadProgramacion.Semanal => d.AddDays(7),
        PeriodicidadProgramacion.Quincenal => d.AddDays(14),
        PeriodicidadProgramacion.Mensual => d.AddMonths(1),
        PeriodicidadProgramacion.Bimestral => d.AddMonths(2),
        PeriodicidadProgramacion.Trimestral => d.AddMonths(3),
        PeriodicidadProgramacion.Semestral => d.AddMonths(6),
        PeriodicidadProgramacion.Anual => d.AddYears(1),
        _ => d.AddDays(1)
    };
}
