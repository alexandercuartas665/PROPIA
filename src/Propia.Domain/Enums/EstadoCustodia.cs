namespace Propia.Domain.Enums;

/// <summary>
/// Estado de la custodia administrativa de una Copropiedad.
/// Definido en spec del modulo 0.1 Super Admin Console y 1.5 Transferencia de Custodia.
/// </summary>
public enum EstadoCustodia
{
    ConAdmin = 1,
    EnTransferencia = 2,
    SinAdmin = 3
}
