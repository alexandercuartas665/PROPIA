using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Catalogo configurable de estados de gestion de cartera por copropiedad. Spec 2.7 v1.0 seccion 14.1.
/// Los 4 estados base se siembran lazy. "En mora" es el unico estado inicial.
/// </summary>
public class EstadoCarteraConfig : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public int DiasAlerta { get; set; }
    public string? Color { get; set; }
    public bool EsInicial { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Configuracion general de cartera por copropiedad. Singleton por tenant (spec 2.7 v1.0 seccion 14).
/// </summary>
public class CarteraConfig : TenantEntity
{
    public ModoCalculoIntereses ModoCalculoIntereses { get; set; } = ModoCalculoIntereses.PorCuotaIndividual;
    public ReglaImputacion ReglaImputacion { get; set; } = ReglaImputacion.InteresesCapitalAntiguo;

    // Paz y salvo
    public bool PazSalvoAutomatico { get; set; }
    public bool PazSalvoCondicionado { get; set; } = true;
    public bool SolicitudResidente { get; set; } = true;
    public int ValidezPazSalvoDias { get; set; } = 30;
    public string? MensajePazSalvo { get; set; }

    // Acuerdos de pago
    public int PlazoAceptacionDias { get; set; } = 5;
    public int GraciaCuotaAcuerdo { get; set; }

    // Tasa de mora (mensual %) - en MVP se almacena aqui, en Fase 2 vendra de 2.3 Finanzas
    public decimal TasaMoraMensual { get; set; } = 1.5m;
    public int PeriodoGraciaDias { get; set; } = 5;
}

/// <summary>
/// Estado de cartera por unidad privada. Spec 2.7 v1.0 tabla cartera_unidad.
/// Snapshot agregado - se mantiene sincronizado por el servicio.
/// </summary>
public class CarteraUnidad : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public Guid? EstadoGestionId { get; set; }
    public EstadoCarteraConfig? EstadoGestion { get; set; }

    public decimal SaldoCapital { get; set; }
    public decimal SaldoIntereses { get; set; }
    public decimal SaldoTotal => SaldoCapital + SaldoIntereses;

    public DateOnly? FechaPrimerMora { get; set; }
    public DateOnly? FechaUltimoPago { get; set; }
    public bool TieneAcuerdoVigente { get; set; }

    /// <summary>Dias en el estado de gestion actual (para alertas de escalamiento).</summary>
    public DateOnly? FechaCambioEstadoActual { get; set; }
}

/// <summary>
/// Cuota impaga con su capital e intereses acumulados. Spec 2.7 v1.0 tabla deuda_detalle.
/// El capital_original es inmutable (RN-03).
/// </summary>
public class DeudaDetalle : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    /// <summary>FK a la LiquidacionUnidad de 2.6 que origino esta deuda.</summary>
    public Guid LiquidacionUnidadId { get; set; }
    public LiquidacionUnidad? LiquidacionUnidad { get; set; }

    public DateOnly Periodo { get; set; }
    public string Concepto { get; set; } = string.Empty;

    public decimal CapitalOriginal { get; set; }
    public decimal CapitalPendiente { get; set; }
    public decimal InteresAcumulado { get; set; }

    public DateOnly FechaVencimiento { get; set; }
    public DateOnly? FechaInicioMora { get; set; }

    public EstadoDeudaDetalle Estado { get; set; } = EstadoDeudaDetalle.Pendiente;
}

/// <summary>Acuerdo de pago. Spec 2.7 v1.0 tabla acuerdos_pago.</summary>
public class AcuerdoPago : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public EstadoAcuerdoPago Estado { get; set; } = EstadoAcuerdoPago.Borrador;

    public decimal MontoTotal { get; set; }
    public decimal CapitalIncluido { get; set; }
    public decimal InteresesIncluidos { get; set; }
    public decimal InteresesCondonados { get; set; }

    public int NumeroCuotas { get; set; }
    public DateOnly FechaPrimeraCuota { get; set; }

    public string? NotasAdmin { get; set; }

    /// <summary>FK al acuerdo padre si esta fue una renegociacion. RN-11.</summary>
    public Guid? AcuerdoPadreId { get; set; }
    public AcuerdoPago? AcuerdoPadre { get; set; }

    // Trazabilidad de aceptacion (inmutable)
    public string? AceptacionHash { get; set; }
    public DateTimeOffset? AceptacionAt { get; set; }
    public Guid? AceptacionPersonaId { get; set; }
    public string? AceptacionIp { get; set; }
    public string? AceptacionMetodo { get; set; }

    public Guid? CreadoPorUsuarioId { get; set; }
    public DateOnly? FechaExpiracion { get; set; }

    public ICollection<AcuerdoCuota> Cuotas { get; set; } = new List<AcuerdoCuota>();
}

/// <summary>Cuota dentro de un acuerdo de pago.</summary>
public class AcuerdoCuota : TenantEntity
{
    public Guid AcuerdoId { get; set; }
    public AcuerdoPago? Acuerdo { get; set; }

    public int NumeroCuota { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public decimal Monto { get; set; }
    public decimal Capital { get; set; }
    public decimal Intereses { get; set; }
    public EstadoCuotaAcuerdo Estado { get; set; } = EstadoCuotaAcuerdo.Pendiente;
    public DateOnly? FechaPago { get; set; }
}

/// <summary>Paz y salvo emitido (documento inmutable). Spec 2.7 v1.0 tabla paz_salvo_emitidos + RN-08.</summary>
public class PazSalvoEmitido : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public TipoPazSalvo Tipo { get; set; }
    public EstadoPazSalvo Estado { get; set; } = EstadoPazSalvo.Vigente;
    public string? Condiciones { get; set; }

    public DateTimeOffset FechaEmision { get; set; } = DateTimeOffset.UtcNow;
    public DateOnly FechaVencimiento { get; set; }

    public Guid EmitidoPorUsuarioId { get; set; }
    public EmisionPazSalvo EmisionTipo { get; set; }

    public string? DocumentoUrl { get; set; }

    /// <summary>SHA-256 verification code, unique global.</summary>
    public string CodigoVerificacion { get; set; } = string.Empty;

    // Anulacion (campos nullable - la fila no se borra, solo se marca)
    public Guid? AnuladoPorUsuarioId { get; set; }
    public DateTimeOffset? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
}

/// <summary>Condonacion de deuda (inmutable con snapshots JSON). Spec 2.7 v1.0 tabla condonaciones + RN-10.</summary>
public class Condonacion : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public TipoCondonacion Tipo { get; set; }
    public decimal MontoCondonado { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? DocumentoSoporteUrl { get; set; }

    /// <summary>Snapshot JSON del estado de la deuda antes de la condonacion.</summary>
    public string SaldoAntes { get; set; } = "{}";

    /// <summary>Snapshot JSON del estado de la deuda despues.</summary>
    public string SaldoDespues { get; set; } = "{}";

    public Guid AutorizadoPorUsuarioId { get; set; }
}

/// <summary>Historial append-only de eventos de gestion de cartera por unidad.</summary>
public class CarteraHistorial : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }

    public TipoEventoCartera TipoEvento { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>JSON con datos adicionales (estado anterior, monto, etc).</summary>
    public string? DatosAdicionales { get; set; }

    public Guid RealizadoPorUsuarioId { get; set; }
    public DateTimeOffset OcurridoAt { get; set; } = DateTimeOffset.UtcNow;
}
