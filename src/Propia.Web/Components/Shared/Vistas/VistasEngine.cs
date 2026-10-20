using System.Globalization;
using System.Text;

namespace Propia.Web.Components.Shared.Vistas;

// ================= Explorador "Vistas del sistema" - motor puro (sin UI) =================
// Portado de la maqueta (mi-copropiedad.html): catalogo de datasets, filtros, pivote, agregacion
// y render SVG/pivote como string. Las FILAS son Dictionary<string,object?> (fijas + dinamicas en Ola 2).

/// <summary>Una columna/dimension o campo mostrable del dataset: clave estable + etiqueta + icono opcional.</summary>
public sealed record Campo(string Key, string Label, string? Icon = null);
/// <summary>Una medida numerica: clave + etiqueta + formato (money|num|pct).</summary>
public sealed record Medida(string Key, string Label, string Fmt);
/// <summary>Definicion de un dataset (fuente). Dims para filtrar/pivotar, meas numericas, fields mostrables.</summary>
public sealed record Dataset(
    string Key, string Label, string Icon,
    IReadOnlyList<Campo> Dims, IReadOnlyList<Medida> Meas,
    string TitleField, IReadOnlyList<Campo> Fields, IReadOnlyList<string> DefFields);

/// <summary>Metrica de agregacion: type = count|sum|avg; F = clave de la medida; Fmt = money|num|pct.</summary>
public sealed record Metrica(string Type = "count", string? F = null, string? Label = null, string? Fmt = null);

/// <summary>Un filtro campo=valor (AND).</summary>
public sealed class Filtro { public string Field { get; set; } = ""; public string Value { get; set; } = ""; }

/// <summary>Definicion persistida de una vista (se serializa a JSON en VistaSistema.Definicion).</summary>
public sealed class VistaDef
{
    public string Kind { get; set; } = "cards";       // cards|table|pivot|chart
    public string DsKey { get; set; } = "residentes";
    public string Title { get; set; } = "";
    public List<Filtro> Filters { get; set; } = new();
    public string RowDim { get; set; } = "";
    public string ColDim { get; set; } = "";
    public Metrica Measure { get; set; } = new();
    public List<string> Fields { get; set; } = new();
    public string Width { get; set; } = "half";       // third|half|twothirds|full
    public string Chart { get; set; } = "bars";       // bars|lines|donut
}

public static class VistasEngine
{
    public static readonly (string Key, string Label)[] Anchos =
        { ("third", "1/3"), ("half", "1/2"), ("twothirds", "2/3"), ("full", "Completo") };

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static string FmtNum(double n) => Math.Round(n).ToString("#,0", Co);
    public static string FmtMeasure(double v, Metrica m)
    {
        if (m.Type == "count") return FmtNum(v);
        if (m.Fmt == "money") return "$" + FmtNum(v);
        if (m.Fmt == "pct") return Math.Round(v) + "%";
        return (Math.Round(v * 100) / 100).ToString("#,0.##", Co);
    }

    // Valor de un campo como string legible (listas -> coma; bool -> Si/No; vacio -> "(vacio)").
    public static string KeyVal(IDictionary<string, object?> it, string dim)
    {
        var v = it.TryGetValue(dim, out var x) ? x : null;
        // í = i-acute, í en "vacio" -> ortografia de UI; el literal queda ASCII en el .cs.
        if (v is IEnumerable<string> arr) { var l = arr.ToList(); return l.Count > 0 ? string.Join(", ", l) : "(vacío)"; }
        if (v is bool b) return b ? "Sí" : "No";
        var s = v?.ToString();
        return string.IsNullOrEmpty(s) ? "(vacío)" : s;
    }

    public static bool MatchFilter(IDictionary<string, object?> it, string field, string value)
    {
        var v = it.TryGetValue(field, out var x) ? x : null;
        if (v is IEnumerable<string> arr) return arr.Contains(value);
        return KeyVal(it, field) == value;
    }

    public static List<Dictionary<string, object?>> ApplyFilters(IEnumerable<Dictionary<string, object?>> rows, IReadOnlyList<Filtro> filters) =>
        rows.Where(it => filters.All(f => string.IsNullOrEmpty(f.Value) || MatchFilter(it, f.Field, f.Value))).ToList();

    public static List<string> Distinct(IEnumerable<Dictionary<string, object?>> rows, string f)
    {
        var set = new HashSet<string>();
        foreach (var it in rows)
        {
            var v = it.TryGetValue(f, out var x) ? x : null;
            if (v is IEnumerable<string> arr) foreach (var s in arr) set.Add(s);
            else set.Add(KeyVal(it, f));
        }
        return set.OrderBy(s => s, StringComparer.CurrentCulture).ToList();
    }

    public static double Agg(IReadOnlyList<Dictionary<string, object?>> items, Metrica m)
    {
        if (m.Type == "count") return items.Count;
        double s = 0;
        foreach (var it in items) s += ToNum(it.TryGetValue(m.F ?? "", out var x) ? x : null);
        return m.Type == "avg" ? (items.Count > 0 ? s / items.Count : 0) : s;
    }

    private static double ToNum(object? v) => v switch
    {
        null => 0,
        double d => d,
        decimal de => (double)de,
        int i => i,
        long l => l,
        _ => double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0
    };

    // Formatea un valor de campo numerico para tabla/tarjetas segun su formato (money|num|pct). null -> em dash.
    public static string FmtField(object? v, string fmt)
    {
        if (v is null) return "—";
        var n = ToNum(v);
        return fmt switch { "money" => "$" + FmtNum(n), "pct" => Math.Round(n) + "%", _ => FmtNum(n) };
    }

    public static string MeasureLabel(Metrica m) =>
        m.Type == "count" ? "Conteo" : (m.Type == "sum" ? "Suma de " : "Promedio de ") + m.Label;

    public static List<(string L, Metrica M)> MeasureOptions(Dataset ds)
    {
        var o = new List<(string, Metrica)> { ("Conteo", new Metrica("count")) };
        foreach (var x in ds.Meas)
        {
            o.Add(("Suma de " + x.Label, new Metrica("sum", x.Key, x.Label, x.Fmt)));
            o.Add(("Promedio de " + x.Label, new Metrica("avg", x.Key, x.Label, x.Fmt)));
        }
        return o;
    }

    public static bool SameM(Metrica a, Metrica b) => a.Type == b.Type && a.F == b.F;

    public static string DimLabel(Dataset ds, string f) => ds.Dims.FirstOrDefault(d => d.Key == f)?.Label ?? f;

    // ---- Motor de pivote ----
    public sealed class Pivote
    {
        public Dictionary<string, (List<Dictionary<string, object?>> Row, Dictionary<string, List<Dictionary<string, object?>>> Cols)> Map = new();
        public List<string> RowKeys = new();
        public List<string> ColKeys = new();
    }

    public static Pivote Pivot(IReadOnlyList<Dictionary<string, object?>> rows, string rowDim, string colDim, Metrica measure)
    {
        var p = new Pivote();
        var rset = new HashSet<string>(); var cset = new HashSet<string>();
        foreach (var it in rows)
        {
            var r = KeyVal(it, rowDim); rset.Add(r);
            if (!p.Map.TryGetValue(r, out var cobj)) { cobj = (new List<Dictionary<string, object?>>(), new Dictionary<string, List<Dictionary<string, object?>>>()); p.Map[r] = cobj; }
            cobj.Row.Add(it);
            if (!string.IsNullOrEmpty(colDim))
            {
                var ck = KeyVal(it, colDim); cset.Add(ck);
                if (!cobj.Cols.TryGetValue(ck, out var lst)) { lst = new(); cobj.Cols[ck] = lst; }
                lst.Add(it);
            }
        }
        p.RowKeys = rset.OrderByDescending(r => Agg(p.Map[r].Row, measure)).ToList();
        p.ColKeys = string.IsNullOrEmpty(colDim) ? new() : cset.OrderBy(s => s, StringComparer.CurrentCulture).ToList();
        return p;
    }

    public static string AutoTitle(Dataset ds, string rowDim, string colDim, Metrica measure) =>
        MeasureLabel(measure) + " de " + ds.Label + " por " + DimLabel(ds, rowDim) + (string.IsNullOrEmpty(colDim) ? "" : " y " + DimLabel(ds, colDim));

    private static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    // ---- Render de la tabla pivote (HTML string) ----
    public static string PivotTable(Dataset ds, IReadOnlyList<Dictionary<string, object?>> rows, string rowDim, string colDim, Metrica m)
    {
        if (rows.Count == 0) return "<div class=\"pv-empty\">Sin registros para los filtros seleccionados.</div>";
        var has2d = !string.IsNullOrEmpty(colDim);
        var p = Pivot(rows, rowDim, colDim, m);
        double maxTot = 1; foreach (var r in p.RowKeys) maxTot = Math.Max(maxTot, Agg(p.Map[r].Row, m));
        var h = new StringBuilder("<div class=\"pv-tablewrap\"><table class=\"pv-table\"><thead><tr><th>" + Esc(DimLabel(ds, rowDim)) + "</th>");
        if (has2d) foreach (var ck in p.ColKeys) h.Append("<th class=\"pv-num\">" + Esc(ck) + "</th>");
        h.Append("<th class=\"pv-tot\">" + Esc(MeasureLabel(m)) + "</th></tr></thead><tbody>");
        foreach (var r in p.RowKeys)
        {
            h.Append("<tr><td class=\"pv-rk\">" + Esc(r) + "</td>");
            if (has2d) foreach (var ck in p.ColKeys) { var items = p.Map[r].Cols.TryGetValue(ck, out var l) ? l : new(); h.Append("<td class=\"pv-num\">" + FmtMeasure(Agg(items, m), m) + "</td>"); }
            var tot = Agg(p.Map[r].Row, m);
            if (has2d) h.Append("<td class=\"pv-tot\">" + FmtMeasure(tot, m) + "</td>");
            else h.Append("<td class=\"pv-tot\"><div class=\"pv-barwrap\"><div class=\"pv-bar\" style=\"width:" + Math.Max(6, 100 * tot / maxTot).ToString("0.0", CultureInfo.InvariantCulture) + "px\"></div><span>" + FmtMeasure(tot, m) + "</span></div></td>");
            h.Append("</tr>");
        }
        h.Append("</tbody><tfoot><tr><td>Total</td>");
        if (has2d) foreach (var ck in p.ColKeys) { var items = rows.Where(it => KeyVal(it, colDim) == ck).ToList(); h.Append("<td class=\"pv-num\">" + FmtMeasure(Agg(items, m), m) + "</td>"); }
        h.Append("<td class=\"pv-tot\">" + FmtMeasure(Agg(rows, m), m) + "</td></tr></tfoot></table></div>");
        return h.ToString();
    }

    public static readonly string[] ChartPal =
    {
        "var(--propia-brand)", "var(--propia-info-strong)", "var(--propia-ok)", "var(--propia-warning)",
        "var(--propia-error-text)", "var(--propia-brand-border)", "var(--propia-text-muted)"
    };

    // ---- Motor de graficos SVG (barras / lineas / dona). Reusa el pivote. ----
    public static string ChartSvg(Dataset ds, IReadOnlyList<Dictionary<string, object?>> rows, string rowDim, string colDim, Metrica m, string ctype)
    {
        if (rows.Count == 0) return "<div class=\"pv-empty\">Sin registros para los filtros seleccionados.</div>";
        var p = Pivot(rows, rowDim, colDim, m);
        var rk = p.RowKeys.Take(12).ToList();
        var has2d = !string.IsNullOrEmpty(colDim);
        var ck = has2d ? p.ColKeys : new List<string> { "__all__" };
        double Val(string r, string c) => Agg(has2d ? (p.Map[r].Cols.TryGetValue(c, out var l) ? l : new()) : p.Map[r].Row, m);
        const double W = 640, H = 270;
        var inv = CultureInfo.InvariantCulture;

        if (ctype == "donut")
        {
            var items = rk.Select(r => (K: r, V: Agg(p.Map[r].Row, m))).Where(x => x.V > 0).ToList();
            double tot = items.Sum(x => x.V); if (tot <= 0) tot = 1; double acc = 0;
            var segs = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                var frac = items[i].V / tot; var largo = (frac * 251.3).ToString("0.0", inv); var off = (-acc * 251.3).ToString("0.0", inv); acc += frac;
                segs.Append($"<circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"none\" stroke=\"{ChartPal[i % ChartPal.Length]}\" stroke-width=\"15\" stroke-dasharray=\"{largo} 251.3\" stroke-dashoffset=\"{off}\" transform=\"rotate(-90 50 50)\"><title>{Esc(items[i].K)}: {FmtMeasure(items[i].V, m)}</title></circle>");
            }
            var leg = new StringBuilder("<div class=\"pv-chart-legend\">");
            for (int i = 0; i < items.Count; i++) leg.Append($"<span><i style=\"background:{ChartPal[i % ChartPal.Length]}\"></i>{Esc(items[i].K)} &middot; {Math.Round(100 * items[i].V / tot)}%</span>");
            leg.Append("</div>");
            return "<div class=\"pv-chart\" style=\"display:flex;align-items:center;gap:18px;flex-wrap:wrap\"><svg viewBox=\"0 0 100 100\" style=\"width:150px;height:150px;flex:0 0 auto\">" + segs
                + "<text x=\"50\" y=\"49\" text-anchor=\"middle\" style=\"font-size:13px;font-weight:800;fill:var(--propia-text-h1)\">" + FmtMeasure(tot, m) + "</text>"
                + "<text x=\"50\" y=\"60\" text-anchor=\"middle\" style=\"font-size:6px;font-weight:700;letter-spacing:.5px;fill:var(--propia-text-faint)\">TOTAL</text></svg>"
                + "<div style=\"flex:1;min-width:160px\">" + leg + "</div></div>";
        }

        const double L = 46, R = 14, T = 14, B = 42; double pw = W - L - R, ph = H - T - B, max = 1;
        foreach (var r in rk) foreach (var c in ck) max = Math.Max(max, Val(r, c));
        string Y(double v) => (T + ph - (v / max) * ph).ToString("0.0", inv);
        var grid = new StringBuilder();
        for (int g = 0; g <= 4; g++) { var yt = T + g * (ph / 4); var gv = max - g * (max / 4); grid.Append($"<line x1=\"{L}\" y1=\"{yt.ToString("0.0", inv)}\" x2=\"{(W - R).ToString("0.0", inv)}\" y2=\"{yt.ToString("0.0", inv)}\" stroke=\"var(--propia-border-divider)\" stroke-width=\"1\"/><text x=\"{(L - 6).ToString("0.0", inv)}\" y=\"{(yt + 3).ToString("0.0", inv)}\" text-anchor=\"end\" class=\"pv-ax\">{FmtMeasure(gv, m)}</text>"); }
        var xlabels = new StringBuilder();
        for (int i = 0; i < rk.Count; i++) { var cx = L + (i + 0.5) * (pw / rk.Count); var lbl = rk[i].Length > 10 ? rk[i].Substring(0, 9) + "…" : rk[i]; xlabels.Append($"<text x=\"{cx.ToString("0.0", inv)}\" y=\"{(H - 22).ToString("0.0", inv)}\" text-anchor=\"middle\" class=\"pv-axl\">{Esc(lbl)}</text>"); }
        var body = new StringBuilder();
        if (ctype == "lines")
        {
            for (int ci = 0; ci < ck.Count; ci++)
            {
                var col = ChartPal[ci % ChartPal.Length];
                var pts = string.Join(" ", rk.Select((r, i) => (L + (i + 0.5) * (pw / rk.Count)).ToString("0.0", inv) + "," + Y(Val(r, ck[ci]))));
                body.Append($"<polyline points=\"{pts}\" fill=\"none\" stroke=\"{col}\" stroke-width=\"2.4\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");
                for (int i = 0; i < rk.Count; i++) body.Append($"<circle cx=\"{(L + (i + 0.5) * (pw / rk.Count)).ToString("0.0", inv)}\" cy=\"{Y(Val(rk[i], ck[ci]))}\" r=\"3\" fill=\"var(--propia-bg-card)\" stroke=\"{col}\" stroke-width=\"2\"><title>{Esc(rk[i])}{(has2d ? " &middot; " + Esc(ck[ci]) : "")}: {FmtMeasure(Val(rk[i], ck[ci]), m)}</title></circle>");
            }
        }
        else
        {
            double gw = pw / rk.Count, inner = gw * 0.72, bw = inner / ck.Count;
            for (int i = 0; i < rk.Count; i++)
            {
                double gx = L + i * gw + (gw - inner) / 2;
                for (int ci = 0; ci < ck.Count; ci++)
                {
                    var v = Val(rk[i], ck[ci]); var bh = (v / max) * ph; var bx = gx + ci * bw; var col = has2d ? ChartPal[ci % ChartPal.Length] : ChartPal[i % ChartPal.Length];
                    body.Append($"<rect x=\"{bx.ToString("0.0", inv)}\" y=\"{Y(v)}\" width=\"{(bw * 0.86).ToString("0.0", inv)}\" height=\"{Math.Max(0, bh).ToString("0.0", inv)}\" rx=\"2\" fill=\"{col}\"><title>{Esc(rk[i])}{(has2d ? " &middot; " + Esc(ck[ci]) : "")}: {FmtMeasure(v, m)}</title></rect>");
                }
            }
        }
        var legend = has2d ? ("<div class=\"pv-chart-legend\">" + string.Concat(ck.Select((k, i) => $"<span><i style=\"background:{ChartPal[i % ChartPal.Length]}\"></i>{Esc(k)}</span>")) + "</div>") : "";
        return "<div class=\"pv-chart\"><svg viewBox=\"0 0 " + W + " " + H + "\" preserveAspectRatio=\"none\">" + grid + body + xlabels + "</svg>" + legend + "</div>";
    }
}
