using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Propia.Application.ServiciosPublicos;
using Propia.Domain.Enums;
using Propia.Web.Components.Pages.Capa2.Spx;
using Propia.Web.Components.Shared.IaIngesta;
using Propia.Web.Components.Shared.Modals;

namespace Propia.Web.Components.Pages.Capa2;

/// <summary>Un recibo subido: su lectura con IA (IaLectura), lo que se leyo y la validacion del servidor.</summary>
public sealed class ReciboEnCola
{
    public Guid Id { get; } = Guid.NewGuid();
    public string FileName { get; init; } = "";
    public byte[] Bytes { get; init; } = Array.Empty<byte>();
    public string Mime { get; init; } = "application/pdf";
    public string Ext { get; init; } = "PDF";
    public string? Thumb { get; set; }
    public IaLectura Ia { get; } = new();
    public ReciboLeidoRequest? Req { get; set; }
    public EvaluacionReciboDto? Eval { get; set; }
    public TipoServicioPublico? TipoDetectado { get; set; }
    /// <summary>La tarjeta de revision ya mostro su entrada animada (re-renders no la repiten).</summary>
    public bool AnimVista { get; set; }
    public bool Ocupado { get; set; }
}

/// <summary>Pestana Recibos y consumo (replica: renderRecibos()).</summary>
public partial class ServiciosPublicosPagina
{
    private readonly List<ReciboEnCola> _cola = new();
    private bool _procesando, _elegirArchivo, _recWired, _vivo = true;
    private ElementReference _recRef;
    private Guid? _selId;
    private CuentaServicioDetalleDto? _detalle;

    private static readonly string[] PasosRecibo = { "Subiendo el archivo", "Leyendo el recibo", "Encontrando los datos del recibo", "Validando contra tus servicios" };
    private sealed record EjemploDef(string Clave, string Icono, string Label);
    private static readonly EjemploDef[] Ejemplos =
    {
        new("alto", "fi-rr-bolt", "Energía EPM (sobre el tope)"),
        new("ok", "fi-rr-raindrops", "Acueducto (en rango)"),
        new("ajeno", "fi-rr-ban", "Recibo de otra copropiedad"),
    };

    /// <summary>Recibos ya leidos que esperan revision (contador de la pestana y KPI "Por revisar").</summary>
    private int PorRevisar => _cola.Count(q => q.Ia.Estado is IaEstado.Listo or IaEstado.Error);
    private CuentaServicioDto? SelCuenta => _selId is { } id ? _cuentas.FirstOrDefault(c => c.Id == id) : null;

    private async Task IrRecibosAsync(Guid? cuentaId)
    {
        _tab = "recibos";
        if (cuentaId is { } id) _selId = id;
        if (SelCuenta is null)
            _selId = _cuentas.OrderBy(c => c.Alias, StringComparer.Create(Es, true)).FirstOrDefault()?.Id;
        if (_selId is { } s && _detalle?.Cuenta.Id != s) await CargarDetalleAsync(s);
    }

    private async Task SeleccionarAsync(Guid id) { _selId = id; await CargarDetalleAsync(id); }

    private async Task CargarDetalleAsync(Guid id)
    {
        try { _detalle = await Auth().GetFromJsonAsync<CuentaServicioDetalleDto>($"/api/servicios-publicos/cuentas/{id}"); }
        catch { _detalle = null; }
    }

    // Velo para soltar en TODA la pestana y "Cargar recibo con IA" (clic derecho) abre el selector de archivos.
    private async Task DespuesDeRenderRecibosAsync()
    {
        if (_tab != "recibos") { _recWired = false; return; }
        if (!_recWired)
        {
            _recWired = true;
            try { await Js.InvokeVoidAsync("propiaIai.wireDrop", _recRef, "Recibos de servicios públicos (PDF o foto), uno o varios"); } catch { }
        }
        if (_elegirArchivo)
        {
            _elegirArchivo = false;
            try { await Js.InvokeVoidAsync("propiaIai.elegir", _recRef); } catch { }
        }
    }

    // ===== Carga: cada archivo entra a la cola "En cola" y se lee uno a uno con el Agente Documental =====
    private async Task OnRecibosSeleccionados(InputFileChangeEventArgs e)
    {
        var nuevos = new List<ReciboEnCola>();
        foreach (var f in e.GetMultipleFiles(20))
        {
            try
            {
                byte[] bytes;
                await using (var s = f.OpenReadStream(20 * 1024 * 1024))
                {
                    using var ms = new MemoryStream();
                    await s.CopyToAsync(ms);
                    bytes = ms.ToArray();
                }
                nuevos.Add(Nuevo(f.Name, bytes, f.ContentType));
            }
            catch { Mensaje($"No se pudo leer {f.Name} (máximo 20 MB).", true); }
        }
        Encolar(nuevos);
    }

    private static ReciboEnCola Nuevo(string nombre, byte[] bytes, string? contentType)
    {
        var ext = Path.GetExtension(nombre).TrimStart('.').ToLowerInvariant();
        var mime = !string.IsNullOrWhiteSpace(contentType) ? contentType : ext switch
        {
            "pdf" => "application/pdf", "jpg" or "jpeg" => "image/jpeg", "png" => "image/png", "webp" => "image/webp",
            "heic" => "image/heic", "heif" => "image/heif", _ => "application/octet-stream"
        };
        var etiqueta = mime.Contains("pdf") ? "PDF" : ext is "jpeg" ? "JPG" : (ext.Length > 0 ? ext.ToUpperInvariant() : "ARCHIVO");
        return new ReciboEnCola { FileName = nombre, Bytes = bytes, Mime = mime, Ext = etiqueta };
    }

    private void Encolar(List<ReciboEnCola> nuevos)
    {
        if (nuevos.Count == 0) return;
        foreach (var q in nuevos) { _cola.Insert(0, q); q.Ia.EnCola(); }   // como la replica (unshift): el ultimo arriba
        StateHasChanged();
        _ = GenerarThumbsAsync(nuevos);
        _ = ProcesarColaAsync();
    }

    private async Task GenerarThumbsAsync(List<ReciboEnCola> lista)
    {
        foreach (var q in lista)
        {
            try
            {
                var b64 = Convert.ToBase64String(q.Bytes);
                if (q.Mime.Contains("pdf", StringComparison.OrdinalIgnoreCase))
                    q.Thumb = await Js.InvokeAsync<string?>("propiaPdfThumb.render", b64, q.Mime, 120);
                else if (IaIngestaLector.EsImagenSoportada(q.Mime) && await Js.InvokeAsync<string?>("propiaPdfThumb.redimensionarImagen", b64, q.Mime, 240) is { Length: > 0 } mini)
                    q.Thumb = "data:image/jpeg;base64," + mini;
            }
            catch { q.Thumb = null; }
            if (_vivo) await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ProcesarColaAsync()
    {
        if (_procesando) return;
        _procesando = true;
        try
        {
            while (_vivo && _cola.LastOrDefault(q => q.Ia.Estado == IaEstado.Cola) is { } q)
            {
                await LeerAsync(q);
                if (_vivo) await InvokeAsync(StateHasChanged);
            }
        }
        finally { _procesando = false; }
    }

    private async Task LeerAsync(ReciboEnCola q)
    {
        List<IaCampo> leidos = new();
        var res = await IaIngestaLector.LeerAsync(q.Ia, q.Bytes, q.Mime, q.FileName, "recibo_servicio", Js, Auth, AuthLong,
            campos => { leidos = campos.ToList(); return campos.Select(c => c.Nombre).Distinct().ToList(); },
            EtiquetaRecibo, "Formato no soportado: sube PDF, JPG o PNG",
            guardarPdfOrigen: true, aceptarImagenes: true,
            validar: async campos =>
            {
                // Paso "Validando contra tus servicios": la regla vive en el servidor (referencia ACTIVA, duplicado, topes).
                q.Req = ArmarRecibo(campos, q.FileName);
                q.TipoDetectado = ReglasServicioPublico.TipoDesdeTexto(q.Req.TipoServicio);
                q.Eval = await EvaluarAsync(q.Req);
            });
        if (res is null) return;   // fallo: la tarjeta roja de IaPanel ya lo dice
        if (leidos.Count == 0) { q.Ia.Fallo("La IA no encontró los datos de un recibo en este archivo."); return; }
        if (q.Eval is null) { q.Ia.Fallo("No se pudo validar el recibo contra tus servicios. Intenta de nuevo."); return; }
        q.Req = q.Req! with { ArchivoKey = res.PdfOrigenKey };
        if (q.Eval.CuentaId is { } cid && _cuentas.FirstOrDefault(c => c.Id == cid) is { } c) q.TipoDetectado = c.Tipo;
    }

    private async Task<EvaluacionReciboDto?> EvaluarAsync(ReciboLeidoRequest req)
    {
        try
        {
            var r = await Auth().PostAsJsonAsync("/api/servicios-publicos/recibos/evaluar", req);
            return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<EvaluacionReciboDto>() : null;
        }
        catch { return null; }
    }

    // Campos del perfil recibo_servicio (ya normalizados en el servidor) -> recibo leido.
    private static ReciboLeidoRequest ArmarRecibo(IReadOnlyList<IaCampo> campos, string archivo)
    {
        string? S(string k) => campos.FirstOrDefault(c => c.Nombre == k)?.Valor is { Length: > 0 } v ? v.Trim() : null;
        decimal? D(string k) => decimal.TryParse(S(k), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
        DateOnly? F(string k) => DateOnly.TryParseExact(S(k), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        List<ConceptoReciboDto>? conceptos = null;
        if (S("conceptos") is { } json)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                conceptos = doc.RootElement.EnumerateArray()
                    .Select(x => new ConceptoReciboDto(x.GetProperty("concepto").GetString() ?? "", x.GetProperty("valor").GetDecimal()))
                    .Where(x => x.Concepto.Length > 0).ToList();
            }
            catch { conceptos = null; }
        }
        return new ReciboLeidoRequest(S("referencia_contrato"), S("prestador"), S("nit_prestador"), S("titular"),
            S("direccion_suministro"), S("tipo_servicio"), F("periodo_desde"), F("periodo_hasta"), F("fecha_expedicion"),
            F("fecha_limite_pago"), D("lectura_anterior"), D("lectura_actual"), D("consumo"), S("unidad_medida"),
            D("valor_periodo"), D("saldo_anterior"), D("valor_total"), conceptos, null, archivo);
    }

    // Etiqueta de cada clave del perfil (chips "datos encontrados" de la transicion).
    private static string? EtiquetaRecibo(string clave) => clave switch
    {
        "referencia_contrato" => "Referencia", "prestador" => "Prestador", "nit_prestador" => "NIT", "titular" => "Titular",
        "direccion_suministro" => "Dirección", "tipo_servicio" => "Servicio", "periodo_desde" or "periodo_hasta" => "Periodo",
        "fecha_expedicion" => "Expedición", "fecha_limite_pago" => "Fecha límite", "lectura_anterior" or "lectura_actual" => "Lecturas",
        "consumo" => "Consumo", "valor_periodo" => "Valor del periodo", "saldo_anterior" => "Saldo anterior",
        "valor_total" => "Total a pagar", "conceptos" => "Desglose", _ => null
    };

    private void Descartar(ReciboEnCola q) { _cola.Remove(q); StateHasChanged(); }

    // Despues de agregar un servicio o registrar un recibo, las demas tarjetas se revalidan (replica: validar()
    // corre en cada render): un recibo "ajeno" con la misma referencia pasa a ser de la copropiedad, etc.
    private async Task ReevaluarColaAsync()
    {
        foreach (var q in _cola.Where(x => x.Req is not null && x.Ia.Estado == IaEstado.Listo).ToList())
            if (await EvaluarAsync(q.Req!) is { } ev) q.Eval = ev;
    }

    private async Task ConfirmarAsync(ReciboEnCola q)
    {
        if (q.Req is null || q.Eval?.CuentaId is not { } cid) return;
        q.Ocupado = true;
        try
        {
            var r = await Auth().PostAsJsonAsync("/api/servicios-publicos/recibos/confirmar", q.Req with { CuentaId = cid });
            if (!r.IsSuccessStatusCode) { Mensaje(await SpxFormato.ErrorDeAsync(r, "No se pudo registrar el recibo."), true); return; }
            var ev = q.Eval;
            _cola.Remove(q);
            _selId = cid;
            var per = ev.PeriodoLabel ?? "";
            Mensaje(ev.Rango switch
            {
                RangoRecibo.Alto => $"Alerta: {ev.CuentaAlias} ({per}) quedó sobre el tope.",
                RangoRecibo.Bajo => $"Alerta: {ev.CuentaAlias} ({per}) quedó bajo el mínimo.",
                _ => $"Recibo de {ev.CuentaAlias} ({per}) registrado."
            }, ev.Rango is RangoRecibo.Alto or RangoRecibo.Bajo);
            await CargarAsync();
            await CargarDetalleAsync(cid);
            await ReevaluarColaAsync();
        }
        catch (Exception ex) { Mensaje(ex.Message, true); }
        finally { q.Ocupado = false; }
    }

    private async Task AgregarComoServicioAsync(ReciboEnCola q)
    {
        if (q.Req is null) return;
        q.Ocupado = true;
        try
        {
            var r = await Auth().PostAsJsonAsync("/api/servicios-publicos/recibos/agregar-servicio", q.Req);
            if (!r.IsSuccessStatusCode) { Mensaje(await SpxFormato.ErrorDeAsync(r, "No se pudo agregar el servicio."), true); return; }
            q.Eval = await r.Content.ReadFromJsonAsync<EvaluacionReciboDto>() ?? q.Eval;
            Mensaje($"Servicio agregado con la referencia {q.Req.Referencia}. Define sus topes en Configuración.", false);
            await CargarAsync();
            await ReevaluarColaAsync();
        }
        catch (Exception ex) { Mensaje(ex.Message, true); }
        finally { q.Ocupado = false; }
    }

    // ===== "Prueba con uno de ejemplo": el servidor arma el recibo con los servicios reales y la IA lo lee =====
    private async Task EjemploAsync(string clase)
    {
        try
        {
            var r = await Auth().GetAsync($"/api/servicios-publicos/recibos/ejemplo/{clase}");
            if (!r.IsSuccessStatusCode) { Mensaje(await SpxFormato.ErrorDeAsync(r, "No se pudo generar el recibo de ejemplo."), true); return; }
            var bytes = await r.Content.ReadAsByteArrayAsync();
            var nombre = r.Content.Headers.ContentDisposition?.FileNameStar ?? r.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "Recibo_ejemplo.pdf";
            Encolar(new List<ReciboEnCola> { Nuevo(nombre, bytes, "application/pdf") });
        }
        catch (Exception ex) { Mensaje(ex.Message, true); }
    }

    // ===== Desglose de un registro del historial =====
    private Task VerDesgloseAsync(CuentaServicioDto c, RegistroConsumoDto r)
        => Modals.OpenAsync<DesgloseReciboModal, bool>(new ModalParameters().Set("Cuenta", c).Set("R", r), new ModalOptions { Width = "560px" });

    // ===== Registrar a mano (cuando la IA no pudo leer un recibo) =====
    private sealed class FormManual
    {
        public int Anio { get; set; } = DateTime.Today.Year;
        public int Mes { get; set; } = DateTime.Today.Month;
        public decimal Consumo { get; set; }
        public decimal Valor { get; set; }
        public EstadoRegistroConsumo Estado { get; set; } = EstadoRegistroConsumo.Confirmado;
        public string? Nota { get; set; }
    }
    private FormManual _fm = new();
    private bool _manualAbierto, _reclAbierto, _busy;
    private string? _modalErr;
    private static readonly string[] MesesNombre = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
    private static string MesNombre(int m) => m is >= 1 and <= 12 ? MesesNombre[m - 1] : m.ToString(CultureInfo.InvariantCulture);
    private void AbrirManual() { _fm = new(); _modalErr = null; _manualAbierto = true; }
    private async Task GuardarManualAsync()
    {
        if (SelCuenta is not { } c) return;
        _busy = true; _modalErr = null;
        try
        {
            var r = await Auth().PostAsJsonAsync($"/api/servicios-publicos/cuentas/{c.Id}/registros",
                new CrearRegistroConsumoRequest(_fm.Anio, _fm.Mes, _fm.Consumo, _fm.Valor, _fm.Estado, _fm.Nota));
            if (!r.IsSuccessStatusCode) { _modalErr = await SpxFormato.ErrorDeAsync(r, "No se pudo registrar el consumo."); return; }
            _manualAbierto = false;
            await CargarAsync();
            await CargarDetalleAsync(c.Id);
        }
        catch (Exception ex) { _modalErr = ex.Message; }
        finally { _busy = false; }
    }

    // ===== Reclamaciones ante el prestador (el modal actual) =====
    private sealed class FormReclamacion { public string? Motivo { get; set; } public string? Radicado { get; set; } public string? Descripcion { get; set; } }
    private FormReclamacion _frc = new();
    private void AbrirReclamacion() { _frc = new(); _modalErr = null; _reclAbierto = true; }
    private async Task GuardarReclamacionAsync()
    {
        if (SelCuenta is not { } c) return;
        _busy = true; _modalErr = null;
        try
        {
            var r = await Auth().PostAsJsonAsync($"/api/servicios-publicos/cuentas/{c.Id}/reclamaciones",
                new CrearReclamacionRequest(_frc.Motivo ?? "", _frc.Radicado, _frc.Descripcion));
            if (!r.IsSuccessStatusCode) { _modalErr = await SpxFormato.ErrorDeAsync(r, "No se pudo radicar la reclamación."); return; }
            _reclAbierto = false;
            await CargarDetalleAsync(c.Id);
        }
        catch (Exception ex) { _modalErr = ex.Message; }
        finally { _busy = false; }
    }
    private static string EstadoRecl(EstadoReclamacionServicio e) => e switch
    {
        EstadoReclamacionServicio.EnTramite => "En trámite", EstadoReclamacionServicio.Resuelta => "Resuelta",
        EstadoReclamacionServicio.Cerrada => "Cerrada", _ => "Radicada"
    };
    private static string FechaRecl(DateOnly d) => ReglasServicioPublico.FechaCorta(d);
}
