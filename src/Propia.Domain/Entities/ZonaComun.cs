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

    /// <summary>Imagen principal de la zona (ficha). Binario en IBlobStorage.</summary>
    public string? ImagenUrl { get; set; }

    // ----- Programa de mantenimiento (ficha de la zona, prototipo) -----
    /// <summary>"Interno" o "Contrato".</summary>
    public string MantenimientoTipo { get; set; } = "Interno";
    /// <summary>Nombre del contrato relacionado si MantenimientoTipo = Contrato.</summary>
    public string? MantenimientoContrato { get; set; }
    /// <summary>"Semanal" / "Quincenal" / "Mensual" / "Trimestral" / "Semestral".</summary>
    public string MantenimientoFrecuencia { get; set; } = "Mensual";
    /// <summary>Dia del mes (1-31) cuando la frecuencia es Mensual.</summary>
    public int? MantenimientoDiaMes { get; set; }

    /// <summary>
    /// Estado operativo de la zona. Spec 2.11 - fuente unica de verdad para Mantenimiento.
    /// Si pasa a EnMantenimiento, 2.13 bloquea reservas automaticamente (RN-08).
    /// </summary>
    public EstadoZonaComunMantenimiento Estado { get; set; } = EstadoZonaComunMantenimiento.Activa;
}
