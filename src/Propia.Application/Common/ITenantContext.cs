namespace Propia.Application.Common;

/// <summary>
/// Contexto del tenant ACTIVO para la request/scope actual.
/// - En ASP.NET Core: lo setea el TenantMiddleware leyendo el claim tenant_id del JWT.
/// - En jobs Hangfire: lo setea el codigo del job al procesar un evento de un tenant.
/// - En tests: se setea manualmente para validar aislamiento.
///
/// EF Core lee CurrentTenantId para aplicar HasQueryFilter en todas las TenantEntity.
/// PropiaDbContext lo usa tambien para asignar TenantId automaticamente en INSERTs.
///
/// CurrentTenantId == null significa que la operacion es global (Capa 0/1 o auth).
/// En ese caso, RLS de PostgreSQL bloquea cualquier SELECT a tablas con tenant_id.
/// </summary>
public interface ITenantContext
{
    Guid? CurrentTenantId { get; }
    void SetTenant(Guid tenantId);
    void Clear();
}
