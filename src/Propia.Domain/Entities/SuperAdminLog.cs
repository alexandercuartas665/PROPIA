using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Log INMUTABLE de acciones del Super Admin Console (modulo 0.1).
/// Regla critica: solo INSERT. Sin UPDATE ni DELETE.
/// Esta regla se refuerza en BD via trigger PostgreSQL en una migracion posterior.
/// Spec: modulo 0.1 seccion "Log inmutable de acciones".
/// </summary>
public class SuperAdminLog : BaseEntity
{
    public Guid ActorId { get; set; }                       // SuperAdminUsuario.Id
    public string ActorEmail { get; set; } = string.Empty;  // copia para legibilidad si el usuario se elimina
    public string Accion { get; set; } = string.Empty;      // ej: "TENANT_SUSPEND", "IMPERSONATE_START"
    public string? EntidadAfectada { get; set; }            // ej: "Tenant:abc-123"
    public string? Justificacion { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
