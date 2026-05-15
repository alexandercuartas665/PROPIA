using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Metodos de autenticacion vinculados a un usuario. Spec 2.5 v1.0 tabla <c>usuario_auth_metodo</c>.
/// El email+password siempre se asume presente para usuarios creados por el flujo standard;
/// el resto son canales adicionales (Magic Link, OTP Whatsapp, Google/Microsoft SSO).
/// </summary>
public class UsuarioAuthMetodo : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public TipoAuthMetodo Tipo { get; set; }
    public string? ProveedorId { get; set; }  // ID externo en Google/Microsoft (sub claim)
    public bool Activo { get; set; } = true;
}
