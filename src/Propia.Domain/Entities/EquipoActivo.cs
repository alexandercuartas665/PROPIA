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

    /// <summary>Equipo (unitario, ej. ascensor) vs Activo (suele agruparse, ej. sillas de salon).</summary>
    public TipoElemento Tipo { get; set; } = TipoElemento.Equipo;

    /// <summary>Para activos agrupados (sillas, mesas, juegos): cuantos. Para equipo unitario = 1.</summary>
    public int Cantidad { get; set; } = 1;

    /// <summary>Si es true se puede reservar (zonas comunes, gimnasios). Ascensores y bombas NO.</summary>
    public bool EsReservable { get; set; }

    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? NumeroSerie { get; set; }

    /// <summary>Codigo de barras / etiqueta de inventario para identificar el activo fijo.</summary>
    public string? CodigoBarra { get; set; }
    public DateOnly? FechaInstalacion { get; set; }
    public DateOnly? GarantiaHasta { get; set; }
    public string? Ubicacion { get; set; }
    public string? Observaciones { get; set; }

    // Ficha tecnica: vida util y costos
    public int? VidaUtilAnios { get; set; }
    public DateOnly? FechaAdquisicion { get; set; }
    public decimal? ValorAdquisicion { get; set; }
    public string? Proveedor { get; set; }
    public string? NumeroFactura { get; set; }

    /// <summary>
    /// Estado operativo del equipo. Spec 2.11 - fuente unica de verdad para el modulo
    /// de Mantenimiento, que escribe sobre este campo al cambiar el estado.
    /// </summary>
    public EstadoEquipoActivo Estado { get; set; } = EstadoEquipoActivo.Operativo;
}
