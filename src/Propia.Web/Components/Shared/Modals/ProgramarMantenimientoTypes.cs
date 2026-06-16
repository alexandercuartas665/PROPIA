using System;

namespace Propia.Web.Components.Shared.Modals;

/// <summary>
/// Resultado de ProgramarMantenimientoModal. Se devuelve al caller cuando el
/// usuario confirma. El caller decide si lo persiste (llama a la API/MCP) o
/// lo descarta. El modal NO toca BD - es solo el captador del intent.
/// </summary>
public class MantenimientoProgramado
{
    public Guid? ActivoId { get; set; }
    public string ActivoNombre { get; set; } = string.Empty;

    /// <summary>Texto libre del proveedor (puede no estar en Directorio aun).</summary>
    public string Proveedor { get; set; } = string.Empty;

    /// <summary>Id del contrato relacionado, opcional.</summary>
    public string? ContratoId { get; set; }

    /// <summary>"Unica" | "Semanal" | "Mensual" | "Trimestral" | "Semestral" | "Anual"</summary>
    public string? Frecuencia { get; set; }

    /// <summary>0=Lunes, 1=Martes, ..., 6=Domingo</summary>
    public int? DiaSemana { get; set; }

    /// <summary>Hora en formato HH:mm o texto libre.</summary>
    public string? Hora { get; set; }

    /// <summary>Costo en COP, opcional.</summary>
    public decimal? Costo { get; set; }

    /// <summary>Nota libre de alcance.</summary>
    public string? Nota { get; set; }
}

/// <summary>Opcion de contrato para el dropdown del modal.</summary>
public class ContratoOption
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
