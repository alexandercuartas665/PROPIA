namespace Propia.Domain.Enums;

/// <summary>Ambiente de operacion de la pasarela Wompi maestra (Super Admin). Portado de CUBOT.travels.</summary>
public enum WompiEnvironment
{
    Sandbox = 0,
    Production = 1
}

/// <summary>Estado de la integracion con la pasarela Wompi maestra.</summary>
public enum WompiIntegrationStatus
{
    NotConfigured = 0,
    Configured = 1,
    Validated = 2,
    Error = 3
}

/// <summary>Resultado del procesamiento de un evento de webhook de Wompi.</summary>
public enum WebhookProcessingStatus
{
    Received = 0,
    Processed = 1,
    NoMatchingPayment = 2,
    InvalidSignature = 3,
    Duplicate = 4,
    Error = 5
}
