using Propia.Application.Documentos;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>Normalizador del Agente Documental: NIT, montos, fechas y si/no en formatos colombianos.</summary>
public class NormalizadorDocumentalTests
{
    [Theory]
    [InlineData("900.456.789-1", "900456789-1")]
    [InlineData("900456789 - 1", "900456789-1")]
    [InlineData("NIT 830.053.105-3", "830053105-3")]
    [InlineData("N.I.T.: 800,123,456-7", "800123456-7")]
    [InlineData("C.C. 43.100.200", "43100200")]
    [InlineData("CC 1.020.304.050", "1020304050")]
    [InlineData(" 9004567891 ", "9004567891")]
    [InlineData("C.E. 456 789", "456789")]
    public void Nit_sin_puntos_ni_espacios_con_dv_separado(string entrada, string esperado)
        => Assert.Equal(esperado, NormalizadorDocumental.Nit(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no aparece")]
    public void Nit_sin_digitos_es_null(string? entrada) => Assert.Null(NormalizadorDocumental.Nit(entrada));

    [Theory]
    [InlineData("$ 36.000.000,00", "36000000")]
    [InlineData("36.000.000", "36000000")]
    [InlineData("36,000,000.00", "36000000")]
    [InlineData("$36.000.000,50", "36000000.50")]
    [InlineData("1.500", "1500")]
    [InlineData("COP 2.450.000", "2450000")]
    [InlineData("36000000", "36000000")]
    [InlineData("36000000.5", "36000000.5")]
    [InlineData("150,25", "150.25")]
    [InlineData("$ 1.200.000 pesos m/cte", "1200000")]
    public void Monto_formato_colombiano(string entrada, string esperado)
        => Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), NormalizadorDocumental.Monto(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin valor")]
    public void Monto_invalido_es_null(string? entrada) => Assert.Null(NormalizadorDocumental.Monto(entrada));

    [Theory]
    [InlineData("2026-01-01", 2026, 1, 1)]
    [InlineData("01/02/2026", 2026, 2, 1)]
    [InlineData("1/2/2026", 2026, 2, 1)]
    [InlineData("15-03-2025", 2025, 3, 15)]
    [InlineData("1 de enero de 2026", 2026, 1, 1)]
    [InlineData("1° de enero del 2026", 2026, 1, 1)]
    [InlineData("Enero 15 de 2026", 2026, 1, 15)]
    [InlineData("a los quince (15) días del mes de enero de dos mil veintiséis (2026)", 2026, 1, 15)]
    [InlineData("31 de diciembre de 2026", 2026, 12, 31)]
    [InlineData("primero de septiembre de 2025", 2025, 9, 1)]
    [InlineData("10 de setiembre de 2025", 2025, 9, 10)]
    [InlineData("2026-01-01T00:00:00", 2026, 1, 1)]
    public void Fecha_formatos_colombianos(string entrada, int a, int m, int d)
        => Assert.Equal(new DateOnly(a, m, d), NormalizadorDocumental.Fecha(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("indefinida")]
    [InlineData("enero")]
    public void Fecha_invalida_es_null(string? entrada) => Assert.Null(NormalizadorDocumental.Fecha(entrada));

    [Theory]
    [InlineData("Sí", true)]
    [InlineData("si", true)]
    [InlineData("X", true)]
    [InlineData("true", true)]
    [InlineData("No", false)]
    [InlineData("false", false)]
    public void SiNo(string entrada, bool esperado) => Assert.Equal(esperado, NormalizadorDocumental.SiNo(entrada));

    [Fact]
    public void SiNo_desconocido_es_null() => Assert.Null(NormalizadorDocumental.SiNo("tal vez"));
}
