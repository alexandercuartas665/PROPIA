using Propia.Domain.Enums;

namespace Propia.Application.UsuariosAccesos;

/// <summary>
/// Servicio del modulo 2.5 Usuarios, Roles y Accesos (spec v1.0).
///
/// Gestiona la bandeja de usuarios de la copropiedad activa (tabs Activos / Pendientes / Inactivos),
/// los flujos de invitacion + aceptacion, el cambio de rol, la revocacion y la consulta de
/// sesiones + metodos de autenticacion. Toda accion administrativa registra en acceso_auditorias.
/// </summary>
public interface IUsuariosService
{
    // --- Bandeja ---
    Task<IReadOnlyList<UsuarioListaDto>> ListarUsuariosAsync(EstadoUsuarioTenant? estado, string? query, CancellationToken ct);
    Task<UsuarioDetalleDto?> GetUsuarioDetalleAsync(Guid usuarioTenantId, CancellationToken ct);

    // --- Invitacion (envio + aceptacion publica) ---
    Task<InvitacionDto> InvitarAsync(CrearInvitacionRequest req, CancellationToken ct);
    Task<bool> ReenviarInvitacionAsync(Guid invitacionId, CancellationToken ct);
    Task<bool> CancelarInvitacionAsync(Guid invitacionId, CancellationToken ct);
    Task<IReadOnlyList<InvitacionDto>> ListarPendientesAsync(CancellationToken ct);

    // Publico (sin tenant en JWT - usa token)
    Task<InvitacionPublicaDto?> ConsultarInvitacionAsync(string token, CancellationToken ct);
    Task<AceptarInvitacionResponse> AceptarInvitacionAsync(AceptarInvitacionRequest req, CancellationToken ct);

    // --- Gestion de acceso ---
    Task<bool> CambiarRolAsync(Guid usuarioTenantId, CambiarRolUsuarioRequest req, CancellationToken ct);
    Task<bool> RevocarAccesoAsync(Guid usuarioTenantId, RevocarAccesoRequest req, CancellationToken ct);
    Task<bool> ReactivarUsuarioAsync(Guid usuarioTenantId, CancellationToken ct);

    // --- Auditoria ---
    Task<IReadOnlyList<AuditoriaEntradaDto>> ListarAuditoriaAsync(int limit, CancellationToken ct);

    // --- Etiquetas de usuario (2.5) ---
    Task<IReadOnlyList<EtiquetaUsuarioDto>> ListarEtiquetasAsync(CancellationToken ct);
    Task<EtiquetaUsuarioDto> CrearEtiquetaAsync(CrearEtiquetaUsuarioRequest req, CancellationToken ct);
    Task<bool> ActualizarEtiquetaAsync(Guid etiquetaId, ActualizarEtiquetaUsuarioRequest req, CancellationToken ct);
    Task<bool> EliminarEtiquetaAsync(Guid etiquetaId, CancellationToken ct);
    Task<bool> AsignarEtiquetaAsync(Guid usuarioTenantId, Guid etiquetaId, CancellationToken ct);
    Task<bool> QuitarEtiquetaAsync(Guid usuarioTenantId, Guid etiquetaId, CancellationToken ct);

    // Foto de perfil (2.5.C) -> actualiza Persona.FotoUrl; devuelve la URL publica resuelta o null si no existe el usuario.
    Task<string?> SubirFotoAsync(Guid usuarioTenantId, System.IO.Stream content, string contentType, string ext, CancellationToken ct);

    // Registrar en Directorio (2.5.E) -> crea el vinculo de la Persona con el tenant. null si no existe el usuario.
    Task<RegistroDirectorioDto?> RegistrarEnDirectorioAsync(Guid usuarioTenantId, CancellationToken ct);
}

/// <summary>Servicio de gestion de roles y permisos (configurable por copropiedad).</summary>
public interface IRolesService
{
    Task<IReadOnlyList<RolDto>> ListarRolesAsync(CancellationToken ct);
    Task<RolDetalleDto?> GetRolDetalleAsync(Guid rolId, CancellationToken ct);
    Task<RolDto> CrearRolAsync(CrearRolRequest req, CancellationToken ct);
    Task<bool> ActualizarRolAsync(Guid rolId, ActualizarRolRequest req, CancellationToken ct);
    Task<bool> EliminarRolAsync(Guid rolId, CancellationToken ct);
    Task<bool> ActualizarPermisoAsync(Guid rolId, ActualizarPermisoRequest req, CancellationToken ct);
    Task<bool> ActivarRolExtendidoAsync(Guid rolId, CancellationToken ct);

    /// <summary>Evaluacion de permisos efectivos para una persona/copropiedad. Usado por modulos para autorizar.</summary>
    Task<IReadOnlyList<PermisoMatrizDto>> GetPermisosEfectivosAsync(Guid personaId, CancellationToken ct);

    /// <summary>
    /// Rol (string) del actor en la copropiedad ACTIVA (tenant del JWT). Devuelve null si no hay
    /// vinculo activo. Usado por el enforcement de autorizacion por rol (P0) mientras la matriz
    /// granular rol_permisos no este sembrada.
    /// </summary>
    Task<string?> GetRolActorAsync(Guid personaId, CancellationToken ct);

    /// <summary>
    /// Idempotente: crea los roles base (globales) con su matriz por defecto si no existen y
    /// linkea los usuarios de la copropiedad activa a su rol base por nombre. Habilita que el
    /// Administrador edite la matriz en Usuarios y Accesos > Roles y permisos y que el enforcement use permisos.
    /// </summary>
    Task EnsureRolesBaseAsync(CancellationToken ct);
}
