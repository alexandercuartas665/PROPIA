using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Activo fisico de la copropiedad (bombas, ascensores, planta electrica, motobombas, etc).
/// El modulo 2.11 Mantenimiento opera sobre estos equipos para preventivos y correctivos.
/// </summary>
public class EquipoActivo : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public CategoriaEquipo Categoria { get; set; } = CategoriaEquipo.Otros;
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? NumeroSerie { get; set; }
    public DateOnly? FechaInstalacion { get; set; }
    public DateOnly? GarantiaHasta { get; set; }
    public string? Ubicacion { get; set; }
    public string? Observaciones { get; set; }
}
