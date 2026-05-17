namespace Propia.Application.Common;

/// <summary>
/// Servicio cross-modulo para calculos de dias habiles en Colombia.
/// Excluye sabados, domingos y festivos nacionales (Ley 51/1983 + religiosos
/// desde Pascua).
///
/// Implementacion cachea los festivos en memoria para evitar hits a BD en cada
/// calculo. Se refresca al primer uso del servicio dentro del proceso.
/// </summary>
public interface ICalendarioHabilService
{
    /// <summary>True si la fecha es habil (no es sabado, domingo ni festivo).</summary>
    Task<bool> EsHabilAsync(DateOnly fecha, CancellationToken ct);

    /// <summary>Suma N dias habiles a la fecha indicada (no cuenta el dia origen).</summary>
    Task<DateOnly> SumarDiasHabilesAsync(DateOnly desde, int dias, CancellationToken ct);

    /// <summary>Cuenta dias habiles entre desde (exclusivo) y hasta (inclusivo).</summary>
    Task<int> ContarDiasHabilesAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    /// <summary>Invalida el cache - util tras una migracion o seed de festivos nuevos.</summary>
    void InvalidarCache();
}
