namespace Propia.Domain.Enums;

/// <summary>Estado de una cuota impaga en la cartera. Spec 2.7 v1.0 deuda_detalle.</summary>
public enum EstadoDeudaDetalle
{
    Pendiente = 1,
    Parcial = 2,
    Pagado = 3,
    Condonado = 4
}

/// <summary>Estado del acuerdo de pago. Spec 2.7 v1.0 seccion 9.1.</summary>
public enum EstadoAcuerdoPago
{
    Borrador = 1,
    PendienteAceptacion = 2,
    Vigente = 3,
    Completado = 4,
    Incumplido = 5,
    Cancelado = 6,
    Expirado = 7
}

/// <summary>Estado de cada cuota dentro de un acuerdo de pago.</summary>
public enum EstadoCuotaAcuerdo
{
    Pendiente = 1,
    Pagada = 2,
    Vencida = 3
}

/// <summary>Tipo de paz y salvo. Spec 2.7 v1.0 seccion 11.1.</summary>
public enum TipoPazSalvo
{
    Pleno = 1,
    Condicionado = 2
}

/// <summary>Estado del paz y salvo emitido.</summary>
public enum EstadoPazSalvo
{
    Vigente = 1,
    Anulado = 2,
    Vencido = 3
}

/// <summary>Como se emitio el paz y salvo. Spec 2.7 v1.0 seccion 11.</summary>
public enum EmisionPazSalvo
{
    Automatico = 1,
    Manual = 2,
    SolicitudResidente = 3
}

/// <summary>Tipo de condonacion. Spec 2.7 v1.0 seccion 10.1.</summary>
public enum TipoCondonacion
{
    Intereses = 1,
    Capital = 2,
    Total = 3
}

/// <summary>Regla de imputacion de pagos. Spec 2.7 v1.0 seccion 8.1.</summary>
public enum ReglaImputacion
{
    InteresesCapitalAntiguo = 1,
    CapitalAntiguoIntereses = 2,
    DistribucionProporcional = 3
}

/// <summary>Modo de calculo de intereses. Spec 2.7 v1.0 seccion 7.1.</summary>
public enum ModoCalculoIntereses
{
    PorCuotaIndividual = 1,
    PorSaldoTotal = 2
}

/// <summary>Tipo de evento en el historial de gestion de cartera (append-only).</summary>
public enum TipoEventoCartera
{
    CambioEstadoGestion = 1,
    NotificacionEnviada = 2,
    AcuerdoCreado = 3,
    AcuerdoAceptado = 4,
    AcuerdoCancelado = 5,
    AcuerdoIncumplido = 6,
    PagoRegistrado = 7,
    CondonacionAplicada = 8,
    PazSalvoEmitido = 9,
    PazSalvoAnulado = 10,
    SincronizacionDesdePresupuesto = 11,
    Otro = 99
}

/// <summary>Estados base de gestion de cartera (catalogo configurable). Spec 2.7 v1.0 seccion 4.</summary>
public static class EstadoCarteraBase
{
    public const string EnMora = "En mora";
    public const string Notificacion = "Notificacion";
    public const string PreJuridico = "Pre-juridico";
    public const string Juridico = "Juridico";

    /// <summary>Catalogo base: (Nombre, Orden, DiasAlerta, Color, EsInicial).</summary>
    public static readonly (string Nombre, int Orden, int DiasAlerta, string Color, bool EsInicial)[] Base = new[]
    {
        (EnMora, 1, 15, "#f59e0b", true),
        (Notificacion, 2, 15, "#f97316", false),
        (PreJuridico, 3, 30, "#ef4444", false),
        (Juridico, 4, 60, "#7c2d12", false)
    };
}
