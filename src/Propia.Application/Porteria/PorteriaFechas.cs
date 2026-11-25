namespace Propia.Application.Porteria;

/// <summary>
/// "Hoy" de Porteria = dia calendario de COLOMBIA (auditoria UX G-01, 2026-11-25). Antes los KPIs cortaban el dia en
/// UTC y la minuta / "Adentro ahora" pedian la fecha con el reloj del servidor web y el API la cortaba en UTC: un
/// ingreso a las 19:00-23:59 hora Colombia (ya es el dia siguiente en UTC) salia en los KPIs pero no en la minuta.
/// Colombia es UTC-5 sin horario de verano: offset fijo, portable (mismo criterio que Propia.Web.FechaColombia).
/// </summary>
public static class PorteriaFechas
{
    public static readonly TimeSpan OffsetColombia = TimeSpan.FromHours(-5);

    /// <summary>Ventana de "Adentro ahora": ingresos sin salida de las ultimas N horas (no se pierden a medianoche).</summary>
    public const int HorasAdentro = 24;

    public static DateOnly DiaColombia(DateTimeOffset instante) => DateOnly.FromDateTime(instante.ToOffset(OffsetColombia).DateTime);

    public static DateOnly HoyColombia() => DiaColombia(DateTimeOffset.UtcNow);

    /// <summary>Rango UTC [Desde, Hasta) del dia calendario de Colombia.</summary>
    public static (DateTimeOffset Desde, DateTimeOffset Hasta) RangoDia(DateOnly dia)
    {
        var ini = new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), OffsetColombia);
        return (ini.ToUniversalTime(), ini.AddDays(1).ToUniversalTime());
    }

    /// <summary>Clave de persona para emparejar ingreso y salida (nombre + documento, sin mayusculas).</summary>
    public static string ClaveVisitante(string? nombre, string? documento)
        => ((nombre ?? "").Trim() + "|" + (documento ?? "").Trim()).ToLowerInvariant();

    /// <summary>
    /// Personas ADENTRO: recorre los eventos en orden de tiempo; un ingreso las agrega y una salida las quita.
    /// Fuente UNICA del calculo para el contador del panel y las tarjetas "Adentro ahora".
    /// </summary>
    public static IReadOnlyList<T> Adentro<T>(IEnumerable<T> eventos, Func<T, string> clave, Func<T, bool> esIngreso, Func<T, DateTimeOffset> momento)
    {
        var dentro = new Dictionary<string, T>();
        foreach (var e in eventos.OrderBy(momento))
        {
            if (esIngreso(e)) dentro[clave(e)] = e;
            else dentro.Remove(clave(e));
        }
        return dentro.Values.OrderByDescending(momento).ToList();
    }
}
