namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Contrato minimo para un job programable invocado por BackgroundJobScheduler.
/// Implementaciones registradas como scoped en DI para que cada ejecucion abra su
/// propio scope con DbContext fresh (evita problemas de DbContext compartido
/// entre ticks del scheduler).
/// </summary>
public interface IBackgroundJob
{
    /// <summary>Nombre tecnico (identifica el job en la tabla job_ejecuciones).</summary>
    string Nombre { get; }

    /// <summary>Cada cuantos minutos se debe ejecutar este job.</summary>
    int FrecuenciaMinutos { get; }

    /// <summary>
    /// Ejecuta la logica del job. Devuelve un objeto serializable a JSON que se
    /// guarda en JobEjecucion.ResultadoJson (ej. contadores de items procesados).
    /// Si throw, el scheduler atrapa, marca Fallido y guarda Error.
    /// </summary>
    Task<object?> EjecutarAsync(CancellationToken ct);
}
