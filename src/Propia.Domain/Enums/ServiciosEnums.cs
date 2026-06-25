namespace Propia.Domain.Enums;

/// <summary>
/// Estado de un Servicio contratado (modulo Finanzas - Servicios y contratos).
/// Distinto de EstadoContrato: un servicio agrupa contratos y se gestiona aparte.
/// </summary>
public enum EstadoServicio
{
    Activo = 1,
    Suspendido = 2,
    Inactivo = 3
}
