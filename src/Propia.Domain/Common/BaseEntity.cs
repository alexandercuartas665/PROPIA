namespace Propia.Domain.Common;

/// <summary>
/// Entidad base con auditoria minima. Toda entidad persistible hereda de esta.
/// Las entidades GLOBALES de la plataforma (sin tenant_id) heredan directamente de BaseEntity:
/// Tenant, Organizacion, Persona, Empresa, SuperAdminUsuario, SuperAdminLog.
/// Las entidades de tenant (operativas de Capa 2) deben heredar de TenantEntity.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
