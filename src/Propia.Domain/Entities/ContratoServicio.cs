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

    /// <summary>Estado declarado por el admin. "Vencido" se deriva por fecha (no se setea a mano).</summary>
    public EstadoContrato Estado { get; set; } = EstadoContrato.Vigente;

    /// <summary>Dias de anticipacion para alertar el vencimiento (RN-15, default 30).</summary>
    public int DiasAnticipacionAlerta { get; set; } = 30;

    /// <summary>Clausula de renovacion automatica del contrato.</summary>
    public bool RenovacionAutomatica { get; set; }

    /// <summary>Servicio al que pertenece (un servicio agrupa uno o mas contratos). Opcional.</summary>
    public Guid? ServicioId { get; set; }
    public Servicio? Servicio { get; set; }

    /// <summary>FK logico a Expediente (modulo 2.15): un contrato puede pertenecer a un expediente.</summary>
    public Guid? ExpedienteId { get; set; }

    /// <summary>FK logico a Tarea con EsProyecto=true (modulo 2.10): un contrato puede atarse a un proyecto.</summary>
    public Guid? ProyectoTareaId { get; set; }

    /// <summary>Archivos del contrato (PDF firmado, anexos, otrosi).</summary>
    public ICollection<ContratoAdjunto> Adjuntos { get; set; } = new List<ContratoAdjunto>();
}
