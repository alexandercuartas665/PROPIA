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
    Task<AdminInvitacionCreadaDto> ReenviarInvitacionAsync(Guid tenantId, Guid invitacionId, AdminActor actor, CancellationToken ct);
    Task CancelarInvitacionAsync(Guid tenantId, Guid invitacionId, AdminActor actor, CancellationToken ct);

    /// <summary>Cuenta lista para entrar con clave temporal generada y ENTREGADA por el servidor (nunca la devuelve).</summary>
    Task<AccesoEntregadoDto> ActivarConClaveTemporalAsync(Guid tenantId, Guid usuarioTenantId, AdminActivarClaveTemporalRequest req, AdminActor actor, CancellationToken ct);

    /// <summary>Corrige nombres / correo / telefono de una persona vinculada a la copropiedad (persona GLOBAL).</summary>
    Task<AdminPersonaActualizadaDto> ActualizarPersonaAsync(Guid tenantId, Guid personaId, AdminActualizarPersonaRequest req, AdminActor actor, CancellationToken ct);
}

/// <summary>Super Admin dueno del JWT o del token pak_ (claims user_id/email), para auditar.</summary>
public sealed record AdminActor(Guid Id, string Email, string? Ip, string? UserAgent);

/// <summary>Rol disponible en la copropiedad (para mandar RolIds en el alta).</summary>
public sealed record AdminRolTenantDto(Guid Id, string Nombre, TipoRol Tipo, bool Activo, bool SoloDirectorio);

/// <summary>Datos de una persona nueva. Si ya existe (por documento y luego por email) se reutiliza.</summary>
public sealed record AdminPersonaNuevaRequest(
    string? Nombres, string? Apellidos, TipoDocumento TipoDocumento, string? Documento,
    string? Email, string? Telefono);

/// <summary>
/// Alta completa en un llamado: persona (existente o nueva) + usuario + acceso. Acceso "invitacion" (por
/// defecto: invitacion si Invitar=true) o "clave-temporal" (crea la cuenta, activa y entrega la clave por Canal).
/// </summary>
public sealed record AdminCrearUsuarioRequest(
    Guid? PersonaId,
    AdminPersonaNuevaRequest? Persona,
    IReadOnlyList<Guid>? RolIds,
    IReadOnlyList<string>? Cargos,
    bool Invitar,
    CanalEnvioInvitacion Canal = CanalEnvioInvitacion.Email,
    string? Acceso = null);

public static class AdminModoAcceso
{
    public const string Invitacion = "invitacion";
    public const string ClaveTemporal = "clave-temporal";
}

public sealed record AdminInvitacionCreadaDto(Guid Id, DateTimeOffset ExpiraAt, CanalEnvioInvitacion Canal, string? LinkAceptacion,
    EnvioCanalesDto? Envio = null);

public sealed record AdminCrearUsuarioResponse(
    Guid UsuarioTenantId, Guid PersonaId, bool PersonaReutilizada, EstadoUsuarioTenant Estado,
    AdminInvitacionCreadaDto? Invitacion,
    // Alta repetida sobre el mismo usuario de la copropiedad (200 en vez de 201).
    bool YaExistia = false,
    // Persona reutilizada cuyos datos enviados difieren de los registrados (no se sobrescriben).
    IReadOnlyList<string>? Advertencias = null,
    AccesoEntregadoDto? AccesoEntregado = null);

public sealed record AdminActivarClaveTemporalRequest(CanalEnvioInvitacion Canal = CanalEnvioInvitacion.Email);

/// <summary>MERGE: solo cambia lo que viene (null = no tocar).</summary>
public sealed record AdminActualizarPersonaRequest(string? Nombres, string? Apellidos, string? Email, string? Telefono);

public sealed record AdminPersonaActualizadaDto(
    Guid PersonaId, string Nombres, string Apellidos, string? Email, string? Telefono,
    // Persona GLOBAL: copropiedades donde tiene usuario (el cambio se ve en todas).
    int CopropiedadesConAcceso,
    bool CuentaActualizada,
    IReadOnlyList<string> Avisos);

/// <summary>Recurso inexistente o de OTRO tenant (tenant, usuario, persona, rol, invitacion): el controller da 404.</summary>
public sealed class AdminRecursoNoEncontradoException : Exception
{
    public AdminRecursoNoEncontradoException(string mensaje) : base(mensaje) { }
}

/// <summary>Conflicto de unicidad (p. ej. el correo ya es de otra persona o cuenta): el controller da 409.</summary>
public sealed class AdminConflictoException : Exception
{
    public AdminConflictoException(string mensaje) : base(mensaje) { }
}
