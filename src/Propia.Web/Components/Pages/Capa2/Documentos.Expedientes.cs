using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Propia.Application.Documentos;
using Propia.Web.Components.Shared;

namespace Propia.Web.Components.Pages.Capa2;

/// <summary>
/// Vista Folders (expedientes) como bandeja CANONICA de Unidades (replica cfgFolders(), PROPUESTAS.md seccion 2):
/// Filtros / Buscar / Ordenar / Agrupar / Columnas / Descargar + selector Galeria | Tabla (propia_view_expedientes).
/// Galeria = las tarjetas de siempre; Tabla = table.tbl con FOLDER editable en linea, alta "Agregar folder" (folder
/// raiz), seleccion con borrado masivo y clic derecho (Abrir / Copiar enlace / Eliminar / Configurar folder).
/// </summary>
public partial class Documentos
{
    private const string VistaExpKey = "propia_view_expedientes", GruposExpKey = "propia_expedientes_grupos", ColsExpKey = "propia_expedientes_cols";
    private static readonly CultureInfo Es = new("es-CO");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ===== Vista (Galeria | Tabla), recordada por navegador =====
    private string _vistaExp = "galeria";
    private static readonly TablaVistaToggle.Opcion[] VistasExpOpc = { new("galeria", "Galería", "tarjetas"), TablaVistaToggle.Tabla("tabla") };
    private async Task SetVistaExp(string v)
    {
        _vistaExp = v == "tabla" ? "tabla" : "galeria";
        if (_vistaExp != "tabla") { _sel.Clear(); CerrarAlta(); }
        await GuardarLocalAsync(VistaExpKey, _vistaExp);
    }

    // ===== Columnas (replica cfgFolders().columnas). FOLDER es la fija. =====
    private static readonly (string Key, string Label, string Ancho)[] ColsDef =
    {
        ("nombre", "FOLDER", "280px"), ("radicado", "RADICADO", "150px"), ("codigo", "CÓDIGO", "110px"),
        ("subfolders", "SUBFOLDERS", "120px"), ("total", "DOCUMENTOS", "130px"), ("avance", "CARGADOS", "200px"),
        ("pend", "OBLIGATORIOS SIN CARGAR", "210px"), ("ultima", "ÚLTIMA CARGA", "150px"),
    };
    private static readonly HashSet<string> ColsNumericas = new() { "subfolders", "total", "avance", "pend" };
    private sealed class ColPref { public string Key { get; set; } = ""; public bool Visible { get; set; } = true; public int? W { get; set; } }
    private static List<ColPref> ColsIniciales() => ColsDef.Select(c => new ColPref { Key = c.Key }).ToList();
    private List<ColPref> _cols = ColsIniciales();
    private static string LabelCol(string k) => ColsDef.FirstOrDefault(c => c.Key == k).Label ?? k;
    private List<(string Key, string Label)> ColsVis => _cols.Where(c => c.Visible).Select(c => (c.Key, LabelCol(c.Key))).ToList();
    private int ColSpanTotal => ColsVis.Count + 3;
    private string AnchoCol(string k) => _cols.FirstOrDefault(c => c.Key == k)?.W is { } w ? $"{w}px" : ColsDef.FirstOrDefault(c => c.Key == k).Ancho ?? "160px";

    private bool _showColMenu;
    private string _colQ = "";
    private async Task MoverCol(string k, int dir)
    {
        var i = _cols.FindIndex(c => c.Key == k); var j = i + dir;
        if (i <= 0 || j <= 0 || j >= _cols.Count) return;   // la 1a columna (FOLDER) queda fija
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
    private async Task RestablecerCol() { _cols = ColsIniciales(); await GuardarColsAsync(); }
    private Task GuardarColsAsync() => GuardarLocalAsync(ColsExpKey, System.Text.Json.JsonSerializer.Serialize(_cols));

    // ---- Mover columna por ARRASTRE (propiaTablaColReorder) y ancho por arrastre (propiaTablaResize) ----
    private DotNetObjectReference<Documentos>? _self;
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
        if (from <= 0 || to <= 0) return;   // FOLDER queda primera
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

    // ===== Valores por columna =====
    private static int PctDe(FolderTarjetaDto t) => t.Total > 0 ? (int)Math.Round(t.Cargados * 100.0 / t.Total) : 0;
    private static string FechaCorta(DateTimeOffset? f) => f is { } x ? x.LocalDateTime.ToString("dd MMM yyyy", Es) : "-";
    private static string? ValorCampo(FolderTarjetaDto t, string k) => k switch
    {
        "nombre" => t.Nombre,
        "radicado" => t.Radicado,
        "codigo" => t.Codigo,
        "subfolders" => t.Subfolders.ToString(Inv),
        "total" => t.Total.ToString(Inv),
        "avance" => PctDe(t).ToString(Inv),
        "pend" => t.PendientesObligatorios.ToString(Inv),
        "ultima" => t.UltimaCarga?.LocalDateTime.ToString("yyyy-MM-dd", Inv),
        _ => ""
    };
    private static string TextoCelda(FolderTarjetaDto t, string k) => k switch
    {
        "radicado" => t.Radicado ?? "-",
        "codigo" => string.IsNullOrEmpty(t.Codigo) ? "-" : t.Codigo,
        "avance" => PctDe(t) + "%",
        "ultima" => FechaCorta(t.UltimaCarga),
        _ => ValorCampo(t, k) ?? ""
    };

    // ===== Buscar + Filtros =====
    private string _q = "";
    private FiltroDinamico? _fd;
    private static readonly List<FiltroDinamico.CampoDef> CamposFiltro = new()
    {
        new() { Key = "nombre", Label = "FOLDER", Kind = "texto" },
        new() { Key = "radicado", Label = "RADICADO", Kind = "texto" },
        new() { Key = "codigo", Label = "CÓDIGO", Kind = "texto" },
        new() { Key = "subfolders", Label = "SUBFOLDERS", Kind = "num" },
        new() { Key = "total", Label = "DOCUMENTOS", Kind = "num" },
        new() { Key = "avance", Label = "CARGADOS", Kind = "num" },
        new() { Key = "pend", Label = "OBLIGATORIOS SIN CARGAR", Kind = "num" },
        new() { Key = "ultima", Label = "ÚLTIMA CARGA", Kind = "fecha" },
    };
    // Busca por folder, codigo y radicado (replica: buscar.campos).
    private List<FolderTarjetaDto> Filtradas()
    {
        var q = _q.Trim();
        return (_tarjetas ?? new()).Where(t =>
            (q.Length == 0 || t.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)
                           || (t.Codigo ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                           || (t.Radicado ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
            && (_fd?.Pasa(campo => ValorCampo(t, campo)) ?? true)).ToList();
    }
    private bool HayFiltroExp => _q.Trim().Length > 0 || (_fd?.Hay ?? false);

    // ===== Ordenar (menu compartido + clic en el encabezado). Sin orden = el del arbol. =====
    private List<TablaOrdenarMenu.Sort> _sorts = new();
    private List<TablaOrdenarMenu.Col> ColsOrdenables() => ColsVis.Select(c => new TablaOrdenarMenu.Col(c.Key, c.Label)).ToList();
    private void OrdenarPorClic(string k)
    {
        var dir = _sorts.Count == 1 && _sorts[0].Key == k ? -_sorts[0].Dir : 1;
        _sorts = new() { new TablaOrdenarMenu.Sort { Key = k, Dir = dir } };
    }
    private static IComparable ValorOrden(FolderTarjetaDto t, string k) => k switch
    {
        "subfolders" => t.Subfolders,
        "total" => t.Total,
        "avance" => PctDe(t),
        "pend" => t.PendientesObligatorios,
        "ultima" => t.UltimaCarga ?? DateTimeOffset.MinValue,
        _ => ValorCampo(t, k) ?? ""
    };
    private IEnumerable<FolderTarjetaDto> Ordenadas()
    {
        var src = Filtradas();
        IEnumerable<FolderTarjetaDto> res = src;
        if (_sorts.Count > 0)
        {
            IOrderedEnumerable<FolderTarjetaDto>? o = null;
            foreach (var s in _sorts)
            {
                Func<FolderTarjetaDto, IComparable> sel = t => ValorOrden(t, s.Key);
                o = o is null ? (s.Dir >= 0 ? src.OrderBy(sel) : src.OrderByDescending(sel))
                              : (s.Dir >= 0 ? o.ThenBy(sel) : o.ThenByDescending(sel));
            }
            res = o!;
        }
        // Los recien creados quedan al final (resaltados) hasta el proximo refresco: OrderBy ESTABLE.
        return _recien.Count == 0 ? res : res.OrderBy(t => _recien.Contains(t.Id) ? 1 : 0);
    }

    // ===== Agrupar =====
    private List<string> _grupos = new();
    private List<TablaAgruparMenu.Col> ColsAgrupables() => ColsVis.Select(c => new TablaAgruparMenu.Col(c.Key, c.Label)).ToList();
    private List<string> Niveles => _grupos.Where(k => ColsVis.Any(c => c.Key == k)).ToList();
    private async Task OnGruposChanged() { await GuardarLocalAsync(GruposExpKey, System.Text.Json.JsonSerializer.Serialize(_grupos)); StateHasChanged(); }
    private IReadOnlyList<NodoGrupo<FolderTarjetaDto>> ArbolGrupos(List<string> niveles)
        => TablaAgrupador.Agrupar(Ordenadas(), niveles, ValorGrupoDe, "(vacio)");
    private static IEnumerable<ValorGrupo> ValorGrupoDe(FolderTarjetaDto t, string k)
    {
        // Titulo de grupo = texto de la celda (como en los demas modulos).
        if (k == "ultima")
        {
            if (t.UltimaCarga is { } u) yield return new ValorGrupo(u.LocalDateTime.ToString("yyyy-MM-dd", Inv), FechaCorta(u), Orden: (int)(u.ToUnixTimeSeconds() / 86400));
            yield break;
        }
        if (ColsNumericas.Contains(k))
        {
            var n = (int)ValorOrden(t, k);
            yield return new ValorGrupo(n.ToString(Inv), TextoCelda(t, k), Orden: n);
            yield break;
        }
        var v = ValorCampo(t, k);
        if (!string.IsNullOrWhiteSpace(v)) yield return new ValorGrupo(v!, v!);
    }

    // ===== Descargar la vista (TablaExportar): columnas visibles en su orden, todas las filas filtradas =====
    private ExportVista VistaExport()
    {
        var cols = ColsVis;
        var columnas = cols.Select(c => new ExportColumna(c.Label, ColsNumericas.Contains(c.Key), ColsNumericas.Contains(c.Key) ? "#,##0" : null)).ToList();
        object? Celda(FolderTarjetaDto t, string k) => ColsNumericas.Contains(k) ? (int)ValorOrden(t, k) : (TextoCelda(t, k) is { Length: > 0 } s && s != "-" ? s : null);
        ExportFila Fila(FolderTarjetaDto t, IReadOnlyList<string>? ruta) => new(cols.Select(col => Celda(t, col.Key)).ToList(), Ruta: ruta);
        var filas = new List<ExportFila>();
        var niveles = Niveles;
        if (niveles.Count == 0) filas.AddRange(Ordenadas().Select(t => Fila(t, null)));
        else
        {
            void Recorrer(IReadOnlyList<NodoGrupo<FolderTarjetaDto>> nodos, List<string> ruta)
            {
                foreach (var n in nodos)
                {
                    filas.Add(ExportFila.DeGrupo(n.Titulo, n.Nivel, n.Conteo));
                    var r = new List<string>(ruta) { n.Titulo };
                    if (n.EsHoja) filas.AddRange(n.Filas.Select(t => Fila(t, r)));
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
        criterios.Add($"{Filtradas().Count} folders");
        return new ExportVista("Expedientes", _copropiedadNombre, columnas, filas, criterios, niveles.Select(LabelCol).ToList());
    }

    // ===== Seleccion multiple + borrado masivo (con sus subfolders; solo si ningun documento tiene archivo) =====
    private readonly HashSet<Guid> _sel = new();
    private bool _confirmDelSel, _delBusy;
    private bool TodasSeleccionadas => Filtradas().Count > 0 && Filtradas().All(t => _sel.Contains(t.Id));
    private void ToggleSelAll(bool on) { _sel.Clear(); if (on) foreach (var t in Filtradas()) _sel.Add(t.Id); }
    private void ToggleSel(Guid id, bool on) { if (on) _sel.Add(id); else _sel.Remove(id); }
    private async Task EliminarSeleccionadasAsync()
    {
        _delBusy = true;
        string? primerError = null;
        foreach (var id in _sel.ToList())
        {
            try
            {
                var r = await Auth().DeleteAsync($"/api/folders/{id}");
                if (!r.IsSuccessStatusCode) primerError ??= await LeerErrorAsync(r, "No se pudo eliminar el folder.");
            }
            catch (Exception ex) { primerError ??= ex.Message; }
        }
        _delBusy = false; _confirmDelSel = false; _sel.Clear();
        await CargarFoldersAsync();
        if (primerError is not null) Toast(primerError, true);
    }

    // ===== Edicion en linea del nombre (PUT MERGE) =====
    private async Task RenombrarAsync(FolderTarjetaDto t, string? raw)
    {
        var v = (raw ?? "").Trim();
        if (v == t.Nombre) return;
        if (v.Length == 0) { Toast("El nombre del folder no puede quedar vacío.", true); await CargarFoldersAsync(); return; }
        try
        {
            var r = await Auth().PutAsJsonAsync($"/api/folders/{t.Id}", new ActualizarFolderRequest(Nombre: v));
            if (!r.IsSuccessStatusCode) { Toast(await LeerErrorAsync(r, "No se pudo guardar."), true); await CargarFoldersAsync(); return; }
            var i = _tarjetas!.FindIndex(x => x.Id == t.Id);
            if (i >= 0) _tarjetas[i] = _tarjetas[i] with { Nombre = v };
        }
        catch (Exception ex) { Toast(ex.Message, true); }
    }

    // ===== Alta en linea ("Agregar folder" = folder raiz; Enter guarda, Esc cancela, Tab/clic fuera guarda) =====
    private string _nuNombre = "";
    private bool _altaAbierta, _creando, _scrollBottomPending;
    private readonly HashSet<Guid> _recien = new();
    private void CerrarAlta() { _altaAbierta = false; _nuNombre = ""; }
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
        if (string.IsNullOrWhiteSpace(_nuNombre)) { CerrarAlta(); StateHasChanged(); return; }
        await CrearDesdeAltaAsync(false);
        StateHasChanged();
    }
    private async Task CrearDesdeAltaAsync(bool abrir)
    {
        if (_creando) return;
        var nombre = _nuNombre.Trim();
        if (nombre.Length == 0) { Toast("Escribe el nombre del folder.", true); return; }
        _creando = true;
        try
        {
            var r = await Auth().PostAsJsonAsync("/api/folders", new CrearFolderRequest(nombre));
            if (!r.IsSuccessStatusCode) { Toast(await LeerErrorAsync(r, "No se pudo crear el folder."), true); return; }
            var nodo = await r.Content.ReadFromJsonAsync<FolderNodoDto>();
            CerrarAlta();
            if (nodo is not null) _recien.Add(nodo.Id);
            await CargarFoldersAsync();
            if (nodo is not null) Toast($"Folder “{nodo.Nombre}” creado con radicado {nodo.Radicado}.", false);
            if (abrir && nodo is not null) _explorarId = nodo.Id;
        }
        catch (Exception ex) { Toast(ex.Message, true); }
        finally { _creando = false; }
    }

    // ===== Clic derecho: Abrir / Copiar enlace / Eliminar / Configurar folder =====
    private bool _ctxOpen;
    private double _ctxX, _ctxY;
    private List<TablaCtxMenu.Item> _ctxItems = new();
    private void AbrirCtx(MouseEventArgs e, FolderTarjetaDto t)
    {
        _ctxX = e.ClientX; _ctxY = e.ClientY;
        _ctxItems = new()
        {
            new("Abrir el folder", "fi-rr-expand", OnClick: () => { AbrirFolder(t); StateHasChanged(); return Task.CompletedTask; }),
            new("Copiar enlace del registro", "fi-rr-link-alt", OnClick: async () => { await CopiarEnlaceAsync(t); StateHasChanged(); }),
            new(Sep: true),
            new("Eliminar registro", "fi-rr-trash", Danger: true, OnClick: () => { _sel.Clear(); _sel.Add(t.Id); _confirmDelSel = true; StateHasChanged(); return Task.CompletedTask; }),
            new(Sep: true),
            new("Configurar folder", "fi-rr-settings", OnClick: () => { _cfgFolderId = t.Id; _showCfg = true; StateHasChanged(); return Task.CompletedTask; }),
        };
        _ctxOpen = true;
    }
    private Guid? _cfgFolderId;
    private async Task CopiarEnlaceAsync(FolderTarjetaDto t)
    {
        var url = Nav.Uri.Split('#')[0] + "#" + t.Id;
        try { await Js.InvokeVoidAsync("navigator.clipboard.writeText", url); Toast("Enlace del registro copiado.", false); }
        catch { Toast("No se pudo copiar el enlace.", true); }
    }
    // "Copiar enlace del registro" lleva #<id>: al abrir ese enlace se abre el folder en el explorador.
    private void AbrirDesdeEnlace()
    {
        var frag = new Uri(Nav.Uri).Fragment.TrimStart('#');
        if (Guid.TryParse(frag, out var id) && _tarjetas?.FirstOrDefault(t => t.Id == id) is { } t) AbrirFolder(t);
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
                if (_fd is not null) await _fd.AgregarReglaCampo(col.Key);
                break;
            case TablaColMenu.Accion.Agrupar: _grupos = new() { col.Key }; await OnGruposChanged(); break;
            case TablaColMenu.Accion.Ocultar:
                if (_cols.FindIndex(c => c.Key == col.Key) == 0) Toast("Esta columna es fija y no se puede ocultar.", false);
                else await ToggleVisibleCol(col.Key);
                break;
            default: Toast("Los folders no tienen campos propios: sus metadatos se definen en Configurar folders.", false); break;
        }
        StateHasChanged();
    }

    // ===== Infra =====
    private string? _toast;
    private bool _toastErr;
    private string? _copropiedadNombre;
    private void Toast(string m, bool err) { _toast = m; _toastErr = err; }
    private async Task GuardarLocalAsync(string k, string v) { try { await Js.InvokeVoidAsync("localStorage.setItem", k, v); } catch { } }
    private static async Task<string> LeerErrorAsync(HttpResponseMessage r, string defecto)
    {
        var txt = await r.Content.ReadAsStringAsync();
        try { var d = System.Text.Json.JsonDocument.Parse(txt); if (d.RootElement.TryGetProperty("error", out var e)) return e.GetString() ?? defecto; } catch { }
        return string.IsNullOrWhiteSpace(txt) ? defecto : txt;
    }
    private async Task CargarPreferenciasExpAsync()
    {
        try
        {
            _copropiedadNombre = await Js.InvokeAsync<string?>("localStorage.getItem", "propia_copropiedad_nombre");
            var v = await Js.InvokeAsync<string?>("localStorage.getItem", VistaExpKey);
            if (v is "tabla" or "galeria") _vistaExp = v;
            var g = await Js.InvokeAsync<string?>("localStorage.getItem", GruposExpKey);
            if (!string.IsNullOrWhiteSpace(g)) _grupos = System.Text.Json.JsonSerializer.Deserialize<List<string>>(g) ?? new();
            var c = await Js.InvokeAsync<string?>("localStorage.getItem", ColsExpKey);
            if (!string.IsNullOrWhiteSpace(c) && System.Text.Json.JsonSerializer.Deserialize<List<ColPref>>(c) is { Count: > 0 } guard)
            {
                // Conserva orden/visibilidad/ancho guardados; columnas nuevas al final; FOLDER siempre 1a y visible.
                var conocidas = guard.Where(x => ColsDef.Any(d => d.Key == x.Key)).GroupBy(x => x.Key).Select(x => x.First()).ToList();
                conocidas.AddRange(ColsDef.Where(d => conocidas.All(x => x.Key != d.Key)).Select(d => new ColPref { Key = d.Key }));
                var fija = conocidas.First(x => x.Key == "nombre"); fija.Visible = true;
                conocidas.Remove(fija); conocidas.Insert(0, fija);
                _cols = conocidas;
            }
        }
        catch { }
    }

    // ===== Ganchos JS de la tabla (lotes / FAB / guardado por blur del alta) =====
    private string? _wiredSig;
    private bool _ctaVisible = true;
    [JSInvokable] public Task CargarMas() => Task.CompletedTask;   // sin lotes: se pinta toda la lista
    [JSInvokable] public void SetCtaVisible(bool visible) { if (_ctaVisible != visible) { _ctaVisible = visible; StateHasChanged(); } }

    private async Task DespuesDeRenderBandejaAsync()
    {
        if (_jwt is not null && _view == "folders" && _vistaExp == "tabla" && _tarjetas is not null)
        {
            var sig = $"{_altaAbierta}|{Filtradas().Count}|{Niveles.Count}|{ColsVis.Count}";
            if (sig != _wiredSig)
            {
                _wiredSig = sig;
                _self ??= DotNetObjectReference.Create(this);
                try { await Js.InvokeVoidAsync("propiaUI.dstInfinite", _self, ".xpd .dst-scroll.tblscroll"); } catch { }
                if (_altaAbierta) { try { await Js.InvokeVoidAsync("propiaUI.altaAutoSave", _self, ".newrow--edit", "AltaBlurGuardar", ".exp-pop"); } catch { } }
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
            try { await Js.InvokeVoidAsync("propiaUI.dstScrollToCta", ".xpd .dst-scroll.tblscroll"); } catch { }
        }
    }

    public void Dispose()
    {
        try { _ = Js.InvokeVoidAsync("propiaUI.altaAutoSaveOff"); } catch { }
        _self?.Dispose();
    }
}
