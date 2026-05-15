using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Evento del feed de actividad reciente de la copropiedad. Spec 2.2 v1.0 seccion 4.1
/// (feed de actividad reciente). Pagina en lotes de 10 (RN-14).
/// Independiente del audit log tecnico - es registro funcional para el usuario.
/// </summary>
public class ActividadFeed : TenantEntity
{
    public TipoEventoActividad Tipo { get; set; }

    /// <summary>Actor que realizo la accion (FK Persona).</summary>
    public Guid? ActorPersonaId { get; set; }
    public string? ActorNombre { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public string? ModuloCodigo { get; set; }
    public string? UrlItem { get; set; }
    public DateTimeOffset OcurridoAt { get; set; } = DateTimeOffset.UtcNow;
}
