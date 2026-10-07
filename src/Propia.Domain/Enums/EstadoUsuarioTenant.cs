namespace Propia.Domain.Enums;

/// <summary>
/// Estado de un usuario dentro de una Copropiedad.
/// Spec del modulo 2.5 Usuarios, Roles y Accesos (tabs Activos / Pendientes / Inactivos).
/// </summary>
public enum EstadoUsuarioTenant
{
    Activo = 1,
    /// <summary>Invitacion enviada, aun no aceptada. (En la UI: "Invitacion pendiente".)</summary>
    Pendiente = 2,
    Inactivo = 3,
    /// <summary>
    /// Pendiente de contacto (2.5 v2.0): alta creada pero sin canal/aceptacion todavia. Se agrupa con
    /// <see cref="Pendiente"/> bajo el tab "Pendientes". Valor 4: NO reordenar los anteriores (se persiste
    /// como int sin HasConversion; cambiar los valores existentes corromperia datos).
    /// </summary>
    PendienteContacto = 4
}
