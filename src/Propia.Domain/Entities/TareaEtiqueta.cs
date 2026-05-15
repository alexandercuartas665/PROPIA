using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Etiqueta configurable de tarea por copropiedad. Spec 2.10 v1.0 seccion 7.
/// Sin catalogo predefinido - cada copropiedad construye el suyo.
/// </summary>
public class TareaEtiqueta : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Tabla puente N:N entre Tarea y TareaEtiqueta.</summary>
public class TareaEtiquetaAsignacion : TenantEntity
{
    public Guid TareaId { get; set; }
    public Tarea? Tarea { get; set; }

    public Guid EtiquetaId { get; set; }
    public TareaEtiqueta? Etiqueta { get; set; }
}
