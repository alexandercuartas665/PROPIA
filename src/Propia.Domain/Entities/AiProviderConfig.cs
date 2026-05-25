using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Cuenta maestra de un proveedor de IA de la plataforma (Super Admin). GLOBAL: un registro por
/// proveedor (Claude, Gemini, ChatGpt, DeepSeek). La API key se guarda cifrada (ISecretProtector)
/// y nunca se expone completa ni se loggea. Portado de CUBOT.travels. Los consumidores (modulo 0.4
/// IA Asistente / T.1) se construiran cuando PROPIA implemente sus agentes; aqui solo la config.
/// </summary>
public class AiProviderConfig : BaseEntity
{
    public AiProvider Provider { get; set; }

    /// <summary>API key del proveedor cifrada en reposo.</summary>
    public string? ApiKeyEncrypted { get; set; }

    /// <summary>Modelo por defecto del proveedor (ej. claude-opus-4-7, gpt-4o, gemini-2.5-pro).</summary>
    public string? Model { get; set; }

    /// <summary>URL base opcional (para gateways/compatibilidad o self-hosting).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Si esta habilitado para uso de la plataforma.</summary>
    public bool IsEnabled { get; set; }
}
