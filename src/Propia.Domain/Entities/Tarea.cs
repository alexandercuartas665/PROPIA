using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Entidad central del modulo 2.10. Spec v1.0 seccion 3 - una sola entidad con
/// jerarquia autorreferencial via PadreId. Proyecto = tarea raiz (PadreId null).
/// </summary>
public class Tarea : TenantEntity
{
    /// <summary>Codigo visible "T-{AÑO}-{SEQ}" generado automaticamente. Spec 6.1.</summary>
    public string NumeroTarea { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public Guid EstadoId { get; set; }
    public TareaEstado? Estado { get; set; }

    public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Normal;

    /// <summary>Responsable principal (FK Persona del Directorio). Opcional.</summary>
    public Guid? AsignadoPersonaId { get; set; }
    public Persona? AsignadoPersona { get; set; }

    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public DateTimeOffset? FechaCompletada { get; set; }

    /// <summary>Padre en la jerarquia. NULL = proyecto o tarea simple raiz.</summary>
    public Guid? PadreId { get; set; }
    public Tarea? Padre { get; set; }

    public OrigenTarea Origen { get; set; } = OrigenTarea.Manual;

    /// <summary>Modulo origen si Origen == ModuloExterno (PQRSD, Asamblea, Mantenimiento).</summary>
    public string? ModuloOrigenCodigo { get; set; }
    public Guid? ModuloOrigenEntidadId { get; set; }

    public Guid? CreadoPorUsuarioId { get; set; }

    public string? MotivoCancelacion { get; set; }

    public ICollection<Tarea> Subtareas { get; set; } = new List<Tarea>();
    public ICollection<TareaComentario> Comentarios { get; set; } = new List<TareaComentario>();
    public ICollection<TareaHistorial> Historial { get; set; } = new List<TareaHistorial>();
    public ICollection<TareaEtiquetaAsignacion> Etiquetas { get; set; } = new List<TareaEtiquetaAsignacion>();
    public ICollection<TareaColaborador> Colaboradores { get; set; } = new List<TareaColaborador>();

    /// <summary>Tareas de las que esta tarea depende (predecesoras). Spec 2.10 Fase 2.</summary>
    public ICollection<TareaDependencia> DependenciasDe { get; set; } = new List<TareaDependencia>();
    /// <summary>Tareas que dependen de esta (sucesoras). Spec 2.10 Fase 2.</summary>
    public ICollection<TareaDependencia> DependenciasA { get; set; } = new List<TareaDependencia>();
}

/// <summary>
/// Dependencia entre dos tareas (Bloqueante / Sucesora). Spec 2.10 v1.0 Fase 2.
/// Tarea A esta bloqueada hasta que Tarea B (Bloqueante) este Completada.
/// Cero ciclos: se valida en codigo + UNIQUE(TareaId, DependeDeTareaId).
/// </summary>
public class TareaDependencia : TenantEntity
{
    public Guid TareaId { get; set; }
    public Tarea? Tarea { get; set; }

    public Guid DependeDeTareaId { get; set; }
    public Tarea? DependeDeTarea { get; set; }

    public TipoDependenciaTarea Tipo { get; set; } = TipoDependenciaTarea.Bloqueante;

    public Guid? CreadoPorUsuarioId { get; set; }
}

/// <summary>Colaborador participante (no rinde cuentas). Spec 2.10 v1.0 seccion 9.</summary>
public class TareaColaborador : TenantEntity
{
    public Guid TareaId { get; set; }
    public Tarea? Tarea { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }
}

/// <summary>Comentario en una tarea. Spec 2.10 v1.0 ciclo de vida.</summary>
public class TareaComentario : TenantEntity
{
    public Guid TareaId { get; set; }
    public Tarea? Tarea { get; set; }

    public Guid AutorUsuarioId { get; set; }
    public string Texto { get; set; } = string.Empty;
}

/// <summary>Historial append-only de la tarea. Trigger SQL bloquea UPDATE/DELETE.</summary>
public class TareaHistorial : TenantEntity
{
    public Guid TareaId { get; set; }
    public Tarea? Tarea { get; set; }

    public TipoEventoTarea TipoEvento { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public Guid RealizadoPorUsuarioId { get; set; }
    public DateTimeOffset OcurridoAt { get; set; } = DateTimeOffset.UtcNow;
}
