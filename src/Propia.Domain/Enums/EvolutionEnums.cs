namespace Propia.Domain.Enums;

/// <summary>Estado de la integracion con el servidor Evolution API (WhatsApp). Portado de CUBOT.travels.</summary>
public enum EvolutionIntegrationStatus
{
    NotConfigured = 0,
    Configured = 1,
    Validated = 2,
    Error = 3
}
