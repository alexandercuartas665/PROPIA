using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Bitacora de ejecuciones de jobs nocturnos (BackgroundService). Entidad global.
///
/// Propositos:
///  - Idempotencia: el scheduler consulta UltimaEjecucionExitosaAt antes de disparar
///    para evitar ejecuciones duplicadas en multiples instancias o tras reinicio.
///  - Auditoria: cada ejecucion deja rastro (cuanto tomo, que produjo, error si fallo).
///  - Alimenta el dashboard 0.3 Monitoria SuperAdmin (panel "Jobs nocturnos").
/// </summary>
public class JobEjecucion : BaseEntity
{
    /// <summary>Nombre tecnico del job (ej. "PqrsdCierreNocturno", "MetricasDiarias").</summary>
    public string JobName { get; set; } = string.Empty;

    public DateTimeOffset IniciadoAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletadoAt { get; set; }

    public EstadoEjecucionJob Estado { get; set; } = EstadoEjecucionJob.Ejecutando;

    /// <summary>Resumen del resultado (JSON con contadores). Ej. {"cerrados": 5}.</summary>
    public string? ResultadoJson { get; set; }

    /// <summary>Mensaje de error si Estado == Fallido. Stack truncado a 4000 chars.</summary>
    public string? Error { get; set; }

    /// <summary>Hostname/ID del proceso que ejecuto el job (multi-instancia).</summary>
    public string? EjecutadoPorHost { get; set; }
}
