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

    /// <summary>Tablero al que pertenece la tarjeta. Null = tareas legacy (Kanban unico).</summary>
    public Guid? TableroId { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public Guid EstadoId { get; set; }
    public TareaEstado? Estado { get; set; }

    /// <summary>Instante (UTC) en que la tarea entro a su estado actual. Se estampa al crear
    /// y en cada cambio de estado. Alimenta el indicador "tiempo en este estado" de la UI.</summary>
    public DateTimeOffset EstadoDesde { get; set; } = DateTimeOffset.UtcNow;

    public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Normal;

    // ----- Campos de tarjeta del prototipo (2.10) -----
    /// <summary>Color hex de la tarjeta (borde/cabecera del modal).</summary>
    public string? Color { get; set; }
    /// <summary>Marca la tarjeta como proyecto.</summary>
    public bool EsProyecto { get; set; }
    /// <summary>Valor/costo estimado en COP.</summary>
    public decimal? Valor { get; set; }
    /// <summary>Avance 0-100.</summary>
    public int Progreso { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }

    /// <summary>Responsable principal (FK Persona del Directorio). Opcional.</summary>
    public Guid? AsignadoPersonaId { get; set; }
    public Persona? AsignadoPersona { get; set; }

    /// <summary>Solicitante: quien pide la tarea (FK Persona del Directorio global). Por defecto el usuario
    /// que la crea; editable con el selector de directorio (busca/crea terceros). Opcional.</summary>
    public Guid? SolicitantePersonaId { get; set; }
    public Persona? SolicitantePersona { get; set; }

    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public DateTimeOffset? FechaCompletada { get; set; }

    /// <summary>Padre en la jerarquia. NULL = proyecto o tarea simple raiz.</summary>
    /// <summary>
    /// Regla del programador que genero esta tarea (null = tarea creada a mano).
    /// Junto con OcurrenciaUtc identifica la ocurrencia exacta: es lo que evita duplicar
    /// al generar por adelantado y lo que permite regenerar el futuro al editar la regla.
    /// </summary>
    public Guid? ProgramacionTareaId { get; set; }

    /// <summary>Instante programado que materializo esta tarea (UTC).</summary>
    public DateTimeOffset? OcurrenciaUtc { get; set; }

    public Guid? PadreId { get; set; }
    public Tarea? Padre { get; set; }

    /// <summary>Si esta tarea es una COPIA de otra, apunta a la original. Es una copia
    /// INDEPENDIENTE (no una subtarea): solo alimenta la traza de copias en el modal. Null = no es copia.</summary>
    public Guid? CopiaDeTareaId { get; set; }
    public Tarea? CopiaDe { get; set; }
    /// <summary>Copias hechas a partir de esta tarea (traza en el modal del original).</summary>
    public ICollection<Tarea> Copias { get; set; } = new List<Tarea>();

    public OrigenTarea Origen { get; set; } = OrigenTarea.Manual;

    /// <summary>Modulo origen si Origen == ModuloExterno (PQRSD, Asamblea, Mantenimiento).</summary>
    public string? ModuloOrigenCodigo { get; set; }
    public Guid? ModuloOrigenEntidadId { get; set; }

    // ----- Relacion de la tarjeta con un elemento de la copropiedad (card del prototipo) -----
    /// <summary>Tipo de relacion: "inmueble" | "zona" | "equipo". Null = sin relacion.</summary>
    public string? OrigenTipo { get; set; }
    /// <summary>Referencia legible del elemento relacionado (numero de unidad, nombre de zona/equipo).</summary>
    public string? OrigenReferencia { get; set; }
    /// <summary>Id del elemento relacionado (UnidadPrivada / ZonaComun / EquipoActivo) segun OrigenTipo.
    /// Vinculo logico por Id, sin FK dura, para el Historial relacionado. Null = sin relacion o legado
    /// (esas tareas viejas se resuelven por nombre via OrigenReferencia).</summary>
    public Guid? OrigenEntidadId { get; set; }

    /// <summary>Soft-delete de la tarjeta (el historial es append-only, no se puede borrar fisico).</summary>
    public bool Eliminada { get; set; }

    public Guid? CreadoPorUsuarioId { get; set; }

    public string? MotivoCancelacion { get; set; }

    /// <summary>Cerrada: al llevar la tarea a un estado terminal se cierra y se archiva (desaparece
    /// del tablero activo, queda en la pestana "Cerrados"). Se pide motivo de cierre.</summary>
    public bool Cerrada { get; set; }
    public DateTimeOffset? CerradaAt { get; set; }
    /// <summary>Motivo de cierre elegido (catalogo configurable MotivoCierre, modulo "tareas").</summary>
    public Guid? MotivoCierreId { get; set; }

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
