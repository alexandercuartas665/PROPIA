using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Metodo de pago registrado por un cliente (organizacion o copropiedad directa).
/// El TokenWompi NO contiene datos de tarjeta - es solo un identificador de Wompi
/// para cobrar recurrentemente sin almacenar PAN en PROPIA.
/// Entidad GLOBAL - asociada a organizacion XOR copropiedad.
/// </summary>
public class MetodoPago : BaseEntity
{
    public Guid? OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    /// <summary>FK a Tenant (Copropiedad) cuando facturacion directa, sin organizacion.</summary>
    public Guid? CopropiedadId { get; set; }
    public Tenant? Copropiedad { get; set; }

    public TipoMetodoPago Tipo { get; set; }
    public string? TokenWompi { get; set; }
    public string? UltimosDigitos { get; set; }
    public string? Marca { get; set; }
    public string? Banco { get; set; }
    public bool EsPredeterminado { get; set; }
    public bool Activo { get; set; } = true;
}
