using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Application.ServiciosPublicos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.ServiciosPublicos;

/// <summary>
/// Modulo 2.17 Servicios publicos. Configuracion de servicios (referencia del contrato, periodicidad, dia de pago y
/// topes), registros de consumo (manuales o leidos de un recibo con IA), reclamaciones, alertas y campos propios.
/// Las reglas puras (referencia normalizada, topes, proximo pago, cuadre) viven en ReglasServicioPublico.
/// </summary>
public class ServiciosPublicosService : IServiciosPublicosService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IBlobStorage _blob;
    public ServiciosPublicosService(PropiaDbContext db, ITenantContext tenant, IBlobStorage blob) { _db = db; _tenant = tenant; _blob = blob; }

    private static int Orden(RegistroConsumoServicio r) => r.Anio * 100 + r.Mes;
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));   // Colombia (UTC-5)
    private static TopesServicio Topes(CuentaServicioPublico c) => new(c.ValorMin, c.ValorMax, c.ConsumoMin, c.ConsumoMax, c.UnidadMedida);

    public async Task<ServiciosPublicosResumenDto> GetResumenAsync(CancellationToken ct)
    {
        var cuentas = await _db.CuentasServicioPublico.AsNoTracking()
            .Include(c => c.Registros).ToListAsync(ct);
        var reclAbiertas = await _db.ReclamacionesServicio.AsNoTracking()
            .CountAsync(r => r.Estado != EstadoReclamacionServicio.Resuelta
                          && r.Estado != EstadoReclamacionServicio.Cerrada, ct);

        decimal gasto = 0; int conAlerta = 0;
        foreach (var c in cuentas.Where(x => x.Activa))
        {
            var ords = c.Registros.OrderByDescending(Orden).ToList();
            if (ords.Count > 0) gasto += ords[0].Valor;
            if (TieneAlerta(c, ords)) conAlerta++;
        }
        var ia = cuentas.Sum(c => c.Registros.Count(r => r.Origen == OrigenRegistroConsumo.IA));
        return new ServiciosPublicosResumenDto(cuentas.Count(c => c.Activa), gasto, conAlerta, reclAbiertas, ia);
    }

    public async Task<IReadOnlyList<CuentaServicioDto>> ListCuentasAsync(CancellationToken ct)
    {
        var cuentas = await _db.CuentasServicioPublico.AsNoTracking()
            .Include(c => c.Registros).OrderBy(c => c.CreatedAt).ThenBy(c => c.Alias).ToListAsync(ct);
        return cuentas.Select(ToCuentaDto).ToList();   // orden de creacion (como la bandeja de la replica)
    }

    public async Task<CuentaServicioDetalleDto?> GetCuentaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.CuentasServicioPublico.AsNoTracking()
            .Include(x => x.Registros).ThenInclude(r => r.Conceptos)
            .Include(x => x.Reclamaciones)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        var regsOrden = c.Registros.OrderBy(Orden).ToList();   // cronologico ascendente
        var registros = new List<RegistroConsumoDto>();
        for (int i = 0; i < regsOrden.Count; i++)
            registros.Add(ToRegistroDto(c, regsOrden[i], i > 0 ? regsOrden[i - 1] : null));
        registros.Reverse(); // mostrar mas reciente primero

        var recl = c.Reclamaciones.OrderByDescending(x => x.Fecha).Select(ToReclDto).ToList();
        decimal? prom = c.Registros.Count > 0 ? Math.Round(c.Registros.Average(x => x.Consumo), 1) : null;
        return new CuentaServicioDetalleDto(ToCuentaDto(c), registros, recl, prom);
    }

    public async Task<CuentaServicioDto> CrearCuentaAsync(CrearCuentaServicioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Alias)) throw new InvalidOperationException("El nombre del servicio es obligatorio.");
        ValidarDiaPago(req.DiaPago);
        var refNorm = ReglasServicioPublico.NormalizarReferencia(req.NumeroCuenta);
        if (refNorm is not null && await _db.CuentasServicioPublico.AnyAsync(x => x.ReferenciaNormalizada == refNorm, ct))
            throw new InvalidOperationException($"Ya hay un servicio con la referencia {req.NumeroCuenta!.Trim()}.");
        var c = new CuentaServicioPublico
        {
            Tipo = req.Tipo,
            Alias = req.Alias.Trim(),
            Prestador = (req.Prestador ?? "").Trim(),
            NumeroCuenta = Limpio(req.NumeroCuenta),
            ReferenciaNormalizada = refNorm,
            MetodoPago = Limpio(req.MetodoPago),
            UnidadMedida = Limpio(req.UnidadMedida) ?? ReglasServicioPublico.UnidadPorDefecto(req.Tipo),
            UmbralAlertaPct = req.UmbralAlertaPct <= 0 ? 25 : req.UmbralAlertaPct,
            Periodicidad = req.Periodicidad ?? PeriodicidadServicio.Mensual,
            DiaPago = req.DiaPago,
            ValorMin = req.ValorMin, ValorMax = req.ValorMax, ConsumoMin = req.ConsumoMin, ConsumoMax = req.ConsumoMax
        };
        _db.CuentasServicioPublico.Add(c);
        await _db.SaveChangesAsync(ct);
        return ToCuentaDto(c);
    }

    public async Task<CuentaServicioDto?> ActualizarCuentaAsync(Guid id, ActualizarCuentaServicioRequest req, CancellationToken ct)
    {
        var c = await _db.CuentasServicioPublico.Include(x => x.Registros).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        var limpiar = new HashSet<string>(req.Limpiar ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        if (req.Alias is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Alias)) throw new InvalidOperationException("El nombre del servicio no puede quedar vacío.");
            c.Alias = req.Alias.Trim();
        }
        if (req.Tipo is { } t)
        {
            c.Tipo = t;
            c.UnidadMedida ??= ReglasServicioPublico.UnidadPorDefecto(t);
        }
        if (req.Prestador is not null) c.Prestador = req.Prestador.Trim();
        if (limpiar.Contains("numeroCuenta")) { c.NumeroCuenta = null; c.ReferenciaNormalizada = null; }
        else if (req.NumeroCuenta is not null)
        {
            var refNorm = ReglasServicioPublico.NormalizarReferencia(req.NumeroCuenta);
            if (refNorm is not null)
            {
                var otra = await _db.CuentasServicioPublico.AsNoTracking()
                    .Where(x => x.ReferenciaNormalizada == refNorm && x.Id != id).Select(x => x.Alias).FirstOrDefaultAsync(ct);
                if (otra is not null) throw new InvalidOperationException($"La referencia {req.NumeroCuenta.Trim()} ya es de \"{otra}\".");
            }
            c.NumeroCuenta = Limpio(req.NumeroCuenta);
            c.ReferenciaNormalizada = refNorm;
        }
        if (limpiar.Contains("metodoPago")) c.MetodoPago = null; else if (req.MetodoPago is not null) c.MetodoPago = Limpio(req.MetodoPago);
        if (limpiar.Contains("unidadMedida")) c.UnidadMedida = null; else if (req.UnidadMedida is not null) c.UnidadMedida = Limpio(req.UnidadMedida);
        if (req.Periodicidad is { } p) c.Periodicidad = p;
        if (limpiar.Contains("diaPago")) c.DiaPago = null; else if (req.DiaPago is { } d) { ValidarDiaPago(d); c.DiaPago = d; }
        if (limpiar.Contains("valorMin")) c.ValorMin = null; else if (req.ValorMin is { } a) c.ValorMin = a;
        if (limpiar.Contains("valorMax")) c.ValorMax = null; else if (req.ValorMax is { } b) c.ValorMax = b;
        if (limpiar.Contains("consumoMin")) c.ConsumoMin = null; else if (req.ConsumoMin is { } e) c.ConsumoMin = e;
        if (limpiar.Contains("consumoMax")) c.ConsumoMax = null; else if (req.ConsumoMax is { } f) c.ConsumoMax = f;
        if (req.Activa is { } act) c.Activa = act;
        await _db.SaveChangesAsync(ct);
        return ToCuentaDto(c);
    }

    public async Task<bool> EliminarCuentaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.CuentasServicioPublico.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        _db.CuentasServicioPublico.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RegistroConsumoDto> AgregarRegistroAsync(Guid cuentaId, CrearRegistroConsumoRequest req, CancellationToken ct)
    {
        var c = await _db.CuentasServicioPublico.Include(x => x.Registros)
            .FirstOrDefaultAsync(x => x.Id == cuentaId, ct)
            ?? throw new InvalidOperationException("Cuenta no encontrada.");
        if (req.Mes is < 1 or > 12) throw new InvalidOperationException("El mes va de 1 a 12.");
        var r = new RegistroConsumoServicio
        {
            CuentaServicioId = cuentaId,
            Anio = req.Anio,
            Mes = req.Mes,
            Consumo = req.Consumo,
            Valor = req.Valor,
            Estado = req.Estado,
            NotaAdmin = req.NotaAdmin?.Trim(),
            FechaLimitePago = req.FechaLimitePago,
            LecturaAnterior = req.LecturaAnterior,
            LecturaActual = req.LecturaActual,
            Origen = OrigenRegistroConsumo.Manual
        };
        _db.RegistrosConsumoServicio.Add(r);
        await _db.SaveChangesAsync(ct);

        var previo = c.Registros.Where(x => x.Id != r.Id && Orden(x) < Orden(r)).OrderByDescending(Orden).FirstOrDefault();
        await AlertarSiFueraAsync(c, r, previo, ct);
        return ToRegistroDto(c, r, previo);
    }

    public async Task<bool> EliminarRegistroAsync(Guid registroId, CancellationToken ct)
    {
        var r = await _db.RegistrosConsumoServicio.FirstOrDefaultAsync(x => x.Id == registroId, ct);
        if (r is null) return false;
        _db.RegistrosConsumoServicio.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Recibos leidos con IA =====================

    public async Task<EvaluacionReciboDto> EvaluarReciboAsync(ReciboLeidoRequest req, CancellationToken ct)
    {
        var refNorm = ReglasServicioPublico.NormalizarReferencia(req.Referencia);
        var periodo = ReglasServicioPublico.Periodo(req.PeriodoDesde, req.PeriodoHasta, req.FechaExpedicion);
        var valor = ValorDe(req);
        var cuadra = ReglasServicioPublico.CuadraDesglose((req.Conceptos ?? Array.Empty<ConceptoReciboDto>()).Select(x => x.Valor), req.ValorTotal ?? req.ValorPeriodo);

        CuentaServicioPublico? cuenta = null; var inactiva = false;
        if (refNorm is not null)
        {
            var candidatas = await _db.CuentasServicioPublico.AsNoTracking().Include(x => x.Registros)
                .Where(x => x.ReferenciaNormalizada == refNorm).ToListAsync(ct);
            cuenta = candidatas.FirstOrDefault(x => x.Activa);
            inactiva = cuenta is null && candidatas.Count > 0;
        }
        if (cuenta is null)
            return new EvaluacionReciboDto(null, null, req.Referencia, refNorm, periodo?.Anio, periodo?.Mes,
                periodo is { } p0 ? ReglasServicioPublico.PeriodoLabel(p0.Anio, p0.Mes) : null,
                false, RangoRecibo.Nada, Array.Empty<string>(), valor, null, null, req.UnidadMedida, cuadra, inactiva);

        var duplicado = periodo is { } p && cuenta.Registros.Any(x => x.Anio == p.Anio && x.Mes == p.Mes);
        var ev = ReglasServicioPublico.Evaluar(Topes(cuenta) with { Unidad = cuenta.UnidadMedida ?? req.UnidadMedida }, valor, req.Consumo);
        return new EvaluacionReciboDto(cuenta.Id, cuenta.Alias, req.Referencia, refNorm, periodo?.Anio, periodo?.Mes,
            periodo is { } p1 ? ReglasServicioPublico.PeriodoLabel(p1.Anio, p1.Mes) : null,
            duplicado, ev.Estado, ev.Motivos, valor, cuenta.ValorMin, cuenta.ValorMax, cuenta.UnidadMedida ?? req.UnidadMedida,
            cuadra, false);
    }

    public async Task<RegistroConsumoDto> ConfirmarReciboAsync(ReciboLeidoRequest req, CancellationToken ct)
    {
        // La validacion se repite en el servidor: nunca se registra un recibo que no sea de la copropiedad.
        var refNorm = ReglasServicioPublico.NormalizarReferencia(req.Referencia)
            ?? throw new InvalidOperationException("El recibo no trae la referencia del contrato.");
        var c = await _db.CuentasServicioPublico.Include(x => x.Registros).ThenInclude(r => r.Conceptos)
            .FirstOrDefaultAsync(x => x.ReferenciaNormalizada == refNorm && x.Activa, ct)
            ?? throw new InvalidOperationException("La referencia del recibo no corresponde a ningún servicio configurado de la copropiedad.");
        if (req.CuentaId is { } cid && cid != c.Id) throw new InvalidOperationException("El recibo no corresponde al servicio indicado.");
        var periodo = ReglasServicioPublico.Periodo(req.PeriodoDesde, req.PeriodoHasta, req.FechaExpedicion)
            ?? throw new InvalidOperationException("No se pudo determinar el periodo del recibo.");
        var valor = ValorDe(req) ?? throw new InvalidOperationException("El recibo no trae el valor a pagar.");

        var r = c.Registros.FirstOrDefault(x => x.Anio == periodo.Anio && x.Mes == periodo.Mes);
        if (r is null)
        {
            r = new RegistroConsumoServicio { CuentaServicioId = c.Id, Anio = periodo.Anio, Mes = periodo.Mes };
            _db.RegistrosConsumoServicio.Add(r);
        }
        else
        {
            // Ya habia un recibo del periodo: se reemplaza (con su desglose).
            _db.RegistrosConsumoConceptos.RemoveRange(r.Conceptos);
            r.Conceptos.Clear();
        }
        r.Consumo = req.Consumo ?? 0;
        r.Valor = valor;
        r.Estado = EstadoRegistroConsumo.Confirmado;
        r.FechaLimitePago = req.FechaLimitePago;
        r.LecturaAnterior = req.LecturaAnterior;
        r.LecturaActual = req.LecturaActual;
        r.Origen = OrigenRegistroConsumo.IA;
        r.ArchivoKey = Limpio(req.ArchivoKey);
        r.ArchivoNombre = Limpio(req.ArchivoNombre);
        r.NotaAdmin = r.ArchivoNombre is { } an ? Recortar("Leído con IA de " + an, 500) : "Leído con IA";
        var orden = 0;
        foreach (var k in (req.Conceptos ?? Array.Empty<ConceptoReciboDto>()).Where(k => !string.IsNullOrWhiteSpace(k.Concepto)))
        {
            // Add explicito por el DbSet (la fixup de EF lo suma a r.Conceptos): la entidad trae su Id (BaseEntity) y,
            // descubierta solo por la navegacion, EF la tomaria como existente (UPDATE de 0 filas).
            _db.RegistrosConsumoConceptos.Add(new RegistroConsumoConcepto { RegistroConsumoId = r.Id, Orden = orden++, Concepto = Recortar(k.Concepto.Trim(), 160), Valor = k.Valor });
        }
        await _db.SaveChangesAsync(ct);

        var previo = c.Registros.Where(x => x.Id != r.Id && Orden(x) < Orden(r)).OrderByDescending(Orden).FirstOrDefault();
        await AlertarSiFueraAsync(c, r, previo, ct);
        return ToRegistroDto(c, r, previo);
    }

    public async Task<EvaluacionReciboDto> AgregarServicioDesdeReciboAsync(ReciboLeidoRequest req, CancellationToken ct)
    {
        var referencia = Limpio(req.Referencia) ?? throw new InvalidOperationException("El recibo no trae la referencia del contrato.");
        var tipo = ReglasServicioPublico.TipoDesdeTexto(req.TipoServicio) ?? TipoServicioPublico.EnergiaElectrica;
        await CrearCuentaAsync(new CrearCuentaServicioRequest(
            tipo, $"{NombreTipo(tipo)} - {referencia}", Limpio(req.Prestador) ?? "", referencia, null,
            Limpio(req.UnidadMedida) ?? ReglasServicioPublico.UnidadPorDefecto(tipo), 25,
            PeriodicidadServicio.Mensual, req.FechaLimitePago?.Day), ct);
        return await EvaluarReciboAsync(req, ct);
    }

    /// <summary>Nombre del tipo como en la replica (TIPOS[].nombre).</summary>
    public static string NombreTipo(TipoServicioPublico t) => t switch
    {
        TipoServicioPublico.EnergiaElectrica => "Energía eléctrica",
        TipoServicioPublico.Acueducto => "Acueducto y alcantarillado",
        TipoServicioPublico.Gas => "Gas natural",
        TipoServicioPublico.Aseo => "Aseo / recolección",
        TipoServicioPublico.Internet => "Internet",
        TipoServicioPublico.TelefoniaFija => "Telefonía fija",
        _ => t.ToString()
    };

    // Valor que se registra y se compara con los topes: el del periodo (sin saldos ni mora); si no viene, el total.
    private static decimal? ValorDe(ReciboLeidoRequest r) => r.ValorPeriodo ?? r.ValorTotal;

    public async Task<ReclamacionServicioDto> AgregarReclamacionAsync(Guid cuentaId, CrearReclamacionRequest req, CancellationToken ct)
    {
        _ = await _db.CuentasServicioPublico.AnyAsync(x => x.Id == cuentaId, ct)
            ? true : throw new InvalidOperationException("Cuenta no encontrada.");
        if (string.IsNullOrWhiteSpace(req.Motivo)) throw new InvalidOperationException("Motivo obligatorio.");
        var rc = new ReclamacionServicio
        {
            CuentaServicioId = cuentaId,
            Motivo = req.Motivo.Trim(),
            Radicado = req.Radicado?.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Estado = EstadoReclamacionServicio.Radicada,
            Fecha = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _db.ReclamacionesServicio.Add(rc);
        await _db.SaveChangesAsync(ct);
        return ToReclDto(rc);
    }

    // ===================== Alertas =====================

    /// <summary>
    /// Con topes manda la regla de topes ("Recibo fuera de tope: {Alias}", Critica si supera el maximo en 50% o mas);
    /// sin topes se mantiene la regla de variacion contra el periodo anterior (UmbralAlertaPct). Nunca duplica la
    /// alerta activa del mismo servicio y periodo.
    /// </summary>
    private async Task AlertarSiFueraAsync(CuentaServicioPublico c, RegistroConsumoServicio r, RegistroConsumoServicio? previo, CancellationToken ct)
    {
        var periodoTxt = ReglasServicioPublico.PeriodoLabel(r.Anio, r.Mes);
        string titulo, descripcion; SeveridadAlerta sev;
        var topes = Topes(c);
        if (!topes.SinTopes)
        {
            var ev = ReglasServicioPublico.Evaluar(topes, r.Valor, r.Consumo);
            if (!ev.Fuera) return;
            titulo = $"Recibo fuera de tope: {c.Alias}";
            descripcion = $"Recibo de {periodoTxt}: " + string.Join(" ", ev.Motivos);
            sev = ev.Critica ? SeveridadAlerta.Critica : SeveridadAlerta.Advertencia;
        }
        else
        {
            decimal? varPct = previo is not null && previo.Consumo != 0
                ? Math.Round((r.Consumo - previo.Consumo) / previo.Consumo * 100, 1) : null;
            if (varPct is not { } vp || vp <= c.UmbralAlertaPct) return;
            titulo = $"Consumo elevado: {c.Alias}";
            descripcion = $"El consumo de {periodoTxt} subio {vp}% frente al periodo anterior (umbral {c.UmbralAlertaPct}%).";
            sev = vp >= c.UmbralAlertaPct * 2 ? SeveridadAlerta.Critica : SeveridadAlerta.Advertencia;
        }
        var yaExiste = await _db.AlertasCopropiedad.AnyAsync(
            a => a.Activa && a.ModuloOrigenCodigo == "servicios-publicos"
                 && a.EntidadId == c.Id && a.Descripcion.Contains(periodoTxt), ct);
        if (yaExiste) return;
        _db.AlertasCopropiedad.Add(new AlertaCopropiedad
        {
            Tipo = TipoAlertaDashboard.Otro,
            Severidad = sev,
            Titulo = Recortar(titulo, 200),
            Descripcion = Recortar(descripcion, 1000),
            UrlAccion = "/servicios",
            ModuloOrigenCodigo = "servicios-publicos",
            EntidadId = c.Id,
            Activa = true
        });
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Campos propios (columna "+"), espejo de Plantillas =====================

    private static ServicioPublicoCampoDefinicionDto ToDto(ServicioPublicoCampoDefinicion d)
        => new(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones, d.Descripcion);

    public async Task<IReadOnlyList<ServicioPublicoCampoDefinicionDto>> ListarCamposAsync(CancellationToken ct)
        => (await _db.ServicioPublicoCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct))
            .Select(ToDto).ToList();

    public async Task<ServicioPublicoCampoDefinicionDto> CrearCampoAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = LabelCampo(req.Label);
        var existente = await _db.ServicioPublicoCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return ToDto(existente);
        var maxOrden = await _db.ServicioPublicoCamposDefiniciones.AnyAsync(ct) ? await _db.ServicioPublicoCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new ServicioPublicoCampoDefinicion
        {
            Label = label,
            Orden = maxOrden + 1,
            Tipo = TipoPermitido(req.Tipo),
            Opciones = NormalizarOpciones(req.Tipo, req.Opciones),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim()
        };
        _db.ServicioPublicoCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return ToDto(def);
    }

    public async Task<bool> ActualizarCampoAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.ServicioPublicoCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        def.Label = LabelCampo(req.Label);
        def.Tipo = TipoPermitido(req.Tipo);
        def.Opciones = NormalizarOpciones(req.Tipo, req.Opciones);
        def.Orden = req.Orden;
        def.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.ServicioPublicoCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.ServicioPublicoCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ServicioPublicoCampoValorFlatDto>> ListarValoresAsync(CancellationToken ct)
        => await _db.ServicioPublicoCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new ServicioPublicoCampoValorFlatDto(v.CuentaServicioId, v.DefinicionId, v.Valor)).ToListAsync(ct);

    public async Task<bool> SetValorAsync(Guid cuentaId, Guid definicionId, string? valor, CancellationToken ct)
    {
        // Servicio y campo deben ser del tenant (el query filter ya lo garantiza; sin esto un id ajeno crearia huerfanos).
        if (!await _db.CuentasServicioPublico.AnyAsync(p => p.Id == cuentaId, ct)) return false;
        if (!await _db.ServicioPublicoCamposDefiniciones.AnyAsync(d => d.Id == definicionId, ct)) return false;
        var v = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        var existente = await _db.ServicioPublicoCamposValores.FirstOrDefaultAsync(x => x.DefinicionId == definicionId && x.CuentaServicioId == cuentaId, ct);
        if (existente is null)
            _db.ServicioPublicoCamposValores.Add(new ServicioPublicoCampoValor { DefinicionId = definicionId, CuentaServicioId = cuentaId, Valor = v });
        else existente.Valor = v;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string LabelCampo(string? label)
    {
        var l = (label ?? "").Trim();
        if (l.Length == 0) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        return l.Length > 80 ? l[..80] : l;
    }

    // Sin motor de calculo para este modulo: formulas y totales caen a Texto (como Plantillas).
    private static TipoCampoTablero TipoPermitido(TipoCampoTablero t)
        => t is TipoCampoTablero.Formula or TipoCampoTablero.Total or TipoCampoTablero.Separador ? TipoCampoTablero.Texto : t;

    private static string? NormalizarOpciones(TipoCampoTablero tipo, string? opciones)
    {
        if (tipo != TipoCampoTablero.Seleccion || string.IsNullOrWhiteSpace(opciones)) return null;
        var limpias = opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return limpias.Length == 0 ? null : string.Join('\n', limpias);
    }

    // ===================== helpers =====================

    private static void ValidarDiaPago(int? d)
    {
        if (d is { } x && (x < 1 || x > 31)) throw new InvalidOperationException("El día de pago va de 1 a 31.");
    }
    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string Recortar(string s, int max) => s.Length > max ? s[..max] : s;

    private static decimal? Variacion(List<RegistroConsumoServicio> ordsDesc)
    {
        if (ordsDesc.Count < 2 || ordsDesc[1].Consumo == 0) return null;
        return Math.Round((ordsDesc[0].Consumo - ordsDesc[1].Consumo) / ordsDesc[1].Consumo * 100, 1);
    }

    // Alerta del ultimo recibo: con topes, fuera de rango; sin topes, variacion sobre el umbral.
    private static bool TieneAlerta(CuentaServicioPublico c, List<RegistroConsumoServicio> ordsDesc)
    {
        if (ordsDesc.Count == 0) return false;
        var t = Topes(c);
        if (!t.SinTopes) return ReglasServicioPublico.Evaluar(t, ordsDesc[0].Valor, ordsDesc[0].Consumo).Fuera;
        return Variacion(ordsDesc) is { } v && v > c.UmbralAlertaPct;
    }

    private static CuentaServicioDto ToCuentaDto(CuentaServicioPublico c)
    {
        var ords = c.Registros.OrderByDescending(Orden).ToList();
        var u = ords.FirstOrDefault();
        var ev = ReglasServicioPublico.Evaluar(Topes(c), u?.Valor, u?.Consumo);
        var prox = ReglasServicioPublico.ProximoPago(c.DiaPago, c.Periodicidad, u is null ? null : (u.Anio, u.Mes), Hoy);
        return new CuentaServicioDto(c.Id, c.Tipo, c.Alias, c.Prestador, c.NumeroCuenta, c.MetodoPago,
            c.UnidadMedida, c.UmbralAlertaPct, c.Activa,
            TieneAlerta(c, ords), u?.Valor, Variacion(ords),
            c.Periodicidad, c.DiaPago, c.ValorMin, c.ValorMax, c.ConsumoMin, c.ConsumoMax,
            u?.Anio, u?.Mes, u?.Consumo, prox, ev.Estado, ev.Motivos);
    }

    private RegistroConsumoDto ToRegistroDto(CuentaServicioPublico c, RegistroConsumoServicio r, RegistroConsumoServicio? previo)
    {
        decimal? varPct = previo is not null && previo.Consumo != 0
            ? Math.Round((r.Consumo - previo.Consumo) / previo.Consumo * 100, 1) : null;
        var ev = ReglasServicioPublico.Evaluar(Topes(c), r.Valor, r.Consumo);
        var alerta = Topes(c).SinTopes ? varPct is { } vp && vp > c.UmbralAlertaPct : ev.Fuera;
        return new RegistroConsumoDto(r.Id, r.CuentaServicioId, r.Anio, r.Mes,
            ReglasServicioPublico.PeriodoLabel(r.Anio, r.Mes), r.Consumo, r.Valor, r.Estado,
            varPct, alerta, r.NotaAdmin,
            r.FechaLimitePago, r.LecturaAnterior, r.LecturaActual, r.Origen, r.ArchivoKey, r.ArchivoNombre,
            r.Conceptos.OrderBy(x => x.Orden).Select(x => new ConceptoReciboDto(x.Concepto, x.Valor)).ToList(),
            ev.Estado, ev.Motivos, string.IsNullOrWhiteSpace(r.ArchivoKey) ? null : _blob.ResolveUrl(r.ArchivoKey));
    }

    private static ReclamacionServicioDto ToReclDto(ReclamacionServicio r)
        => new(r.Id, r.CuentaServicioId, r.Motivo, r.Radicado, r.Descripcion, r.Estado, r.Fecha);
}
