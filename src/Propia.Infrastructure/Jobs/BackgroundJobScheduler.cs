using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Scheduler in-process estilo Hangfire-lite. IHostedService que se levanta con
/// la API, despierta cada `TickIntervalMinutos` (default 1 min) y revisa para
/// cada IBackgroundJob registrado si debe ejecutarse segun su FrecuenciaMinutos
/// y la ultima ejecucion Exitosa en BD (idempotencia).
///
/// Por que no Hangfire/Quartz:
///  - 2 jobs hoy, ~5-10 jobs Fase 2. La complejidad operativa de Hangfire
///    (dashboard separado, dependencias adicionales) no se justifica.
///  - Cero overhead operacional: solo una tabla mas (job_ejecuciones).
///
/// OJO - NO hay exclusion mutua entre procesos. Este comentario prometia una UNIQUE de
/// (JobName, IniciadoAt cercano) que NUNCA existio: job_ejecuciones solo tiene su PK y dos
/// indices btree normales. DebeEjecutarAsync LEE si hay algo Ejecutando y EjecutarUnoAsync
/// INSERTA despues, sin transaccion ni lock, asi que dos procesos que ticken a la vez pueden
/// correr el mismo job (TOCTOU). Mientras no exista un lock real (advisory lock de Postgres o
/// una UNIQUE de verdad), la regla operativa es: UNA sola instancia con el scheduler encendido.
/// Las demas se levantan con Jobs:Enabled=false (ver Program.cs de Api y Web).
///
/// Si en el futuro hay 50+ jobs o necesidad de retries automaticos con backoff,
/// se cambia a Hangfire sin tocar las implementaciones IBackgroundJob.
/// </summary>
public class BackgroundJobScheduler : BackgroundService
{
    private const int TickIntervalMinutos = 1;

    /// <summary>
    /// Una ejecucion que lleva mas de estos minutos en estado Ejecutando se da por COLGADA: el
    /// proceso murio a mitad del job (pasa en cada recompilacion en dev) y ya nadie va a cerrar
    /// esa fila. Sin esto, la fila bloquea ese job PARA SIEMPRE y deja de correr sin error visible.
    /// </summary>
    private const int EjecucionColgadaMinutos = 30;

    private readonly IServiceProvider _services;
    private readonly ILogger<BackgroundJobScheduler> _log;

    public BackgroundJobScheduler(IServiceProvider services, ILogger<BackgroundJobScheduler> log)
    {
        _services = services;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("BackgroundJobScheduler iniciado (tick cada {Mins} min)", TickIntervalMinutos);

        // Espera 30 seg al arranque para no competir por DB con migraciones/seed
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (Exception ex) { _log.LogError(ex, "Error en tick del scheduler"); }
            try { await Task.Delay(TimeSpan.FromMinutes(TickIntervalMinutos), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    /// <summary>
    /// Procesa un tick: revisa cada job registrado y decide si ejecutar.
    /// PUBLICO para tests - se puede invocar manualmente con scope custom.
    /// </summary>
    public async Task TickAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var jobs = scope.ServiceProvider.GetServices<IBackgroundJob>().ToList();
        if (jobs.Count == 0) return;

        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var ahora = DateTimeOffset.UtcNow;
        var host = Environment.MachineName;

        foreach (var job in jobs)
        {
            try
            {
                var debe = await DebeEjecutarAsync(db, job, ahora, ct);
                if (!debe) continue;
                await EjecutarUnoAsync(scope.ServiceProvider, job, host, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Scheduler: error procesando job {Nombre}", job.Nombre);
            }
        }
    }

    private async Task<bool> DebeEjecutarAsync(
        PropiaDbContext db, IBackgroundJob job, DateTimeOffset ahora, CancellationToken ct)
    {
        // Bloqueo si hay una ejecucion en curso del mismo job (multi-tick / multi-instancia).
        // Antes se descartaba el job ante CUALQUIER fila Ejecutando, sin mirar la antiguedad: si
        // el proceso moria a mitad del job, la fila quedaba viva para siempre y ese job no volvia
        // a correr nunca. Ahora las que pasan de EjecucionColgadaMinutos se cierran como Fallido.
        var enCurso = await db.JobEjecuciones
            .Where(e => e.JobName == job.Nombre && e.Estado == EstadoEjecucionJob.Ejecutando)
            .ToListAsync(ct);

        var limiteColgada = ahora.AddMinutes(-EjecucionColgadaMinutos);
        var colgadas = enCurso.Where(e => e.IniciadoAt < limiteColgada).ToList();
        if (colgadas.Count > 0)
        {
            foreach (var colgada in colgadas)
            {
                colgada.Estado = EstadoEjecucionJob.Fallido;
                colgada.CompletadoAt = ahora;
                colgada.Error = $"Marcada como colgada por el scheduler: seguia en Ejecutando tras "
                    + $"{EjecucionColgadaMinutos} min (host {colgada.EjecutadoPorHost}). Lo normal "
                    + "es que el proceso muriera a mitad del job.";
            }
            await db.SaveChangesAsync(ct);
            _log.LogWarning(
                "Scheduler: {N} ejecucion(es) colgada(s) de {Nombre} marcada(s) como Fallido",
                colgadas.Count, job.Nombre);
        }

        // Queda alguna viva de verdad: no arrancar otra.
        if (enCurso.Count > colgadas.Count) return false;

        var ultima = await db.JobEjecuciones.AsNoTracking()
            .Where(e => e.JobName == job.Nombre
                        && (e.Estado == EstadoEjecucionJob.Exitoso
                            || e.Estado == EstadoEjecucionJob.Fallido))
            .OrderByDescending(e => e.IniciadoAt).FirstOrDefaultAsync(ct);
        if (ultima is null) return true; // nunca ha corrido
        return (ahora - ultima.IniciadoAt).TotalMinutes >= job.FrecuenciaMinutos;
    }

    /// <summary>
    /// Ejecuta un job en su propio scope (DbContext fresh). PUBLICO para que los
    /// tests puedan forzar la ejecucion sin esperar al tick del scheduler.
    /// </summary>
    public async Task EjecutarUnoAsync(
        IServiceProvider scopedProvider, IBackgroundJob job, string host, CancellationToken ct)
    {
        var db = scopedProvider.GetRequiredService<PropiaDbContext>();
        var ejecucion = new JobEjecucion
        {
            JobName = job.Nombre,
            IniciadoAt = DateTimeOffset.UtcNow,
            Estado = EstadoEjecucionJob.Ejecutando,
            EjecutadoPorHost = host
        };
        db.JobEjecuciones.Add(ejecucion);
        await db.SaveChangesAsync(ct);

        try
        {
            var resultado = await job.EjecutarAsync(ct);
            ejecucion.Estado = EstadoEjecucionJob.Exitoso;
            ejecucion.CompletadoAt = DateTimeOffset.UtcNow;
            ejecucion.ResultadoJson = resultado is null ? null : JsonSerializer.Serialize(resultado);
            await db.SaveChangesAsync(ct);
            _log.LogInformation("Job {Nombre} OK en {Ms}ms - resultado: {Resultado}",
                job.Nombre, (ejecucion.CompletadoAt - ejecucion.IniciadoAt)?.TotalMilliseconds,
                ejecucion.ResultadoJson);
        }
        catch (Exception ex)
        {
            ejecucion.Estado = EstadoEjecucionJob.Fallido;
            ejecucion.CompletadoAt = DateTimeOffset.UtcNow;
            var msg = ex.ToString();
            ejecucion.Error = msg.Length > 4000 ? msg.Substring(0, 4000) : msg;
            await db.SaveChangesAsync(ct);
            _log.LogError(ex, "Job {Nombre} FALLIDO", job.Nombre);
        }
    }
}
