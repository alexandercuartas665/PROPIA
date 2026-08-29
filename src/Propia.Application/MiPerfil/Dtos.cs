using Propia.Domain.Enums;

namespace Propia.Application.MiPerfil;

/// <summary>Foto y firma resueltas a URL publica.</summary>
public record MiPerfilMediaDto(string? FotoUrl, string? FirmaUrl);

/// <summary>Contacto (correo/telefono) del usuario para recibir notificaciones.</summary>
public record ContactoNotificacionDto(Guid Id, CanalNotificacion Canal, string Valor, bool Activo);

public record CrearContactoNotificacionRequest(CanalNotificacion Canal, string Valor);

public record ActualizarContactoNotificacionRequest(string? Valor = null, bool? Activo = null);

public record CambiarPasswordRequest(string CurrentPassword, string NewPassword);
