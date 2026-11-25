using Microsoft.AspNetCore.Mvc;
using Propia.Domain.Enums;

namespace Propia.Api.Authorization;

/// <summary>
/// Respuesta 403 UNICA de los filtros de permisos (RequierePermiso / RequiereRol). Alex 2026-11-24: antes el cuerpo
/// era {error:"forbidden"} y la UI mostraba "forbidden" (o se caia). Ahora "error" trae un MENSAJE claro para el
/// usuario (que modulo, que accion y a quien pedirlo); las pantallas que ya muestran el "error" del API lo pintan
/// sin cambios. Se conservan "reason" (sin_persona / rol_insuficiente / permiso_insuficiente) para quien lo lea, y
/// "codigo" = "forbidden" como marca estable. Texto de UI con tildes via escapes Unicode (el codigo es ASCII).
/// </summary>
public static class PermisoDenegado
{
    public const string Codigo = "forbidden";

    public static ObjectResult Respuesta(string reason, string? modulo = null, AccionPermiso? accion = null)
    {
        var cuerpo = new Dictionary<string, string>
        {
            ["error"] = Mensaje(reason, modulo, accion),
            ["codigo"] = Codigo,
            ["reason"] = reason
        };
        if (!string.IsNullOrEmpty(modulo)) cuerpo["modulo"] = modulo;
        if (accion is { } a) cuerpo["accion"] = a.ToString();
        return new ObjectResult(cuerpo) { StatusCode = StatusCodes.Status403Forbidden };
    }

    public static string Mensaje(string reason, string? modulo, AccionPermiso? accion) => reason switch
    {
        "permiso_insuficiente" when accion is { } a && !string.IsNullOrEmpty(modulo) =>
            $"No tienes permiso para {AccionPermisoTexto.Verbo(a)} en {ModuloCodigo.Nombre(modulo)}. "
            + "P\u00eddele a un Administrador que lo habilite en Usuarios y Accesos > Roles y permisos.",
        "permiso_insuficiente" =>
            "No tienes permiso para esta acci\u00f3n. P\u00eddele a un Administrador que lo habilite en "
            + "Usuarios y Accesos > Roles y permisos.",
        "rol_insuficiente" =>
            "Tu rol no tiene acceso a esta acci\u00f3n. Se necesita un Administrador de la copropiedad.",
        "sin_persona" =>
            "Tu usuario no est\u00e1 vinculado a esta copropiedad. Cierra sesi\u00f3n y vuelve a entrar; si sigue "
            + "igual, pide al Administrador que revise tu acceso.",
        _ => "No tienes permiso para esta acci\u00f3n."
    };
}
