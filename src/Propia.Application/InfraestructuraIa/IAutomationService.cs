using Propia.Domain.Enums;

namespace Propia.Application.InfraestructuraIa;

/// <summary>Una regla de automatizacion tal como se muestra en la UI.</summary>
public sealed record AutomationRuleDto(
    Guid Id,
    string Name,
    AutomationTrigger Trigger,
    int ThresholdMinutes,
    string? TimeWindowStart,
    string? TimeWindowEnd,
    AutomationAction Action,
    string? MensajePlantilla,
    string? TareaTitulo,
    bool IsActive,
    int SortOrder,
    int ExecutionCount,
    DateTimeOffset? LastRunAt,
    /// <summary>true si esta combinacion trigger+accion tiene ejecucion real hoy (si no, es scaffolding "proximamente").</summary>
    bool Implemented);

public sealed record SaveAutomationRuleRequest(
    string? Name,
    AutomationTrigger Trigger,
    int ThresholdMinutes,
    string? TimeWindowStart,
    string? TimeWindowEnd,
    AutomationAction Action,
    string? MensajePlantilla,
    string? TareaTitulo);

/// <summary>Resultado de una corrida del motor de automatizaciones.</summary>
public sealed record AutomationRunResult(int RulesEvaluated, int ActionsFired);

/// <summary>
/// Reglas de automatizacion event-driven de la copropiedad (Infraestructura IA). Re-mapeadas de
/// CUBOT.travels al dominio de copropiedades. CRUD + encendido + corrida manual ("Ejecutar ahora").
/// El motor real (RunNowAsync) hoy solo ejecuta ChatSinRespuesta -> NotificarAdministracion; el
/// resto es scaffolding. Un job en background llama a RunNowAsync periodicamente por tenant.
/// </summary>
public interface IAutomationService
{
    Task<IReadOnlyList<AutomationRuleDto>> ListAsync(CancellationToken ct = default);
    Task<AutomationRuleDto?> CreateAsync(SaveAutomationRuleRequest req, CancellationToken ct = default);
    Task<AutomationRuleDto?> UpdateAsync(Guid id, SaveAutomationRuleRequest req, CancellationToken ct = default);
    Task<bool> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Evalua y ejecuta las reglas activas IMPLEMENTADAS del tenant actual. Devuelve el conteo.</summary>
    Task<AutomationRunResult> RunNowAsync(CancellationToken ct = default);

    /// <summary>Si el tenant no tiene reglas, siembra ejemplos (algunos activos) para arrancar. Idempotente.</summary>
    Task<int> SeedDefaultsAsync(CancellationToken ct = default);

    /// <summary>true si la combinacion trigger+accion tiene ejecucion real hoy.</summary>
    static bool IsImplemented(AutomationTrigger trigger, AutomationAction action)
        => trigger == AutomationTrigger.ChatSinRespuesta && action == AutomationAction.NotificarAdministracion;
}
