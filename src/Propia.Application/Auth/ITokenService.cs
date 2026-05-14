using Propia.Domain.Entities;

namespace Propia.Application.Auth;

/// <summary>
/// Servicio que emite JWTs con los claims que necesita PROPIA:
/// sub (user id), email, persona_id (si existe), tenant_id (el activo seleccionado).
/// El claim tenant_id es el que lee TenantMiddleware para setear app.tenant_id en PostgreSQL.
/// </summary>
public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(ApplicationUser user, Guid? activeTenantId);
}
