using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Evento interno del calendario multi-copropiedad. Spec 1.2 seccion 8 y 19.
/// Global - pertenece a una Organizacion (Capa 1), no a un Tenant.
/// </summary>
public class CalendarioEvento : BaseEntity
{
    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    /// <summary>NULL = evento sin copropiedad asociada (de toda la organizacion).</summary>
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public TipoEventoInterno Tipo { get; set; } = TipoEventoInterno.RecordatorioPersonal;

    public DateTimeOffset FechaInicio { get; set; }
    public DateTimeOffset? FechaFin { get; set; }
    public bool EsDiaCompleto { get; set; }

    public string ZonaHoraria { get; set; } = "America/Bogota";

    /// <summary>NULL = sin recordatorio. Si tiene valor, minutos antes del inicio.</summary>
    public int? RecordatorioMinutos { get; set; }
    public bool RecordatorioEnviado { get; set; }

    /// <summary>FK a Persona/UsuarioApp que creo el evento.</summary>
    public Guid CreadoPorUsuarioId { get; set; }
}

/// <summary>
/// Configuracion del calendario por usuario + organizacion. Spec 1.2 seccion 19.
/// Una fila por (UsuarioId, OrganizacionId).
/// </summary>
public class CalendarioConfigUsuario : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public VistaCalendario VistaDefault { get; set; } = VistaCalendario.Agenda;
    public VistaCalendario UltimaVista { get; set; } = VistaCalendario.Agenda;

    /// <summary>JSON array de TenantId. NULL = todas las PH visibles.</summary>
    public string? FiltroCopropiedadesJson { get; set; }

    /// <summary>JSON array de categorias. NULL = todas.</summary>
    public string? FiltroTiposJson { get; set; }

    /// <summary>Token para feed iCal personal. NULL = no generado aun.</summary>
    public Guid? IcalToken { get; set; }
    public DateTimeOffset? IcalTokenGeneradoAt { get; set; }

    // Anticipacion de recordatorios (dias)
    public int AnticipacionAsamblea { get; set; } = 7;
    public int AnticipacionTarea { get; set; } = 1;
    public int AnticipacionMantenimiento { get; set; } = 3;
    public int AnticipacionPqrsd { get; set; } = 2;
}
