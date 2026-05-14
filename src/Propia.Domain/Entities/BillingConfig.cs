using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Configuracion global de billing - singleton (un solo registro en la tabla).
/// Modificable desde la consola del SuperAdmin sin necesidad de despliegue.
/// Definicion de valores por defecto en spec del modulo 0.2 seccion 13.
/// </summary>
public class BillingConfig : BaseEntity
{
    /// <summary>ID fijo del singleton - se crea con este UUID en la migracion seed.</summary>
    public static readonly Guid SingletonId = new("11111111-2222-3333-4444-555555555555");

    public int DiasGracia { get; set; } = 5;
    public int DiaAlertaMora1 { get; set; } = 6;
    public int DiaAlertaMora2 { get; set; } = 10;
    public int DiaSuspension { get; set; } = 15;
    public int DiaAlertaCancelacion { get; set; } = 30;
    public int DiaCancelacion { get; set; } = 45;

    public int ReintentosCobro { get; set; } = 3;
    /// <summary>JSON array con los dias de espera entre reintentos. Default [1,3,7].</summary>
    public string DiasEntreReintentos { get; set; } = "[1,3,7]";

    public int DiasPreavisoCobro { get; set; } = 3;
    public int RetencionDatosMeses { get; set; } = 12;
    public int RetencionFacturasAnios { get; set; } = 5;

    /// <summary>IVA por defecto 0% (Art. 476 ET - SaaS excluido). Configurable para LATAM.</summary>
    public decimal ImpuestoPct { get; set; }

    public string Moneda { get; set; } = "COP";
    public ProveedorContable? ProveedorContable { get; set; }
}
