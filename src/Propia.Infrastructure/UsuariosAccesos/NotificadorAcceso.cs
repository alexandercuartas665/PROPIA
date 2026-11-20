using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.UsuariosAccesos;

/// <summary>
/// Envia la invitacion y el acceso con clave temporal al destinatario. Correo por IEmailSender; WhatsApp
/// por la plantilla de nombre fijo de la copropiedad activa (PlantillasAcceso), desde la linea a la que
/// pertenece esa plantilla (Meta Cloud / YCloud exigen plantilla aprobada para iniciar conversacion; en
/// lineas Evolution sale el cuerpo como texto). Requiere el tenant fijado (EF + RLS) por el llamador.
/// Best-effort: nunca lanza por un envio; devuelve el estado por canal.
/// </summary>
public sealed class NotificadorAcceso : INotificadorAcceso
{
    private readonly PropiaDbContext _db;
    private readonly IEmailSender _email;
    private readonly IWhatsAppConnectorService _whatsapp;
    private readonly ILogger<NotificadorAcceso> _log;

    public NotificadorAcceso(PropiaDbContext db, IEmailSender email, IWhatsAppConnectorService whatsapp, ILogger<NotificadorAcceso> log)
    {
        _db = db;
        _email = email;
        _whatsapp = whatsapp;
        _log = log;
    }

    public async Task<EnvioCanalesDto> EnviarInvitacionAsync(InvitacionMensaje m, CanalEnvioInvitacion canal, CancellationToken ct)
    {
        var (correo, detCorreo) = PideCorreo(canal)
            ? await EnviarCorreoAsync(m.Email, $"Invitación a {m.Copropiedad} en PROPIA", HtmlInvitacion(m), ct)
            : (EstadoEnvioCanal.Omitido, "No se pidió este canal.");
        var (wa, detWa) = PideWhatsapp(canal)
            ? await EnviarWhatsappAsync(m.Telefono, PlantillasAcceso.Invitacion,
                new[] { m.Nombre, m.Copropiedad, m.LinkAceptacion },
                $"Hola {m.Nombre}, te invitaron a {m.Copropiedad} en PROPIA. Crea tu cuenta aquí (vence en 72 horas): {m.LinkAceptacion}", ct)
            : (EstadoEnvioCanal.Omitido, "No se pidió este canal.");
        return new EnvioCanalesDto(correo, wa, detCorreo, detWa);
    }

    public async Task<EnvioCanalesDto> EnviarAccesoTemporalAsync(AccesoTemporalMensaje m, CanalEnvioInvitacion canal, CancellationToken ct)
    {
        var (correo, detCorreo) = PideCorreo(canal)
            ? await EnviarCorreoAsync(m.Email, $"Tu acceso a PROPIA - {m.Copropiedad}", HtmlAcceso(m), ct)
            : (EstadoEnvioCanal.Omitido, "No se pidió este canal.");
        var (wa, detWa) = PideWhatsapp(canal)
            ? await EnviarWhatsappAsync(m.Telefono, PlantillasAcceso.Acceso,
                new[] { m.Nombre, m.Copropiedad, m.Email, m.ClaveTemporal, m.LinkIngreso },
                $"Hola {m.Nombre}, tu acceso a {m.Copropiedad} en PROPIA está listo. Usuario: {m.Email}. Contraseña temporal: {m.ClaveTemporal}. "
                + $"Ingresa en {m.LinkIngreso}; al entrar deberás cambiarla.", ct)
            : (EstadoEnvioCanal.Omitido, "No se pidió este canal.");
        return new EnvioCanalesDto(correo, wa, detCorreo, detWa);
    }

    public async Task<CanalesEntregables> CanalesAccesoAsync(string? email, string? telefono, CanalEnvioInvitacion canal, CancellationToken ct)
    {
        bool correo = false, wa = false;
        string? motCorreo = null, motWa = null;
        if (PideCorreo(canal))
        {
            correo = !string.IsNullOrWhiteSpace(email);
            if (!correo) motCorreo = "La persona no tiene correo.";
        }
        if (PideWhatsapp(canal))
        {
            if (string.IsNullOrWhiteSpace(Telefono(telefono))) motWa = "La persona no tiene teléfono.";
            else if (await PlantillaAsync(PlantillasAcceso.Acceso, ct) is null)
                motWa = $"La copropiedad no tiene la plantilla de WhatsApp '{PlantillasAcceso.Acceso}' aprobada.";
            else wa = true;
        }
        return new CanalesEntregables(correo, wa, motCorreo, motWa);
    }

    // ===================== Envio por canal =====================

    private static bool PideCorreo(CanalEnvioInvitacion c) => c is CanalEnvioInvitacion.Email or CanalEnvioInvitacion.Ambos;
    private static bool PideWhatsapp(CanalEnvioInvitacion c) => c is CanalEnvioInvitacion.Whatsapp or CanalEnvioInvitacion.Ambos;

    private async Task<(string Estado, string? Detalle)> EnviarCorreoAsync(string? para, string asunto, string html, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(para)) return (EstadoEnvioCanal.Omitido, "La persona no tiene correo.");
        try
        {
            var r = await _email.SendAsync(para.Trim(), asunto, html, ct);
            return r.Success ? (EstadoEnvioCanal.Enviado, null) : (EstadoEnvioCanal.Fallo, r.Error ?? "El servidor de correo rechazó el envío.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Envio de correo de acceso fallo");
            return (EstadoEnvioCanal.Fallo, "No se pudo enviar el correo.");
        }
    }

    private async Task<(string Estado, string? Detalle)> EnviarWhatsappAsync(
        string? telefono, string plantilla, IReadOnlyList<string> variables, string textoAlterno, CancellationToken ct)
    {
        var numero = Telefono(telefono);
        if (string.IsNullOrWhiteSpace(numero)) return (EstadoEnvioCanal.Omitido, "La persona no tiene teléfono.");
        var t = await PlantillaAsync(plantilla, ct);
        if (t is null)
            return (EstadoEnvioCanal.Omitido, $"La copropiedad no tiene la plantilla de WhatsApp '{plantilla}' aprobada.");
        try
        {
            var r = await _whatsapp.SendTemplateAsync(t.WhatsAppLineId, numero, t.Name, t.Language, variables, textoAlterno, ct);
            return r.Ok ? (EstadoEnvioCanal.Enviado, null) : (EstadoEnvioCanal.Fallo, r.Error ?? "La línea de WhatsApp rechazó el envío.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Envio de WhatsApp de acceso fallo");
            return (EstadoEnvioCanal.Fallo, "No se pudo enviar el WhatsApp.");
        }
    }

    /// <summary>Plantilla aprobada y activa con ese nombre en la copropiedad activa (prefiere idioma "es").</summary>
    private async Task<PlantillaWhatsApp?> PlantillaAsync(string nombre, CancellationToken ct)
    {
        var candidatas = await _db.PlantillasWhatsApp.AsNoTracking()
            .Where(p => p.Name == nombre && p.Activa && p.Estado == PlantillaWaEstado.Approved)
            .ToListAsync(ct);
        return candidatas.FirstOrDefault(p => p.Language.StartsWith("es", StringComparison.OrdinalIgnoreCase))
            ?? candidatas.FirstOrDefault();
    }

    /// <summary>Solo digitos; un celular colombiano de 10 digitos (3xx) se completa con el indicativo 57.</summary>
    private static string Telefono(string? raw)
    {
        var d = new string((raw ?? "").Where(char.IsDigit).ToArray());
        return d.Length == 10 && d[0] == '3' ? "57" + d : d;
    }

    // ===================== Plantillas de correo =====================

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Marco(string cuerpo) => $@"<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;color:#1f2937"">{cuerpo}
  <p style=""font-size:12px;color:#6b7280;margin-top:28px"">Este mensaje lo envió PROPIA. Si no esperabas este correo, puedes ignorarlo.</p>
</div>";

    private static string Boton(string link, string texto)
        => $@"<p style=""margin:28px 0""><a href=""{E(link)}"" style=""background:#6D4FE3;color:#ffffff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600"">{E(texto)}</a></p>
  <p style=""font-size:12px;color:#6b7280"">Si el botón no funciona, copia y pega este enlace en tu navegador:<br>{E(link)}</p>";

    private static string HtmlInvitacion(InvitacionMensaje m) => Marco($@"
  <h2 style=""color:#111827"">Hola {E(m.Nombre)},</h2>
  <p>Te invitaron a <strong>{E(m.Copropiedad)}</strong> en PROPIA con el rol <strong>{E(m.Rol)}</strong>.</p>
  <p>Haz clic en el botón para crear tu contraseña y entrar. El enlace vence en 72 horas.</p>
  {Boton(m.LinkAceptacion, "Aceptar invitación")}");

    private static string HtmlAcceso(AccesoTemporalMensaje m) => Marco($@"
  <h2 style=""color:#111827"">Hola {E(m.Nombre)},</h2>
  <p>Tu acceso a <strong>{E(m.Copropiedad)}</strong> en PROPIA está listo.</p>
  <table style=""border-collapse:collapse;margin:16px 0"">
    <tr><td style=""padding:6px 12px 6px 0;color:#6b7280"">Usuario</td><td style=""padding:6px 0""><strong>{E(m.Email)}</strong></td></tr>
    <tr><td style=""padding:6px 12px 6px 0;color:#6b7280"">Contraseña temporal</td><td style=""padding:6px 0;font-family:Consolas,monospace;font-size:15px""><strong>{E(m.ClaveTemporal)}</strong></td></tr>
  </table>
  <p>Al ingresar, el sistema te pedirá cambiarla por una propia (mínimo 10 caracteres, con al menos una mayúscula y un número).</p>
  {Boton(m.LinkIngreso, "Ingresar a PROPIA")}");
}
