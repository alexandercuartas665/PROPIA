using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Asignacion de una etiqueta (valor de texto) a un vinculo persona/empresa-copropiedad.
/// Spec 2.4 v1.0 tabla directorio_etiqueta. Desde 2026-10 la etiqueta es un VALOR de texto de la lista
/// de catalogo global <c>directorio.etiqueta</c> (mas los custom por copropiedad), no una FK a un catalogo
/// rico con color/icono/grupo. Una persona/empresa puede tener varias etiquetas (chips neutros).
/// </summary>
public class DirectorioEtiqueta : TenantEntity
{
    public Guid VinculoId { get; set; }
    public DirectorioVinculo? Vinculo { get; set; }

    /// <summary>Etiqueta como texto (label de la lista directorio.etiqueta o un custom del tenant).</summary>
    public string Valor { get; set; } = string.Empty;
}
