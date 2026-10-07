using Propia.Domain.Entities;

namespace Propia.Application.Auth;

/// <summary>
/// Servicio que emite JWTs con los claims que necesita PROPIA:
/// sub (user id), email, persona_id (si existe), tenant_id (el activo seleccionado),
/// rol_id (el rol ACTIVO por sesion, 2.5 v2.0 multi-rol: opcional).
/// El claim tenant_id es el que lee TenantMiddleware para setear app.tenant_id en PostgreSQL;
/// el claim rol_id lo lee el enforcement RBAC para autorizar con el rol elegido esta sesion.
/// </summary>
public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(ApplicationUser user, Guid? activeTenantId, Guid? activeRolId = null);
}
