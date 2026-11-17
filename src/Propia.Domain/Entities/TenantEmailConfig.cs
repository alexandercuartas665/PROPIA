using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Cuenta de correo saliente PROPIA de la copropiedad (tenant), via SMTP. Empezamos por Gmail con
/// "contrasena de aplicacion" (smtp.gmail.com:587). Sirve para enviar las respuestas de PQRSD y otros
/// correos desde el correo de la copropiedad. La clave se guarda CIFRADA (ISecretProtector) y nunca se
/// expone ni se loggea. Es una alternativa al envio por Gmail OAuth (gmail_envio_conexiones).
/// </summary>
public class TenantEmailConfig : TenantEntity
{
    /// <summary>Host SMTP (por defecto smtp.gmail.com).</summary>
    public string? SmtpHost { get; set; } = "smtp.gmail.com";

    /// <summary>Puerto SMTP (587 STARTTLS por defecto; 465 SSL).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Usuario SMTP: el correo de la cuenta (ej. copropiedad@gmail.com).</summary>
    public string? SmtpUser { get; set; }

    /// <summary>Contrasena de aplicacion (Gmail) cifrada en reposo.</summary>
    public string? SmtpPasswordEncrypted { get; set; }

    /// <summary>Usar SSL/TLS al conectar.</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>Direccion remitente (From). Suele ser el mismo correo de la cuenta.</summary>
    public string? FromEmail { get; set; }

    /// <summary>Nombre visible del remitente (ej. nombre de la copropiedad).</summary>
    public string? FromName { get; set; }

    /// <summary>Si el envio por esta cuenta esta habilitado.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Ultima validacion (envio de prueba) exitosa.</summary>
    public DateTimeOffset? LastValidatedAt { get; set; }

    // ---- Lector de buzon (IMAP, misma cuenta y contrasena de aplicacion) ----

    /// <summary>Host IMAP (por defecto imap.gmail.com).</summary>
    public string? ImapHost { get; set; } = "imap.gmail.com";

    /// <summary>Puerto IMAP (993 SSL).</summary>
    public int ImapPort { get; set; } = 993;

    /// <summary>Si el job revisa el buzon de esta copropiedad.</summary>
    public bool LecturaActiva { get; set; }

    /// <summary>Solo se leen correos recibidos desde esta fecha (se fija al activar la lectura).</summary>
    public DateTimeOffset? LecturaDesde { get; set; }

    /// <summary>UIDVALIDITY de la bandeja de entrada cuando se fijo <see cref="LecturaUltimoUid"/>.</summary>
    public long? LecturaUidValidity { get; set; }

    /// <summary>Ultimo UID IMAP ya leido (el siguiente ciclo pide solo los mayores).</summary>
    public long? LecturaUltimoUid { get; set; }

    public DateTimeOffset? LecturaUltimaRevision { get; set; }

    /// <summary>Error de la ultima revision (null si fue bien).</summary>
    public string? LecturaUltimoError { get; set; }
}
