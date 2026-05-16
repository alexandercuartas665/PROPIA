using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Proceso de transferencia de custodia. Spec 1.5 seccion 15.
/// RN-16: solo una transferencia activa por copropiedad (unique parcial).
/// Global - referencia a 2 organizaciones + 1 copropiedad.
/// </summary>
public class TransferenciaCustodia : BaseEntity
{
    public Guid CopropiedadId { get; set; }
    public Tenant? Copropiedad { get; set; }

    /// <summary>NULL si la PH nunca tuvo admin o el admin salio de la plataforma.</summary>
    public Guid? OrganizacionSalienteId { get; set; }

    /// <summary>NULL hasta que se identifica al entrante (escenario A puede empezar sin entrante).</summary>
    public Guid? OrganizacionEntranteId { get; set; }

    public EscenarioTransferencia Escenario { get; set; }
    public EstadoTransferencia Estado { get; set; } = EstadoTransferencia.Iniciado;

    public Guid IniciadoPorUsuarioId { get; set; }

    /// <summary>Solo escenario A: fecha declarada de terminacion.</summary>
    public DateOnly? FechaEfectivaSaliente { get; set; }

    /// <summary>Fecha limite para respuesta del saliente. Default 15 dias (RN-03).</summary>
    public DateOnly? FechaVencimientoVentana { get; set; }

    /// <summary>Timestamp exacto de la transferencia ejecutada (corte).</summary>
    public DateTimeOffset? FechaCorte { get; set; }

    /// <summary>FK al acta de entrega digital en modulo 2.15 Documentos.</summary>
    public Guid? ActaEntregaDocumentoId { get; set; }
    public Documento? ActaEntregaDocumento { get; set; }

    /// <summary>JSON con snapshot del estado de la PH al momento del corte.</summary>
    public string? SnapshotEstadoJson { get; set; }

    /// <summary>JSON con prorrateo calculado para el entrante.</summary>
    public string? AjusteFacturacionJson { get; set; }

    public ICollection<TransferenciaDocumento> Documentos { get; set; } = new List<TransferenciaDocumento>();
    public ICollection<TransferenciaEvento> Eventos { get; set; } = new List<TransferenciaEvento>();
}

/// <summary>
/// Documentos adjuntos al proceso (actas de asamblea subidas por el entrante).
/// Spec 1.5 seccion 15.
/// </summary>
public class TransferenciaDocumento : BaseEntity
{
    public Guid TransferenciaId { get; set; }
    public TransferenciaCustodia? Transferencia { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string UrlStorage { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;

    public ResultadoValidacionActa ResultadoValidacionIa { get; set; } = ResultadoValidacionActa.Pendiente;

    /// <summary>JSON con NIT extraido, fecha, tipo decision, quorum, motivo de fallo (Fase 2).</summary>
    public string? DetalleValidacionIaJson { get; set; }

    public Guid SubidoPorUsuarioId { get; set; }
}

/// <summary>
/// Historial inmutable de eventos del proceso. Spec 1.5 seccion 15.
/// </summary>
public class TransferenciaEvento : BaseEntity
{
    public Guid TransferenciaId { get; set; }
    public TransferenciaCustodia? Transferencia { get; set; }

    public TipoEventoTransferencia TipoEvento { get; set; }

    /// <summary>NULL si es evento automatico del sistema.</summary>
    public Guid? ActorUsuarioId { get; set; }

    /// <summary>Canal por el que se origino el evento. NULL para eventos automaticos.</summary>
    public string? Canal { get; set; }

    /// <summary>JSON opcional con detalles del evento.</summary>
    public string? DetalleJson { get; set; }
}
