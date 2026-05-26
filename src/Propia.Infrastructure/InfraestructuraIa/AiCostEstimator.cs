using Propia.Domain.Enums;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Estimacion aproximada de costo en USD por consumo de IA, segun tarifas publicas por 1M de tokens
/// (entrada/salida). Es una aproximacion para el dashboard; no pretende ser exacta. Portado de CUBOT.
/// </summary>
public static class AiCostEstimator
{
    // (USD por 1M tokens de entrada, USD por 1M tokens de salida) - tarifas aproximadas.
    private static (decimal In, decimal Out) Rates(AiProvider p) => p switch
    {
        AiProvider.Claude => (3.00m, 15.00m),
        AiProvider.Gemini => (1.25m, 5.00m),
        AiProvider.ChatGpt => (2.50m, 10.00m),
        AiProvider.DeepSeek => (0.27m, 1.10m),
        _ => (1.00m, 3.00m)
    };

    public static decimal Estimate(AiProvider provider, int inputTokens, int outputTokens)
    {
        var (rin, rout) = Rates(provider);
        return Math.Round(inputTokens / 1_000_000m * rin + outputTokens / 1_000_000m * rout, 6);
    }
}
