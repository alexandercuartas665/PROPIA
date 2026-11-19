using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Lector de buzon (Ola 1b, 2026-11-17): un buzon de correo de la copropiedad que el "Coordinador de correo" lee por
/// IMAP con contrasena de aplicacion. Una copropiedad puede tener varios (administracion, contabilidad, porteria...).
/// Cada uno lleva su propia cuenta y su propio avance por UID. Se abre en SOLO LECTURA: no se marca nada como leido.
/// Independiente del correo saliente (TenantEmailConfig).
/// </summary>
public class BuzonCorreo : TenantEntity
{
    /// <summary>Nombre visible del buzon (ej. "Administracion").</summary>
    public string Nombre { get; set; } = "";

    /// <summary>Usuario IMAP: la direccion de correo de la cuenta.</summary>
    public string Cuenta { get; set; } = "";

    /// <summary>Contrasena de aplicacion cifrada (ISecretProtector).</summary>
    public string? PasswordCifrada { get; set; }

    public string ImapHost { get; set; } = "imap.gmail.com";
    public int ImapPort { get; set; } = 993;

    /// <summary>Si el job lee este buzon.</summary>
    public bool Activo { get; set; }

    /// <summary>Solo se leen correos recibidos desde esta fecha (se fija al activar la lectura).</summary>
    public DateTimeOffset? LecturaDesde { get; set; }

    /// <summary>UIDVALIDITY de la bandeja de entrada cuando se fijo <see cref="UltimoUid"/>.</summary>
    public long? UidValidity { get; set; }

    /// <summary>Ultimo UID IMAP ya leido (el siguiente ciclo pide solo los mayores).</summary>
    public long? UltimoUid { get; set; }

    public DateTimeOffset? UltimaRevision { get; set; }

    /// <summary>Error de la ultima revision (null si fue bien).</summary>
    public string? UltimoError { get; set; }

    /// <summary>Claves de los temas (lista "correo.tema") que atiende este buzon, en JSON ["pqrsd",...]. null = todos.
    /// "otro" e "irrelevante" aplican siempre.</summary>
    public string? TemasJson { get; set; }
}
