using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Configuracion de reservas por zona comun. Spec 2.13 seccion 4 y 15.
/// Una fila por (ZonaComunId, TenantId). Vinculada a zona_comun de 2.3.
/// </summary>
public class ZonaConfigReserva : TenantEntity
{
    public Guid ZonaComunId { get; set; }
    public ZonaComun? ZonaComun { get; set; }

    // Politica general
    public bool RequiereAprobacion { get; set; }
    public int? MaxReservasActivasPorUnidad { get; set; }
    public bool BloqueoPorCartera { get; set; } = true;

    // Anticipacion
    public int AnticipacionMinimaHoras { get; set; } = 1;
    public int AnticipacionMaximaDias { get; set; } = 30;

    // Duracion
    public int DuracionMinimaMinutos { get; set; } = 60;
    public int DuracionMaximaMinutos { get; set; } = 240;
    public int IntervaloBloqueMinutos { get; set; } = 60;

    // Tarifa
    public bool TieneTarifa { get; set; }
    public ModalidadCobroReserva ModalidadCobro { get; set; } = ModalidadCobroReserva.PorHora;
    public decimal ValorTarifa { get; set; }
    public PoliticaReembolso PoliticaReembolso { get; set; } = PoliticaReembolso.SinReembolso;
    public decimal ValorPenalidadCancelacion { get; set; }

    // Cancelacion
    public int LimiteCancelacionHoras { get; set; } = 24;
    public bool PermiteCancelacionResidente { get; set; } = true;
    public ComportamientoCancelacionTardia CancelacionTardia { get; set; } = ComportamientoCancelacionTardia.ConPenalidad;

    // Reglamento
    public string? ReglamentoTexto { get; set; }
    public string? ReglamentoArchivoUrl { get; set; }
    public bool RequiereAceptacionReglamento { get; set; }

    // Notificaciones (T.2 diferido)
    public bool NotifConfirmacionWhatsapp { get; set; } = true;
    public bool NotifConfirmacionInapp { get; set; } = true;
    public bool NotifRecordatorioWhatsapp { get; set; } = true;
    public int NotifRecordatorioHorasAntes { get; set; } = 2;

    // Visibilidad
    public bool MotivoBloqueoVisible { get; set; }
    public bool VisibleParaResidentes { get; set; } = true;

    public ICollection<ZonaFranja> Franjas { get; set; } = new List<ZonaFranja>();
}

/// <summary>
/// Franja horaria por dia de semana para una zona. Spec 2.13 seccion 4.2.
/// Si una zona no tiene franja para un dia, no acepta reservas ese dia.
/// </summary>
public class ZonaFranja : TenantEntity
{
    public Guid ZonaConfigReservaId { get; set; }
    public ZonaConfigReserva? ZonaConfigReserva { get; set; }

    public DiaSemanaReserva DiaSemana { get; set; }
    public TimeOnly HoraApertura { get; set; }
    public TimeOnly HoraCierre { get; set; }

    public bool Activa { get; set; } = true;
}

/// <summary>
/// Reserva individual. Spec 2.13 seccion 15.
/// RN-05: no pueden existir dos reservas activas que se solapen en la misma zona.
/// </summary>
public class Reserva : TenantEntity
{
    /// <summary>RSV-{ANO}-{SECUENCIAL} unico por copropiedad.</summary>
    public string Codigo { get; set; } = string.Empty;

    public Guid ZonaComunId { get; set; }
    public ZonaComun? ZonaComun { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public DateOnly Fecha { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    public EstadoReserva Estado { get; set; } = EstadoReserva.Confirmada;

    public bool EsRecurrente { get; set; }
    public Guid? ReservaRecurrenteId { get; set; }
    public ReservaRecurrente? ReservaRecurrente { get; set; }

    public bool ReglamentoAceptado { get; set; }
    public DateTimeOffset? ReglamentoAceptadoAt { get; set; }

    public string? MotivoCancelacion { get; set; }
    public Guid? CanceladaPorPersonaId { get; set; }
    public DateTimeOffset? CanceladaAt { get; set; }

    /// <summary>FK 1:1 al cobro si la zona tiene tarifa.</summary>
    public Guid? ReservaPagoId { get; set; }
    public ReservaPago? ReservaPago { get; set; }
}

/// <summary>
/// Cabecera de una serie recurrente. Spec 2.13 seccion 6 + RN-10/RN-11.
/// (Recurrencia diferida a Fase 2 - la entidad existe para FK pero el motor no se construye en MVP).
/// </summary>
public class ReservaRecurrente : TenantEntity
{
    public Guid ZonaComunId { get; set; }
    public Guid PersonaId { get; set; }
    public Guid UnidadPrivadaId { get; set; }

    public FrecuenciaRecurrencia Frecuencia { get; set; }

    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public int? TotalOcurrencias { get; set; }

    public EstadoReserva Estado { get; set; } = EstadoReserva.Confirmada;
}

/// <summary>
/// Cobro asociado a una reserva con tarifa. Spec 2.13 seccion 15.
/// MVP: solo registra el monto en Pendiente. Integracion Wompi real en Fase 2.
/// </summary>
public class ReservaPago : TenantEntity
{
    public Guid ReservaId { get; set; }
    public Reserva? Reserva { get; set; }

    public decimal Monto { get; set; }
    public EstadoPagoReserva EstadoPago { get; set; } = EstadoPagoReserva.Pendiente;

    public string? WompiTransactionId { get; set; }
    public string? WompiReference { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}

/// <summary>
/// Bloqueo manual del admin sobre una zona. Spec 2.13 seccion 8 + RN-15.
/// Auto bloqueo desde 2.3 cuando zona pasa a EnMantenimiento (origen AutomaticoSistema).
/// </summary>
public class ZonaBloqueo : TenantEntity
{
    public Guid ZonaComunId { get; set; }
    public ZonaComun? ZonaComun { get; set; }

    public TipoBloqueoZona Tipo { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }

    public EtiquetaBloqueo Etiqueta { get; set; } = EtiquetaBloqueo.Otro;
    public string? MotivoPersonalizado { get; set; }

    public bool VisibleParaResidentes { get; set; }

    public OrigenBloqueoZona Origen { get; set; } = OrigenBloqueoZona.ManualAdmin;
    public Guid? CreadoPorPersonaId { get; set; }
}
