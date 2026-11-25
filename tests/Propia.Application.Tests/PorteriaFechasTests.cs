using Propia.Application.Porteria;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>
/// Auditoria UX G-01 (2026-11-25): el "hoy" de Porteria es el dia calendario de COLOMBIA. Caso real del hallazgo:
/// "Visitante Auditoria UX" registrado a las 21:17 del 2026-11-24 hora Colombia = 2026-11-25 02:17 UTC. Con el corte
/// en UTC salia como "hoy" en los KPIs del 25 y no en la minuta del 24.
/// </summary>
public class PorteriaFechasTests
{
    private static readonly DateTimeOffset Ingreso2117Co = new(2026, 11, 25, 2, 17, 28, TimeSpan.Zero);

    [Fact]
    public void Un_ingreso_a_las_21_hora_Colombia_pertenece_al_dia_de_Colombia_no_al_de_UTC()
    {
        Assert.Equal(new DateOnly(2026, 11, 24), PorteriaFechas.DiaColombia(Ingreso2117Co));

        var (d24, h24) = PorteriaFechas.RangoDia(new DateOnly(2026, 11, 24));
        Assert.True(Ingreso2117Co >= d24 && Ingreso2117Co < h24);          // sale en la minuta del 24

        var (d25, h25) = PorteriaFechas.RangoDia(new DateOnly(2026, 11, 25));
        Assert.False(Ingreso2117Co >= d25 && Ingreso2117Co < h25);         // NO cuenta como "hoy" el 25
    }

    [Fact]
    public void El_rango_del_dia_va_de_medianoche_a_medianoche_de_Colombia_en_UTC()
    {
        var (desde, hasta) = PorteriaFechas.RangoDia(new DateOnly(2026, 11, 24));
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 5, 0, 0, TimeSpan.Zero), desde);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 5, 0, 0, TimeSpan.Zero), hasta);
        Assert.Equal(TimeSpan.Zero, desde.Offset);   // Npgsql exige offset 0 en timestamptz
        // 08:00 Colombia del 24 tambien cae en el 24.
        Assert.Equal(new DateOnly(2026, 11, 24), PorteriaFechas.DiaColombia(new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.Zero)));
    }

    private sealed record Ev(string Nombre, string? Doc, bool Ingreso, DateTimeOffset At);

    [Fact]
    public void Adentro_empareja_ingreso_y_salida_por_nombre_y_documento_sin_mayusculas()
    {
        var t = Ingreso2117Co;
        var eventos = new[]
        {
            new Ev("Ana Perez", "123", true, t),
            new Ev("ANA PEREZ ", "123", false, t.AddMinutes(30)),   // misma persona: salio
            new Ev("Luis", null, true, t.AddMinutes(5)),             // sigue adentro
            new Ev("Luis", "999", true, t.AddMinutes(6)),            // otra persona (otro documento)
        };
        var dentro = PorteriaFechas.Adentro(eventos, e => PorteriaFechas.ClaveVisitante(e.Nombre, e.Doc), e => e.Ingreso, e => e.At);
        Assert.Equal(2, dentro.Count);
        Assert.Equal("999", dentro[0].Doc);   // mas reciente primero
        Assert.DoesNotContain(dentro, e => e.Nombre.StartsWith("Ana", StringComparison.OrdinalIgnoreCase));
    }
}
