using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Catalogo de etiquetas del Directorio. Spec 2.4 seccion 5 v1.0.
/// Las etiquetas <c>base</c> (TenantId == null) son globales y no eliminables.
/// Las etiquetas personalizadas (TenantId != null) son por copropiedad y eliminables si no estan vinculadas.
/// </summary>
public class EtiquetaCatalogo : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public GrupoEtiqueta Grupo { get; set; }
    public AplicaEtiqueta AplicaA { get; set; } = AplicaEtiqueta.Persona;
    public bool EsBase { get; set; }
    public bool TieneLogicaEspecial { get; set; }

    /// <summary>Null = etiqueta base global. NOT NULL = etiqueta personalizada por copropiedad.</summary>
    public Guid? TenantId { get; set; }

    public bool Activo { get; set; } = true;
}
