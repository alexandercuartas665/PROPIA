namespace Propia.Application.MiPerfil;

/// <summary>
/// Self-service del usuario autenticado (modulo "Mi Perfil" / Mi cuenta): foto, firma y
/// contactos de notificacion. Todo se resuelve por el userId del JWT (no requiere tenant).
/// </summary>
public interface IMiPerfilService
{
    Task<string?> SubirFotoAsync(Guid userId, Stream content, string contentType, string ext, CancellationToken ct);
    Task<string?> SubirFirmaAsync(Guid userId, Stream content, string contentType, string ext, CancellationToken ct);

    Task<IReadOnlyList<ContactoNotificacionDto>> ListarContactosAsync(Guid userId, CancellationToken ct);
    Task<ContactoNotificacionDto?> AgregarContactoAsync(Guid userId, CrearContactoNotificacionRequest req, CancellationToken ct);
    Task<bool> ActualizarContactoAsync(Guid userId, Guid contactoId, ActualizarContactoNotificacionRequest req, CancellationToken ct);
    Task<bool> EliminarContactoAsync(Guid userId, Guid contactoId, CancellationToken ct);
}
