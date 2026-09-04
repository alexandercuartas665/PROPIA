namespace Propia.Infrastructure.Onboarding;

/// <summary>
/// Plantillas HTML para los correos del wizard 2.1 (Onboarding y Activacion).
/// Estilo simple inline (sin Razor) para que el SMTP no dependa del render server-side.
/// </summary>
internal static class OnboardingEmailTemplates
{
    /// <summary>Correo enviado en Paso1Registrar: bienvenida + codigo OTP de verificacion.</summary>
    public static (string Subject, string HtmlBody) BienvenidaConOtp(string nombre, string otp)
    {
        var subject = "Bienvenido a PROPIA - codigo de verificacion";
        var html = $@"<!doctype html>
<html lang=""es"">
  <body style=""margin:0;padding:0;background:#f3f1fc;font-family:Arial,Helvetica,sans-serif;color:#2d2d44"">
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background:#f3f1fc;padding:40px 0"">
      <tr><td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""480"" style=""background:#ffffff;border-radius:14px;box-shadow:0 10px 40px rgba(89,85,209,0.08);padding:36px 32px"">
          <tr><td align=""center"" style=""padding-bottom:24px"">
            <span style=""font-size:22px;font-weight:700;letter-spacing:1px;background:linear-gradient(135deg,#5955D1,#7A77E0);-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text"">PROPIA</span>
          </td></tr>
          <tr><td style=""padding-bottom:8px"">
            <h2 style=""margin:0;font-size:20px;font-weight:600;color:#2d2d44"">Bienvenido, {EscapeHtml(nombre)}</h2>
          </td></tr>
          <tr><td style=""padding-bottom:20px"">
            <p style=""margin:0;font-size:14px;line-height:1.5;color:#5b5b6e"">Tu cuenta en PROPIA quedo creada. Para continuar con la configuracion de tu copropiedad usa este codigo de verificacion:</p>
          </td></tr>
          <tr><td align=""center"" style=""padding:8px 0 24px 0"">
            <div style=""display:inline-block;font-size:28px;font-weight:700;letter-spacing:8px;padding:14px 26px;border-radius:10px;background:#f3f1fc;color:#5955D1"">{otp}</div>
          </td></tr>
          <tr><td style=""padding-bottom:8px"">
            <p style=""margin:0;font-size:13px;line-height:1.5;color:#8a8a9a"">El codigo expira en 15 minutos. Si no fuiste tu, puedes ignorar este correo.</p>
          </td></tr>
          <tr><td style=""padding-top:24px;border-top:1px solid #eef0f5;text-align:center"">
            <p style=""margin:0;font-size:11px;color:#8a8a9a"">&copy; A&amp;D GROUP S.A.S &middot; PROPIA</p>
          </td></tr>
        </table>
      </td></tr>
    </table>
  </body>
</html>";
        return (subject, html);
    }

    /// <summary>
    /// S-04b: aviso al dueno del correo cuando alguien intenta registrarse con un email que YA tiene
    /// cuenta. Se envia en lugar de revelar la existencia en la respuesta del registro.
    /// </summary>
    public static (string Subject, string HtmlBody) CuentaYaExiste(string nombre)
    {
        var subject = "Intento de registro con tu correo en PROPIA";
        var html = $@"<!doctype html>
<html lang=""es"">
  <body style=""margin:0;padding:0;background:#f3f1fc;font-family:Arial,Helvetica,sans-serif;color:#2d2d44"">
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background:#f3f1fc;padding:40px 0"">
      <tr><td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""480"" style=""background:#ffffff;border-radius:14px;box-shadow:0 10px 40px rgba(89,85,209,0.08);padding:36px 32px"">
          <tr><td align=""center"" style=""padding-bottom:24px"">
            <span style=""font-size:22px;font-weight:700;letter-spacing:1px;color:#5955D1"">PROPIA</span>
          </td></tr>
          <tr><td style=""padding-bottom:8px"">
            <h2 style=""margin:0;font-size:20px;font-weight:600;color:#2d2d44"">Hola, {EscapeHtml(nombre)}</h2>
          </td></tr>
          <tr><td style=""padding-bottom:20px"">
            <p style=""margin:0;font-size:14px;line-height:1.5;color:#5b5b6e"">Alguien intento crear una cuenta en PROPIA con este correo, pero ya tienes una. Si fuiste tu, simplemente inicia sesion; si olvidaste tu clave, puedes restablecerla desde la pantalla de ingreso. Si no fuiste tu, puedes ignorar este mensaje: no se creo ninguna cuenta nueva.</p>
          </td></tr>
          <tr><td style=""padding-top:24px;border-top:1px solid #eef0f5;text-align:center"">
            <p style=""margin:0;font-size:11px;color:#8a8a9a"">&copy; A&amp;D GROUP S.A.S &middot; PROPIA</p>
          </td></tr>
        </table>
      </td></tr>
    </table>
  </body>
</html>";
        return (subject, html);
    }

    /// <summary>Correo enviado en Paso5Activar: copropiedad activa.</summary>
    public static (string Subject, string HtmlBody) CopropiedadActivada(string nombre, string copropiedadNombre)
    {
        var subject = $"Tu copropiedad {copropiedadNombre} esta activa en PROPIA";
        var html = $@"<!doctype html>
<html lang=""es"">
  <body style=""margin:0;padding:0;background:#f3f1fc;font-family:Arial,Helvetica,sans-serif;color:#2d2d44"">
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background:#f3f1fc;padding:40px 0"">
      <tr><td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""480"" style=""background:#ffffff;border-radius:14px;box-shadow:0 10px 40px rgba(89,85,209,0.08);padding:36px 32px"">
          <tr><td align=""center"" style=""padding-bottom:24px"">
            <span style=""font-size:22px;font-weight:700;letter-spacing:1px;background:linear-gradient(135deg,#5955D1,#7A77E0);-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text"">PROPIA</span>
          </td></tr>
          <tr><td style=""padding-bottom:12px"">
            <h2 style=""margin:0;font-size:20px;font-weight:600;color:#2d2d44"">Listo, {EscapeHtml(nombre)}</h2>
          </td></tr>
          <tr><td style=""padding-bottom:20px"">
            <p style=""margin:0;font-size:14px;line-height:1.5;color:#5b5b6e"">Tu copropiedad <strong>{EscapeHtml(copropiedadNombre)}</strong> quedo activa en PROPIA. Ya puedes entrar a configurar unidades, residentes, parametros financieros y todo lo demas.</p>
          </td></tr>
          <tr><td style=""padding-bottom:20px"">
            <ul style=""margin:0;padding-left:18px;font-size:13px;line-height:1.7;color:#5b5b6e"">
              <li>Configura tus unidades y coeficientes en Mi Copropiedad.</li>
              <li>Invita a tu equipo administrativo y a los residentes.</li>
              <li>Carga el presupuesto del ano y empieza a generar cuotas.</li>
            </ul>
          </td></tr>
          <tr><td style=""padding-top:24px;border-top:1px solid #eef0f5;text-align:center"">
            <p style=""margin:0;font-size:11px;color:#8a8a9a"">&copy; A&amp;D GROUP S.A.S &middot; PROPIA</p>
          </td></tr>
        </table>
      </td></tr>
    </table>
  </body>
</html>";
        return (subject, html);
    }

    private static string EscapeHtml(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
