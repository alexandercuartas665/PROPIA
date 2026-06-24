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

/// <summary>Estado de una reclamacion ante el prestador del servicio.</summary>
public enum EstadoReclamacionServicio
{
    Radicada = 1,
    EnTramite = 2,
    Resuelta = 3,
    Cerrada = 4
}
