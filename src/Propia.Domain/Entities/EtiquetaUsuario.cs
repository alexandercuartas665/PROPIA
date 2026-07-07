using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Etiqueta de usuario (modulo 2.5 Usuarios y Roles): definicion por copropiedad con color,
/// para clasificar/etiquetar usuarios y miembros (ej. "Consejo", "Aseo", "Mantenimiento aire").
/// Un usuario puede tener varias. Mismo patron que TareaEtiqueta.
/// </summary>
public class EtiquetaUsuario : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Tabla puente N:N entre UsuarioTenant y EtiquetaUsuario.</summary>
public class UsuarioTenantEtiqueta : TenantEntity
{
    public Guid UsuarioTenantId { get; set; }
    public UsuarioTenant? UsuarioTenant { get; set; }

    public Guid EtiquetaId { get; set; }
    public EtiquetaUsuario? Etiqueta { get; set; }
}
