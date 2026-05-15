using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Persona del equipo operativo de la copropiedad. Spec 2.3 - seccion 3 Equipo de trabajo.
/// Una persona puede tener varios roles en la misma PH (vinculos via multiples filas).
/// El acceso al sistema se gestiona en modulo 2.5 - aqui solo registramos el vinculo.
/// </summary>
public class MiembroEquipo : TenantEntity
{
    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public RolEquipo Rol { get; set; }
    public string? RolPersonalizado { get; set; }  // Solo cuando Rol == Otro
    public TipoVinculacion Tipo { get; set; } = TipoVinculacion.Interno;
    public DateOnly FechaVinculacion { get; set; }
    public DateOnly? FechaFin { get; set; }
    public bool Activo { get; set; } = true;
    public bool EsUsuarioSistema { get; set; }  // Tiene credenciales (se gestiona en 2.5)
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Observaciones { get; set; }
}
