using Microsoft.AspNetCore.Identity;

namespace Propia.Domain.Entities;

/// <summary>
/// Usuario de la plataforma (login + credenciales). Entidad GLOBAL.
/// El ApplicationUser representa LA CREDENCIAL de acceso. La identidad humana
/// vive en Persona (modulo 2.4 Directorio). Una Persona puede tener UNA
/// ApplicationUser (login) o cero (si solo es contacto sin acceso).
///
/// El vinculo Usuario -> Tenant lo gestiona UsuarioTenant (modulo 2.5).
/// Una misma ApplicationUser puede tener N UsuarioTenant (multi-copropiedad
/// tipo Slack).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Persona del Directorio asociada a este login (si existe).
    /// Puede ser null para usuarios temporales o invitados antes de completar perfil.
    /// </summary>
    public Guid? PersonaId { get; set; }
    public Persona? Persona { get; set; }

    /// <summary>
    /// Preferencia de tema visual de la UI: "light" o "dark". NULL = no se ha definido,
    /// se respeta la preferencia del sistema operativo (prefers-color-scheme). Spec 2026-05.
    /// </summary>
    public string? UiTheme { get; set; }

    /// <summary>
    /// Secciones de Mi Copropiedad (2.3) colapsadas por el usuario, como CSV de claves
    /// (ej. "srv,zon,eq"). Persiste el estado expandido/colapsado por usuario (RN-24).
    /// </summary>
    public string? MiCopropiedadSeccionesColapsadas { get; set; }

    /// <summary>
    /// Agente preferido del usuario para el asistente "Ask AI" embebido, POR copropiedad.
    /// JSON map { "tenantId": "agentId" }. Permite que cada usuario elija su agente y que
    /// la eleccion no se cruce entre copropiedades (multi-PH). Null = sin preferencia.
    /// </summary>
    public string? AskAiAgentesPorTenant { get; set; }
}
