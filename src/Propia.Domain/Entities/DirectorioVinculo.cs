using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Vinculo de una persona o empresa con una copropiedad. Spec 2.4 v1.0 tabla directorio_vinculo.
/// FK polimorfica (EntidadTipo + EntidadId) hacia Persona.id o Empresa.id.
/// </summary>
public class DirectorioVinculo : TenantEntity
{
    public EntidadDirectorio EntidadTipo { get; set; }
    public Guid EntidadId { get; set; }

    public DateOnly FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }

    public EstadoVinculo Estado { get; set; } = EstadoVinculo.Activo;
    public string? MotivoInactivacion { get; set; }
}
