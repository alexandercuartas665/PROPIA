using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Revisor Fiscal de la copropiedad. Spec 2.3 - seccion 4 Gobierno.
/// La Ley 675 de 2001 (Art. 38) obliga Revisor Fiscal en copropiedades con mas de 30 unidades.
/// Puede ser persona natural o empresa - aqui referenciamos a Persona (Empresa se modela en 2.4).
/// </summary>
public class RevisorFiscal : TenantEntity
{
    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public string? NumeroTarjetaProfesional { get; set; }
    public DateOnly FechaPosesion { get; set; }
    public DateOnly? FechaFin { get; set; }
    public bool Activo { get; set; } = true;
}
