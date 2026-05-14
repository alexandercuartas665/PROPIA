using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Middleware;

/// <summary>
/// Middleware que extrae el tenant_id del JWT y lo propaga a:
/// 1. ITenantContext - para HasQueryFilter en EF Core.
/// 2. SET LOCAL app.tenant_id en la sesion PostgreSQL - para RLS.
///
/// Si el request no tiene token o el token no trae tenant_id, el contexto queda
/// vacio. RLS bloqueara automaticamente cualquier query a tablas de tenant.
/// Esto es seguro por defecto.
///
/// Endpoints publicos (login, /health, etc.) no son afectados - simplemente no
/// pueden leer datos de tenant si no tienen contexto.
///
/// IMPORTANTE: este middleware DEBE registrarse DESPUES de UseAuthentication()
/// y ANTES de UseAuthorization() / MapControllers().
/// </summary>
public class TenantMiddleware
{
    private const string TenantClaim = "tenant_id";
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context, ITenantContext tenantContext, PropiaDbContext dbContext)
    {
        var tenantClaim = context.User?.FindFirst(TenantClaim)?.Value;

        if (!string.IsNullOrWhiteSpace(tenantClaim) && Guid.TryParse(tenantClaim, out var tenantId))
        {
            tenantContext.SetTenant(tenantId);
            // SET LOCAL aplica solo a la transaccion/sesion actual del pool.
            // En cada request EF abre/cierra conexion (Pool); por eso lo seteamos antes
            // de cualquier query, y al cerrar la conexion el SET LOCAL se descarta.
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', {0}, false)",
                tenantId.ToString());
        }
        else
        {
            // Sin tenant: forzamos limpieza por si la conexion del pool venia con valor previo.
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', '', false)");
        }

        await _next(context);
    }
}
