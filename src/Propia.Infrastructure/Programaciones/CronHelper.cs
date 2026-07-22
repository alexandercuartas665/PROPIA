using Cronos;

namespace Propia.Infrastructure.Programaciones;

/// <summary>
/// Envoltura sobre Cronos para las programaciones tipo Cron. Centraliza el parseo, la
/// validacion y el calculo de la proxima ejecucion, para que el job y el servicio de CRUD
/// usen exactamente la misma interpretacion (misma zona horaria, mismo formato de 5 campos).
///
/// Formato: "min hora dia-del-mes mes dia-de-semana" (5 campos, sin segundos).
/// Ej: "0 8 * * 1" = todos los lunes a las 8:00 en la zona horaria de la programacion.
/// </summary>
public static class CronHelper
{
    public const string ZonaHorariaPorDefecto = "America/Bogota";

    /// <summary>Parsea la expresion. Devuelve null si no es valida (no lanza).</summary>
    public static CronExpression? TryParse(string? expresion)
    {
        if (string.IsNullOrWhiteSpace(expresion)) return null;
        try { return CronExpression.Parse(expresion.Trim(), CronFormat.Standard); }
        catch (CronFormatException) { return null; }
    }

    public static bool EsValida(string? expresion) => TryParse(expresion) is not null;

    /// <summary>
    /// Resuelve la zona horaria IANA. Cae a America/Bogota si el id no existe en el host,
    /// para que un dato malo no tumbe el job entero.
    /// </summary>
    public static TimeZoneInfo Zona(string? zonaHoraria)
    {
        var id = string.IsNullOrWhiteSpace(zonaHoraria) ? ZonaHorariaPorDefecto : zonaHoraria.Trim();
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { }
        catch (InvalidTimeZoneException) { }
        try { return TimeZoneInfo.FindSystemTimeZoneById(ZonaHorariaPorDefecto); }
        catch { return TimeZoneInfo.Utc; }
    }

    /// <summary>
    /// Proxima ocurrencia estrictamente posterior a <paramref name="desdeUtc"/>.
    /// Null si la expresion no es valida o si ya no vuelve a ocurrir nunca.
    ///
    /// Siempre se devuelve en UTC: Cronos entrega el instante con el offset de la zona
    /// (-05:00 en Colombia) y Npgsql rechaza escribir un timestamptz que no tenga offset 0.
    /// Normalizar aqui evita repetir el ToUniversalTime en cada llamador.
    /// </summary>
    public static DateTimeOffset? ProximaEjecucion(string? expresion, string? zonaHoraria, DateTimeOffset desdeUtc)
    {
        var cron = TryParse(expresion);
        if (cron is null) return null;
        return cron.GetNextOccurrence(desdeUtc, Zona(zonaHoraria), inclusive: false)?.ToUniversalTime();
    }

    /// <summary>Las siguientes N ocurrencias, para previsualizar la regla en la UI.</summary>
    public static IReadOnlyList<DateTimeOffset> Proximas(string? expresion, string? zonaHoraria, DateTimeOffset desdeUtc, int cuantas)
    {
        var cron = TryParse(expresion);
        if (cron is null || cuantas <= 0) return Array.Empty<DateTimeOffset>();

        var zona = Zona(zonaHoraria);
        var resultado = new List<DateTimeOffset>(cuantas);
        var cursor = desdeUtc;
        for (var i = 0; i < cuantas; i++)
        {
            var siguiente = cron.GetNextOccurrence(cursor, zona, inclusive: false)?.ToUniversalTime();
            if (siguiente is null) break;
            resultado.Add(siguiente.Value);
            cursor = siguiente.Value;
        }
        return resultado;
    }
}
