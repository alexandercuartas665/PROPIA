using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Miembro de un comite. La persona se vincula desde el Directorio (modulo 2.4).
/// </summary>
public class ComiteMiembro : TenantEntity
{
    public Guid ComiteId { get; set; }
    public Comite? Comite { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public string? CargoEnComite { get; set; }  // Coordinador, Vocal, Suplente, etc. (libre)
    public bool Activo { get; set; } = true;
}
