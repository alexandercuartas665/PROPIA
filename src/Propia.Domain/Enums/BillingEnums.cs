namespace Propia.Domain.Enums;

/// <summary>Estado del Plan en el catalogo (visible para clientes nuevos).</summary>
public enum EstadoPlan
{
    Activo = 1,
    Inactivo = 2,
    Archivado = 3
}

/// <summary>Ciclo de facturacion de una Suscripcion.</summary>
public enum CicloFacturacion
{
    Mensual = 1,
    Anual = 2
}

/// <summary>Estado actual de una Suscripcion.</summary>
public enum EstadoSuscripcion
{
    Trial = 1,
    Activa = 2,
    Suspendida = 3,
    EnCancelacion = 4,
    Cancelada = 5,
    Archivada = 6
}

/// <summary>Estado de una Factura.</summary>
public enum EstadoFactura
{
    Pendiente = 1,
    Pagada = 2,
    Vencida = 3,
    Anulada = 4
}

/// <summary>Resultado de un intento de cobro automatico.</summary>
public enum ResultadoIntentoCobro
{
    Pendiente = 1,
    Exitoso = 2,
    Fallido = 3
}

/// <summary>Tipo de metodo de pago tokenizado/registrado por el cliente.</summary>
public enum TipoMetodoPago
{
    TarjetaCredito = 1,
    PSE = 2,
    Nequi = 3,
    Daviplata = 4,
    ACH = 5,
    BreB = 6
}

/// <summary>Tipo de evento en el historial de la suscripcion (append-only).</summary>
public enum TipoEventoSuscripcion
{
    Activacion = 1,
    Upgrade = 2,
    Downgrade = 3,
    Suspension = 4,
    Reactivacion = 5,
    Cancelacion = 6,
    CambioMetodoPago = 7,
    AjusteManual = 8,
    CreditoAplicado = 9
}

/// <summary>Origen de un evento en el historial: quien lo disparo.</summary>
public enum OrigenEventoSuscripcion
{
    Sistema = 1,
    SuperAdmin = 2,
    Cliente = 3
}

/// <summary>Tipo de cupon de descuento.</summary>
public enum TipoCupon
{
    Porcentaje = 1,
    FijoCop = 2,
    MesesGratis = 3
}

/// <summary>Como se aplica el cupon sobre la suscripcion.</summary>
public enum AplicacionCupon
{
    UnaVez = 1,
    Recurrente = 2,
    Permanente = 3
}

/// <summary>Proveedor de facturacion electronica DIAN.</summary>
public enum ProveedorContable
{
    Siigo = 1,
    Alegra = 2
}
