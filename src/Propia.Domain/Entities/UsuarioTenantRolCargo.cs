using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Tabla puente N:N entre <see cref="UsuarioTenant"/> y <see cref="Rol"/> (modulo 2.5 v2.0, multi-rol).
/// Un usuario puede tener VARIOS roles en una copropiedad. El acceso (UsuarioTenant) conserva
/// <see cref="UsuarioTenant.RolId"/> como "rol principal"/compat; el conjunto completo de roles asignados
/// vive aqui. En la sesion el usuario entra con UNO de estos (rol activo por sesion). Es TenantEntity (RLS).
/// </summary>
public class UsuarioTenantRol : TenantEntity
{
    public Guid UsuarioTenantId { get; set; }
    public UsuarioTenant? UsuarioTenant { get; set; }

    public Guid RolId { get; set; }
    public Rol? Rol { get; set; }
}

/// <summary>
/// Cargo(s) de un usuario en la copropiedad (modulo 2.5 v2.0). DESCRIPTIVO (puesto operativo), NO otorga
/// permisos (a diferencia del Rol). Seleccion multiple. Las opciones son editables por A&amp;D desde el
/// catalogo global (lista "usuario.cargo"); aqui se guarda la clave/etiqueta elegida. Es TenantEntity (RLS).
/// </summary>
public class UsuarioTenantCargo : TenantEntity
{
    public Guid UsuarioTenantId { get; set; }
    public UsuarioTenant? UsuarioTenant { get; set; }

    /// <summary>Valor del cargo (etiqueta/clave del catalogo "usuario.cargo"). Ej. "Portero", "Aseador".</summary>
    public string Cargo { get; set; } = string.Empty;
}
