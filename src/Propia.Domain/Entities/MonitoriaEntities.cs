using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Log estructurado del sistema (append-only). Modulo 0.3 Monitoria y Auditoria
/// Global. Solo accesible para SuperAdmin via /api/admin/monitoria/logs.
/// Trigger SQL bloquea UPDATE/DELETE (replica patron de super_admin_logs).
/// </summary>
public class SistemaLog : BaseEntity
{
    public TipoEventoSistema TipoEvento { get; set; }
    public SeveridadIncidente Severidad { get; set; } = SeveridadIncidente.Info;

    /// <summary>Tenant relacionado al evento. Null = evento global.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Usuario o SuperAdmin que ejecuto la accion. Null = sistema/job.</summary>
    public Guid? ActorUsuarioId { get; set; }

    public string Mensaje { get; set; } = string.Empty;

    /// <summary>Codigo del modulo origen (ej. "0.1", "1.5", "2.10").</summary>
    public string? ModuloOrigenCodigo { get; set; }

    /// <summary>JSON con datos adicionales del evento.</summary>
    public string? DetalleJson { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>
/// Incidente operacional que requiere atencion. Modulo 0.3.
/// A diferencia del log (granular), un incidente es algo accionable: caida
/// detectada, integracion externa fallando, anomalia de seguridad, etc.
/// </summary>
public class SistemaIncidente : BaseEntity
{
    public SeveridadIncidente Severidad { get; set; }
    public EstadoIncidente Estado { get; set; } = EstadoIncidente.Abierto;

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Modulo o servicio afectado (ej. "T.2", "0.2", "Infrastructure").</summary>
    public string? ServicioAfectado { get; set; }

    /// <summary>Tenant impactado (si aplica). Null = global.</summary>
    public Guid? TenantImpactadoId { get; set; }

    public Guid? AsignadoSuperAdminId { get; set; }

    public DateTimeOffset DetectadoAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResueltoAt { get; set; }

    public string? CausaRaiz { get; set; }
    public string? SolucionAplicada { get; set; }
}

/// <summary>
/// Snapshot diario de metricas globales del sistema. Modulo 0.3.
/// Calculado por BackgroundService nocturno. Hist&oacute;rico para dashboards de salud.
/// </summary>
public class MetricaUsoDiaria : BaseEntity
{
    public DateOnly Fecha { get; set; }

    public int TotalTenants { get; set; }
    public int TenantsActivos { get; set; }
    public int TotalOrganizaciones { get; set; }
    public int TotalUsuarios { get; set; }
    public int TotalSuperAdmins { get; set; }

    public int TareasCreadas24h { get; set; }
    public int PqrsdsRadicadas24h { get; set; }
    public int ComunicadosEnviados24h { get; set; }
    public int NotificacionesDespachadas24h { get; set; }
    public int IncidentesAbiertos { get; set; }
    public int IncidentesCriticos { get; set; }
}
