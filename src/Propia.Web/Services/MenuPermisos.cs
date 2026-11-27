using Propia.Application.UsuariosAccesos;
using Propia.Domain.Enums;

namespace Propia.Web.Services;

/// <summary>
/// Filtra el menu lateral por los permisos del usuario (Alex 2026-11-27): un modulo sin permiso de Ver no se
/// ofrece en el menu. El mapa va por la CLAVE base del item (estable aunque el Super Admin lo renombre o lo mueva
/// de seccion) y sale del permiso con el que el API del modulo protege sus escrituras. Los items que piden rol
/// Administrador en el API (Servicios publicos, Seguros, IA, Informes) solo los ve el administrador. Los items
/// sin mapa (propios del Super Admin, organizacion, cuenta) se ven siempre.
/// </summary>
public static class MenuPermisos
{
    /// <summary>Marca de "solo administrador" (el API pide el rol Administrador, no un permiso de modulo).</summary>
    public const string SoloAdmin = "__ADMIN__";

    private static readonly Dictionary<string, string> Mapa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mi-dashboard"] = ModuloCodigo.Dashboard,
        ["mi-config-hub"] = ModuloCodigo.MiCopropiedad,
        ["mi-copropiedad"] = ModuloCodigo.MiCopropiedad,
        ["mi-distribucion"] = ModuloCodigo.MiCopropiedad,
        ["mi-residentes"] = ModuloCodigo.MiCopropiedad,
        ["mi-vehiculos"] = ModuloCodigo.MiCopropiedad,
        ["mi-mascotas"] = ModuloCodigo.MiCopropiedad,
        ["mi-espacios"] = ModuloCodigo.MiCopropiedad,
        ["fin-contratos"] = ModuloCodigo.MiCopropiedad,
        ["fin-numeracion"] = ModuloCodigo.MiCopropiedad,
        ["mi-directorio"] = ModuloCodigo.Directorio,
        ["mi-usuarios"] = ModuloCodigo.UsuariosAccesos,
        ["mi-roles"] = ModuloCodigo.UsuariosAccesos,
        ["cfg-plantillas"] = ModuloCodigo.Plantillas,
        ["cfg-buzones"] = ModuloCodigo.Comunicaciones,
        ["com-correos"] = ModuloCodigo.Comunicaciones,
        ["com-comunicaciones"] = ModuloCodigo.Comunicaciones,
        ["fin-presupuesto"] = ModuloCodigo.Presupuesto,
        ["fin-cartera"] = ModuloCodigo.Cartera,
        ["fin-siniestros"] = ModuloCodigo.Siniestros,
        ["gob-asambleas"] = ModuloCodigo.Asambleas,
        ["gob-pqrs"] = ModuloCodigo.Pqrs,
        ["op-tareas"] = ModuloCodigo.Tareas,
        ["op-tableros"] = ModuloCodigo.Tareas,
        ["op-mantenimiento"] = ModuloCodigo.Mantenimiento,
        ["op-porteria"] = ModuloCodigo.Porteria,
        ["op-reservas"] = ModuloCodigo.Reservas,
        ["com-documentos"] = ModuloCodigo.Documentos,
        ["com-reportes"] = ModuloCodigo.Reportes,
        ["fin-servicios"] = SoloAdmin,
        ["fin-seguros"] = SoloAdmin,
        ["com-informes"] = SoloAdmin,
        ["ia-lineas"] = SoloAdmin,
        ["ia-agentes"] = SoloAdmin,
        ["ia-conversaciones"] = SoloAdmin,
        ["ia-bitacora"] = SoloAdmin,
        ["ia-automatizaciones"] = SoloAdmin,
        ["ia-lista-negra"] = SoloAdmin,
        ["ia-plantillas"] = SoloAdmin,
    };

    /// <summary>true si el usuario puede ver el item (sin mapa = siempre).</summary>
    public static bool PuedeVer(string itemKey, MisPermisosDto p)
    {
        if (p.EsAdministrador || !Mapa.TryGetValue(itemKey, out var modulo)) return true;
        return modulo != SoloAdmin && p.Puede(modulo, AccionPermiso.Ver);
    }
}
