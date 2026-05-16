using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Plan de mantenimiento preventivo sobre un activo (equipo o zona).
/// Spec 2.11 v1.0 - seccion 5. Un activo puede tener varios planes activos
/// simultaneos (RN-01) y se desactiva con soft delete (RN-09).
/// </summary>
public class MantenimientoPlan : TenantEntity
{
    public TipoActivoMantenimiento ActivoTipo { get; set; }

    /// <summary>FK logico a EquipoActivo.Id si ActivoTipo=Equipo, o ZonaComun.Id si ZonaComun.</summary>
    public Guid ActivoId { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public FrecuenciaMantenimiento Frecuencia { get; set; }

    /// <summary>Solo si Frecuencia = Personalizada. Validado 1..365.</summary>
    public int? FrecuenciaDias { get; set; }

    public DateOnly FechaInicio { get; set; }

    /// <summary>Calculado por el sistema. Inicia = FechaInicio.</summary>
    public DateOnly ProximaEjecucion { get; set; }

    /// <summary>FK a DirectorioContacto (Persona/Empresa). Proveedor sugerido al generar la intervencion (RN-12).</summary>
    public Guid? ProveedorPreferidoId { get; set; }
    public Persona? ProveedorPreferido { get; set; }

    public DisparoPlanMantenimiento Disparo { get; set; } = DisparoPlanMantenimiento.ConConfirmacion;

    /// <summary>Dias previos al vencimiento para alerta amarilla. Default 7.</summary>
    public int DiasAlertaPrevio { get; set; } = 7;

    public bool GeneraNotifResidentes { get; set; }

    /// <summary>Soft delete (RN-09) - planes inactivos conservan historial.</summary>
    public bool Activo { get; set; } = true;

    public Guid CreadoPorUsuarioId { get; set; }

    public ICollection<MantenimientoIntervencion> Intervenciones { get; set; } = new List<MantenimientoIntervencion>();
}

/// <summary>
/// Ejecucion real de mantenimiento - preventivo o correctivo.
/// Spec 2.11 v1.0 - seccion 6. Cada intervencion tiene bitacora propia y
/// genera tarea en 2.10 (RN-03). Codigo MNT-{ANO}-{SEC} unico por tenant y ano (RN-15).
/// </summary>
public class MantenimientoIntervencion : TenantEntity
{
    public string Codigo { get; set; } = string.Empty;

    public TipoIntervencionMantenimiento Tipo { get; set; }

    public TipoActivoMantenimiento ActivoTipo { get; set; }
    public Guid ActivoId { get; set; }

    /// <summary>Solo en preventivos. NULL en correctivos.</summary>
    public Guid? PlanId { get; set; }
    public MantenimientoPlan? Plan { get; set; }

    public OrigenIntervencion Origen { get; set; } = OrigenIntervencion.Manual;

    /// <summary>FK logico al expediente PQRSD si Origen=Pqrsd.</summary>
    public Guid? OrigenReferenciaId { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public EstadoIntervencion Estado { get; set; } = EstadoIntervencion.Programada;
    public PrioridadIntervencion Prioridad { get; set; } = PrioridadIntervencion.Normal;

    /// <summary>FK a Persona del Directorio (proveedor empresa o tecnico).</summary>
    public Guid? ProveedorId { get; set; }
    public Persona? Proveedor { get; set; }

    public Guid? ResponsableInternoId { get; set; }
    public Persona? ResponsableInterno { get; set; }

    public DateOnly? FechaProgramada { get; set; }
    public DateOnly? FechaInicioReal { get; set; }
    public DateOnly? FechaCierre { get; set; }

    /// <summary>Tarea generada en 2.10 (RN-03). Vinculo bidireccional.</summary>
    public Guid? TareaId { get; set; }
    public Tarea? Tarea { get; set; }

    public bool CambioEstadoActivo { get; set; }

    /// <summary>Solo si CambioEstadoActivo=true. Texto del enum del activo correspondiente.</summary>
    public string? EstadoActivoNuevo { get; set; }

    public bool NotificarResidentes { get; set; }

    /// <summary>Obligatorio si Estado=Cancelada (validacion seccion 16).</summary>
    public string? MotivoCancelacion { get; set; }

    public Guid CreadoPorUsuarioId { get; set; }

    public ICollection<MantenimientoBitacora> Bitacora { get; set; } = new List<MantenimientoBitacora>();
}

/// <summary>
/// Entrada en bitacora de intervencion. Append-only (RN-06) - trigger SQL bloquea UPDATE y DELETE.
/// Spec 2.11 v1.0 - seccion 9.
/// </summary>
public class MantenimientoBitacora : TenantEntity
{
    public Guid IntervencionId { get; set; }
    public MantenimientoIntervencion? Intervencion { get; set; }

    public Guid AutorUsuarioId { get; set; }

    public TipoAutorBitacoraMantenimiento TipoAutor { get; set; }

    public string Contenido { get; set; } = string.Empty;

    public ICollection<MantenimientoAdjunto> Adjuntos { get; set; } = new List<MantenimientoAdjunto>();
}

/// <summary>
/// Adjunto de una entrada de bitacora. Tamano maximo 20 MB (seccion 9).
/// Tipos: PDF, JPG, PNG, WEBP, MP4, XLSX.
/// </summary>
public class MantenimientoAdjunto : TenantEntity
{
    public Guid BitacoraId { get; set; }
    public MantenimientoBitacora? Bitacora { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }

    /// <summary>Path en bucket. Nunca se expone directo - solo URL firmada.</summary>
    public string UrlStorage { get; set; } = string.Empty;

    public Guid SubidoPorUsuarioId { get; set; }
}

/// <summary>
/// Historial append-only (RN-14) de cambios de estado del activo.
/// Trigger SQL bloquea UPDATE y DELETE. Spec 2.11 v1.0 - seccion 13.
/// </summary>
public class MantenimientoHistorialEstado : TenantEntity
{
    public TipoActivoMantenimiento ActivoTipo { get; set; }
    public Guid ActivoId { get; set; }

    /// <summary>NULL si el cambio se hizo fuera del contexto de una intervencion.</summary>
    public Guid? IntervencionId { get; set; }
    public MantenimientoIntervencion? Intervencion { get; set; }

    public string EstadoAnterior { get; set; } = string.Empty;
    public string EstadoNuevo { get; set; } = string.Empty;

    public string? Motivo { get; set; }
    public bool NotificadoResidentes { get; set; }

    public Guid ActorUsuarioId { get; set; }
}
