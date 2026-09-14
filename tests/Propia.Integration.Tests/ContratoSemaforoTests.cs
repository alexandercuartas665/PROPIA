using Propia.Domain.Enums;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Seguros;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// K-06: cobertura del semaforo por % de dias del contrato (MiCopropiedadService.CalcularSemaforoContrato)
/// y de la poliza (SegurosService.SemaforoPoliza). Son metodos estaticos puros: no necesitan BD. Fija los
/// limites (20% -> Amarillo, 10% o vencido -> Rojo; sin fecha fin -> Ninguno; fin <= inicio -> Rojo) para
/// que un cambio en la formula rompa aqui.
/// </summary>
public class ContratoSemaforoTests
{
    private static readonly DateOnly Hoy = new(2026, 6, 1);

    [Theory]
    // total 100 dias; el % es dias_restantes / total.
    [InlineData(-79, 21, SemaforoContrato.Verde)]     // 21% restante -> verde (> 20%)
    [InlineData(-80, 20, SemaforoContrato.Amarillo)]  // 20% -> amarillo (<= 20%)
    [InlineData(-89, 11, SemaforoContrato.Amarillo)]  // 11% -> amarillo
    [InlineData(-90, 10, SemaforoContrato.Rojo)]      // 10% -> rojo (<= 10%)
    [InlineData(-100, 0, SemaforoContrato.Rojo)]      // vence hoy -> 0% -> rojo
    public void Semaforo_contrato_por_porcentaje(int offInicio, int offFin, SemaforoContrato esperado)
    {
        var s = MiCopropiedadService.CalcularSemaforoContrato(Hoy.AddDays(offInicio), Hoy.AddDays(offFin), Hoy);
        Assert.Equal(esperado, s);
    }

    [Fact]
    public void Semaforo_contrato_sin_fecha_fin_es_Ninguno()
        => Assert.Equal(SemaforoContrato.Ninguno, MiCopropiedadService.CalcularSemaforoContrato(Hoy.AddDays(-10), null, Hoy));

    [Fact]
    public void Semaforo_contrato_vencido_es_Rojo()
        => Assert.Equal(SemaforoContrato.Rojo, MiCopropiedadService.CalcularSemaforoContrato(Hoy.AddDays(-30), Hoy.AddDays(-1), Hoy));

    [Fact]
    public void Semaforo_contrato_con_fin_igual_al_inicio_es_Rojo()
        => Assert.Equal(SemaforoContrato.Rojo, MiCopropiedadService.CalcularSemaforoContrato(Hoy, Hoy, Hoy));

    // ----------------------------- Poliza -----------------------------

    [Fact]
    public void Semaforo_poliza_sin_fecha_fin_es_Ninguno()
        => Assert.Equal(SemaforoContrato.Ninguno, SegurosService.SemaforoPoliza(new DateOnly(2026, 1, 1), null, Hoy));

    [Theory]
    // Sin fecha de inicio se asume vigencia anual (inicio = fin - 1 anio), total ~365.
    [InlineData(30, SemaforoContrato.Rojo)]       // 30/365 ~ 8% -> rojo
    [InlineData(100, SemaforoContrato.Verde)]     // 100/365 ~ 27% -> verde
    [InlineData(-1, SemaforoContrato.Rojo)]       // vencida -> rojo
    public void Semaforo_poliza_sin_inicio_asume_vigencia_anual(int offFin, SemaforoContrato esperado)
        => Assert.Equal(esperado, SegurosService.SemaforoPoliza(null, Hoy.AddDays(offFin), Hoy));

    [Fact]
    public void Semaforo_poliza_con_inicio_usa_el_porcentaje_real()
        // inicio hoy-80, fin hoy+20 -> total 100, restante 20 -> 20% -> amarillo.
        => Assert.Equal(SemaforoContrato.Amarillo, SegurosService.SemaforoPoliza(Hoy.AddDays(-80), Hoy.AddDays(20), Hoy));
}
