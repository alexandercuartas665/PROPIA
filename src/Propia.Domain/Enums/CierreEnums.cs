namespace Propia.Domain.Enums;

/// <summary>
/// Intencion/clasificacion de un cierre de tarjeta (tarea o PQRSD). Cada MotivoCierre
/// configurable lleva una de estas.
/// </summary>
public enum ClasificacionCierre
{
    /// <summary>El caso se resolvio como corresponde.</summary>
    CierreCorrecto = 1,
    /// <summary>Se agoto la via interna sin resolver (ej. PQRSD sin acuerdo).</summary>
    ViaInternaAgotada = 2,
    /// <summary>Se pierde/desiste (sin respuesta del solicitante, duplicada, no aplica).</summary>
    Perdida = 3
}
