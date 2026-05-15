using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Permiso base de un cargo en un modulo de Capa 1. Spec 1.3 v1.0 tabla org_cargo_permiso.
/// Define la plantilla por cargo. Los colaboradores pueden tener overrides individuales
/// en <see cref="OrgColaboradorPermiso"/>.
/// </summary>
public class OrgCargoPermiso : BaseEntity
{
    public Guid CargoId { get; set; }
    public OrgCargo? Cargo { get; set; }

    public ModuloCapa1 Modulo { get; set; }
    public NivelPermisoCapa1 Nivel { get; set; } = NivelPermisoCapa1.SinAcceso;
}
