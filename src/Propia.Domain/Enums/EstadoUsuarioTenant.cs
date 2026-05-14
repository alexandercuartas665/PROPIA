namespace Propia.Domain.Enums;

/// <summary>
/// Estado de un usuario dentro de una Copropiedad.
/// Spec del modulo 2.5 Usuarios, Roles y Accesos (tabs Activos / Pendientes / Inactivos).
/// </summary>
public enum EstadoUsuarioTenant
{
    Activo = 1,
    Pendiente = 2,
    Inactivo = 3
}
