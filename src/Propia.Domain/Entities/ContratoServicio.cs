using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Contrato vigente con un proveedor externo (aseo, seguridad, ascensores, seguro PH, etc).
/// Genera alertas automaticas cuando se acerca FechaFin (modulo 2.10 Tareas).
/// </summary>
public class ContratoServicio : TenantEntity
{
    public TipoServicio Tipo { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string? NitProveedor { get; set; }
    public string? Contacto { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public decimal? ValorMensual { get; set; }

    /// <summary>Texto libre: "Renovacion automatica con 30 dias de aviso", etc.</summary>
    public string? Observaciones { get; set; }
}
