using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Registro de ingresos (logins) de usuarios de copropiedad. GLOBAL (sin RLS): lo consulta el
/// Super Admin por organizacion/copropiedad. Guarda tanto ingresos exitosos como intentos fallidos
/// (con el motivo) para trazabilidad y seguridad. No guarda claves ni datos sensibles.
/// </summary>
public class LoginAuditEvent : BaseEntity
{
    /// <summary>Correo con el que se intento ingresar (siempre se guarda, exista o no la cuenta).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Usuario resuelto (ApplicationUser), si el correo existe.</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Persona asociada al usuario, si se resolvio.</summary>
    public Guid? PersonaId { get; set; }

    /// <summary>Copropiedad (tenant) resuelta para el ingreso, cuando es determinable.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Organizacion de la copropiedad, para agrupar en el Super Admin.</summary>
    public Guid? OrganizacionId { get; set; }

    /// <summary>true = ingreso exitoso; false = intento fallido.</summary>
    public bool Exito { get; set; }

    /// <summary>Codigo del motivo cuando el intento falla (ej. clave_incorrecta, email_no_confirmado).</summary>
    public string? Motivo { get; set; }

    /// <summary>IP de origen (best-effort, detras del proxy usa ForwardedHeaders).</summary>
    public string? Ip { get; set; }

    /// <summary>User-Agent del navegador (recortado).</summary>
    public string? UserAgent { get; set; }

    /// <summary>Tipo de cuenta: "client" (copropiedad) o "superadmin".</summary>
    public string TipoCuenta { get; set; } = "client";
}
