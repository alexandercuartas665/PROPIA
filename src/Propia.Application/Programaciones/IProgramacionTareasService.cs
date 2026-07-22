namespace Propia.Application.Programaciones;

/// <summary>
/// Programador reutilizable de tareas (modulo 2.10). CRUD de programaciones que el
/// ProgramacionTareasJob materializa en tareas reales segun su periodicidad. Se invoca
/// desde distintos escenarios (vencimiento de contrato, mantenimientos, etc.).
/// </summary>
public interface IProgramacionTareasService
{
    /// <summary>Lista programaciones, opcionalmente filtradas por su origen (modulo + entidad).</summary>
    Task<IReadOnlyList<ProgramacionTareaDto>> ListarAsync(string? moduloOrigen, Guid? entidadOrigenId, CancellationToken ct);
    Task<ProgramacionTareaDto?> GetAsync(Guid id, CancellationToken ct);
    Task<ProgramacionTareaDto> CrearAsync(CrearProgramacionRequest req, Guid usuarioId, CancellationToken ct);
    Task<bool> ActualizarAsync(Guid id, ActualizarProgramacionRequest req, CancellationToken ct);
    Task<bool> ToggleActivaAsync(Guid id, bool activa, CancellationToken ct);
    Task<bool> EliminarAsync(Guid id, CancellationToken ct);

    /// <summary>Valida una expresion cron y devuelve sus proximas corridas (para previsualizar en la UI).</summary>
    CronPreviewResult PreviewCron(CronPreviewRequest req);
}
