using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Asignacion de una etiqueta del catalogo a un vinculo persona/empresa-copropiedad.
/// Spec 2.4 v1.0 tabla directorio_etiqueta.
/// </summary>
public class DirectorioEtiqueta : TenantEntity
{
    public Guid VinculoId { get; set; }
    public DirectorioVinculo? Vinculo { get; set; }

    public Guid EtiquetaId { get; set; }
    public EtiquetaCatalogo? Etiqueta { get; set; }
}
