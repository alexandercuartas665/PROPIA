using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Enums;

namespace Propia.Api.Authorization;

/// <summary>
/// Exige que el usuario autenticado tenga uno de los roles de copropiedad indicados,
/// resuelto del vinculo usuario-copropiedad en el tenant ACTIVO (no del JWT, para reflejar
/// cambios de rol). Cierra la escalada de privilegios del modulo 2.5 (hallazgo P0):
/// hoy cualquier usuario autenticado podia gestionar usuarios/roles.
///
/// Nota: enforcement por rol-string mientras la matriz granular rol_permisos no este sembrada.
/// Cuando exista la matriz, evolucionar a [RequierePermiso(modulo, accion)] usando
/// IRolesService.GetPermisosEfectivosAsync.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiereRolAttribute : TypeFilterAttribute
{
    public RequiereRolAttribute(params string[] rolesPermitidos) : base(typeof(RequiereRolFilter))
    {
        Arguments = new object[] { rolesPermitidos };
    }
}

public sealed class RequiereRolFilter : IAsyncAuthorizationFilter
{
    private readonly string[] _rolesPermitidos;
    private readonly IRolesService _roles;

    public RequiereRolFilter(string[] rolesPermitidos, IRolesService roles)
    {
        _rolesPermitidos = rolesPermitidos;
        _roles = roles;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var personaRaw = context.HttpContext.User.FindFirstValue("persona_id");
        if (!Guid.TryParse(personaRaw, out var personaId))
        {
            context.Result = Forbidden("sin_persona");
            return;
        }

        var rol = await _roles.GetRolActorAsync(personaId, context.HttpContext.RequestAborted);
        if (rol is null || !_rolesPermitidos.Contains(rol, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = Forbidden("rol_insuficiente");
        }
    }

    private static ObjectResult Forbidden(string reason) =>
        new(new { error = "forbidden", reason }) { StatusCode = StatusCodes.Status403Forbidden };
}

/// <summary>
/// Exige que el rol del usuario tenga habilitado (modulo, accion) en la matriz de permisos
/// (rol_permisos), configurable por el Administrador en /configuracion/roles. El Administrador
/// siempre pasa (bypass) - regla "Administrador conserva acceso". Devuelve 403 si no.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequierePermisoAttribute : TypeFilterAttribute
{
    public RequierePermisoAttribute(string modulo, AccionPermiso accion) : base(typeof(RequierePermisoFilter))
    {
        Arguments = new object[] { modulo, accion };
    }
}

public sealed class RequierePermisoFilter : IAsyncAuthorizationFilter
{
    private readonly string _modulo;
    private readonly AccionPermiso _accion;
    private readonly IRolesService _roles;

    public RequierePermisoFilter(string modulo, AccionPermiso accion, IRolesService roles)
    {
        _modulo = modulo;
        _accion = accion;
        _roles = roles;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var personaRaw = context.HttpContext.User.FindFirstValue("persona_id");
        if (!Guid.TryParse(personaRaw, out var personaId))
        {
            context.Result = Deny("sin_persona"); return;
        }

        var ct = context.HttpContext.RequestAborted;
        var rol = await _roles.GetRolActorAsync(personaId, ct);

        // El Administrador siempre conserva acceso (regla de seguridad spec 2.5).
        if (string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase)) return;

        var permisos = await _roles.GetPermisosEfectivosAsync(personaId, ct);
        var ok = permisos.Any(p => p.ModuloCodigo == _modulo && p.Accion == _accion && p.Habilitado);
        if (!ok) context.Result = Deny("permiso_insuficiente");
    }

    private static ObjectResult Deny(string reason) =>
        new(new { error = "forbidden", reason }) { StatusCode = StatusCodes.Status403Forbidden };
}
