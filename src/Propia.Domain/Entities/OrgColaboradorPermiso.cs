using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Override individual de permiso para un colaborador en un modulo de Capa 1.
/// Spec 1.3 v1.0 tabla org_colaborador_permiso.
/// Si no existe registro, se usa el nivel de la plantilla del cargo.
/// </summary>
public class OrgColaboradorPermiso : BaseEntity
{
    public Guid ColaboradorId { get; set; }
    public OrgColaborador? Colaborador { get; set; }

    public ModuloCapa1 Modulo { get; set; }
    public NivelPermisoCapa1 Nivel { get; set; }
}
