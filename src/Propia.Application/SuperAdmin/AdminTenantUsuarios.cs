using Propia.Application.Directorio;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Enums;

namespace Propia.Application.SuperAdmin;

/// <summary>
/// Gestion de usuarios de CUALQUIER copropiedad desde la API del Super Admin (JWT de Super Admin o token
/// pak_). Solo fija el tenant de la ruta (EF + RLS) y delega en IUsuariosService / IRolesService /
/// IDirectorioService: no duplica la logica de alta, invitacion ni roles. Nunca recibe ni devuelve
/// contrasenas: el usuario entra por el link de invitacion.
/// </summary>
public interface IAdminTenantUsuariosService
{
    Task<IReadOnlyList<UsuarioListaDto>> ListarUsuariosAsync(Guid tenantId, EstadoUsuarioTenant? estado, string? q, CancellationToken ct);
    Task<UsuarioDetalleDto> ObtenerUsuarioAsync(Guid tenantId, Guid usuarioTenantId, CancellationToken ct);
    Task<IReadOnlyList<AdminRolTenantDto>> ListarRolesAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<PersonaResumenDto>> BuscarPersonasAsync(Guid tenantId, string? q, CancellationToken ct);
    Task<AdminCrearUsuarioResponse> CrearUsuarioAsync(Guid tenantId, AdminCrearUsuarioRequest req, AdminActor actor, CancellationToken ct);
    Task ActualizarRolesAsync(Guid tenantId, Guid usuarioTenantId, ActualizarRolesUsuarioRequest req, AdminActor actor, CancellationToken ct);
    Task ActualizarCargosAsync(Guid tenantId, Guid usuarioTenantId, ActualizarCargosUsuarioRequest req, AdminActor actor, CancellationToken ct);
    Task RevocarAsync(Guid tenantId, Guid usuarioTenantId, RevocarAccesoRequest req, AdminActor actor, CancellationToken ct);
    Task ReactivarAsync(Guid tenantId, Guid usuarioTenantId, AdminActor actor, CancellationToken ct);
    Task ReenviarInvitacionAsync(Guid tenantId, Guid invitacionId, AdminActor actor, CancellationToken ct);
    Task CancelarInvitacionAsync(Guid tenantId, Guid invitacionId, AdminActor actor, CancellationToken ct);
}

/// <summary>Super Admin dueno del JWT o del token pak_ (claims user_id/email), para auditar.</summary>
public sealed record AdminActor(Guid Id, string Email, string? Ip, string? UserAgent);

/// <summary>Rol disponible en la copropiedad (para mandar RolIds en el alta).</summary>
public sealed record AdminRolTenantDto(Guid Id, string Nombre, TipoRol Tipo, bool Activo, bool SoloDirectorio);

/// <summary>Datos de una persona nueva. Si ya existe (por documento y luego por email) se reutiliza.</summary>
public sealed record AdminPersonaNuevaRequest(
    string? Nombres, string? Apellidos, TipoDocumento TipoDocumento, string? Documento,
    string? Email, string? Telefono);

/// <summary>Alta completa en un llamado: persona (existente o nueva) + usuario + invitacion opcional.</summary>
public sealed record AdminCrearUsuarioRequest(
    Guid? PersonaId,
    AdminPersonaNuevaRequest? Persona,
    IReadOnlyList<Guid>? RolIds,
    IReadOnlyList<string>? Cargos,
    bool Invitar,
    CanalEnvioInvitacion Canal = CanalEnvioInvitacion.Email);

public sealed record AdminInvitacionCreadaDto(Guid Id, DateTimeOffset ExpiraAt, CanalEnvioInvitacion Canal, string? LinkAceptacion);

public sealed record AdminCrearUsuarioResponse(
    Guid UsuarioTenantId, Guid PersonaId, bool PersonaReutilizada, EstadoUsuarioTenant Estado,
    AdminInvitacionCreadaDto? Invitacion);

/// <summary>Recurso inexistente o de OTRO tenant (tenant, usuario, persona, rol, invitacion): el controller da 404.</summary>
public sealed class AdminRecursoNoEncontradoException : Exception
{
    public AdminRecursoNoEncontradoException(string mensaje) : base(mensaje) { }
}
