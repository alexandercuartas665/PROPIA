using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Programaciones;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests de la generacion anticipada del programador de tareas. Es logica pura, sin BD:
/// decide cuantas tareas se materializan de una vez, que es justo la parte que puede
/// llenar un tablero de basura si se equivoca.
///
/// Cubre:
///  - Horizonte 0 = comportamiento clasico (una tarea, cuando llega la fecha).
///  - Horizonte 365 sobre regla trimestral = las 4 del anio de una vez.
///  - Horizonte no adelanta mas alla de FechaFin.
///  - Job caido: no se genera el backlog, solo una ocurrencia pasada.
///  - Regla no vencida y sin horizonte no produce nada.
///  - Cron con horizonte enumera todas las ocurrencias de la ventana.
/// </summary>
public class PlanificadorOcurrenciasTests
{
    private static readonly DateOnly Hoy = new(2026, 7, 22);
    private static readonly DateTimeOffset Ahora = new(2026, 7, 22, 14, 0, 0, TimeSpan.Zero);

    private static ProgramacionTarea Regla(
        PeriodicidadProgramacion periodicidad,
        DateOnly proxima,
        int horizonte = 0,
        DateOnly? fin = null) => new()
        {
            Titulo = "Mantenimiento ascensor",
            Tipo = TipoProgramacion.Periodicidad,
            Periodicidad = periodicidad,
            FechaProximaEjecucion = proxima,
            HorizonteDias = horizonte,
            FechaFin = fin,
            Activa = true
        };

    [Fact]
    public void SinHorizonte_SoloMaterializaLaVencida()
    {
        var p = Regla(PeriodicidadProgramacion.Trimestral, Hoy);

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        Assert.Single(oc);
        Assert.Equal(Hoy, oc[0].Fecha);
    }

    [Fact]
    public void SinHorizonte_ReglaFuturaNoGeneraNada()
    {
        // La proxima es en octubre: sin adelanto no hay nada que hacer hoy.
        var p = Regla(PeriodicidadProgramacion.Trimestral, new DateOnly(2026, 10, 22));

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        Assert.Empty(oc);
    }

    [Fact]
    public void HorizonteAnual_SobreTrimestral_AdelantaLasCuatroDelAnio()
    {
        var p = Regla(PeriodicidadProgramacion.Trimestral, Hoy, horizonte: 365);

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        // Jul 22, Oct 22, Ene 22, Abr 22 y Jul 22 del anio siguiente: 5 caen dentro de los 365 dias.
        Assert.Equal(5, oc.Count);
        Assert.Equal(new DateOnly(2026, 7, 22), oc[0].Fecha);
        Assert.Equal(new DateOnly(2026, 10, 22), oc[1].Fecha);
        Assert.Equal(new DateOnly(2027, 1, 22), oc[2].Fecha);
        Assert.Equal(new DateOnly(2027, 4, 22), oc[3].Fecha);
        Assert.Equal(new DateOnly(2027, 7, 22), oc[4].Fecha);
        // Cada ocurrencia trae una clave distinta: es lo que permite deduplicar entre corridas.
        Assert.Equal(oc.Count, oc.Select(x => x.Instante).Distinct().Count());
    }

    [Fact]
    public void Horizonte_NoPasaDeFechaFin()
    {
        var p = Regla(PeriodicidadProgramacion.Mensual, Hoy, horizonte: 365,
                      fin: new DateOnly(2026, 10, 1));

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        Assert.Equal(3, oc.Count);   // jul 22, ago 22, sep 22
        Assert.All(oc, o => Assert.True(o.Fecha <= new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void JobCaido_NoGeneraElBacklog()
    {
        // La regla venia diaria desde hace 3 meses y el job no corrio.
        var p = Regla(PeriodicidadProgramacion.Diaria, new DateOnly(2026, 4, 22));

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        // Una sola tarea atrasada, no noventa.
        Assert.Single(oc);
        Assert.Equal(new DateOnly(2026, 4, 22), oc[0].Fecha);
    }

    [Fact]
    public void JobCaido_ConHorizonte_UnaAtrasadaMasLasFuturas()
    {
        var p = Regla(PeriodicidadProgramacion.Mensual, new DateOnly(2026, 4, 22), horizonte: 90);

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        // Solo la primera puede quedar en el pasado; el resto son de hoy en adelante.
        Assert.Equal(new DateOnly(2026, 4, 22), oc[0].Fecha);
        Assert.All(oc.Skip(1), o => Assert.True(o.Fecha >= Hoy));
        Assert.Contains(oc, o => o.Fecha == new DateOnly(2026, 8, 22));
        Assert.Contains(oc, o => o.Fecha == new DateOnly(2026, 9, 22));
        Assert.DoesNotContain(oc, o => o.Fecha == new DateOnly(2026, 5, 22));
    }

    [Fact]
    public void Unica_SoloUnaOcurrencia()
    {
        var p = Regla(PeriodicidadProgramacion.Unica, Hoy, horizonte: 365);

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        Assert.Single(oc);
    }

    [Fact]
    public void Cron_ConHorizonte_EnumeraLaVentanaCompleta()
    {
        var p = new ProgramacionTarea
        {
            Titulo = "Revision semanal",
            Tipo = TipoProgramacion.Cron,
            CronExpresion = "0 8 * * 1",           // lunes 8:00
            ZonaHoraria = "America/Bogota",
            ProximaEjecucionUtc = Ahora,
            HorizonteDias = 30,
            Activa = true
        };

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        // ~4 lunes en 30 dias, ninguno mas alla de la ventana.
        Assert.InRange(oc.Count, 4, 5);
        Assert.All(oc, o => Assert.True(o.Fecha <= Hoy.AddDays(30)));
        Assert.Equal(oc.Count, oc.Select(x => x.Instante).Distinct().Count());
    }

    [Fact]
    public void Cron_SinHorizonte_SoloLaVencida()
    {
        var p = new ProgramacionTarea
        {
            Titulo = "Revision semanal",
            Tipo = TipoProgramacion.Cron,
            CronExpresion = "0 8 * * 1",
            ZonaHoraria = "America/Bogota",
            ProximaEjecucionUtc = Ahora,
            HorizonteDias = 0,
            Activa = true
        };

        var oc = PlanificadorOcurrencias.Calcular(p, Ahora, Hoy);

        Assert.True(oc.Count <= 1);
    }

    [Fact]
    public void Vencida_DistingueLosDosModosDeDisparo()
    {
        var porFecha = Regla(PeriodicidadProgramacion.Mensual, Hoy);
        var futura = Regla(PeriodicidadProgramacion.Mensual, Hoy.AddDays(5));

        Assert.True(PlanificadorOcurrencias.Vencida(porFecha, Ahora, Hoy));
        Assert.False(PlanificadorOcurrencias.Vencida(futura, Ahora, Hoy));
    }
}
