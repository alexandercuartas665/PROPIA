using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Configuracion personal del panel por usuario y organizacion. Spec 1.1 v1.0
/// tabla panel_configuracion_usuario + RN-12 (personal, no global).
/// </summary>
public class PanelConfiguracionUsuario : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public VistaPanelDefault VistaDefault { get; set; } = VistaPanelDefault.Tarjetas;

    /// <summary>JSON con lista ordenada de codigos de KPI activos.</summary>
    public string KpisGlobales { get; set; } = "[]";

    /// <summary>JSON con lista ordenada de codigos de indicadores en tarjeta.</summary>
    public string TarjetaIndicadores { get; set; } = "[]";

    public bool FeedActivo { get; set; }
    public bool ProximosEventosActivo { get; set; } = true;
    public int ProximosEventosCount { get; set; } = 7;
}
