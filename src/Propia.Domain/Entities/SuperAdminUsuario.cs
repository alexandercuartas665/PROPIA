using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Usuario interno de A&D GROUP con acceso a la Consola de Administracion (modulo 0.1).
/// Entidad GLOBAL - NO tiene tenant_id. Vive en URL/dominio separado (admin.propia.app).
/// Spec: modulo 0.1 Super Admin Console - reglas criticas: MFA TOTP obligatorio, IP allowlist.
/// </summary>
public class SuperAdminUsuario : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolSuperAdmin Rol { get; set; } = RolSuperAdmin.Soporte;

    /// <summary>
    /// Secret TOTP cifrado en reposo. Se genera al primer login y NUNCA es opcional.
    /// </summary>
    public string? MfaSecret { get; set; }
    public bool MfaConfigurado { get; set; }

    public bool Activo { get; set; } = true;
    public DateTimeOffset? UltimoAcceso { get; set; }
    public string? UltimaIp { get; set; }
}
