using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Expediente principal de PQRSD. Spec 2.9 v1.0 tabla pqrsd_expediente.
/// Numero de radicado inmutable PQRS-{ANIO}-{SEQ} unico por tenant (RN-13).
/// El radicador_id NUNCA se borra aunque haya reserva de identidad (RN-03).
/// </summary>
public class PqrsdExpediente : TenantEntity
{
    public string NumeroRadicado { get; set; } = string.Empty;

    public TipoPqrsd Tipo { get; set; }

    public Guid CategoriaId { get; set; }
    public PqrsdCategoria? Categoria { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public EstadoPqrsd Estado { get; set; } = EstadoPqrsd.Recibida;

    /// <summary>FK siempre almacenada (RN-03). La reserva es solo capa de presentacion.</summary>
    public Guid RadicadorPersonaId { get; set; }
    public Persona? RadicadorPersona { get; set; }

    /// <summary>Solo valido si Tipo == Denuncia (RN-02).</summary>
    public bool IdentidadReservada { get; set; }

    public bool TutelaActiva { get; set; }
    public DateTimeOffset? TutelaActivadaAt { get; set; }
    public Guid? TutelaActivadaPorUsuarioId { get; set; }

    /// <summary>Fecha de vencimiento del plazo legal, calculada en backend en dias habiles.</summary>
    public DateOnly FechaVencimiento { get; set; }

    /// <summary>FK a la tarea interna en modulo 2.10 (opcional, invisible para el radicador - RN-10).</summary>
    public Guid? TareaId { get; set; }

    public string? RespuestaAdmin { get; set; }
    public DateTimeOffset? RespuestaAdminAt { get; set; }
    public Guid? RespuestaAdminPorUsuarioId { get; set; }

    /// <summary>Texto de inconformidad del radicador (solo una vez - RN-06).</summary>
    public string? InconformidadTexto { get; set; }
    public DateTimeOffset? InconformidadAt { get; set; }

    public string? RespuestaDefinitiva { get; set; }
    public DateTimeOffset? RespuestaDefinitivaAt { get; set; }

    public DateTimeOffset? FechaCierre { get; set; }
    public Guid? CerradoPorUsuarioId { get; set; }

    public ICollection<PqrsdAdjunto> Adjuntos { get; set; } = new List<PqrsdAdjunto>();
    public ICollection<PqrsdHistorialEstado> Historial { get; set; } = new List<PqrsdHistorialEstado>();
}

/// <summary>Catalogo de categorias configurables por copropiedad. Spec 2.9 v1.0 seccion 4.</summary>
public class PqrsdCategoria : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public bool EsPredeterminada { get; set; }
    public bool Activa { get; set; } = true;
    public int Orden { get; set; }
}

/// <summary>Adjunto del expediente. Spec 2.9 v1.0 tabla pqrsd_adjunto.</summary>
public class PqrsdAdjunto : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public PqrsdExpediente? Expediente { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string UrlStorage { get; set; } = string.Empty;
    public Guid SubidoPorUsuarioId { get; set; }
}

/// <summary>Historial append-only de estados. Spec 2.9 v1.0 tabla pqrsd_historial_estado + trigger SQL.</summary>
public class PqrsdHistorialEstado : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public PqrsdExpediente? Expediente { get; set; }

    public EstadoPqrsd? EstadoAnterior { get; set; }
    public EstadoPqrsd EstadoNuevo { get; set; }

    public Guid? ActorUsuarioId { get; set; }
    public OrigenCambioEstado Origen { get; set; } = OrigenCambioEstado.Manual;

    public string? Nota { get; set; }
}

/// <summary>Configuracion de plazos por tipo y copropiedad. Spec 2.9 v1.0 tabla pqrsd_configuracion_plazo.</summary>
public class PqrsdConfiguracionPlazo : TenantEntity
{
    public TipoPqrsd Tipo { get; set; }
    public int DiasHabiles { get; set; }
    public int DiasInconformidad { get; set; } = 3;
    public NivelUrgenciaPqrsd NivelUrgencia { get; set; }
}

/// <summary>Sesion del Comite de Convivencia (Art. 58 Ley 675/2001). Spec 2.9 v1.0 seccion 6.</summary>
public class PqrsdComiteSesion : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public PqrsdExpediente? Expediente { get; set; }

    public DateTimeOffset? FechaSesion { get; set; }
    public ModalidadComite Modalidad { get; set; }
    public string? EnlaceReunion { get; set; }

    public ResultadoComite? Resultado { get; set; }

    public string? BorradorActa { get; set; }
    public string? ActaFinal { get; set; }

    /// <summary>FK opcional a documento en 2.15 cuando se publica el acta firmada.</summary>
    public Guid? ActaDocumentoId { get; set; }

    public Guid ActivadaPorUsuarioId { get; set; }

    public ICollection<PqrsdComiteMiembroSesion> Miembros { get; set; } = new List<PqrsdComiteMiembroSesion>();
}

/// <summary>Miembros del Comite asignados a una sesion. Spec 2.9 v1.0 tabla pqrsd_comite_miembro_sesion.</summary>
public class PqrsdComiteMiembroSesion : TenantEntity
{
    public Guid SesionId { get; set; }
    public PqrsdComiteSesion? Sesion { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }
}
