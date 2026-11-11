using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Propia.Application.MiCopropiedad;
using Propia.Application.ServiciosPublicos;
using Propia.Domain.Enums;
using Propia.Web.Components.Pages.Capa2.Spx;
using Propia.Web.Components.Shared;
using Propia.Web.Components.Shared.Modals;

namespace Propia.Web.Components.Pages.Capa2;

/// <summary>Pestana Configuracion: bandeja canonica de los servicios publicos (replica cfgBandeja()).</summary>
public partial class ServiciosPublicosPagina
{
    private string? _jwt;
    private bool _authChecked, _cargado;
    private string? _copropiedadNombre;
    private string _tab = "config";
    private List<CuentaServicioDto> _cuentas = new();
    private ServiciosPublicosResumenDto? _resumen;
    private string? _toast;
    private bool _toastErr;
    private static readonly CultureInfo Es = new("es-CO");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const string GruposKey = "propia_servicios_publicos_grupos", ColsKey = "propia_servicios_publicos_cols";

    // ===== Reglas de la replica sobre las cuentas (el servidor ya evaluo el ultimo recibo) =====
    private static bool SinTopes(CuentaServicioDto c) => c.ValorMin is null && c.ValorMax is null && c.ConsumoMin is null && c.ConsumoMax is null;
    private static bool Fuera(CuentaServicioDto c) => c.Rango is RangoRecibo.Alto or RangoRecibo.Bajo;
    // KPI "Proximo pago": el mas cercano entre los servicios activos.
    private CuentaServicioDto? ProximoKpi() => _cuentas.Where(c => c.Activa && c.ProximoPago is not null).OrderBy(c => c.ProximoPago).FirstOrDefault();

    // ===== Columnas (replica cfgBandeja().columnas). SERVICIO es la fija; consumo y unidad ocultas por defecto. =====
    private static readonly (string Key, string Label, string Ancho, bool Visible)[] ColsDef =
    {
        ("alias", "SERVICIO", "220px", true), ("tipo", "TIPO", "190px", true), ("prestador", "PRESTADOR", "150px", true),
        ("numeroCuenta", "REFERENCIA DEL CONTRATO", "200px", true), ("periodicidad", "PERIODICIDAD", "140px", true),
        ("diaPago", "DÍA DE PAGO", "120px", true), ("proximo", "PRÓXIMO PAGO", "140px", true),
        ("valorMin", "VALOR MÍNIMO", "150px", true), ("valorMax", "VALOR MÁXIMO", "150px", true),
        ("consumoMin", "CONSUMO MÍN.", "130px", false), ("consumoMax", "CONSUMO MÁX.", "130px", false),
        ("unidadMedida", "UNIDAD", "100px", false), ("ultimo", "ÚLTIMO RECIBO", "230px", true),
    };
    private static readonly HashSet<string> ColsNumericas = new() { "diaPago", "valorMin", "valorMax", "consumoMin", "consumoMax" };
    private sealed class ColPref { public string Key { get; set; } = ""; public bool Visible { get; set; } = true; public int? W { get; set; } }
    private static List<ColPref> ColsIniciales() => ColsDef.Select(c => new ColPref { Key = c.Key, Visible = c.Visible }).ToList();
    private List<ColPref> _cols = ColsIniciales();
    private static string LabelFija(string k) => ColsDef.FirstOrDefault(c => c.Key == k).Label ?? k;
    private string LabelCol(string k) => CampoDe(k) is { } d ? d.Label.ToUpperInvariant() : LabelFija(k);
    private List<(string Key, string Label)> ColsVis => _cols.Where(c => c.Visible).Select(c => (c.Key, LabelCol(c.Key))).ToList();
    private int ColSpanTotal => ColsVis.Count + 3;
    private string AnchoCol(string k) => _cols.FirstOrDefault(c => c.Key == k)?.W is { } w ? $"{w}px" : ColsDef.FirstOrDefault(c => c.Key == k).Ancho ?? "160px";

    private bool _showColMenu;
    private string _colQ = "";
    private async Task MoverCol(string k, int dir)
    {
        var i = _cols.FindIndex(c => c.Key == k); var j = i + dir;
        if (i <= 0 || j <= 0 || j >= _cols.Count) return;   // la 1a columna (SERVICIO) queda fija
        (_cols[i], _cols[j]) = (_cols[j], _cols[i]);
        await GuardarColsAsync();
    }
    private async Task ToggleVisibleCol(string k)
    {
        var c = _cols.FirstOrDefault(x => x.Key == k);
        if (c is null || _cols.IndexOf(c) == 0) return;
        c.Visible = !c.Visible;
        await GuardarColsAsync();
    }
    private async Task OcultarTodasCol() { for (var i = 0; i < _cols.Count; i++) _cols[i].Visible = i == 0; await GuardarColsAsync(); }
    private async Task MostrarTodasCol() { foreach (var c in _cols) c.Visible = true; await GuardarColsAsync(); }
    private async Task RestablecerCol() { _cols = ColsIniciales(); SincronizarCols(); await GuardarColsAsync(); }
    private Task GuardarColsAsync() => GuardarLocalAsync(ColsKey, System.Text.Json.JsonSerializer.Serialize(_cols));

    // ---- Mover columna por ARRASTRE (propiaTablaColReorder) y ancho por arrastre (propiaTablaResize) ----
    private DotNetObjectReference<ServiciosPublicosPagina>? _self;
    private async Task IniciarReorder(MouseEventArgs e, string clave)
    {
        _self ??= DotNetObjectReference.Create(this);
        try { await Js.InvokeVoidAsync("propiaTablaColReorder.start", _self, clave, e.ClientX, e.ClientY); } catch { }
    }
    [JSInvokable]
    public async Task OnColReorder(string src, string dest)
    {
        if (src == dest) return;
        var from = _cols.FindIndex(c => c.Key == src); var to = _cols.FindIndex(c => c.Key == dest);
        if (from <= 0 || to <= 0) return;   // SERVICIO queda primera
        var c = _cols[from]; _cols.RemoveAt(from); _cols.Insert(to, c);
        StateHasChanged();
        await GuardarColsAsync();
    }
    private async Task IniciarResize(MouseEventArgs e, string clave)
    {
        _self ??= DotNetObjectReference.Create(this);
        try { await Js.InvokeVoidAsync("propiaTablaResize.start", _self, clave, e.ClientX); } catch { }
    }
    [JSInvokable]
    public async Task OnColResize(string clave, int width)
    {
        var c = _cols.FirstOrDefault(x => x.Key == clave); if (c is null) return;
        c.W = width <= 0 ? null : Math.Max(70, width);
        StateHasChanged();
        await GuardarColsAsync();
    }

    // ===== Campos propios (columna "+"): catalogo /api/servicios-publicos/campos. Clave de columna "cd:{id}". =====
    private List<CampoDinDef> _defs = new();
    private Dictionary<(Guid Cuenta, Guid Def), string> _vals = new();
    private bool _campoAbierto;
    private CampoDinDef? _campoEditar, _campoEliminar;
    private static bool EsCampoPropio(string k) => k.StartsWith("cd:", StringComparison.Ordinal);
    private CampoDinDef? CampoDe(string k) => EsCampoPropio(k) && Guid.TryParse(k.AsSpan(3), out var g) ? _defs.FirstOrDefault(d => d.Id == g) : null;
    private string? ValorPropio(CuentaServicioDto c, CampoDinDef d) => _vals.TryGetValue((c.Id, d.Id), out var v) ? v : null;
    private static bool EsNumerico(CampoDinDef d) => d.Tipo is TipoCampoTablero.Numero or TipoCampoTablero.Moneda or TipoCampoTablero.Porcentaje;
    private static decimal? Num(string? v) => decimal.TryParse(v, NumberStyles.Any, Inv, out var n) ? n : null;
    private static string TextoPropio(CampoDinDef d, string? v) => d.Tipo switch
    {
        _ when string.IsNullOrWhiteSpace(v) => "",
        TipoCampoTablero.Booleano => v == "true" ? "Si" : "No",
        TipoCampoTablero.Moneda when Num(v) is { } n => "$" + n.ToString("N0", Es),
        TipoCampoTablero.Porcentaje when Num(v) is { } n => n.ToString("0.##", Es) + "%",
        _ => v!
    };
    private void AbrirCampo(CampoDinDef? d) { _campoEditar = d; _campoAbierto = true; }
    private async Task CargarCamposAsync()
    {
        try { _defs = await Auth().GetFromJsonAsync<List<CampoDinDef>>("/api/servicios-publicos/campos") ?? new(); } catch { _defs = new(); }
        try
        {
            var flat = await Auth().GetFromJsonAsync<List<ServicioPublicoCampoValorFlatDto>>("/api/servicios-publicos/campos-valores") ?? new();
            _vals = flat.Where(v => v.Valor is not null).GroupBy(v => (v.CuentaId, v.DefinicionId)).ToDictionary(g => g.Key, g => g.First().Valor!);
        }
        catch { _vals = new(); }
        SincronizarCols();
    }
    private void SincronizarCols()
    {
        _cols.RemoveAll(c => EsCampoPropio(c.Key) && CampoDe(c.Key) is null);
        _grupos.RemoveAll(k => EsCampoPropio(k) && CampoDe(k) is null);
        _sorts.RemoveAll(s => EsCampoPropio(s.Key) && CampoDe(s.Key) is null);
        foreach (var d in _defs.OrderBy(d => d.Orden))
            if (_cols.All(c => c.Key != "cd:" + d.Id)) _cols.Add(new ColPref { Key = "cd:" + d.Id });
    }
    private async Task RecargarCamposAsync()
    {
        _campoAbierto = false; _campoEditar = null;
        await CargarCamposAsync();
        await GuardarColsAsync();
        StateHasChanged();
    }
    private async Task EliminarCampoAsync()
    {
        if (_campoEliminar is not { } d) return;
        _delBusy = true;
        try
        {
            var r = await Auth().DeleteAsync($"/api/servicios-publicos/campos/{d.Id}");
            if (!r.IsSuccessStatusCode) Mensaje(await SpxFormato.ErrorDeAsync(r, "No se pudo eliminar el campo."), true);
        }
        catch (Exception ex) { Mensaje(ex.Message, true); }
        _delBusy = false; _campoEliminar = null;
        await RecargarCamposAsync();
    }
    private async Task DuplicarCampoAsync(CampoDinDef d)
    {
        var r = await Auth().PostAsJsonAsync("/api/servicios-publicos/campos",
            new CrearCampoDefinicionRequest(d.Label + " (copia)", d.Tipo, d.Opciones, d.Descripcion));
        if (r.IsSuccessStatusCode) { await RecargarCamposAsync(); Mensaje("Campo duplicado.", false); }
        else Mensaje(await SpxFormato.ErrorDeAsync(r, "No se pudo duplicar el campo."), true);
    }
    private async Task GuardarValorAsync(CuentaServicioDto c, CampoDinDef d, string? v)
    {
        v = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        if (v == ValorPropio(c, d)) return;
        try
        {
            var r = await Auth().PutAsJsonAsync($"/api/servicios-publicos/campos-valores/{c.Id}/{d.Id}", new SetCampoValorRequest(v));
            if (!r.IsSuccessStatusCode) { Mensaje(await SpxFormato.ErrorDeAsync(r, "No se pudo guardar."), true); return; }
            if (v is null) _vals.Remove((c.Id, d.Id)); else _vals[(c.Id, d.Id)] = v;
        }
        catch (Exception ex) { Mensaje(ex.Message, true); }
    }

    // ===== Buscar + Filtros =====
    private string _q = "";
    private FiltroDinamico? _fd;
    private List<FiltroDinamico.CampoDef> CamposFiltro => new List<FiltroDinamico.CampoDef>
    {
        new() { Key = "alias", Label = "SERVICIO", Kind = "texto" },
        new() { Key = "tipo", Label = "TIPO", Kind = "sel", Opciones = TiposServicioPublico.Todos.Select(t => (t.Clave, t.Nombre)).ToList() },
        new() { Key = "prestador", Label = "PRESTADOR", Kind = "texto" },
        new() { Key = "numeroCuenta", Label = "REFERENCIA DEL CONTRATO", Kind = "texto" },
        new() { Key = "periodicidad", Label = "PERIODICIDAD", Kind = "sel", Opciones = TiposServicioPublico.Periodicidades.Select(p => (p.Nombre, p.Nombre)).ToList() },
        new() { Key = "diaPago", Label = "DÍA DE PAGO", Kind = "num" },
        new() { Key = "proximo", Label = "PRÓXIMO PAGO", Kind = "fecha" },
        new() { Key = "valorMin", Label = "VALOR MÍNIMO", Kind = "num" },
        new() { Key = "valorMax", Label = "VALOR MÁXIMO", Kind = "num" },
        new() { Key = "consumoMin", Label = "CONSUMO MÍN.", Kind = "num" },
        new() { Key = "consumoMax", Label = "CONSUMO MÁX.", Kind = "num" },
        new() { Key = "unidadMedida", Label = "UNIDAD", Kind = "texto" },
        new() { Key = "ultimo", Label = "ÚLTIMO RECIBO", Kind = "num" },
    }.Concat(_defs.Select(d => new FiltroDinamico.CampoDef
    {
        Key = "cd:" + d.Id, Label = d.Label.ToUpperInvariant(),
        Kind = EsNumerico(d) ? "num" : d.Tipo == TipoCampoTablero.Fecha ? "fecha"
             : d.Tipo is TipoCampoTablero.Seleccion or TipoCampoTablero.Booleano ? "sel" : "texto",
        Opciones = d.Tipo == TipoCampoTablero.Booleano ? new List<(string, string)> { ("true", "Si"), ("false", "No") }
                 : d.Tipo == TipoCampoTablero.Seleccion ? d.OpcionesArray().Select(o => (o, o)).ToList() : new List<(string, string)>()
    })).ToList();
    private static string? Dec(decimal? v) => v?.ToString("0.##", Inv);
    private string? ValorCampo(CuentaServicioDto c, string campo) => campo switch
    {
        _ when CampoDe(campo) is { } d => ValorPropio(c, d),
        "alias" => c.Alias,
        "tipo" => TiposServicioPublico.De(c.Tipo).Clave,
        "prestador" => c.Prestador,
        "numeroCuenta" => c.NumeroCuenta,
        "periodicidad" => ReglasServicioPublico.Periodicidad(c.Periodicidad),
        "diaPago" => c.DiaPago?.ToString(Inv),
        "proximo" => c.ProximoPago?.ToString("yyyy-MM-dd", Inv),
        "valorMin" => Dec(c.ValorMin), "valorMax" => Dec(c.ValorMax),
        "consumoMin" => Dec(c.ConsumoMin), "consumoMax" => Dec(c.ConsumoMax),
        "unidadMedida" => c.UnidadMedida,
        "ultimo" => Dec(c.UltimoValor),
        _ => ""
    };
    // Busca por servicio, prestador, referencia y tipo (replica: buscar.campos).
    private List<CuentaServicioDto> Filtradas()
    {
        var q = _q.Trim();
        return _cuentas.Where(c =>
            (q.Length == 0 || c.Alias.Contains(q, StringComparison.OrdinalIgnoreCase)
                           || (c.Prestador ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                           || (c.NumeroCuenta ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                           || TiposServicioPublico.De(c.Tipo).Nombre.Contains(q, StringComparison.OrdinalIgnoreCase))
            && (_fd?.Pasa(campo => ValorCampo(c, campo)) ?? true)).ToList();
    }

    // ===== Ordenar (menu compartido + clic en el encabezado). Sin orden = el de la lista (por nombre). =====
    private List<TablaOrdenarMenu.Sort> _sorts = new();
    private List<TablaOrdenarMenu.Col> ColsOrdenables() => ColsVis.Select(c => new TablaOrdenarMenu.Col(c.Key, c.Label)).ToList();
    private void OrdenarPorClic(string k)
    {
        var dir = _sorts.Count == 1 && _sorts[0].Key == k ? -_sorts[0].Dir : 1;
        _sorts = new() { new TablaOrdenarMenu.Sort { Key = k, Dir = dir } };
    }
    private IComparable ValorOrden(CuentaServicioDto c, string k) => k switch
    {
        _ when CampoDe(k) is { } d => EsNumerico(d) ? (IComparable)(Num(ValorPropio(c, d)) ?? decimal.MinValue) : TextoPropio(d, ValorPropio(c, d)),
        "tipo" => TiposServicioPublico.De(c.Tipo).Nombre,
        "periodicidad" => (int)c.Periodicidad,
        "diaPago" => c.DiaPago ?? int.MinValue,
        "proximo" => c.ProximoPago ?? DateOnly.MinValue,
        "valorMin" => c.ValorMin ?? decimal.MinValue, "valorMax" => c.ValorMax ?? decimal.MinValue,
        "consumoMin" => c.ConsumoMin ?? decimal.MinValue, "consumoMax" => c.ConsumoMax ?? decimal.MinValue,
        "ultimo" => c.UltimoValor ?? decimal.MinValue,
        _ => ValorCampo(c, k) ?? ""
    };
    private IEnumerable<CuentaServicioDto> Ordenadas()
    {
        var src = Filtradas();
        IEnumerable<CuentaServicioDto> res = src;
        if (_sorts.Count > 0)
        {
            IOrderedEnumerable<CuentaServicioDto>? o = null;
            foreach (var s in _sorts)
            {
                Func<CuentaServicioDto, IComparable> sel = c => ValorOrden(c, s.Key);
                o = o is null ? (s.Dir >= 0 ? src.OrderBy(sel) : src.OrderByDescending(sel))
                              : (s.Dir >= 0 ? o.ThenBy(sel) : o.ThenByDescending(sel));
            }
            res = o!;
        }
        // Los recien creados quedan al final (resaltados) hasta el proximo refresco: OrderBy ESTABLE.
        return _recien.Count == 0 ? res : res.OrderBy(c => _recien.Contains(c.Id) ? 1 : 0);
    }

    // ===== Agrupar =====
    private List<string> _grupos = new();
    private List<TablaAgruparMenu.Col> ColsAgrupables() => ColsVis.Select(c => new TablaAgruparMenu.Col(c.Key, c.Label)).ToList();
    private List<string> Niveles => _grupos.Where(k => ColsVis.Any(c => c.Key == k)).ToList();
    private async Task OnGruposChanged() { await GuardarLocalAsync(GruposKey, System.Text.Json.JsonSerializer.Serialize(_grupos)); StateHasChanged(); }
    private IReadOnlyList<NodoGrupo<CuentaServicioDto>> ArbolGrupos(List<string> niveles)
        => TablaAgrupador.Agrupar(Ordenadas(), niveles, ValorGrupoDe, "(vacio)");
    private IEnumerable<ValorGrupo> ValorGrupoDe(CuentaServicioDto c, string k)
    {
        // Titulo de grupo = texto de la celda (como en los demas modulos).
        if (k == "tipo")
        {
            var t = TiposServicioPublico.De(c.Tipo);
            yield return new ValorGrupo(t.Clave, t.Nombre, Color: t.Color, Orden: TiposServicioPublico.Todos.ToList().IndexOf(t));
            yield break;
        }
        if (k == "periodicidad") { yield return new ValorGrupo(c.Periodicidad.ToString(), ReglasServicioPublico.Periodicidad(c.Periodicidad), Orden: (int)c.Periodicidad); yield break; }
        if (k == "proximo")
        {
            if (c.ProximoPago is { } p) yield return new ValorGrupo(p.ToString("yyyy-MM-dd", Inv), ReglasServicioPublico.FechaCorta(p), Orden: p.DayNumber);
            yield break;
        }
        if (k == "ultimo")
        {
            var txt = c.Rango switch { RangoRecibo.Ok => "En rango", RangoRecibo.Alto => "Sobre el tope", RangoRecibo.Bajo => "Bajo el mínimo", RangoRecibo.Sin => "Sin topes", _ => "Sin recibos" };
            yield return new ValorGrupo(c.Rango.ToString(), txt, Orden: (int)c.Rango);
            yield break;
        }
        if (ColsNumericas.Contains(k))
        {
            var n = Num(ValorCampo(c, k));
            if (n is { } x) yield return new ValorGrupo(x.ToString(Inv), k.StartsWith("valor") ? ReglasServicioPublico.Plata(x) : ReglasServicioPublico.Num(x), Orden: (int)Math.Clamp(x, int.MinValue, int.MaxValue));
            yield break;
        }
        if (CampoDe(k) is { } cd)
        {
            var t = TextoPropio(cd, ValorPropio(c, cd));
            var n = EsNumerico(cd) ? Num(ValorPropio(c, cd)) : null;
            if (t.Length > 0) yield return new ValorGrupo(t, t, Orden: n is { } x ? (int)Math.Clamp(x, int.MinValue, int.MaxValue) : 0);
            yield break;
        }
        var v = ValorCampo(c, k);
        if (!string.IsNullOrWhiteSpace(v)) yield return new ValorGrupo(v!, v!);
    }

    // ===== Descargar la vista (TablaExportar): columnas visibles en su orden, todas las filas filtradas =====
    private string TextoCelda(CuentaServicioDto c, string k) => k switch
    {
        "tipo" => TiposServicioPublico.De(c.Tipo).Nombre,
        "periodicidad" => ReglasServicioPublico.Periodicidad(c.Periodicidad),
        "proximo" => ReglasServicioPublico.FechaCorta(c.ProximoPago),
        "ultimo" => c.UltimoValor is { } uv && c.UltimoAnio is { } a && c.UltimoMes is { } m ? ReglasServicioPublico.Plata(uv) + " · " + ReglasServicioPublico.PeriodoLabel(a, m) : "-",
        _ when CampoDe(k) is { } d => TextoPropio(d, ValorPropio(c, d)),
        _ => ValorCampo(c, k) ?? ""
    };
    private ExportVista VistaExport()
    {
        var cols = ColsVis;
        bool EsNum(string k) => ColsNumericas.Contains(k) || (CampoDe(k) is { } d && EsNumerico(d));
        var columnas = cols.Select(c => new ExportColumna(c.Label, EsNum(c.Key),
            c.Key.StartsWith("valor") || CampoDe(c.Key)?.Tipo == TipoCampoTablero.Moneda ? "\"$\" #,##0" : EsNum(c.Key) ? "#,##0.##" : null)).ToList();
        object? Celda(CuentaServicioDto c, string k) => EsNum(k) ? Num(ValorCampo(c, k)) : (TextoCelda(c, k) is { Length: > 0 } t ? t : null);
        ExportFila Fila(CuentaServicioDto c, IReadOnlyList<string>? ruta) => new(cols.Select(col => Celda(c, col.Key)).ToList(), Ruta: ruta);
        var filas = new List<ExportFila>();
        var niveles = Niveles;
        if (niveles.Count == 0) filas.AddRange(Ordenadas().Select(c => Fila(c, null)));
        else
        {
            void Recorrer(IReadOnlyList<NodoGrupo<CuentaServicioDto>> nodos, List<string> ruta)
            {
                foreach (var n in nodos)
                {
                    filas.Add(ExportFila.DeGrupo(n.Titulo, n.Nivel, n.Conteo));
                    var r = new List<string>(ruta) { n.Titulo };
                    if (n.EsHoja) filas.AddRange(n.Filas.Select(c => Fila(c, r)));
                    else Recorrer(n.Subgrupos, r);
                }
            }
            Recorrer(ArbolGrupos(niveles), new());
        }
        var criterios = new List<string>();
        if (!string.IsNullOrWhiteSpace(_q)) criterios.Add($"Búsqueda: \"{_q.Trim()}\"");
        if (_fd?.Resumen() is { Length: > 0 } fr) criterios.Add("Filtros: " + fr);
        if (_sorts.Count > 0) criterios.Add("Orden: " + string.Join(", ", _sorts.Select(s => $"{LabelCol(s.Key)} {(s.Dir >= 0 ? "ascendente" : "descendente")}")));
        if (niveles.Count > 0) criterios.Add("Agrupado por: " + string.Join(" > ", niveles.Select(LabelCol)));
        criterios.Add($"{Filtradas().Count} servicios");
        return new ExportVista("Servicios públicos", _copropiedadNombre, columnas, filas, criterios, niveles.Select(LabelCol).ToList());
    }

    // ===== Seleccion multiple + borrado masivo =====
    private readonly HashSet<Guid> _sel = new();
    private bool _confirmDelSel, _delBusy;
    private bool TodasSeleccionadas => Filtradas().Count > 0 && Filtradas().All(c => _sel.Contains(c.Id));
    private void ToggleSelAll(bool on) { _sel.Clear(); if (on) foreach (var c in Filtradas()) _sel.Add(c.Id); }
    private void ToggleSel(Guid id, bool on) { if (on) _sel.Add(id); else _sel.Remove(id); }
    private async Task EliminarSeleccionadasAsync()
    {
        _delBusy = true;
        var fallidas = 0;
        foreach (var id in _sel.ToList())
        {
            try { var r = await Auth().DeleteAsync($"/api/servicios-publicos/cuentas/{id}"); if (!r.IsSuccessStatusCode) fallidas++; }
            catch { fallidas++; }
        }
        _delBusy = false; _confirmDelSel = false; _sel.Clear();
        await CargarAsync();
        if (fallidas > 0) Mensaje(fallidas == 1 ? "No se pudo eliminar 1 servicio." : $"No se pudieron eliminar {fallidas} servicios.", true);
    }

    // ===== Edicion en linea (replica onCelda): validaciones y PUT MERGE de UNA clave =====
    private async Task EditarAsync(CuentaServicioDto c, string k, string? raw)
    {
        var v = (raw ?? "").Trim();
        ActualizarCuentaServicioRequest req;
        string? aviso = null;
        switch (k)
        {
            case "alias":
                if (v.Length == 0) { Mensaje("El nombre del servicio no puede quedar vacío.", true); await CargarAsync(); return; }
                if (v == c.Alias) return;
                req = new(Alias: v); break;
            case "tipo":
                var t = TiposServicioPublico.Todos.FirstOrDefault(x => x.Clave == v);
                if (t is null || t.Valor == c.Tipo) return;
                req = new(Tipo: t.Valor); break;
            case "prestador": req = new(Prestador: v); break;
            case "numeroCuenta":
                var norm = ReglasServicioPublico.NormalizarReferencia(v);
                var otra = norm is null ? null : _cuentas.FirstOrDefault(x => x.Id != c.Id && ReglasServicioPublico.NormalizarReferencia(x.NumeroCuenta) == norm);
                if (otra is not null) { Mensaje($"La referencia {v} ya es de \"{otra.Alias}\".", true); await CargarAsync(); return; }
                req = v.Length == 0 ? new(Limpiar: new[] { "numeroCuenta" }) : new(NumeroCuenta: v); break;
            case "periodicidad":
                var p = TiposServicioPublico.Periodicidades.FirstOrDefault(x => x.Nombre == v);
                if (p.Nombre is null) return;
                req = new(Periodicidad: p.Valor); break;
            case "diaPago":
                var d = SpxFormato.NumOrNull(v);
                if (d is { } dd && (dd < 1 || dd > 31)) { Mensaje("El día de pago va de 1 a 31.", true); await CargarAsync(); return; }
                req = d is null ? new(Limpiar: new[] { "diaPago" }) : new(DiaPago: (int)d.Value); break;
            case "valorMin" or "valorMax" or "consumoMin" or "consumoMax":
                var n = SpxFormato.NumOrNull(v);
                req = n is null ? new(Limpiar: new[] { k }) : k switch
                {
                    "valorMin" => new(ValorMin: n), "valorMax" => new(ValorMax: n),
                    "consumoMin" => new(ConsumoMin: n), _ => new(ConsumoMax: n)
                };
                // Replica: el minimo mayor que el maximo se guarda pero avisa.
                var min = k.StartsWith("valor") ? (k == "valorMin" ? n : c.ValorMin) : (k == "consumoMin" ? n : c.ConsumoMin);
                var max = k.StartsWith("valor") ? (k == "valorMax" ? n : c.ValorMax) : (k == "consumoMax" ? n : c.ConsumoMax);
                if (min is { } a && max is { } b && a > b) aviso = "Ojo: el mínimo quedó mayor que el máximo.";
                break;
            case "unidadMedida": req = v.Length == 0 ? new(Limpiar: new[] { "unidadMedida" }) : new(UnidadMedida: v); break;
            default: return;
        }
        try
        {
            var r = await Auth().PutAsJsonAsync($"/api/servicios-publicos/cuentas/{c.Id}", req);
            if (!r.IsSuccessStatusCode) { Mensaje(await SpxFormato.ErrorDeAsync(r, "No se pudo guardar."), true); await CargarAsync(); return; }
            var dto = await r.Content.ReadFromJsonAsync<CuentaServicioDto>();
            if (dto is not null) { var i = _cuentas.FindIndex(x => x.Id == c.Id); if (i >= 0) _cuentas[i] = dto; }
            if (aviso is not null) Mensaje(aviso, true);
        }
        catch (Exception ex) { Mensaje(ex.Message, true); }
    }

    // ===== Alta en linea ("Agregar servicio"; Enter guarda, Esc cancela, Tab/clic fuera guarda) =====
    private sealed class NuevoServicio
    {
        public string Alias { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Prestador { get; set; } = "";
        public string Referencia { get; set; } = "";
        public string Periodicidad { get; set; } = "";
        public string DiaPago { get; set; } = "";
        public string ValorMin { get; set; } = "";
        public string ValorMax { get; set; } = "";
        public string ConsumoMin { get; set; } = "";
        public string ConsumoMax { get; set; } = "";
        public string Unidad { get; set; } = "";
    }
    private NuevoServicio _nu = new();
    private bool _altaAbierta, _creando, _scrollBottomPending;
    private readonly HashSet<Guid> _recien = new();
    private void CerrarAlta() { _altaAbierta = false; _nu = new(); }
    private void FabAgregar() { _altaAbierta = true; _scrollBottomPending = true; }
    private async Task OnAltaKeydown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") CerrarAlta();
        else if (e.Key == "Enter") await CrearDesdeAltaAsync(false);
    }
    [JSInvokable]
    public async Task AltaBlurGuardar()
    {
        if (!_altaAbierta || _creando) return;
        if (string.IsNullOrWhiteSpace(_nu.Alias)) { CerrarAlta(); StateHasChanged(); return; }
        await CrearDesdeAltaAsync(false);
        StateHasChanged();
    }
    private async Task CrearDesdeAltaAsync(bool abrir)
    {
        if (_creando) return;
        var alias = _nu.Alias.Trim();
        if (alias.Length == 0) { Mensaje("Escribe el nombre del servicio.", true); return; }
        var refTxt = _nu.Referencia.Trim();
        var norm = ReglasServicioPublico.NormalizarReferencia(refTxt);
        if (norm is not null && _cuentas.Any(x => ReglasServicioPublico.NormalizarReferencia(x.NumeroCuenta) == norm))
        { Mensaje($"Ya hay un servicio con la referencia {refTxt}.", true); return; }
        var tipo = TiposServicioPublico.Todos.FirstOrDefault(x => x.Clave == _nu.Tipo) ?? TiposServicioPublico.Todos[0];
        var per = TiposServicioPublico.Periodicidades.FirstOrDefault(x => x.Nombre == _nu.Periodicidad);
        var dia = SpxFormato.NumOrNull(_nu.DiaPago);
        if (dia is { } dd && (dd < 1 || dd > 31)) { Mensaje("El día de pago va de 1 a 31.", true); return; }
        _creando = true;
        try
        {
            var req = new CrearCuentaServicioRequest(tipo.Valor, alias, _nu.Prestador.Trim(), refTxt.Length == 0 ? null : refTxt, null,
                _nu.Unidad.Trim().Length == 0 ? tipo.Unidad : _nu.Unidad.Trim(), 25,
                per.Nombre is null ? PeriodicidadServicio.Mensual : per.Valor, dia is { } di ? (int)di : 15,
                SpxFormato.NumOrNull(_nu.ValorMin), SpxFormato.NumOrNull(_nu.ValorMax),
                SpxFormato.NumOrNull(_nu.ConsumoMin), SpxFormato.NumOrNull(_nu.ConsumoMax));
            var r = await Auth().PostAsJsonAsync("/api/servicios-publicos/cuentas", req);
            if (!r.IsSuccessStatusCode) { Mensaje(await SpxFormato.ErrorDeAsync(r, "No se pudo crear el servicio."), true); return; }
            var dto = await r.Content.ReadFromJsonAsync<CuentaServicioDto>();
            CerrarAlta();
            if (dto is not null) { _recien.Add(dto.Id); _cuentas.Add(dto); }
            if (abrir && dto is not null) await AbrirFichaAsync(dto);
        }
        catch (Exception ex) { Mensaje(ex.Message, true); }
        finally { _creando = false; }
    }

    // ===== Clic derecho: acciones de la bandeja + "Ver recibos y consumo" y "Cargar recibo con IA" =====
    private bool _ctxOpen;
    private double _ctxX, _ctxY;
    private List<TablaCtxMenu.Item> _ctxItems = new();
    private void AbrirCtx(MouseEventArgs e, CuentaServicioDto c)
    {
        _ctxX = e.ClientX; _ctxY = e.ClientY;
        _ctxItems = new()
        {
            new("Insertar registro arriba", "fi-rr-arrow-small-up", OnClick: () => { Mensaje("Accion aun no disponible en esta version.", false); StateHasChanged(); return Task.CompletedTask; }),
            new("Insertar registro abajo", "fi-rr-arrow-small-down", OnClick: () => { Mensaje("Accion aun no disponible en esta version.", false); StateHasChanged(); return Task.CompletedTask; }),
            new("Abrir el servicio", "fi-rr-expand", OnClick: async () => { await AbrirFichaAsync(c); StateHasChanged(); }),
            new("Copiar enlace del registro", "fi-rr-link-alt", OnClick: async () => { await CopiarEnlaceAsync(c); StateHasChanged(); }),
            new(Sep: true),
            new("Eliminar registro", "fi-rr-trash", Danger: true, OnClick: () => { _sel.Clear(); _sel.Add(c.Id); _confirmDelSel = true; StateHasChanged(); return Task.CompletedTask; }),
            new(Sep: true),
            new("Ver recibos y consumo", "fi-rr-chart-histogram", OnClick: async () => { await IrRecibosAsync(c.Id); StateHasChanged(); }),
            new("Cargar recibo con IA", "fi-rr-sparkles", OnClick: async () => { await IrRecibosAsync(c.Id); _elegirArchivo = true; StateHasChanged(); }),
        };
        _ctxOpen = true;
    }
    private async Task CopiarEnlaceAsync(CuentaServicioDto c)
    {
        var url = Nav.Uri.Split('#')[0] + "#" + c.Id;
        try { await Js.InvokeVoidAsync("navigator.clipboard.writeText", url); Mensaje("Enlace del registro copiado.", false); }
        catch { Mensaje("No se pudo copiar el enlace.", true); }
    }

    // ===== Menu de columna (flecha del encabezado) =====
    private TablaColMenu? _colMenu;
    private async Task OnColAccion((TablaColMenu.Accion Accion, TablaColMenu.Ctx Col) a)
    {
        var (accion, col) = a;
        switch (accion)
        {
            case TablaColMenu.Accion.OrdenarAsc: _sorts = new() { new TablaOrdenarMenu.Sort { Key = col.Key, Dir = 1 } }; break;
            case TablaColMenu.Accion.OrdenarDesc: _sorts = new() { new TablaOrdenarMenu.Sort { Key = col.Key, Dir = -1 } }; break;
            case TablaColMenu.Accion.Filtrar:
                if (_fd is not null && CamposFiltro.Any(c => c.Key == col.Key)) await _fd.AgregarReglaCampo(col.Key);
                else Mensaje("Filtrar por este campo aun no esta disponible; usa el boton Filtros.", false);
                break;
            case TablaColMenu.Accion.Agrupar: _grupos = new() { col.Key }; await OnGruposChanged(); break;
            case TablaColMenu.Accion.Ocultar:
                if (_cols.FindIndex(c => c.Key == col.Key) == 0) Mensaje("Esta columna es fija y no se puede ocultar.", false);
                else await ToggleVisibleCol(col.Key);
                break;
            case TablaColMenu.Accion.Editar when CampoDe(col.Key) is { } de: AbrirCampo(de); break;
            case TablaColMenu.Accion.Duplicar when CampoDe(col.Key) is { } dd: await DuplicarCampoAsync(dd); break;
            case TablaColMenu.Accion.Eliminar when CampoDe(col.Key) is { } dx: _campoEliminar = dx; break;
            case TablaColMenu.Accion.InsertarIzq or TablaColMenu.Accion.InsertarDer: AbrirCampo(null); break;
            case TablaColMenu.Accion.Duplicar: Mensaje("Solo se pueden duplicar campos propios.", false); break;
            default: Mensaje("Esta opción solo aplica a los campos propios (los creados con +).", false); break;
        }
        StateHasChanged();
    }

    // ===== Ficha del servicio (modal 760px) =====
    private async Task AbrirFichaAsync(CuentaServicioDto c)
    {
        var res = await Modals.OpenAsync<FichaServicioPublicoModal, string>(
            new ModalParameters().Set("Cuenta", c).Set("Todas", (IReadOnlyList<CuentaServicioDto>)_cuentas).Set("Jwt", _jwt),
            new ModalOptions { Width = "760px" });
        if (res == "guardado") { await CargarAsync(); if (_selId == c.Id) await CargarDetalleAsync(c.Id); }
        else if (res == "recibos") await IrRecibosAsync(c.Id);
        StateHasChanged();
    }

    // ===== Datos =====
    private HttpClient Auth()
    {
        var http = HttpFactory.CreateClient("PropiaApi");
        if (!string.IsNullOrEmpty(_jwt))
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwt);
        return http;
    }
    // Cliente de 5 min para la lectura por VISION (fotos y PDF escaneados).
    private HttpClient AuthLong()
    {
        var http = HttpFactory.CreateClient("PropiaApiLong");
        if (!string.IsNullOrEmpty(_jwt))
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwt);
        return http;
    }
    private async Task CargarAsync(bool reintento = false)
    {
        try
        {
            _cuentas = await Auth().GetFromJsonAsync<List<CuentaServicioDto>>("/api/servicios-publicos/cuentas") ?? new();
            try { _resumen = await Auth().GetFromJsonAsync<ServiciosPublicosResumenDto>("/api/servicios-publicos/resumen"); } catch { }
            _recien.RemoveWhere(id => _cuentas.All(c => c.Id != id));
            _sel.RemoveWhere(id => _cuentas.All(c => c.Id != id));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized && !reintento)
        {
            var nuevo = await Propia.Web.Services.SessionHelper.HandleUnauthorizedAsync(Js, Nav);
            if (!string.IsNullOrEmpty(nuevo)) { _jwt = nuevo; await CargarAsync(reintento: true); return; }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            Mensaje("Solo el administrador de la copropiedad gestiona los servicios públicos.", true);
        }
        catch (Exception ex) { Mensaje(ex.Message, true); }
        _cargado = true;
    }
    private void Mensaje(string m, bool err) { _toast = m; _toastErr = err; }
    private async Task GuardarLocalAsync(string k, string v) { try { await Js.InvokeVoidAsync("localStorage.setItem", k, v); } catch { } }
    private async Task CargarPreferenciasAsync()
    {
        try
        {
            _copropiedadNombre = await Js.InvokeAsync<string?>("localStorage.getItem", "propia_copropiedad_nombre");
            var g = await Js.InvokeAsync<string?>("localStorage.getItem", GruposKey);
            if (!string.IsNullOrWhiteSpace(g)) _grupos = System.Text.Json.JsonSerializer.Deserialize<List<string>>(g) ?? new();
            var c = await Js.InvokeAsync<string?>("localStorage.getItem", ColsKey);
            if (!string.IsNullOrWhiteSpace(c) && System.Text.Json.JsonSerializer.Deserialize<List<ColPref>>(c) is { Count: > 0 } guard)
            {
                // Conserva orden/visibilidad/ancho guardados; columnas nuevas al final; SERVICIO siempre 1a y visible.
                var conocidas = guard.Where(x => ColsDef.Any(d => d.Key == x.Key) || EsCampoPropio(x.Key)).GroupBy(x => x.Key).Select(x => x.First()).ToList();
                conocidas.AddRange(ColsDef.Where(d => conocidas.All(x => x.Key != d.Key)).Select(d => new ColPref { Key = d.Key, Visible = d.Visible }));
                var fija = conocidas.First(x => x.Key == "alias"); fija.Visible = true;
                conocidas.Remove(fija); conocidas.Insert(0, fija);
                _cols = conocidas;
            }
        }
        catch { }
    }

    // ===== Ciclo de vida: sesion, preferencias, ganchos JS de la tabla y del velo =====
    private string? _wiredSig;
    private bool _ctaVisible = true;
    [JSInvokable] public Task CargarMas() => Task.CompletedTask;   // sin lotes: se pinta toda la lista
    [JSInvokable] public void SetCtaVisible(bool visible) { if (_ctaVisible != visible) { _ctaVisible = visible; StateHasChanged(); } }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _jwt is null)
        {
            try
            {
                var stored = await Js.InvokeAsync<string?>("sessionStorage.getItem", "propia_jwt");
                _authChecked = true;
                if (!string.IsNullOrWhiteSpace(stored))
                {
                    _jwt = stored;
                    await CargarPreferenciasAsync();
                    await CargarAsync();
                    await CargarCamposAsync();
                    await AbrirDesdeEnlaceAsync();
                }
                StateHasChanged();
            }
            catch { }
            return;
        }

        if (_jwt is not null && _tab == "config" && _cargado)
        {
            var sig = $"{_altaAbierta}|{Filtradas().Count}|{Niveles.Count}|{ColsVis.Count}";
            if (sig != _wiredSig)
            {
                _wiredSig = sig;
                _self ??= DotNetObjectReference.Create(this);
                try { await Js.InvokeVoidAsync("propiaUI.dstInfinite", _self, ".dst-scroll.tblscroll"); } catch { }
                if (_altaAbierta) { try { await Js.InvokeVoidAsync("propiaUI.altaAutoSave", _self, ".newrow--edit", "AltaBlurGuardar", ".sp-pop"); } catch { } }
                else { try { await Js.InvokeVoidAsync("propiaUI.altaAutoSaveOff"); } catch { } }
            }
        }
        else if (_wiredSig is not null)
        {
            _wiredSig = null;
            try { await Js.InvokeVoidAsync("propiaUI.altaAutoSaveOff"); } catch { }
        }

        if (_scrollBottomPending)
        {
            _scrollBottomPending = false;
            try { await Js.InvokeVoidAsync("propiaUI.dstScrollToCta", ".dst-scroll.tblscroll"); } catch { }
        }

        await DespuesDeRenderRecibosAsync();
    }

    // "Copiar enlace del registro" lleva #<id>: al abrir ese enlace se abre la ficha del servicio.
    private async Task AbrirDesdeEnlaceAsync()
    {
        var frag = new Uri(Nav.Uri).Fragment.TrimStart('#');
        if (Guid.TryParse(frag, out var id) && _cuentas.FirstOrDefault(c => c.Id == id) is { } c)
            await AbrirFichaAsync(c);
    }

    public void Dispose()
    {
        try { _ = Js.InvokeVoidAsync("propiaUI.altaAutoSaveOff"); } catch { }
        _self?.Dispose();
        _vivo = false;
    }
}
