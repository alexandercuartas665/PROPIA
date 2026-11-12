using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Propia.Application.Asambleas;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Enums;
using Propia.Web.Components.Shared;
using Propia.Web.Components.Shared.Asambleas;
using Propia.Web.Components.Shared.Modals;

namespace Propia.Web.Components.Pages.Capa2;

/// <summary>
/// Bandeja de Asambleas (replica RP.modAsambleas, PROPUESTAS.md 7.1): vista canonica de Unidades de SOLO LECTURA
/// (sin alta en linea; "+ Nueva sesion" abre el asistente). Columnas: Titulo (con las insignias 2da convocatoria /
/// Acta publicada), Tipo, Modalidad, Fecha, Estado, Puntos y Participantes; orden inicial fecha descendente.
/// Orden/visibilidad/ancho de columnas por copropiedad (unidades-config, entidad 'asamblea'); vista, agrupacion y
/// totales por navegador (localStorage). Eliminar solo aplica a sesiones en Borrador o Canceladas.
/// </summary>
public partial class Asambleas
{
    [Parameter] public Guid? Id { get; set; }

    private const string VistaKey = "propia_view_asambleas", GruposKey = "propia_asambleas_grupos", AggKey = "propia_agregado_asambleas";
    private static readonly CultureInfo Es = new("es-CO");

    private string? _jwt;
    private bool _authChecked;
    private string? _msg; private string _msgClass = "alert-info";
    private SesionBandejaDto? _b;
    private string? _copropiedadNombre;
    private IReadOnlyList<SesionListaDto> Items => _b?.Items ?? Array.Empty<SesionListaDto>();

    // ===================== Carga + enlaces profundos =====================
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _jwt is not null) return;
        try
        {
            var stored = await Js.InvokeAsync<string?>("sessionStorage.getItem", "propia_jwt");
            _authChecked = true;
            if (string.IsNullOrWhiteSpace(stored)) { StateHasChanged(); return; }
            _jwt = stored;
            await CargarPreferenciasAsync();
            await CargarAsync();
            StateHasChanged();
            await AbrirDesdeRutaAsync();
        }
        catch { }
    }

    private bool _rutaAtendida;
    protected override async Task OnParametersSetAsync()
    {
        // Navegar a /asambleas/{Id} o /asambleas/nueva con la bandeja ya cargada (p. ej. desde una notificacion).
        if (_jwt is not null && _b is not null) { _rutaAtendida = false; await AbrirDesdeRutaAsync(); }
    }

    private async Task AbrirDesdeRutaAsync()
    {
        if (_rutaAtendida) return;
        _rutaAtendida = true;
        if (Id is Guid id) await AbrirFichaAsync(id, desdeRuta: true);
        else if (Nav.Uri.TrimEnd('/').EndsWith("/asambleas/nueva", StringComparison.OrdinalIgnoreCase)) await AbrirNuevaAsync();
    }

    private async Task CargarAsync()
    {
        try
        {
            _b = await Auth().GetFromJsonAsync<SesionBandejaDto>("/api/asambleas");
            await CargarConfigColsAsync();
            _sel.RemoveWhere(id => Items.All(s => s.Id != id));
        }
        catch (Exception ex) { _msg = ex.Message; _msgClass = "alert-danger"; }
    }

    // ===================== Ficha y asistente en MODAL =====================
    private bool _modalAbierto;
    private async Task AbrirFichaAsync(Guid id, string? tab = null, bool desdeRuta = false)
    {
        if (_modalAbierto) return;
        _modalAbierto = true;
        try
        {
            // Navegacion < n/total > entre las sesiones de la vista (orden y filtros de la bandeja).
            var lista = Ordenadas().Select(s => s.Id).ToList();
            var p = new ModalParameters().Set("Jwt", _jwt).Set("SesionId", id).Set("Lista", lista).Set("Tab", tab);
            var opts = new ModalOptions { Width = "1056px", Height = "86vh", CssClass = "is-fixedh asm-modal" };
            var cambio = await Modals.OpenAsync<AsambleaFichaModal, bool>(p, opts);
            if (cambio) await CargarAsync();
        }
        finally
        {
            _modalAbierto = false;
            if (desdeRuta) Nav.NavigateTo("/asambleas", replace: true);
            StateHasChanged();
        }
    }

    private async Task AbrirNuevaAsync()
    {
        if (_modalAbierto || string.IsNullOrEmpty(_jwt)) return;
        _modalAbierto = true;
        Guid? creada = null;
        try
        {
            var opts = new ModalOptions { Width = "1056px", Height = "86vh", CssClass = "is-fixedh asm-modal", CloseOnBackdrop = false };
            creada = await Modals.OpenAsync<AsambleaNuevaModal, Guid?>(new ModalParameters().Set("Jwt", _jwt), opts);
        }
        finally
        {
            _modalAbierto = false;
            if (Nav.Uri.TrimEnd('/').EndsWith("/asambleas/nueva", StringComparison.OrdinalIgnoreCase)) Nav.NavigateTo("/asambleas", replace: true);
        }
        if (creada is Guid id)
        {
            await CargarAsync();
            StateHasChanged();
            await AbrirFichaAsync(id);
        }
    }

    // ===================== Organos de gobierno (consejo, comites, revisor fiscal) =====================
    // "Asambleas y Organos de Gobierno" (hueco #12): el mismo modal de la pagina /gobierno, desde la bandeja.
    private async Task AbrirGobiernoAsync()
    {
        if (_modalAbierto || string.IsNullOrEmpty(_jwt)) return;
        _modalAbierto = true;
        try { await Modals.OpenAsync<GestionarGobiernoModal, bool>(new ModalParameters().Set("Jwt", _jwt), new ModalOptions { Width = "920px" }); }
        finally { _modalAbierto = false; }
    }

    // ===================== Configuracion de asambleas (modal) =====================
    private sealed class ConfigVm
    {
        public CriterioMayoria Criterio { get; set; }
        public int PlazoCitacionDias { get; set; }
        public int LimitePoderes { get; set; }
        public int GraciaReconexionSeg { get; set; }
        public int NotifRecordatorioDias { get; set; }
    }
    private ConfigVm? _asmCfg;
    private string? _asmCfgError;
    private bool _asmCfgBusy;

    private async Task AbrirConfigAsync()
    {
        _asmCfgError = null;
        try
        {
            var c = await Auth().GetFromJsonAsync<AsambleaConfigDto>("/api/asambleas/config");
            if (c is null) return;
            _asmCfg = new ConfigVm
            {
                Criterio = c.CriterioMayoria,
                PlazoCitacionDias = c.PlazoCitacionDias,
                LimitePoderes = c.LimitePoderesPorPersona ?? 0,
                GraciaReconexionSeg = c.GraciaReconexionSeg,
                NotifRecordatorioDias = c.NotifRecordatorioDias
            };
        }
        catch (Exception ex) { _msg = ex.Message; _msgClass = "alert-danger"; }
    }

    private async Task GuardarConfigAsync()
    {
        if (_asmCfg is null) return;
        _asmCfgBusy = true; _asmCfgError = null;
        try
        {
            var req = new ActualizarAsambleaConfigRequest(_asmCfg.PlazoCitacionDias, _asmCfg.LimitePoderes,
                _asmCfg.GraciaReconexionSeg, _asmCfg.NotifRecordatorioDias, _asmCfg.Criterio);
            var resp = await Auth().PutAsJsonAsync("/api/asambleas/config", req);
            if (resp.IsSuccessStatusCode)
            {
                _asmCfg = null;
                _msg = "Configuración de asambleas guardada."; _msgClass = "alert-success";
            }
            else if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                _asmCfgError = "No tienes permiso para cambiar la configuración de asambleas.";
            else _asmCfgError = await LeerErrorAsync(resp, "No se pudo guardar la configuración.");
        }
        catch (Exception ex) { _asmCfgError = ex.Message; }
        finally { _asmCfgBusy = false; }
    }

    // ===================== Vista (Tarjetas | Tabla) =====================
    private string _vista = "tabla";
    private static readonly TablaVistaToggle.Opcion[] VistasOpc = { TablaVistaToggle.Tarjetas("tarjetas"), TablaVistaToggle.Tabla("tabla") };
    private async Task SetVista(string v)
    {
        _vista = v == "tarjetas" ? "tarjetas" : "tabla";
        if (_vista != "tabla") _sel.Clear();
        await GuardarLocalAsync(VistaKey, _vista);
    }

    // ===================== Columnas (orden/visibilidad/ancho por copropiedad) =====================
    private sealed record ColDef(string Key, string Label, bool Numeric, int Ancho);
    private static readonly ColDef[] Catalogo =
    {
        new("titulo", "Titulo", false, 320), new("tipo", "Tipo", false, 180), new("modalidad", "Modalidad", false, 140),
        new("fecha", "Fecha", false, 160), new("estado", "Estado", false, 150), new("puntos", "Puntos", true, 100),
        new("participantes", "Participantes", true, 130),
    };
    private List<string> _colOrder = Catalogo.Select(c => c.Key).ToList();
    private readonly HashSet<string> _colHidden = new();
    private readonly Dictionary<string, int> _colW = new(StringComparer.Ordinal);
    private Dictionary<string, UnidadCampoConfigDto> _cfg = new(StringComparer.Ordinal);
    private const string EntCfg = "asamblea";
    private bool _showColMenu;
    private string _colQ = "";

    private IEnumerable<string> ColsVisibles => _colOrder.Where(k => !_colHidden.Contains(k));
    private int ColSpanTotal => ColsVisibles.Count() + 3;
    private static string ColLabel(string k) => Catalogo.First(c => c.Key == k).Label;
    private static bool EsNum(string k) => Catalogo.First(c => c.Key == k).Numeric;
    private string ColStyle(string k) => $"width:{(_colW.TryGetValue(k, out var w) ? w : Catalogo.First(c => c.Key == k).Ancho)}px";

    private UnidadCampoConfigDto? CfgDe(string k) => _cfg.TryGetValue(k, out var c) ? c : null;
    private GuardarUnidadCampoConfigRequest FilaConfig(string clave)
    {
        var c = CfgDe(clave);
        return new GuardarUnidadCampoConfigRequest(clave, c?.Alias, c?.Opciones, c?.Tipo, c?.Formato, Oculto: c?.Oculto ?? false, Orden: c?.Orden, Entidad: EntCfg, Descripcion: c?.Descripcion);
    }
    private async Task GuardarCfg(GuardarUnidadCampoConfigRequest req)
    { try { await Auth().PutAsJsonAsync("/api/mi-copropiedad/unidades-config", req); } catch { } }

    private async Task CargarConfigColsAsync()
    {
        try
        {
            var lista = await Auth().GetFromJsonAsync<List<UnidadCampoConfigDto>>($"/api/mi-copropiedad/unidades-config?entidad={EntCfg}") ?? new();
            _cfg = lista.ToDictionary(c => c.CampoClave, StringComparer.Ordinal);
        }
        catch { _cfg = new(StringComparer.Ordinal); }
        // TITULO queda siempre primero y visible (columna fija).
        _colOrder = Catalogo.Skip(1).Select((c, i) => (c.Key, ord: CfgDe(c.Key)?.Orden ?? (1000 + i)))
                            .OrderBy(x => x.ord).Select(x => x.Key).Prepend("titulo").ToList();
        _colHidden.Clear();
        foreach (var c in Catalogo.Skip(1)) if (CfgDe(c.Key)?.Oculto ?? false) _colHidden.Add(c.Key);
        _colW.Clear();
        foreach (var kv in _cfg) if (AnchoDeFormato(kv.Value.Formato) is int w and > 0) _colW[kv.Key] = w;
    }
    private async Task GuardarOrdenAsync(IReadOnlyList<string> orden)
    {
        for (var i = 0; i < orden.Count; i++) await GuardarCfg(FilaConfig(orden[i]) with { Orden = i });
        await CargarConfigColsAsync();
    }
    private async Task MoverCol(int idx, int delta)
    {
        var j = idx + delta;
        if (idx <= 0 || j <= 0 || j >= _colOrder.Count) return;   // Titulo queda fijo
        var orden = _colOrder.ToList();
        (orden[idx], orden[j]) = (orden[j], orden[idx]);
        await GuardarOrdenAsync(orden);
    }
    private async Task SetColVisible(string key, bool visible)
    {
        if (key == "titulo") return;
        if (visible) _colHidden.Remove(key); else _colHidden.Add(key);
        await GuardarCfg(FilaConfig(key) with { Oculto = !visible });
        await CargarConfigColsAsync();
    }
    private async Task MostrarTodo() { foreach (var c in Catalogo.Skip(1)) await GuardarCfg(FilaConfig(c.Key) with { Oculto = false }); await CargarConfigColsAsync(); }
    private async Task OcultarTodo() { foreach (var c in Catalogo.Skip(1)) await GuardarCfg(FilaConfig(c.Key) with { Oculto = true }); await CargarConfigColsAsync(); }
    private async Task RestablecerCol()
    {
        foreach (var c in Catalogo) await GuardarCfg(FilaConfig(c.Key) with { Oculto = false, Orden = null, Formato = ConAncho(CfgDe(c.Key)?.Formato, null) });
        await CargarConfigColsAsync();
    }

    private static int? AnchoDeFormato(string? json)
    { try { var o = System.Text.Json.Nodes.JsonNode.Parse(json ?? "") as System.Text.Json.Nodes.JsonObject; return o?["w"]?.GetValue<int>(); } catch { return null; } }
    private static string? ConAncho(string? json, int? w)
    {
        System.Text.Json.Nodes.JsonObject o;
        try { o = string.IsNullOrWhiteSpace(json) ? new() : (System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject ?? new()); }
        catch { o = new(); }
        if (w is null) o.Remove("w"); else o["w"] = w.Value;
        return o.Count == 0 ? null : o.ToJsonString();
    }

    // ---- Mover columna por arrastre (propiaTablaColReorder) y ancho por arrastre (propiaTablaResize) ----
    private DotNetObjectReference<Asambleas>? _self;
    private async Task IniciarReorder(MouseEventArgs e, string k)
    { _self ??= DotNetObjectReference.Create(this); try { await Js.InvokeVoidAsync("propiaTablaColReorder.start", _self, k, e.ClientX, e.ClientY); } catch { } }
    [JSInvokable]
    public async Task OnColReorder(string src, string dest)
    {
        if (src == dest || src == "titulo" || dest == "titulo") return;
        var orden = _colOrder.ToList();
        var from = orden.IndexOf(src); var to = orden.IndexOf(dest);
        if (from < 0 || to < 0) return;
        orden.RemoveAt(from); orden.Insert(to, src);
        await GuardarOrdenAsync(orden);
        StateHasChanged();
    }
    private async Task IniciarResize(MouseEventArgs e, string k)
    { _self ??= DotNetObjectReference.Create(this); try { await Js.InvokeVoidAsync("propiaTablaResize.start", _self, k, e.ClientX); } catch { } }
    [JSInvokable]
    public async Task OnColResize(string clave, int width)
    {
        int? w = width <= 0 ? null : Math.Max(70, width);
        if (w is null) _colW.Remove(clave); else _colW[clave] = w.Value;
        StateHasChanged();
        await GuardarCfg(FilaConfig(clave) with { Formato = ConAncho(CfgDe(clave)?.Formato, w) });
        await CargarConfigColsAsync();
    }

    // ===================== Valores por columna =====================
    private static string TipoTexto(TipoSesion t) => t switch
    {
        TipoSesion.AsambleaOrdinaria => "Asamblea ordinaria",
        TipoSesion.AsambleaExtraordinaria => "Asamblea extraordinaria",
        TipoSesion.Consejo => "Consejo de administración",
        TipoSesion.Comite => "Comité",
        _ => t.ToString()
    };
    private static string EstadoTexto(EstadoSesion e) => e switch
    {
        EstadoSesion.Borrador => "Borrador",
        EstadoSesion.Citada => "Citada",
        EstadoSesion.EnCurso => "En curso",
        EstadoSesion.Cerrada => "Cerrada",
        EstadoSesion.QuorumFallido => "Quórum fallido",
        EstadoSesion.Cancelada => "Cancelada",
        _ => ""
    };
    // Valor crudo para filtrar/agrupar: el texto visible (como la replica: filtro por TipoTexto/EstadoTexto).
    private static string? ValorCampo(SesionListaDto s, string k) => k switch
    {
        "titulo" => s.Titulo,
        "tipo" => TipoTexto(s.Tipo),
        "modalidad" => s.Modalidad.ToString(),
        "fecha" => s.FechaSesion.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "estado" => EstadoTexto(s.Estado),
        "puntos" => s.CantidadPuntos.ToString(CultureInfo.InvariantCulture),
        "participantes" => s.CantidadParticipantes.ToString(CultureInfo.InvariantCulture),
        _ => null,
    };
    private static string TextoCelda(SesionListaDto s, string k) => k switch
    {
        "fecha" => s.FechaSesion.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        _ => ValorCampo(s, k) ?? ""
    };

    // ===================== Buscar + Filtros =====================
    private string _q = "";
    private FiltroDinamico? _fd;
    private static readonly List<FiltroDinamico.CampoDef> CamposFiltro = new()
    {
        new() { Key = "titulo", Label = "Titulo", Kind = "texto" },
        new() { Key = "tipo", Label = "Tipo", Kind = "sel", Opciones = Enum.GetValues<TipoSesion>().Select(t => (TipoTexto(t), TipoTexto(t))).ToList() },
        new() { Key = "modalidad", Label = "Modalidad", Kind = "sel", Opciones = Enum.GetValues<ModalidadSesion>().Select(m => (m.ToString(), m.ToString())).ToList() },
        new() { Key = "fecha", Label = "Fecha", Kind = "fecha" },
        new() { Key = "estado", Label = "Estado", Kind = "sel", Opciones = Enum.GetValues<EstadoSesion>().Select(e => (EstadoTexto(e), EstadoTexto(e))).ToList() },
        new() { Key = "puntos", Label = "Puntos", Kind = "num" },
        new() { Key = "participantes", Label = "Participantes", Kind = "num" },
    };
    // Busca por titulo, tipo y estado (replica: buscar.campos).
    private List<SesionListaDto> Filtradas()
    {
        var q = _q.Trim();
        return Items.Where(s =>
            (q.Length == 0 || s.Titulo.Contains(q, StringComparison.OrdinalIgnoreCase)
                           || TipoTexto(s.Tipo).Contains(q, StringComparison.OrdinalIgnoreCase)
                           || EstadoTexto(s.Estado).Contains(q, StringComparison.OrdinalIgnoreCase))
            && (_fd?.Pasa(campo => ValorCampo(s, campo)) ?? true)).ToList();
    }

    // ===================== Ordenar (menu + clic en el encabezado). Inicial: fecha descendente =====================
    private List<TablaOrdenarMenu.Sort> _sorts = new() { new TablaOrdenarMenu.Sort { Key = "fecha", Dir = -1 } };
    private List<TablaOrdenarMenu.Col> ColsOrdenables() => ColsVisibles.Select(k => new TablaOrdenarMenu.Col(k, ColLabel(k))).ToList();
    private void OrdenarPorClic(string k)
    {
        var dir = _sorts.Count == 1 && _sorts[0].Key == k ? -_sorts[0].Dir : 1;
        _sorts = new() { new TablaOrdenarMenu.Sort { Key = k, Dir = dir } };
    }
    private static IComparable ValorOrden(SesionListaDto s, string k) => k switch
    {
        "fecha" => s.FechaSesion,
        "puntos" => s.CantidadPuntos,
        "participantes" => s.CantidadParticipantes,
        _ => ValorCampo(s, k) ?? ""
    };
    private IEnumerable<SesionListaDto> Ordenadas()
    {
        var src = Filtradas();
        if (_sorts.Count == 0) return src;
        IOrderedEnumerable<SesionListaDto>? o = null;
        foreach (var s in _sorts)
        {
            Func<SesionListaDto, IComparable> sel = x => ValorOrden(x, s.Key);
            o = o is null ? (s.Dir >= 0 ? src.OrderBy(sel) : src.OrderByDescending(sel))
                          : (s.Dir >= 0 ? o.ThenBy(sel) : o.ThenByDescending(sel));
        }
        return o!;
    }

    // ===================== Agrupar (2 niveles) + totales por columna =====================
    private List<string> _grupos = new();
    private List<TablaAgruparMenu.Col> ColsAgrupables() => ColsVisibles.Select(k => new TablaAgruparMenu.Col(k, ColLabel(k))).ToList();
    private List<string> Niveles => _grupos.Where(k => ColsVisibles.Contains(k)).ToList();
    private async Task OnGruposChanged() { await GuardarLocalAsync(GruposKey, System.Text.Json.JsonSerializer.Serialize(_grupos)); StateHasChanged(); }
    private IReadOnlyList<NodoGrupo<SesionListaDto>> ArbolGrupos(List<string> niveles)
        => TablaAgrupador.Agrupar(Ordenadas(), niveles, ValorGrupoDe, "(vacio)");
    private static IEnumerable<ValorGrupo> ValorGrupoDe(SesionListaDto s, string k)
    {
        if (k == "fecha")
        {
            var d = s.FechaSesion.LocalDateTime.Date;
            yield return new ValorGrupo(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Orden: (int)(d.Ticks / TimeSpan.TicksPerDay));
            yield break;
        }
        if (EsNum(k))
        {
            var n = (int)ValorOrden(s, k);
            yield return new ValorGrupo(n.ToString(CultureInfo.InvariantCulture), n.ToString(CultureInfo.InvariantCulture), Orden: n);
            yield break;
        }
        var v = ValorCampo(s, k);
        if (!string.IsNullOrWhiteSpace(v)) yield return new ValorGrupo(v!, v!);
    }
    private static IReadOnlyList<SesionListaDto> TodasFilas(NodoGrupo<SesionListaDto> n)
    {
        if (n.EsHoja) return n.Filas;
        var acc = new List<SesionListaDto>();
        foreach (var s in n.Subgrupos) acc.AddRange(TodasFilas(s));
        return acc;
    }
    // Vista Tarjetas agrupada: titulos de grupo intercalados con las filas.
    private sealed record ItemLista(NodoGrupo<SesionListaDto>? Grupo, SesionListaDto? Fila);
    private static IEnumerable<ItemLista> Aplanar(IReadOnlyList<NodoGrupo<SesionListaDto>> nodos)
    {
        foreach (var n in nodos)
        {
            yield return new ItemLista(n, null);
            if (n.EsHoja) foreach (var f in n.Filas) yield return new ItemLista(null, f);
            else foreach (var x in Aplanar(n.Subgrupos)) yield return x;
        }
    }

    private readonly Dictionary<string, string> _agg = new(StringComparer.Ordinal);
    private string ModoActivo(string key) => _agg.TryGetValue(key, out var m) ? m : (EsNum(key) ? "suma" : "off");
    private static int ModoCodigo(string modo) => modo switch { "suma" => 1, "prom" => 2, "cont" => 3, "min" => 4, "max" => 5, _ => 0 };
    private TablaColMenu.AggOferta AggOferta(string key) => new(EsNum(key), ModoCodigo(ModoActivo(key)));
    private async Task FijarAgg(string key, string modo)
    {
        _agg[key] = modo;
        await GuardarLocalAsync(AggKey, string.Join(";", _agg.Select(kv => kv.Key + "=" + kv.Value)));
    }
    private static string TituloAgg(string tag) => tag switch
    {
        "PROM" => "Promedio del grupo",
        "MIN" => "Minimo del grupo",
        "MAX" => "Maximo del grupo",
        "CONT" => "Conteo (no vacios) del grupo",
        _ => "Suma del grupo"
    };
    private (bool Hay, string Texto, string Tag) AggCelda(IReadOnlyList<SesionListaDto> miembros, string key)
    {
        var modo = ModoActivo(key);
        if (modo == "off") return (false, "", "");
        if (modo == "cont") return (true, miembros.Count(s => !string.IsNullOrWhiteSpace(ValorCampo(s, key))).ToString(Es), "CONT");
        if (!EsNum(key) || miembros.Count == 0) return (false, "", "");
        var vals = miembros.Select(s => (decimal)(int)ValorOrden(s, key)).ToList();
        var (res, tag) = modo switch
        {
            "prom" => (vals.Average(), "PROM"),
            "min" => (vals.Min(), "MIN"),
            "max" => (vals.Max(), "MAX"),
            _ => (vals.Sum(), "")
        };
        return (true, Math.Round(res, 2, MidpointRounding.AwayFromZero).ToString("0.##", Es), tag);
    }

    // ===================== Descargar la vista (TablaExportar) =====================
    private ExportVista VistaExport()
    {
        var cols = ColsVisibles.ToList();
        var columnas = cols.Select(k => new ExportColumna(ColLabel(k), EsNum(k), EsNum(k) ? "#,##0" : null)).ToList();
        object? CeldaExp(SesionListaDto s, string k)
        {
            if (EsNum(k)) return (int)ValorOrden(s, k);
            var t = k == "titulo" ? s.Titulo + (s.SegundaConvocatoria ? " - 2da convocatoria" : "") + (s.ActaPublicada ? " - Acta publicada" : "") : TextoCelda(s, k);
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }
        ExportFila Fila(SesionListaDto s, IReadOnlyList<string>? ruta) => new(cols.Select(k => CeldaExp(s, k)).ToList(), Ruta: ruta);
        var filas = new List<ExportFila>();
        var niveles = Niveles;
        if (niveles.Count == 0) filas.AddRange(Ordenadas().Select(s => Fila(s, null)));
        else
        {
            void Recorrer(IReadOnlyList<NodoGrupo<SesionListaDto>> nodos, List<string> ruta)
            {
                foreach (var n in nodos)
                {
                    filas.Add(ExportFila.DeGrupo(n.Titulo, n.Nivel, n.Conteo));
                    var r = new List<string>(ruta) { n.Titulo };
                    if (n.EsHoja) filas.AddRange(n.Filas.Select(s => Fila(s, r)));
                    else Recorrer(n.Subgrupos, r);
                }
            }
            Recorrer(ArbolGrupos(niveles), new());
        }
        var criterios = new List<string>();
        if (!string.IsNullOrWhiteSpace(_q)) criterios.Add($"Búsqueda: \"{_q.Trim()}\"");
        if (_fd?.Resumen() is { Length: > 0 } fr) criterios.Add("Filtros: " + fr);
        if (_sorts.Count > 0) criterios.Add("Orden: " + string.Join(", ", _sorts.Select(s => $"{ColLabel(s.Key)} {(s.Dir >= 0 ? "ascendente" : "descendente")}")));
        if (niveles.Count > 0) criterios.Add("Agrupado por: " + string.Join(" > ", niveles.Select(ColLabel)));
        criterios.Add($"{Filtradas().Count} asambleas");
        return new ExportVista("Asambleas y Organos de Gobierno", _copropiedadNombre, columnas, filas, criterios, niveles.Select(ColLabel).ToList());
    }

    // ===================== Seleccion + eliminar (solo Borrador o Cancelada) =====================
    private readonly HashSet<Guid> _sel = new();
    private bool _confirmDel, _delBusy;
    private bool TodasSeleccionadas => Filtradas().Count > 0 && Filtradas().All(s => _sel.Contains(s.Id));
    private void ToggleSelAll(bool on) { _sel.Clear(); if (on) foreach (var s in Filtradas()) _sel.Add(s.Id); }
    private void ToggleSel(Guid id, bool on) { if (on) _sel.Add(id); else _sel.Remove(id); }
    private async Task EliminarSeleccionadasAsync()
    {
        _delBusy = true;
        var noBorrables = 0;
        string? error = null;
        foreach (var id in _sel.ToList())
        {
            var s = Items.FirstOrDefault(x => x.Id == id);
            if (s is null) continue;
            if (s.Estado != EstadoSesion.Borrador && s.Estado != EstadoSesion.Cancelada) { noBorrables++; continue; }
            try
            {
                var r = await Auth().DeleteAsync($"/api/asambleas/{id}");
                if (!r.IsSuccessStatusCode) error ??= r.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "No tienes permiso para eliminar sesiones." : await LeerErrorAsync(r, "No se pudo eliminar la sesión.");
            }
            catch (Exception ex) { error ??= ex.Message; }
        }
        _delBusy = false; _confirmDel = false; _sel.Clear();
        await CargarAsync();
        if (error is not null) Toast(error, true);
        else if (noBorrables > 0) Toast($"{noBorrables} sesión(es) no se eliminaron: solo se borran Borrador o Canceladas.", true);
    }

    // ===================== Clic derecho: Abrir / Copiar enlace / Eliminar =====================
    private bool _ctxOpen;
    private double _ctxX, _ctxY;
    private List<TablaCtxMenu.Item> _ctxItems = new();
    private void AbrirCtx(MouseEventArgs e, SesionListaDto s)
    {
        _ctxX = e.ClientX; _ctxY = e.ClientY;
        _ctxItems = new()
        {
            new("Abrir la asamblea", "fi-rr-expand", OnClick: async () => { await AbrirFichaAsync(s.Id); }),
            new("Copiar enlace del registro", "fi-rr-link-alt", OnClick: async () => { await CopiarEnlaceAsync(s); StateHasChanged(); }),
            new(Sep: true),
            new("Eliminar sesión", "fi-rr-trash", Danger: true, OnClick: () =>
            {
                if (s.Estado != EstadoSesion.Borrador && s.Estado != EstadoSesion.Cancelada)
                    Toast("Solo se pueden eliminar sesiones en Borrador o Canceladas.", true);
                else { _sel.Clear(); _sel.Add(s.Id); _confirmDel = true; }
                StateHasChanged();
                return Task.CompletedTask;
            }),
        };
        _ctxOpen = true;
    }
    private async Task CopiarEnlaceAsync(SesionListaDto s)
    {
        var url = Nav.BaseUri + "asambleas/" + s.Id;
        try { await Js.InvokeVoidAsync("navigator.clipboard.writeText", url); Toast("Enlace del registro copiado.", false); }
        catch { Toast("No se pudo copiar el enlace.", true); }
    }

    // ===================== Menu de columna (flecha del encabezado) =====================
    private TablaColMenu? _colMenu;
    private async Task OnColAccion((TablaColMenu.Accion Accion, TablaColMenu.Ctx Col) a)
    {
        var (accion, col) = a;
        switch (accion)
        {
            case TablaColMenu.Accion.OrdenarAsc: _sorts = new() { new TablaOrdenarMenu.Sort { Key = col.Key, Dir = 1 } }; break;
            case TablaColMenu.Accion.OrdenarDesc: _sorts = new() { new TablaOrdenarMenu.Sort { Key = col.Key, Dir = -1 } }; break;
            case TablaColMenu.Accion.Filtrar: if (_fd is not null) await _fd.AgregarReglaCampo(col.Key); break;
            case TablaColMenu.Accion.Agrupar: _grupos = new() { col.Key }; await OnGruposChanged(); break;
            case TablaColMenu.Accion.Ocultar:
                if (col.Key == "titulo") Toast("Esta columna es fija y no se puede ocultar.", false);
                else await SetColVisible(col.Key, false);
                break;
            case TablaColMenu.Accion.AgregarSuma: await FijarAgg(col.Key, "suma"); break;
            case TablaColMenu.Accion.AgregarPromedio: await FijarAgg(col.Key, "prom"); break;
            case TablaColMenu.Accion.AgregarMinimo: await FijarAgg(col.Key, "min"); break;
            case TablaColMenu.Accion.AgregarMaximo: await FijarAgg(col.Key, "max"); break;
            case TablaColMenu.Accion.AgregarConteo: await FijarAgg(col.Key, "cont"); break;
            case TablaColMenu.Accion.AgregarNinguno: await FijarAgg(col.Key, "off"); break;
            default: Toast("Las columnas de las asambleas son del sistema: no se editan ni se eliminan.", false); break;
        }
        StateHasChanged();
    }

    // ===================== Infra =====================
    private string? _toast;
    private bool _toastErr;
    private void Toast(string m, bool err) { _toast = m; _toastErr = err; }
    private async Task GuardarLocalAsync(string k, string v) { try { await Js.InvokeVoidAsync("localStorage.setItem", k, v); } catch { } }
    private static async Task<string> LeerErrorAsync(HttpResponseMessage r, string defecto)
    {
        var txt = await r.Content.ReadAsStringAsync();
        try { var d = System.Text.Json.JsonDocument.Parse(txt); if (d.RootElement.TryGetProperty("error", out var e)) return e.GetString() ?? defecto; } catch { }
        return string.IsNullOrWhiteSpace(txt) ? defecto : txt;
    }
    private async Task CargarPreferenciasAsync()
    {
        try
        {
            _copropiedadNombre = await Js.InvokeAsync<string?>("localStorage.getItem", "propia_copropiedad_nombre");
            var v = await Js.InvokeAsync<string?>("localStorage.getItem", VistaKey);
            if (v is "tabla" or "tarjetas") _vista = v;
            var g = await Js.InvokeAsync<string?>("localStorage.getItem", GruposKey);
            if (!string.IsNullOrWhiteSpace(g)) _grupos = System.Text.Json.JsonSerializer.Deserialize<List<string>>(g) ?? new();
            var a = await Js.InvokeAsync<string?>("localStorage.getItem", AggKey);
            _agg.Clear();
            if (!string.IsNullOrWhiteSpace(a))
                foreach (var par in a.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                { var i = par.IndexOf('='); if (i > 0) _agg[par[..i]] = par[(i + 1)..]; }
        }
        catch { }
    }
    private HttpClient Auth()
    {
        var http = HttpFactory.CreateClient("PropiaApi");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwt!);
        return http;
    }
    public void Dispose() { _self?.Dispose(); }
}
