using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Token de API de la Consola A&amp;D (Super Admin). Permite llamar los endpoints de Super Admin desde scripts
/// o herramientas con <c>Authorization: Bearer pak_...</c>, con el mismo poder que la sesion del Super Admin que
/// lo creo. Global (sin tenant). Solo se guarda el HASH (sha256) del token: el valor completo se muestra una
/// unica vez al crearlo. Se puede revocar y vencer; registra el ultimo uso.
/// </summary>
public class SuperAdminApiToken : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Primeros caracteres del token (ej. "pak_7Hq2"), para reconocerlo en la lista. No sirve para autenticar.</summary>
    public string Prefijo { get; set; } = string.Empty;

    /// <summary>sha256 (hex en minuscula) del token completo.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Super Admin que lo creo: el token actua en su nombre (si se desactiva, el token deja de servir).</summary>
    public Guid SuperAdminUsuarioId { get; set; }
    public SuperAdminUsuario? SuperAdminUsuario { get; set; }

    /// <summary>Null = no vence.</summary>
    public DateTimeOffset? ExpiraAt { get; set; }
    public DateTimeOffset? RevocadoAt { get; set; }
    public DateTimeOffset? UltimoUsoAt { get; set; }
    public string? UltimoUsoIp { get; set; }
}
