using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Snapshot materializado de indicadores por copropiedad para el panel consolidado.
/// Spec 1.1 v1.0 tabla panel_snapshot_copropiedad.
/// Es entidad GLOBAL (Capa 1) - filtra por OrganizacionId. Una fila por (Org, Tenant).
/// El job programado o el recalculo on-demand actualizan este snapshot - el panel
/// NUNCA consulta tablas operativas de Capa 2 directamente.
/// </summary>
public class PanelSnapshotCopropiedad : BaseEntity
{
    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public EstadoSaludCopropiedad EstadoSalud { get; set; } = EstadoSaludCopropiedad.Verde;
    public int AlertasCriticas { get; set; }
    public int TareasVencidas { get; set; }
    public int PqrsdSinResponder { get; set; }

    /// <summary>% recaudo del mes en curso (0-100).</summary>
    public decimal? RecaudoMesPorcentaje { get; set; }

    public decimal? CarteraVencidaCop { get; set; }

    public DateOnly? ProximoEventoFecha { get; set; }
    public string? ProximoEventoTipo { get; set; }
    public string? ProximoEventoLabel { get; set; }

    public DateTimeOffset CalculadoAt { get; set; } = DateTimeOffset.UtcNow;
}
