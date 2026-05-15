using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Sesiones activas por usuario. Spec 2.5 v1.0 tabla <c>usuario_sesion</c>.
/// Guardamos el hash del JWT, nunca el token en claro. Util para listar sesiones
/// activas y cerrarlas remotamente (cuando se revoque acceso o el usuario lo solicite).
/// </summary>
public class UsuarioSesion : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Guid? TenantId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? Dispositivo { get; set; }
    public string? IpOrigen { get; set; }
    public TipoAuthMetodo CanalAuth { get; set; }
    public bool Activa { get; set; } = true;
    public DateTimeOffset UltimoUsoAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiraAt { get; set; }
}
