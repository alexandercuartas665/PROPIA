using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Tareas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Programaciones;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Materializa las programaciones de tareas vencidas en tareas reales (modulo 2.10).
/// Como corre sin contexto de request, itera los tenants (tabla global Tenants) y para cada
/// uno setea ITenantContext + reabre la conexion (el TenantConnectionInterceptor aplica
/// app.tenant_id) para que RLS permita leer/crear. Crea la tarea via ITareasService (reusa la
/// generacion de numero/estado/responsables).
///
/// Conviven dos modos de disparo:
///  - Periodicidad: vence por dia (FechaProximaEjecucion menor o igual a hoy), avanza con Avanzar().
///  - Cron: vence por hora (ProximaEjecucionUtc menor o igual a ahora), avanza con CronHelper.
/// Por eso corre cada 15 minutos y no cada 6 horas: un cron "todos los dias a las 8:00" se
/// dispararia con horas de retraso si el tick fuera de 6 horas.
///
/// Si la programacion lo pide, avisa por correo a los responsables que tengan email. El envio
/// es best-effort: un SMTP caido no debe impedir que la tarea quede creada.
/// </summary>
public class ProgramacionTareasJob : IBackgroundJob
{
    public string Nombre => "ProgramacionTareas";
    public int FrecuenciaMinutos => 15;

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ITareasService _tareas;
    private readonly IEmailSender _email;

    public ProgramacionTareasJob(PropiaDbContext db, ITenantContext tenant, ITareasService tareas, IEmailSender email)
    {
        _db = db;
        _tenant = tenant;
        _tareas = tareas;
        _email = email;
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var ahora = DateTimeOffset.UtcNow;

        // Tenants es global (sin RLS); nos da la lista para iterar.
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);

        int tareasCreadas = 0, programacionesProcesadas = 0, desactivadas = 0, tenantsConTrabajo = 0, errores = 0;
        int correosEnviados = 0, correosFallidos = 0;

        foreach (var tid in tenantIds)
        {
          try
          {
            _tenant.SetTenant(tid);
            await _db.Database.CloseConnectionAsync(); // el interceptor aplica app.tenant_id al reabrir

            var due = await _db.ProgramacionTareas
                .Include(p => p.Responsables)
                .Where(p => p.Activa && (
                    (p.Tipo == TipoProgramacion.Periodicidad && p.FechaProximaEjecucion <= hoy) ||
                    (p.Tipo == TipoProgramacion.Cron && p.ProximaEjecucionUtc != null && p.ProximaEjecucionUtc <= ahora)))
                .OrderBy(p => p.FechaProximaEjecucion)
                .Take(500)
                .ToListAsync(ct);
            if (due.Count == 0) continue;
            tenantsConTrabajo++;

            foreach (var prog in due)
            {
                // Fecha que se le pone de vencimiento a la tarea. En cron es el dia local de
                // la ocurrencia; en periodicidad es la fecha programada tal cual.
                var fechaEjecucion = prog.Tipo == TipoProgramacion.Cron && prog.ProximaEjecucionUtc.HasValue
                    ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(prog.ProximaEjecucionUtc.Value, CronHelper.Zona(prog.ZonaHoraria)).DateTime)
                    : prog.FechaProximaEjecucion;

                // Vencio la ventana de vigencia: desactivar sin crear.
                if (prog.FechaFin.HasValue && fechaEjecucion > prog.FechaFin.Value)
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
                    fechaEjecucion,             // FechaVencimiento = fecha de ejecucion programada
                    null,                       // PadreId
                    null,                       // EtiquetaIds
                    TableroId: prog.TableroId,
                    OrigenTipo: prog.ModuloOrigenCodigo,
                    OrigenReferencia: prog.OrigenReferencia,
                    ResponsablePersonaIds: colaboradores.Count > 0 ? colaboradores : null);

                var tarea = await _tareas.CrearTareaAsync(req, ct);
                tareasCreadas++;
                programacionesProcesadas++;

                if (prog.NotificarPorCorreo && responsables.Count > 0)
                {
                    var (ok, fallo) = await NotificarAsync(prog, tarea.NumeroTarea, fechaEjecucion, responsables, ct);
                    correosEnviados += ok;
                    correosFallidos += fallo;
                }

                prog.TareasGeneradas += 1;
                prog.UltimaEjecucion = DateTimeOffset.UtcNow;

                if (prog.Tipo == TipoProgramacion.Cron)
                {
                    // Se calcula desde "ahora" y no desde la ocurrencia vencida: si el job
                    // estuvo caido dos dias no queremos crear las tareas atrasadas de golpe.
                    var siguiente = CronHelper.ProximaEjecucion(prog.CronExpresion, prog.ZonaHoraria, ahora);
                    prog.ProximaEjecucionUtc = siguiente;
                    if (siguiente is null) { prog.Activa = false; desactivadas++; }
                    else
                    {
                        var diaSiguiente = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(siguiente.Value, CronHelper.Zona(prog.ZonaHoraria)).DateTime);
                        prog.FechaProximaEjecucion = diaSiguiente;
                        if (prog.FechaFin.HasValue && diaSiguiente > prog.FechaFin.Value) { prog.Activa = false; desactivadas++; }
                    }
                }
                else if (prog.Periodicidad == PeriodicidadProgramacion.Unica)
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
          catch
          {
            // Aisla el fallo de un tenant para no abortar el resto + limpia el tracker
            // (evita arrastrar cambios pendientes al siguiente tenant).
            errores++;
            _db.ChangeTracker.Clear();
          }
        }

        _tenant.Clear();
        return new
        {
            tenants = tenantIds.Count,
            tenantsConTrabajo,
            programacionesProcesadas,
            tareasCreadas,
            desactivadas,
            correosEnviados,
            correosFallidos,
            errores,
            fecha = hoy.ToString("yyyy-MM-dd")
        };
    }

    /// <summary>
    /// Avisa a los responsables con email. Devuelve (enviados, fallidos); nunca lanza, porque
    /// la tarea ya quedo creada y un fallo de correo no debe revertirla ni abortar el tenant.
    /// </summary>
    private async Task<(int Ok, int Fallo)> NotificarAsync(
        ProgramacionTarea prog, string numeroTarea, DateOnly fechaEjecucion, List<Guid> personaIds, CancellationToken ct)
    {
        int ok = 0, fallo = 0;
        try
        {
            var destinatarios = await _db.Personas.AsNoTracking()
                .Where(p => personaIds.Contains(p.Id) && p.Email != null && p.Email != "")
                .Select(p => new { p.Email, p.Nombres })
                .ToListAsync(ct);

            foreach (var d in destinatarios)
            {
                var asunto = $"[{numeroTarea}] {prog.Titulo}";
                var cuerpo =
                    $"<p>Hola {System.Net.WebUtility.HtmlEncode(d.Nombres ?? "")},</p>" +
                    $"<p>Se genero automaticamente la tarea <b>{System.Net.WebUtility.HtmlEncode(numeroTarea)} - {System.Net.WebUtility.HtmlEncode(prog.Titulo)}</b>, " +
                    $"con vencimiento el <b>{fechaEjecucion:dd/MM/yyyy}</b>.</p>" +
                    (string.IsNullOrWhiteSpace(prog.Descripcion) ? "" : $"<p>{System.Net.WebUtility.HtmlEncode(prog.Descripcion)}</p>") +
                    (string.IsNullOrWhiteSpace(prog.OrigenReferencia) ? "" : $"<p>Origen: {System.Net.WebUtility.HtmlEncode(prog.OrigenReferencia)}</p>") +
                    "<p>Puedes verla en el modulo de Tareas de PROPIA.</p>";

                var r = await _email.SendAsync(d.Email!, asunto, cuerpo, ct);
                if (r.Success) ok++; else fallo++;
            }
        }
        catch { fallo++; }
        return (ok, fallo);
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
