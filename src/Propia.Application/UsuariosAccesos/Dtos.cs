using Propia.Domain.Enums;

namespace Propia.Application.UsuariosAccesos;

// ====================== Roles + Permisos ======================

public record RolDto(
    Guid Id, Guid? TenantId, string Nombre, string? Descripcion,
    TipoRol Tipo, bool EsEliminable, bool Activo,
    int CantidadUsuariosActivos,
    string? FacetasSemilla = null, bool SoloDirectorio = false);

public record RolDetalleDto(
    Guid Id, Guid? TenantId, string Nombre, string? Descripcion,
    TipoRol Tipo, bool EsEliminable, bool Activo,
    int CantidadUsuariosActivos,
    IReadOnlyList<PermisoMatrizDto> Permisos,
    string? FacetasSemilla = null, bool SoloDirectorio = false);

public record PermisoMatrizDto(
    string ModuloCodigo, AccionPermiso Accion, bool Habilitado, NivelDato NivelDato);

public record CrearRolRequest(
    string Nombre, string? Descripcion, Guid? CopiarDeRolId);

public record ActualizarRolRequest(
    string Nombre, string? Descripcion, bool Activo,
    string? FacetasSemilla = null, bool SoloDirectorio = false);

public record ActualizarPermisoRequest(
    string ModuloCodigo, AccionPermiso Accion, bool Habilitado, NivelDato NivelDato);

// ====================== Usuarios (vista 2.5) ======================

public record UsuarioListaDto(
    Guid UsuarioTenantId,
    Guid PersonaId, string NombreCompleto, string Documento,
    string? Email, string? Telefono,
    Guid? RolId, string RolNombre,
    EstadoUsuarioTenant Estado,
    DateTimeOffset? UltimoAcceso,
    DateTimeOffset? FechaInvitacion,
    bool TieneCuentaPropia,   // si Persona tiene ApplicationUser asociado
    string? FotoUrl,          // avatar de la Persona (resuelto a URL publica actual)
    bool EnDirectorio,        // si la Persona ya tiene vinculo activo en el Directorio del tenant (2.5.E)
    IReadOnlyList<EtiquetaUsuarioDto> Etiquetas,
    IReadOnlyList<string> GruposGobierno);  // "Consejo", "Comite: X", "Revisor fiscal", "Equipo: Rol"

public record RegistroDirectorioDto(bool YaEstaba, bool PerfilIncompleto);

public record UsuarioDetalleDto(
    Guid UsuarioTenantId,
    Guid PersonaId, string NombreCompleto, string Documento,
    string? Email, string? Telefono,
    Guid? RolId, string RolNombre,
    EstadoUsuarioTenant Estado,
    DateTimeOffset? UltimoAcceso,
    DateTimeOffset? FechaInvitacion,
    DateTimeOffset? FechaActivacion,
    DateTimeOffset? FechaRevocacion,
    string? MotivoRevocacion,
    bool TieneCuentaPropia,
    IReadOnlyList<UsuarioSesionDto> SesionesActivas,
    IReadOnlyList<AuthMetodoDto> MetodosAutenticacion);

public record UsuarioSesionDto(
    Guid Id, string? Dispositivo, string? IpOrigen,
    TipoAuthMetodo CanalAuth, DateTimeOffset UltimoUsoAt, DateTimeOffset ExpiraAt);

public record AuthMetodoDto(TipoAuthMetodo Tipo, bool Activo);

public record CambiarRolUsuarioRequest(Guid RolId);

public record RevocarAccesoRequest(string? Motivo);

// ====================== Etiquetas de usuario (2.5) ======================

public record EtiquetaUsuarioDto(Guid Id, string Nombre, string? Color, bool Activo);

public record CrearEtiquetaUsuarioRequest(string Nombre, string? Color);

public record ActualizarEtiquetaUsuarioRequest(string Nombre, string? Color, bool Activo);

// ====================== Invitaciones ======================

public record InvitacionDto(
    Guid Id, Guid PersonaId, string PersonaNombre, string PersonaDocumento,
    Guid RolId, string RolNombre,
    // S-02b: Token/LinkAceptacion pueden venir null cuando el destinatario es miembro de otro tenant
    // (no se expone el link al invitador; el token viaja solo al destinatario por correo/WhatsApp).
    string? Token, EstadoInvitacion Estado,
    DateTimeOffset ExpiraAt, CanalEnvioInvitacion CanalEnvio,
    string? LinkAceptacion);

public record CrearInvitacionRequest(
    Guid PersonaId, Guid RolId, CanalEnvioInvitacion Canal);

// Invitar a un externo (por email) a colaborar en un tablero de Tareas.
// Si la persona no existe en el Directorio se crea al vuelo con documento placeholder.
public record InvitarExternoTableroRequest(
    string Email, string Nombre, Guid RolId, Guid TableroId);

// Datos para vista publica de aceptar invitacion (sin auth)
public record InvitacionPublicaDto(
    string CopropiedadNombre,
    string PersonaNombre,
    string PersonaEmail,
    string RolNombre,
    bool EstaPendiente,
    bool ExpiroToken);

public record AceptarInvitacionRequest(
    string Token, string Password, string ConfirmPassword);

public record AceptarInvitacionResponse(
    bool Aceptada, string Email, string? AccessToken,
    Guid TenantId, string CopropiedadNombre, string Mensaje);

// ====================== Auditoria ======================

public record AuditoriaEntradaDto(
    Guid Id, DateTimeOffset CreatedAt,
    TipoEventoAuditoria TipoEvento,
    Guid? ActorUsuarioId, string? ActorNombre,
    Guid? EntidadAfectadaId,
    string? IpOrigen, string? Dispositivo,
    string? Detalle);
