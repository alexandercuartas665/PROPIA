using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Evento del feed de actividad consolidado del panel. Spec 1.1 v1.0 tabla panel_feed_evento.
/// Entidad GLOBAL (Capa 1) - paginada en lotes de 20 (RN-10).
/// </summary>
public class PanelFeedEvento : BaseEntity
{
    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public TipoEventoPanel TipoEvento { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Tipo de entidad de origen (tarea, pqrsd, pago, etc).</summary>
    public string? EntidadTipo { get; set; }
    public Guid? EntidadId { get; set; }
    public string? UrlAccion { get; set; }

    public DateTimeOffset OcurridoAt { get; set; } = DateTimeOffset.UtcNow;
}
