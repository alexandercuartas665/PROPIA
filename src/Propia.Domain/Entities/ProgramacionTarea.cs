using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Programador reutilizable de tareas (modulo 2.10 Tareas). Materializa tareas en un tablero
/// con una periodicidad, a partir de una fecha de ejecucion, asignadas a uno o mas usuarios.
/// Lo dispara ProgramacionTareasJob (background). Se invoca desde varios escenarios (vencimiento
/// de contrato, mantenimientos, etc.) via el componente ProgramadorTareaModal; el origen queda
/// en ModuloOrigenCodigo/EntidadOrigenId para trazabilidad.
/// </summary>
public class ProgramacionTarea : TenantEntity
{
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Normal;

    /// <summary>Tablero donde se crean las tareas. Null = tablero General.</summary>
    public Guid? TableroId { get; set; }

    /// <summary>Periodicidad simple o expresion cron. Las programaciones viejas quedan en Periodicidad.</summary>
    public TipoProgramacion Tipo { get; set; } = TipoProgramacion.Periodicidad;

    public PeriodicidadProgramacion Periodicidad { get; set; }

    /// <summary>Cron de 5 campos (min hora dia mes diasemana). Solo aplica si Tipo = Cron.</summary>
    public string? CronExpresion { get; set; }

    /// <summary>Zona horaria IANA en la que se interpreta el cron.</summary>
    public string ZonaHoraria { get; set; } = "America/Bogota";

    /// <summary>
    /// Proxima ejecucion con hora, para Tipo = Cron. En modo Periodicidad se usa
    /// FechaProximaEjecucion (dia completo, sin hora) y esta queda en null.
    /// </summary>
    public DateTimeOffset? ProximaEjecucionUtc { get; set; }

    /// <summary>Proxima fecha en que se materializa una tarea (= vencimiento de la tarea creada).</summary>
    public DateOnly FechaProximaEjecucion { get; set; }

    /// <summary>Al generar la tarea, avisar por correo a los responsables que tengan email.</summary>
    public bool NotificarPorCorreo { get; set; }

    /// <summary>Fecha tope: al superarla la programacion se desactiva. Opcional.</summary>
    public DateOnly? FechaFin { get; set; }

    public bool Activa { get; set; } = true;

    // Trazabilidad del origen (escenario que creo la programacion).
    public string? ModuloOrigenCodigo { get; set; }   // ej. "contrato"
    public Guid? EntidadOrigenId { get; set; }         // ej. ContratoServicio.Id
    public string? OrigenReferencia { get; set; }      // texto legible que se copia a la tarea creada

    public Guid CreadoPorUsuarioId { get; set; }
    public int TareasGeneradas { get; set; }
    public DateTimeOffset? UltimaEjecucion { get; set; }

    public ICollection<ProgramacionTareaResponsable> Responsables { get; set; } = new List<ProgramacionTareaResponsable>();
}

/// <summary>Usuario (Persona) afectado por una programacion: se asigna a la tarea generada.</summary>
public class ProgramacionTareaResponsable : TenantEntity
{
    public Guid ProgramacionTareaId { get; set; }
    public ProgramacionTarea? ProgramacionTarea { get; set; }
    public Guid PersonaId { get; set; }
    public string? NombreSnapshot { get; set; }
}
