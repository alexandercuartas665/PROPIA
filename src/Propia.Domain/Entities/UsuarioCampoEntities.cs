using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>Definicion (catalogo) de un campo dinamico propio de los USUARIOS del tenant.
/// Calcado de <see cref="ZonaCampoDefinicion"/>: EAV con definicion compartida por copropiedad.</summary>
public class UsuarioCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
    /// <summary>Nota/ayuda del campo, editable desde el menu de columna (opcional).</summary>
    public string? Descripcion { get; set; }
}

/// <summary>Valor de un campo dinamico para un usuario-tenant concreto (EAV).
/// El registro es el <c>UsuarioTenant.Id</c> (la membresia del usuario en la copropiedad).</summary>
public class UsuarioCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public UsuarioCampoDefinicion? Definicion { get; set; }
    public Guid UsuarioTenantId { get; set; }
    public string? Valor { get; set; }
}
