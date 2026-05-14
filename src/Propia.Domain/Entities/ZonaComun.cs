using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Zona comun (salon social, BBQ, gimnasio, piscina, etc).
/// Si EsReservable=true, aparece en el modulo 2.13 Reservas Zonas Comunes.
/// </summary>
public class ZonaComun : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public CategoriaZonaComun Categoria { get; set; } = CategoriaZonaComun.Social;
    public string? Descripcion { get; set; }

    public bool EsReservable { get; set; }
    public decimal? TarifaReserva { get; set; }  // COP - tarifa de reserva si aplica
    public int? CapacidadPersonas { get; set; }

    /// <summary>Texto libre: "Lunes a Viernes 8am - 10pm" por ejemplo.</summary>
    public string? HorariosUso { get; set; }
    public string? ReglasUso { get; set; }
}
