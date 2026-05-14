using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Miembro del Consejo de Administracion (Ley 675 de 2001 art. 53).
/// La eleccion formal ocurre en el modulo 2.8 Asambleas - aqui se mantiene la
/// composicion actual para que otros modulos la consulten.
/// </summary>
public class MiembroConsejo : TenantEntity
{
    /// <summary>Persona del Directorio (entidad global) que ocupa el cargo.</summary>
    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public CargoConsejo Cargo { get; set; }

    /// <summary>Periodo de mandato.</summary>
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }

    public bool Activo { get; set; } = true;
}
