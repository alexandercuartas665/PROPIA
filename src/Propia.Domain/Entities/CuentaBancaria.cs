using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Cuenta bancaria de la copropiedad. Se usan para recaudo y para mostrar en facturas.
/// Una copropiedad puede tener varias cuentas (corrientes, ahorros, encargos fiduciarios)
/// y marca cuales aparecen en la factura. Las canceladas se conservan por historia.
/// </summary>
public class CuentaBancaria : TenantEntity
{
    public string NumeroCuenta { get; set; } = null!;
    public TipoCuentaBancaria TipoCuenta { get; set; }
    public string Banco { get; set; } = null!;

    /// <summary>Si esta marcada se imprime/incluye en el cuerpo de la factura al residente.</summary>
    public bool VerEnFactura { get; set; }

    /// <summary>Marcador de cancelacion: oculta de operaciones nuevas pero se conserva historia.</summary>
    public bool Cancelada { get; set; }
    public DateTimeOffset? FechaCancelacion { get; set; }
}
