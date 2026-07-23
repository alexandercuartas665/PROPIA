using Propia.Domain.Entities;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.Programaciones;

/// <summary>
/// Calcula QUE ocurrencias de una programacion toca materializar en una corrida del job.
/// Es una funcion pura (sin BD ni reloj propio) a proposito: la decision de cuantas tareas
/// se crean por adelantado es la parte delicada de la generacion anticipada, y asi se puede
/// probar sola. El job se limita a persistir lo que aqui se decide.
/// </summary>
public static class PlanificadorOcurrencias
{
    /// <summary>Tope duro de tareas que una sola regla puede materializar en una corrida.</summary>
    public const int MaxOcurrenciasPorCorrida = 200;

    /// <summary>Una ocurrencia: el instante que la identifica (clave de dedupe) y el dia que vence.</summary>
    public readonly record struct Ocurrencia(DateTimeOffset Instante, DateOnly Fecha);

    /// <summary>Si la programacion ya vencio segun su propio modo de disparo.</summary>
    public static bool Vencida(ProgramacionTarea p, DateTimeOffset ahora, DateOnly hoy) =>
        p.Tipo == TipoProgramacion.Cron
            ? p.ProximaEjecucionUtc.HasValue && p.ProximaEjecucionUtc <= ahora
            : p.FechaProximaEjecucion <= hoy;

    /// <summary>
    /// Ocurrencias a materializar, en orden cronologico.
    ///
    /// Con HorizonteDias = 0 devuelve a lo sumo la ocurrencia vencida (comportamiento clasico:
    /// la tarea aparece cuando llega la fecha). Con HorizonteDias mayor que 0 devuelve todas las
    /// que caen hasta hoy + horizonte, que es lo que permite ver el periodo completo en el
    /// calendario y repartir el trabajo con tiempo.
    ///
    /// Nunca genera atrasos: si el job estuvo caido, solo la PRIMERA ocurrencia puede quedar en
    /// el pasado; las demas se descartan en vez de crear el backlog de golpe.
    /// </summary>
    public static List<Ocurrencia> Calcular(ProgramacionTarea prog, DateTimeOffset ahora, DateOnly hoy)
    {
        var lista = new List<Ocurrencia>();
        var horizonte = Math.Max(0, prog.HorizonteDias);
        var hastaFecha = hoy.AddDays(horizonte);
        var zona = CronHelper.Zona(prog.ZonaHoraria);

        if (prog.Tipo == TipoProgramacion.Cron)
        {
            var desde = prog.ProximaEjecucionUtc ?? ahora;
            var hastaInstante = new DateTimeOffset(hastaFecha.ToDateTime(new TimeOnly(23, 59)), TimeSpan.Zero);
            // -1 segundo para no perder la ocurrencia que esta justo en el puntero.
            foreach (var inst in CronHelper.Proximas(prog.CronExpresion, prog.ZonaHoraria, desde.AddSeconds(-1), MaxOcurrenciasPorCorrida))
            {
                if (inst > hastaInstante) break;
                var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(inst, zona).DateTime);
                if (prog.FechaFin.HasValue && fecha > prog.FechaFin.Value) break;
                if (fecha < hoy && lista.Count > 0) continue;   // sin backlog
                lista.Add(new Ocurrencia(inst, fecha));
            }
        }
        else if (prog.Periodicidad == PeriodicidadProgramacion.Unica)
        {
            var f = prog.FechaProximaEjecucion;
            if ((!prog.FechaFin.HasValue || f <= prog.FechaFin.Value) && f <= hastaFecha)
                lista.Add(new Ocurrencia(Instante(f), f));
        }
        else
        {
            var f = prog.FechaProximaEjecucion;
            while (f <= hastaFecha && lista.Count < MaxOcurrenciasPorCorrida)
            {
                if (prog.FechaFin.HasValue && f > prog.FechaFin.Value) break;
                if (f >= hoy || lista.Count == 0) lista.Add(new Ocurrencia(Instante(f), f));   // sin backlog
                f = Avanzar(f, prog.Periodicidad);
            }
        }

        // Modo clasico: una tarea por corrida y punto. Sin este tope una regla diaria atrasada
        // devolveria la vencida MAS la de hoy, que es justo el burst que se quiere evitar.
        if (horizonte == 0 && lista.Count > 1) lista.RemoveRange(1, lista.Count - 1);

        return lista;
    }

    /// <summary>Instante canonico de una ocurrencia por dia: 00:00 UTC. Solo se usa como clave de dedupe.</summary>
    public static DateTimeOffset Instante(DateOnly f) =>
        new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public static DateOnly Avanzar(DateOnly d, PeriodicidadProgramacion p) => p switch
    {
        PeriodicidadProgramacion.Diaria => d.AddDays(1),
        PeriodicidadProgramacion.Semanal => d.AddDays(7),
        PeriodicidadProgramacion.Quincenal => d.AddDays(14),
        PeriodicidadProgramacion.Mensual => d.AddMonths(1),
        PeriodicidadProgramacion.Bimestral => d.AddMonths(2),
        PeriodicidadProgramacion.Trimestral => d.AddMonths(3),
        PeriodicidadProgramacion.Semestral => d.AddMonths(6),
        PeriodicidadProgramacion.Anual => d.AddYears(1),
        _ => d.AddDays(1)
    };
}
