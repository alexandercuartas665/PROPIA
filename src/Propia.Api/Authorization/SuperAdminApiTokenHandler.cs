using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Propia.Application.SuperAdmin;

namespace Propia.Api.Authorization;

/// <summary>
/// Autenticacion con token de API de la Consola A&amp;D: <c>Authorization: Bearer pak_...</c>. Solo la usa la
/// politica SuperAdmin (AddAuthenticationSchemes): no abre ningun endpoint de copropiedad. El principal lleva los
/// mismos claims que el JWT del Super Admin que creo el token (user_id, email, is_super_admin, super_admin_rol) mas
/// auth_method=api_token y api_token_id. Las escrituras quedan en el log del Super Admin.
/// </summary>
public sealed class SuperAdminApiTokenHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "SuperAdminApiToken";
    public const string ClaimMetodo = "auth_method";
    public const string MetodoToken = "api_token";
    private const string Bearer = "Bearer ";

    private readonly IApiTokensService _tokens;

    public SuperAdminApiTokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        IApiTokensService tokens) : base(options, logger, encoder)
        => _tokens = tokens;

    /// <summary>true si la peticion trae un token de API (para que el JwtBearer no intente leerlo como JWT).</summary>
    public static bool TraeToken(HttpRequest req)
        => req.Headers.Authorization.ToString().StartsWith(Bearer + "pak_", StringComparison.Ordinal);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TraeToken(Request)) return AuthenticateResult.NoResult();
        var token = Request.Headers.Authorization.ToString()[Bearer.Length..].Trim();
        var escribe = !(HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method) || HttpMethods.IsOptions(Request.Method));
        var v = await _tokens.ValidarAsync(token, Context.Connection.RemoteIpAddress?.ToString(),
            escribe ? $"{Request.Method} {Request.Path}" : null, Context.RequestAborted);
        if (v is null) return AuthenticateResult.Fail("Token de API invalido, vencido o revocado.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, v.UsuarioId.ToString()),
            new(ClaimTypes.NameIdentifier, v.UsuarioId.ToString()),
            new("user_id", v.UsuarioId.ToString()),
            new(ClaimTypes.Email, v.Email),
            new("email", v.Email),
            new("is_super_admin", "true"),
            new("super_admin_rol", v.Rol.ToString()),
            new(ClaimMetodo, MetodoToken),
            new("api_token_id", v.TokenId.ToString()),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme));
    }
}

/// <summary>
/// Acciones que exigen sesion interactiva del Super Admin (login + MFA) y NO se pueden hacer con un token de API:
/// gestionar tokens, MFA y el equipo A&amp;D. Asi un token filtrado no puede crear administradores ni tokens nuevos.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SoloSesionInteractivaAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.User.HasClaim(SuperAdminApiTokenHandler.ClaimMetodo, SuperAdminApiTokenHandler.MetodoToken))
            context.Result = new ObjectResult(new { error = "Esta acción requiere iniciar sesión en la consola; no se permite con un token de API." })
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}
