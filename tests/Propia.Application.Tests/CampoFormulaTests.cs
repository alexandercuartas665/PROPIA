using Propia.Application.MiCopropiedad;
using Propia.Domain.Enums;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>
/// Fase 2 Formula MULTI-PASO: fija (a) la RETROCOMPATIBILIDAD -las formulas viejas de 1-op {op,campos}
/// (la referencia ya guardada en Unidades) computan IDENTICO- y (b) la evaluacion multi-paso (pasos que
/// referencian pasos previos, constantes y operaciones binarias). Si el refactor rompe el computo viejo,
/// este test lo caza.
/// </summary>
public class CampoFormulaTests
{
    private static Func<string, decimal?> Valores(params (string k, decimal? v)[] pares)
    {
        var d = pares.ToDictionary(p => p.k, p => p.v);
        return k => d.TryGetValue(k, out var v) ? v : null;
    }

    // ---------- Retrocompatibilidad: formato legado {op, campos} ----------

    [Fact]
    public void Legado_Suma_computa_identico()
    {
        // Mismo caso que el test de integracion de Residentes (120 + 80 = 200).
        var json = "{\"op\":\"Suma\",\"campos\":[\"a\",\"b\"]}";
        Assert.Equal("200", CampoFormulaConfig.ComputarTexto(json, Valores(("a", 120m), ("b", 80m))));
    }

    [Fact]
    public void Legado_Promedio_Conteo_Min_Max_computan_identico()
    {
        var v = Valores(("a", 10m), ("b", 20m), ("c", 30m));
        Assert.Equal("20", CampoFormulaConfig.ComputarTexto("{\"op\":\"Promedio\",\"campos\":[\"a\",\"b\",\"c\"]}", v));
        Assert.Equal("3", CampoFormulaConfig.ComputarTexto("{\"op\":\"Conteo\",\"campos\":[\"a\",\"b\",\"c\"]}", v));
        Assert.Equal("10", CampoFormulaConfig.ComputarTexto("{\"op\":\"Minimo\",\"campos\":[\"a\",\"b\",\"c\"]}", v));
        Assert.Equal("30", CampoFormulaConfig.ComputarTexto("{\"op\":\"Maximo\",\"campos\":[\"a\",\"b\",\"c\"]}", v));
    }

    [Fact]
    public void Legado_Suma_sin_valores_es_vacio_pero_Conteo_es_cero()
    {
        var v = Valores(("a", (decimal?)null));
        Assert.Null(CampoFormulaConfig.ComputarTexto("{\"op\":\"Suma\",\"campos\":[\"a\"]}", v));
        Assert.Equal("0", CampoFormulaConfig.ComputarTexto("{\"op\":\"Conteo\",\"campos\":[\"a\"]}", v));
    }

    [Fact]
    public void Legado_se_lee_como_un_unico_paso_agregado()
    {
        var cfg = CampoFormulaConfig.Parse("{\"op\":\"Suma\",\"campos\":[\"a\",\"b\"]}");
        Assert.NotNull(cfg);
        Assert.Single(cfg!.Pasos);
        Assert.Equal(OperacionFormula.Suma, cfg.Pasos[0].Operacion);
        Assert.Equal(2, cfg.Pasos[0].Operandos.Count);
        Assert.All(cfg.Pasos[0].Operandos, o => Assert.Equal(OperandoFormula.TipoCampo, o.Tipo));
    }

    // ---------- Multi-paso ----------

    [Fact]
    public void Multipaso_resta_de_paso_previo_con_constante()
    {
        // paso0 = Suma(a,b) = 200 ; paso1 = Resta(paso0, 50) = 150 (resultado = ultimo paso).
        var cfg = new CampoFormulaConfig(new[]
        {
            new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("a"), OperandoFormula.DeCampo("b") }),
            new PasoFormula(OperacionFormula.Resta, new[] { OperandoFormula.DePaso(0), OperandoFormula.DeConstante(50m) }),
        });
        Assert.Equal("150", CampoFormulaConfig.ComputarTexto(cfg.Serializar(), Valores(("a", 120m), ("b", 80m))));
    }

    [Fact]
    public void Multipaso_division_por_cero_es_vacio()
    {
        var cfg = new CampoFormulaConfig(new[]
        {
            new PasoFormula(OperacionFormula.Division, new[] { OperandoFormula.DeCampo("a"), OperandoFormula.DeCampo("b") }),
        });
        Assert.Null(CampoFormulaConfig.ComputarTexto(cfg.Serializar(), Valores(("a", 10m), ("b", 0m))));
        Assert.Equal("5", CampoFormulaConfig.ComputarTexto(cfg.Serializar(), Valores(("a", 10m), ("b", 2m))));
    }

    [Fact]
    public void Multipaso_serializa_y_parsea_ida_y_vuelta()
    {
        var cfg = new CampoFormulaConfig(new[]
        {
            new PasoFormula(OperacionFormula.Multiplicacion, new[] { OperandoFormula.DeCampo("a"), OperandoFormula.DeConstante(3m) }),
        });
        var round = CampoFormulaConfig.Parse(cfg.Serializar());
        Assert.NotNull(round);
        Assert.Equal("21", CampoFormulaConfig.ComputarTexto(round!.Serializar(), Valores(("a", 7m))));
    }

    // ---------- Validacion ----------

    [Fact]
    public void Validar_binaria_exige_dos_operandos()
    {
        var cfg = new CampoFormulaConfig(new[]
        {
            new PasoFormula(OperacionFormula.Resta, new[] { OperandoFormula.DeCampo("a") }),
        });
        Assert.NotNull(CampoFormulaConfig.Validar(cfg, _ => TipoCampoTablero.Numero));
    }

    [Fact]
    public void Validar_rechaza_ref_a_paso_no_anterior()
    {
        var cfg = new CampoFormulaConfig(new[]
        {
            new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DePaso(0) }),   // se referencia a si mismo
        });
        Assert.NotNull(CampoFormulaConfig.Validar(cfg, _ => TipoCampoTablero.Numero));
    }

    [Fact]
    public void Validar_rechaza_fuente_no_numerica()
    {
        var cfg = new CampoFormulaConfig(new[]
        {
            new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("a") }),
        });
        Assert.NotNull(CampoFormulaConfig.Validar(cfg, _ => TipoCampoTablero.Texto));
    }

    [Fact]
    public void Validar_acepta_formula_multipaso_valida()
    {
        var cfg = new CampoFormulaConfig(new[]
        {
            new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("a"), OperandoFormula.DeCampo("b") }),
            new PasoFormula(OperacionFormula.Resta, new[] { OperandoFormula.DePaso(0), OperandoFormula.DeConstante(5m) }),
        });
        Assert.Null(CampoFormulaConfig.Validar(cfg, _ => TipoCampoTablero.Moneda));
    }
}
