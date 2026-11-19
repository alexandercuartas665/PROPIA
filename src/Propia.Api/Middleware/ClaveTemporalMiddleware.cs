using Propia.Application.Auth;

namespace Propia.Api.Middleware;

/// <summary>
/// Usuario con clave TEMPORAL (puesta por el administrador de la copropiedad): su JWT trae el claim
/// pwd_chg. Mientras no la cambie, el API le responde 403 {error:"debe_cambiar_clave"} en /api,
/// /hubs y /mcp; solo /connect/* queda abierto (me, refresh, switch-tenant y
/// cambiar-clave-obligatoria). Va despues de UseAuthentication/UseAuthorization.
/// </summary>
public sealed class ClaveTemporalMiddleware
{
    private readonly RequestDelegate _next;

    public ClaveTemporalMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true
            && ctx.User.FindFirst(ClaveTemporal.JwtClaim)?.Value == "1"
            && (ctx.Request.Path.StartsWithSegments("/api")
                || ctx.Request.Path.StartsWithSegments("/hubs")
                || ctx.Request.Path.StartsWithSegments("/mcp")))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = ClaveTemporal.ErrorDebeCambiar });
            return;
        }
        await _next(ctx);
    }
}
