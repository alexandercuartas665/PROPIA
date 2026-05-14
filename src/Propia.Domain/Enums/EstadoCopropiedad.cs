namespace Propia.Domain.Enums;

/// <summary>
/// Estados de una Copropiedad (Tenant) segun spec del modulo 0.1 - Super Admin Console.
/// </summary>
public enum EstadoCopropiedad
{
    Activa = 1,
    Suspendida = 2,
    EnCancelacion = 3,
    CanceladoArchivado = 4,
    Eliminado = 5
}
