using System.Globalization;
using Propia.Domain.Enums;

namespace Propia.Application.ServiciosPublicos;

/// <summary>Resultado de comparar un recibo con los topes de su servicio.</summary>
public enum RangoRecibo { Nada, Sin, Ok, Alto, Bajo }

/// <summary>Topes de un servicio (todos opcionales) y su unidad de medida para los textos.</summary>
public sealed record TopesServicio(decimal? ValorMin, decimal? ValorMax, decimal? ConsumoMin, decimal? ConsumoMax, string? Unidad)
{
    public bool SinTopes => ValorMin is null && ValorMax is null && ConsumoMin is null && ConsumoMax is null;
}

/// <summary>Evaluacion de un recibo: estado, motivos (textos de la replica) y si amerita alerta critica.</summary>
public sealed record EvaluacionRango(RangoRecibo Estado, IReadOnlyList<string> Motivos, bool Critica)
{
    public bool Fuera => Estado is RangoRecibo.Alto or RangoRecibo.Bajo;
}

/// <summary>
/// Reglas del modulo 2.17 Servicios publicos (replica js/mod-servicios-publicos.js): referencia normalizada,
/// evaluacion de topes con sus motivos, proximo pago y cuadre del desglose. Puras (sin BD) para que las usen el
/// servidor, la pagina y las pruebas con el MISMO resultado.
/// </summary>
public static class ReglasServicioPublico
{
    private static readonly CultureInfo Es = new("es-CO");
    private static readonly string[] Mes3 = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

    /// <summary>Severidad critica cuando el recibo supera el maximo en este porcentaje o mas.</summary>
    public const decimal ExcesoCriticoPct = 50m;
    /// <summary>Tolerancia del cuadre del desglose contra el total (1%).</summary>
    public const decimal ToleranciaCuadre = 0.01m;

    /// <summary>Referencia para comparar: sin espacios, puntos ni guiones, en mayusculas. Vacia -> null.</summary>
    public static string? NormalizarReferencia(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var s = new string(v.Where(c => !char.IsWhiteSpace(c) && c is not '.' and not '-' and not '_').ToArray()).ToUpperInvariant();
        return s.Length == 0 ? null : s;
    }

    public static int Meses(PeriodicidadServicio p) => (int)p;

    public static string Periodicidad(PeriodicidadServicio p) => p switch
    {
        PeriodicidadServicio.Bimestral => "Bimestral",
        PeriodicidadServicio.Trimestral => "Trimestral",
        PeriodicidadServicio.Semestral => "Semestral",
        PeriodicidadServicio.Anual => "Anual",
        _ => "Mensual"
    };

    /// <summary>"oct 2026".</summary>
    public static string PeriodoLabel(int anio, int mes) => $"{(mes is >= 1 and <= 12 ? Mes3[mes - 1] : mes.ToString())} {anio}";

    /// <summary>"15 nov 2026" (como fCorta de la replica).</summary>
    public static string FechaCorta(DateOnly? d) => d is { } x ? $"{x.Day:00} {Mes3[x.Month - 1]} {x.Year}" : "-";

    /// <summary>"$5.083.700" (como plata de la replica); null -> "-".</summary>
    public static string Plata(decimal? v) => v is { } x ? "$" + Math.Round(x, MidpointRounding.AwayFromZero).ToString("#,##0", Es) : "-";

    /// <summary>"5.952" (como num de la replica); null -> "-".</summary>
    public static string Num(decimal? v) => v is { } x ? Math.Round(x, MidpointRounding.AwayFromZero).ToString("#,##0", Es) : "-";

    // Diferencia porcentual con un decimal, como pct() de la replica (JS: 26.1 / 24).
    private static string Pct(decimal a, decimal b)
        => b == 0 ? "0" : (Math.Round(Math.Abs(a - b) / b * 1000m, MidpointRounding.AwayFromZero) / 10m).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// Compara un recibo con los topes. Sin recibo -> Nada; sin topes -> Sin. El consumo solo cuenta si es mayor
    /// que cero (servicios de cargo fijo). Critica = supera un maximo (valor o consumo) en 50% o mas.
    /// </summary>
    public static EvaluacionRango Evaluar(TopesServicio t, decimal? valor, decimal? consumo)
    {
        if (valor is null) return new(RangoRecibo.Nada, Array.Empty<string>(), false);
        if (t.SinTopes) return new(RangoRecibo.Sin, Array.Empty<string>(), false);
        var alto = new List<string>(); var bajo = new List<string>(); var u = t.Unidad ?? "";
        var critica = false;
        var v = valor.Value;
        if (t.ValorMax is { } vmax && v > vmax)
        {
            alto.Add($"El valor {Plata(v)} supera el máximo de {Plata(vmax)} (+{Pct(v, vmax)}%).");
            critica |= vmax > 0 && (v - vmax) / vmax * 100m >= ExcesoCriticoPct;
        }
        if (t.ValorMin is { } vmin && v < vmin)
            bajo.Add($"El valor {Plata(v)} está por debajo del mínimo de {Plata(vmin)} (-{Pct(v, vmin)}%).");
        if (consumo is { } c && c != 0)
        {
            if (t.ConsumoMax is { } cmax && c > cmax)
            {
                alto.Add($"El consumo {Num(c)} {u} supera el tope de {Num(cmax)} {u} (+{Pct(c, cmax)}%).");
                critica |= cmax > 0 && (c - cmax) / cmax * 100m >= ExcesoCriticoPct;
            }
            if (t.ConsumoMin is { } cmin && c < cmin)
                bajo.Add($"El consumo {Num(c)} {u} está por debajo del mínimo de {Num(cmin)} {u}.");
        }
        var estado = alto.Count > 0 ? RangoRecibo.Alto : bajo.Count > 0 ? RangoRecibo.Bajo : RangoRecibo.Ok;
        return new(estado, alto.Concat(bajo).ToList(), critica);
    }

    /// <summary>
    /// Proximo pago: el dia de pago del periodo siguiente al ultimo recibo (o del mes actual si no hay recibos),
    /// avanzando segun la periodicidad hasta no quedar en el pasado. Si el mes no tiene ese dia, el ultimo del mes.
    /// </summary>
    public static DateOnly? ProximoPago(int? diaPago, PeriodicidadServicio periodicidad, (int Anio, int Mes)? ultimo, DateOnly hoy)
    {
        if (diaPago is not { } dia || dia < 1) return null;
        var baseMes = ultimo is { } u ? new DateOnly(u.Anio, u.Mes, 1).AddMonths(1) : new DateOnly(hoy.Year, hoy.Month, 1);
        static DateOnly EnMes(DateOnly primero, int dia) => new(primero.Year, primero.Month, Math.Min(dia, DateTime.DaysInMonth(primero.Year, primero.Month)));
        var paso = Math.Max(1, Meses(periodicidad));
        var d = EnMes(baseMes, dia);
        for (var guard = 0; d < hoy && guard < 400; guard++) { baseMes = baseMes.AddMonths(paso); d = EnMes(baseMes, dia); }
        return d;
    }

    /// <summary>True si la suma de los conceptos cuadra con el total (+-1%). Sin conceptos o sin total -> true.</summary>
    public static bool CuadraDesglose(IEnumerable<decimal> conceptos, decimal? total)
    {
        var lista = conceptos.ToList();
        if (lista.Count == 0 || total is not { } t || t == 0) return true;
        return Math.Abs(lista.Sum() - t) <= Math.Abs(t) * ToleranciaCuadre;
    }

    /// <summary>Periodo de un recibo: el mes de periodo_hasta (un periodo que cruza dos meses va al segundo);
    /// si no hay, el de periodo_desde y por ultimo el de la fecha de expedicion.</summary>
    public static (int Anio, int Mes)? Periodo(DateOnly? desde, DateOnly? hasta, DateOnly? expedicion)
        => (hasta ?? desde ?? expedicion) is { } d ? (d.Year, d.Month) : null;

    /// <summary>Unidad por defecto de cada tipo (la de la replica).</summary>
    public static string? UnidadPorDefecto(TipoServicioPublico t) => t switch
    {
        TipoServicioPublico.EnergiaElectrica => "kWh",
        TipoServicioPublico.Acueducto or TipoServicioPublico.Gas => "m3",
        TipoServicioPublico.Internet => "GB",
        TipoServicioPublico.TelefoniaFija => "min",
        _ => null
    };

    /// <summary>Tipo de servicio a partir de lo que dice el recibo ("energia", "acueducto y alcantarillado"...).</summary>
    public static TipoServicioPublico? TipoDesdeTexto(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var s = new string(v.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        if (s.Contains("energ") || s.Contains("luz") || s.Contains("electr")) return TipoServicioPublico.EnergiaElectrica;
        if (s.Contains("acued") || s.Contains("agua") || s.Contains("alcant")) return TipoServicioPublico.Acueducto;
        if (s.Contains("gas")) return TipoServicioPublico.Gas;
        if (s.Contains("aseo") || s.Contains("basura") || s.Contains("recole")) return TipoServicioPublico.Aseo;
        if (s.Contains("internet") || s.Contains("banda") || s.Contains("fibra") || s.Contains("datos")) return TipoServicioPublico.Internet;
        if (s.Contains("telef") || s.Contains("voz")) return TipoServicioPublico.TelefoniaFija;
        return null;
    }
}
