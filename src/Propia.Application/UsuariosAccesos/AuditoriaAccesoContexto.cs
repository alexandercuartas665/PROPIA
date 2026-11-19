using Propia.Domain.Enums;

namespace Propia.Application.UsuariosAccesos;

/// <summary>
/// Actor de la auditoria de usuarios (acceso_auditorias) para el request en curso. Por defecto vacio
/// (la UI de la copropiedad audita como siempre). La API de Super Admin lo llena antes de delegar en
/// IUsuariosService para que cada evento quede con el Super Admin como actor, canal Api y la marca
/// "via API" en el detalle. Scoped: vive solo en el request.
/// </summary>
public sealed class AuditoriaAccesoContexto
{
    public Guid? ActorUsuarioId { get; set; }
    public CanalAcceso? Canal { get; set; }
    /// <summary>Prefijo del detalle, ej. "[via API Super Admin founder@x]".</summary>
    public string? Marca { get; set; }
    public string? Ip { get; set; }
}
