namespace Propia.Domain.Common;

/// <summary>
/// Entidad scope-tenant. Pertenece a una sola Copropiedad (Tenant).
/// El TenantId se asigna automaticamente desde ITenantContext en SaveChangesAsync,
/// y se filtra via HasQueryFilter en EF + RLS de PostgreSQL como red de seguridad.
/// CUALQUIER tabla de Capa 2 (operativa de Copropiedad) debe heredar de aqui.
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}
