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

                // Candidatas: las vencidas (modo clasico) mas TODAS las que generan por adelantado.
                // A estas ultimas hay que revisarlas aunque su proxima ocurrencia sea lejana: lo que
                // las hace trabajar no es vencer, es tener huecos sin materializar dentro del horizonte.
                var candidatas = await _db.ProgramacionTareas
                    .Include(p => p.Responsables)
                    .Where(p => p.Activa && (
                        p.HorizonteDias > 0 ||
                        (p.Tipo == TipoProgramacion.Periodicidad && p.FechaProximaEjecucion <= hoy) ||
                        (p.Tipo == TipoProgramacion.Cron && p.ProximaEjecucionUtc != null && p.ProximaEjecucionUtc <= ahora)))
                    .OrderBy(p => p.FechaProximaEjecucion)
                    .Take(500)
                    .ToListAsync(ct);
                if (candidatas.Count == 0) continue;
                tenantsConTrabajo++;

                foreach (var prog in candidatas)
                {
                    var ocurrencias = PlanificadorOcurrencias.Calcular(prog, ahora, hoy);

                    // Sin ocurrencias y ademas vencida: la corto FechaFin. Se desactiva sin crear.
                    if (ocurrencias.Count == 0)
                    {
                        if (PlanificadorOcurrencias.Vencida(prog, ahora, hoy)) { prog.Activa = false; desactivadas++; }
                        continue;
                    }

                    // Ocurrencias ya materializadas. Sin este dedupe, cada corrida del job volveria
                    // a crear las mismas tareas del horizonte cada 15 minutos.
                    // Se excluyen las Eliminada a proposito: son las que retiro la edicion de la regla
                    // justamente para que se rehagan con los datos nuevos.
                    var vistas = (await _db.Tareas.AsNoTracking()
                            .Where(t => t.ProgramacionTareaId == prog.Id && !t.Eliminada && t.OcurrenciaUtc != null)
                            .Select(t => t.OcurrenciaUtc!.Value)
                            .ToListAsync(ct))
                        .ToHashSet();

                    var responsables = prog.Responsables.Select(r => r.PersonaId).Distinct().ToList();
                    Guid? asignado = responsables.Count > 0 ? responsables[0] : null;
                    var colaboradores = responsables.Skip(1).ToList();

                    int generadasAqui = 0;

                    foreach (var oc in ocurrencias)
                    {
                        if (!vistas.Add(oc.Instante)) continue;   // esa ocurrencia ya existe

                        var req = new CrearTareaRequest(
                            prog.Titulo,
                            prog.Descripcion,
                            prog.Prioridad,
                            null,                       // EstadoId -> primer estado por defecto
                            asignado,
                            null,                       // FechaInicio
                            oc.Fecha,                   // FechaVencimiento = fecha de la ocurrencia
                            null,                       // PadreId
                            null,                       // EtiquetaIds
                            TableroId: prog.TableroId,
                            OrigenTipo: prog.ModuloOrigenCodigo,
                            OrigenReferencia: prog.OrigenReferencia,
                            ResponsablePersonaIds: colaboradores.Count > 0 ? colaboradores : null);

                        var tarea = await _tareas.CrearTareaAsync(req, ct);

                        // Marca de origen: identifica la ocurrencia exacta. Es lo que permite
                        // deduplicar aqui y regenerar el futuro cuando se edita la regla.
                        var creada = await _db.Tareas.FirstOrDefaultAsync(t => t.Id == tarea.Id, ct);
                        if (creada is not null)
                        {
                            creada.ProgramacionTareaId = prog.Id;
                            creada.OcurrenciaUtc = oc.Instante;
                        }

                        tareasCreadas++;
                        generadasAqui++;

                        if (prog.NotificarPorCorreo && responsables.Count > 0)
                        {
                            var (ok, fallo) = await NotificarAsync(prog, tarea.NumeroTarea, oc.Fecha, responsables, ct);
                            correosEnviados += ok;
                            correosFallidos += fallo;
                        }
                    }

                    if (generadasAqui == 0) continue;   // el horizonte ya estaba cubierto
                    programacionesProcesadas++;
                    prog.TareasGeneradas += generadasAqui;
                    prog.UltimaEjecucion = DateTimeOffset.UtcNow;

                    // Los punteros quedan en la primera ocurrencia POSTERIOR a la ultima generada, no
                    // en "la siguiente a hoy": si ya adelantamos todo el anio, la proxima pendiente
                    // real es la del anio que viene.
                    var ultima = ocurrencias[ocurrencias.Count - 1];

                    if (prog.Tipo == TipoProgramacion.Cron)
                    {
                        var siguiente = CronHelper.ProximaEjecucion(prog.CronExpresion, prog.ZonaHoraria, ultima.Instante.AddMinutes(1));
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
                        var next = PlanificadorOcurrencias.Avanzar(ultima.Fecha, prog.Periodicidad);
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
}
