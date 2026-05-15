namespace Propia.Domain.Enums;

/// <summary>
/// Tipo de alerta critica en el dashboard de copropiedad. Spec 2.2 v1.0 seccion 4.1.
/// </summary>
public enum TipoAlertaDashboard
{
    MoraCritica = 1,
    ContratoPorVencer = 2,
    TareaVencida = 3,
    PqrsdSinAtender = 4,
    AsambleaSinQuorum = 5,
    Otro = 99
}

/// <summary>Severidad visual de la alerta. Spec 2.2 v1.0 banda critica.</summary>
public enum SeveridadAlerta
{
    Info = 1,
    Advertencia = 2,
    Critica = 3
}

/// <summary>Tipo de evento en el feed de actividad de la copropiedad. Spec 2.2 seccion 4.1 (feed).</summary>
public enum TipoEventoActividad
{
    TareaCreada = 1,
    TareaCompletada = 2,
    PqrsdRadicada = 3,
    PagoRegistrado = 4,
    AsambleaConvocada = 5,
    ContratoRenovado = 6,
    UnidadAsignada = 7,
    Otro = 99
}
