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
///  - Multi-instancia futura: la UNIQUE de (JobName, IniciadoAt cercano) +
///    consulta de UltimaExitosa antes de tick evitan duplicados entre dos
///    procesos del mismo deployment.
///  - Cero overhead operacional: solo una tabla mas (job_ejecuciones).
///
/// Si en el futuro hay 50+ jobs o necesidad de retries automaticos con backoff,
/// se cambia a Hangfire sin tocar las implementaciones IBackgroundJob.
/// </summary>
public class BackgroundJobScheduler : BackgroundService
{
    private const int TickIntervalMinutos = 1;

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

    private static async Task<bool> DebeEjecutarAsync(
        PropiaDbContext db, IBackgroundJob job, DateTimeOffset ahora, CancellationToken ct)
    {
        // Bloqueo si hay una ejecucion en curso del mismo job (multi-tick / multi-instancia).
        var enCurso = await db.JobEjecuciones.AsNoTracking()
            .AnyAsync(e => e.JobName == job.Nombre && e.Estado == EstadoEjecucionJob.Ejecutando, ct);
        if (enCurso) return false;

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
