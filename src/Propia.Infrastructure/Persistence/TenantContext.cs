using Propia.Application.Common;

namespace Propia.Infrastructure.Persistence;

/// <summary>
/// Implementacion AsyncLocal de ITenantContext.
/// Se registra como Scoped en DI - una instancia por HTTP request o scope de job.
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid? CurrentTenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        CurrentTenantId = tenantId;
    }

    public void Clear()
    {
        CurrentTenantId = null;
    }
}
