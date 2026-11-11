namespace Propia.Domain.Enums;

/// <summary>Tipo de servicio publico domiciliario (modulo 2.17).</summary>
public enum TipoServicioPublico
{
    EnergiaElectrica = 1,
    Acueducto = 2,
    Gas = 3,
    TelefoniaFija = 4,
    Internet = 5,
    Aseo = 6
}

/// <summary>Estado de un registro de consumo: confirmado por recibo o estimado.</summary>
public enum EstadoRegistroConsumo
{
    Confirmado = 1,
    Estimado = 2
}

/// <summary>Cada cuanto factura el prestador (avanza el "proximo pago").</summary>
public enum PeriodicidadServicio
{
    Mensual = 1,
    Bimestral = 2,
    Trimestral = 3,
    Semestral = 6,
    Anual = 12
}

/// <summary>Como entro un registro de consumo: leido de un recibo con IA o digitado a mano.</summary>
public enum OrigenRegistroConsumo
{
    Manual = 1,
    IA = 2
}

/// <summary>Estado de una reclamacion ante el prestador del servicio.</summary>
public enum EstadoReclamacionServicio
{
    Radicada = 1,
    EnTramite = 2,
    Resuelta = 3,
    Cerrada = 4
}
