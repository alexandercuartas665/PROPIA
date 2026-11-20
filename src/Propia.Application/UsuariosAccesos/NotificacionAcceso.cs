using Propia.Domain.Enums;

namespace Propia.Application.UsuariosAccesos;

/// <summary>
/// Entrega al DESTINATARIO de lo que le permite entrar a PROPIA: la invitacion (link de aceptacion) y el
/// acceso con contrasena temporal. Correo por IEmailSender y WhatsApp por una plantilla de nombre fijo de
/// la copropiedad (ver <see cref="PlantillasAcceso"/>). Best-effort: nunca lanza por un envio fallido; el
/// resultado dice por canal si salio, fallo o se omitio (y por que).
/// </summary>
public interface INotificadorAcceso
{
    Task<EnvioCanalesDto> EnviarInvitacionAsync(InvitacionMensaje m, CanalEnvioInvitacion canal, CancellationToken ct);

    Task<EnvioCanalesDto> EnviarAccesoTemporalAsync(AccesoTemporalMensaje m, CanalEnvioInvitacion canal, CancellationToken ct);

    /// <summary>
    /// Canales que SI se pueden intentar para el acceso temporal (sin enviar nada): correo si el canal lo
    /// pide y hay correo; WhatsApp si el canal lo pide, hay telefono y la copropiedad (tenant activo) tiene
    /// la plantilla de acceso aprobada. Sirve para fallar ANTES de crear cuentas o cambiar claves.
    /// </summary>
    Task<CanalesEntregables> CanalesAccesoAsync(string? email, string? telefono, CanalEnvioInvitacion canal, CancellationToken ct);
}

/// <summary>Plantillas de WhatsApp de nombre fijo (las crea y aprueba cada copropiedad en Plantillas de WhatsApp).</summary>
public static class PlantillasAcceso
{
    /// <summary>Variables: {{1}} nombre, {{2}} copropiedad, {{3}} link de aceptacion.</summary>
    public const string Invitacion = "propia_invitacion";

    /// <summary>Variables: {{1}} nombre, {{2}} copropiedad, {{3}} usuario (correo), {{4}} contrasena temporal, {{5}} link de ingreso.</summary>
    public const string Acceso = "propia_acceso";
}

public static class EstadoEnvioCanal
{
    public const string Enviado = "enviado";
    public const string Fallo = "fallo";
    public const string Omitido = "omitido";
}

/// <summary>Resultado del envio por canal ("enviado" | "fallo" | "omitido") con el motivo si no salio.</summary>
public sealed record EnvioCanalesDto(string Correo, string Whatsapp, string? DetalleCorreo = null, string? DetalleWhatsapp = null)
{
    public static EnvioCanalesDto SinNotificador { get; } = new(EstadoEnvioCanal.Omitido, EstadoEnvioCanal.Omitido,
        "Envio no configurado.", "Envio no configurado.");
}

public sealed record CanalesEntregables(bool Correo, bool Whatsapp, string? MotivoCorreo, string? MotivoWhatsapp)
{
    public bool Alguno => Correo || Whatsapp;
}

public sealed record InvitacionMensaje(
    string Nombre, string? Email, string? Telefono, string Copropiedad, string Rol, string LinkAceptacion);

public sealed record AccesoTemporalMensaje(
    string Nombre, string Email, string? Telefono, string Copropiedad, string ClaveTemporal, string LinkIngreso);

/// <summary>
/// Resultado de dejar la cuenta lista con clave temporal. NUNCA lleva la clave: dice por donde se entrego y
/// a que destino (enmascarado). Modo "temporal" (clave fijada) o "enlace" (cuenta en otra copropiedad o de
/// A&amp;D: solo se le envio el enlace de restablecimiento a su correo).
/// </summary>
public sealed record AccesoEntregadoDto(
    string Modo,
    IReadOnlyList<string> EntregadoPor,
    string? Destino,
    IReadOnlyList<string> Fallidos,
    string? Motivo,
    bool CuentaCreada);
