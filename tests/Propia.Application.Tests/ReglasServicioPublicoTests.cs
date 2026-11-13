using Propia.Application.Documentos;
using Propia.Application.ServiciosPublicos;
using Propia.Domain.Enums;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>Reglas de Servicios publicos (2.17): referencia normalizada, topes con los textos de la replica,
/// proximo pago, periodo del recibo y cuadre del desglose.</summary>
public class ReglasServicioPublicoTests
{
    // ---------- Referencia del contrato ----------
    [Theory]
    [InlineData("1029384756", "1029384756")]
    [InlineData("3345-221", "3345221")]
    [InlineData(" 9001.234 5 ", "90012345")]
    [InlineData("ac-55812", "AC55812")]
    [InlineData("NIC 1.234-567", "NIC1234567")]
    public void Referencia_sin_espacios_puntos_ni_guiones(string entrada, string esperado)
        => Assert.Equal(esperado, ReglasServicioPublico.NormalizarReferencia(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" - . ")]
    public void Referencia_vacia_es_null(string? entrada) => Assert.Null(ReglasServicioPublico.NormalizarReferencia(entrada));

    [Fact]
    public void Referencias_escritas_distinto_coinciden()
        => Assert.Equal(ReglasServicioPublico.NormalizarReferencia("9001234-5"), ReglasServicioPublico.NormalizarReferencia("9001.234 5"));

    // ---------- Montos, fechas y periodo de un recibo colombiano ----------
    [Theory]
    [InlineData("$ 5.297.300", 5297300)]
    [InlineData("1.953.000,00", 1953000)]
    [InlineData("$1,866,200", 1866200)]
    public void Montos_del_recibo(string entrada, decimal esperado) => Assert.Equal(esperado, NormalizadorDocumental.Monto(entrada));

    [Fact]
    public void Fecha_limite_en_formato_colombiano()
        => Assert.Equal(new DateOnly(2026, 12, 15), NormalizadorDocumental.Fecha("15/12/2026"));

    [Fact]
    public void Periodo_que_cruza_dos_meses_va_al_mes_de_periodo_hasta()
        => Assert.Equal((2026, 11), ReglasServicioPublico.Periodo(new DateOnly(2026, 10, 17), new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 20)));

    [Fact]
    public void Periodo_sin_hasta_usa_desde_y_luego_expedicion()
    {
        Assert.Equal((2026, 10), ReglasServicioPublico.Periodo(new DateOnly(2026, 10, 1), null, null));
        Assert.Equal((2026, 9), ReglasServicioPublico.Periodo(null, null, new DateOnly(2026, 9, 30)));
        Assert.Null(ReglasServicioPublico.Periodo(null, null, null));
    }

    // ---------- Cuadre del desglose ----------
    [Fact]
    public void Desglose_que_suma_el_total_cuadra()
        => Assert.True(ReglasServicioPublico.CuadraDesglose(new[] { 820300m, 156200m, 742100m, 136700m, 97700m }, 1953000m));

    [Fact]
    public void Desglose_dentro_del_uno_por_ciento_cuadra()
        => Assert.True(ReglasServicioPublico.CuadraDesglose(new[] { 990000m }, 1000000m));

    [Fact]
    public void Desglose_fuera_del_uno_por_ciento_no_cuadra()
        => Assert.False(ReglasServicioPublico.CuadraDesglose(new[] { 4131900m, 317800m }, 5297300m));

    [Fact]
    public void Sin_conceptos_o_sin_total_no_marca_dudoso()
    {
        Assert.True(ReglasServicioPublico.CuadraDesglose(Array.Empty<decimal>(), 100m));
        Assert.True(ReglasServicioPublico.CuadraDesglose(new[] { 10m }, null));
    }

    // ---------- Topes ----------
    private static readonly TopesServicio Energia = new(3_200_000m, 4_200_000m, 3600m, 4800m, "kWh");

    [Fact]
    public void En_rango()
    {
        var ev = ReglasServicioPublico.Evaluar(Energia, 3_900_000m, 4200m);
        Assert.Equal(RangoRecibo.Ok, ev.Estado);
        Assert.Empty(ev.Motivos);
        Assert.False(ev.Fuera);
    }

    [Fact]
    public void Sobre_el_tope_con_los_textos_de_la_replica()
    {
        var ev = ReglasServicioPublico.Evaluar(Energia, 5_297_300m, 5952m);
        Assert.Equal(RangoRecibo.Alto, ev.Estado);
        Assert.Equal(new[]
        {
            "El valor $5.297.300 supera el máximo de $4.200.000 (+26.1%).",
            "El consumo 5.952 kWh supera el tope de 4.800 kWh (+24%).",
        }, ev.Motivos);
        Assert.False(ev.Critica);   // +26% < 50%
    }

    [Fact]
    public void Sobre_el_tope_en_50_por_ciento_o_mas_es_critica()
        => Assert.True(ReglasServicioPublico.Evaluar(Energia, 6_300_000m, null).Critica);

    [Fact]
    public void Bajo_el_minimo()
    {
        var ev = ReglasServicioPublico.Evaluar(Energia, 3_000_000m, 3500m);
        Assert.Equal(RangoRecibo.Bajo, ev.Estado);
        Assert.Equal("El valor $3.000.000 está por debajo del mínimo de $3.200.000 (-6.3%).", ev.Motivos[0]);
        Assert.Equal("El consumo 3.500 kWh está por debajo del mínimo de 3.600 kWh.", ev.Motivos[1]);
    }

    [Fact]
    public void Alto_manda_sobre_bajo()
        => Assert.Equal(RangoRecibo.Alto, ReglasServicioPublico.Evaluar(Energia, 4_500_000m, 3000m).Estado);

    [Fact]
    public void Sin_topes_y_sin_recibo()
    {
        Assert.Equal(RangoRecibo.Sin, ReglasServicioPublico.Evaluar(new TopesServicio(null, null, null, null, null), 100m, 1m).Estado);
        Assert.Equal(RangoRecibo.Nada, ReglasServicioPublico.Evaluar(Energia, null, null).Estado);
    }

    [Fact]
    public void Consumo_cero_no_se_compara_con_los_topes_de_consumo()
        => Assert.Equal(RangoRecibo.Ok, ReglasServicioPublico.Evaluar(Energia, 4_000_000m, 0m).Estado);

    // ---------- Proximo pago ----------
    [Fact]
    public void Proximo_pago_es_el_dia_de_pago_del_periodo_siguiente_al_ultimo_recibo()
        => Assert.Equal(new DateOnly(2026, 11, 15),
            ReglasServicioPublico.ProximoPago(15, PeriodicidadServicio.Mensual, (2026, 10), new DateOnly(2026, 11, 2)));

    [Fact]
    public void Proximo_pago_avanza_segun_la_periodicidad_hasta_no_quedar_en_el_pasado()
        => Assert.Equal(new DateOnly(2027, 1, 25),
            ReglasServicioPublico.ProximoPago(25, PeriodicidadServicio.Bimestral, (2026, 8), new DateOnly(2026, 11, 26)));

    [Fact]
    public void Dia_31_en_mes_corto_usa_el_ultimo_dia()
        => Assert.Equal(new DateOnly(2027, 2, 28),
            ReglasServicioPublico.ProximoPago(31, PeriodicidadServicio.Mensual, (2027, 1), new DateOnly(2027, 2, 1)));

    [Fact]
    public void Sin_recibos_cuenta_desde_el_mes_actual_y_sin_dia_no_hay_proximo()
    {
        Assert.Equal(new DateOnly(2026, 11, 20), ReglasServicioPublico.ProximoPago(20, PeriodicidadServicio.Mensual, null, new DateOnly(2026, 11, 11)));
        Assert.Null(ReglasServicioPublico.ProximoPago(null, PeriodicidadServicio.Mensual, null, new DateOnly(2026, 11, 11)));
    }

    // ---------- Tipo de servicio leido del recibo ----------
    [Theory]
    [InlineData("Energía eléctrica", TipoServicioPublico.EnergiaElectrica)]
    [InlineData("ACUEDUCTO Y ALCANTARILLADO", TipoServicioPublico.Acueducto)]
    [InlineData("gas natural", TipoServicioPublico.Gas)]
    [InlineData("Aseo", TipoServicioPublico.Aseo)]
    [InlineData("Internet fibra", TipoServicioPublico.Internet)]
    [InlineData("Telefonía", TipoServicioPublico.TelefoniaFija)]
    public void Tipo_desde_el_texto_del_recibo(string texto, TipoServicioPublico esperado)
        => Assert.Equal(esperado, ReglasServicioPublico.TipoDesdeTexto(texto));

    // ---------- Consumo efectivo y coherencia del recibo con el servicio (ola B) ----------
    [Theory]
    [InlineData(120.5, null, null, 120.5)]       // trae consumo: se respeta
    [InlineData(null, 1000d, 1180d, 180d)]          // sin consumo: lectura actual - anterior
    [InlineData(0d, 1000d, 1180d, 180d)]             // consumo 0 pero lecturas validas
    [InlineData(null, 1180d, 1000d, null)]         // lecturas invertidas: no se inventa
    [InlineData(0d, null, null, 0d)]               // cargo fijo sin lecturas
    public void Consumo_efectivo_usa_las_lecturas_si_falta(double? consumo, double? ant, double? act, double? esperado)
        => Assert.Equal(esperado is null ? null : (decimal)esperado.Value,
            ReglasServicioPublico.ConsumoEfectivo(consumo is null ? null : (decimal)consumo.Value, ant is null ? null : (decimal)ant.Value, act is null ? null : (decimal)act.Value));

    [Theory]
    [InlineData("M3", "m3")]
    [InlineData("m³", "m3")]
    [InlineData("metros cúbicos", "m3")]
    [InlineData("Kw/h", "kwh")]
    [InlineData("kWh", "kwh")]
    [InlineData("toneladas", null)]
    public void Unidad_normalizada(string u, string? esperado) => Assert.Equal(esperado, ReglasServicioPublico.UnidadNormalizada(u));

    [Fact]
    public void Avisa_si_el_recibo_es_de_otro_tipo_de_servicio_o_mide_en_otra_unidad()
    {
        // El caso real: el recibo de aseo de EMCALI (contrato 133611) cayo en el servicio de energia con el mismo contrato.
        var tipo = ReglasServicioPublico.MotivosCoherencia(TipoServicioPublico.EnergiaElectrica, "Energia", "kWh", "aseo", "M3");
        Assert.Equal("El recibo parece de aseo, pero \"Energia\" es un servicio de energía: revisa que sea el servicio correcto antes de confirmar.", Assert.Single(tipo));

        var unidad = ReglasServicioPublico.MotivosCoherencia(TipoServicioPublico.Acueducto, "Agua", "m3", "acueducto", "kWh");
        Assert.Contains("mide el consumo en kWh", Assert.Single(unidad));

        Assert.Empty(ReglasServicioPublico.MotivosCoherencia(TipoServicioPublico.EnergiaElectrica, "Energia", "kWh", "Energía eléctrica", "KWH"));
        Assert.Empty(ReglasServicioPublico.MotivosCoherencia(TipoServicioPublico.Internet, "Internet", null, null, null));
    }

    [Theory]
    [InlineData(0.19, "0,19")]
    [InlineData(1234.5, "1.234,5")]
    [InlineData(180, "180")]
    public void Cantidad_con_decimales(double v, string esperado) => Assert.Equal(esperado, ReglasServicioPublico.Cant((decimal)v));
}
