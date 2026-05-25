using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Servidor Evolution API maestro de la plataforma (Super Admin). GLOBAL y singleton.
/// Es el gateway de WhatsApp que usara el canal del Motor de Notificaciones (T.2). La API key se
/// guarda cifrada (ISecretProtector) y nunca se expone completa ni se loggea. Portado de CUBOT.travels.
/// Por ahora solo se persiste la configuracion; el consumo (ingest de mensajes) se diseña cuando
/// se active el canal WhatsApp de T.2.
/// </summary>
public class EvolutionMasterConfig : BaseEntity
{
    /// <summary>URL base del servidor Evolution API (p.ej. https://evo.cubot.com.co).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>API key global del servidor (AUTHENTICATION_API_KEY) cifrada en reposo.</summary>
    public string? ApiKeyEncrypted { get; set; }

    public EvolutionIntegrationStatus Status { get; set; } = EvolutionIntegrationStatus.NotConfigured;
    public DateTimeOffset? LastValidatedAt { get; set; }

    /// <summary>Modo del webhook: "Development" (tunel local) o "Production" (URL fija del dominio).</summary>
    public string WebhookMode { get; set; } = "Production";

    /// <summary>URL publica fija para produccion (p.ej. https://app.propia.com.co).</summary>
    public string? WebhookPublicUrl { get; set; }

    /// <summary>Token compartido que valida los webhooks entrantes de Evolution.</summary>
    public string? WebhookToken { get; set; }
}
