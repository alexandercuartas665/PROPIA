using Microsoft.EntityFrameworkCore;
using Propia.Application.Seguros;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Seguros;

/// <summary>Modulo Seguros (Ola 4): polizas dedicadas, campos dinamicos y reclamaciones.
/// El aislamiento por tenant lo aplica el HasQueryFilter global del DbContext.</summary>
public class SegurosService : ISegurosService
{
    private readonly PropiaDbContext _db;
    private readonly Storage.IBlobStorage _blob;
    public SegurosService(PropiaDbContext db, Storage.IBlobStorage blob) { _db = db; _blob = blob; }

    // ----------------------------- Polizas -----------------------------
    public async Task<IReadOnlyList<PolizaDto>> ListPolizasAsync(CancellationToken ct)
    {
        var polizas = await _db.Polizas.AsNoTracking().OrderByDescending(p => p.FechaFin).ToListAsync(ct);
        var ids = polizas.Select(p => p.Id).ToList();
        var valores = await _db.PolizaCampoValores.AsNoTracking().Where(v => ids.Contains(v.PolizaId)).ToListAsync(ct);
        var recl = await _db.PolizaReclamaciones.AsNoTracking().Where(r => ids.Contains(r.PolizaId))
            .GroupBy(r => r.PolizaId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var reclCount = recl.ToDictionary(x => x.Key, x => x.N);
        var porPoliza = valores.GroupBy(v => v.PolizaId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PolizaCampoValorDto>)g.Select(v => new PolizaCampoValorDto(v.PolizaCampoId, v.Valor)).ToList());
        return polizas.Select(p => ToDto(p, porPoliza.GetValueOrDefault(p.Id), reclCount.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<PolizaDto?> ObtenerPolizaAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Polizas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var valores = await _db.PolizaCampoValores.AsNoTracking().Where(v => v.PolizaId == id)
            .Select(v => new PolizaCampoValorDto(v.PolizaCampoId, v.Valor)).ToListAsync(ct);
        var n = await _db.PolizaReclamaciones.CountAsync(r => r.PolizaId == id, ct);
        return ToDto(p, valores, n);
    }

    public async Task<PolizaDto> CrearPolizaAsync(CrearPolizaRequest req, CancellationToken ct)
    {
        await ValidarPolizaAsync(req.Aseguradora, req.FechaInicio, req.FechaFin, req.ValorPoliza,
            req.FormaPagoCuotas, req.PagoMensual, req.NumeroPoliza, null, ct);
        var p = new Poliza
        {
            NumeroPoliza = Limpio(req.NumeroPoliza),
            Aseguradora = req.Aseguradora.Trim(),
            AseguradoraPersonaId = req.AseguradoraPersonaId,
            AseguradoraEmpresaId = req.AseguradoraEmpresaId,
            Corredor = Limpio(req.Corredor),
            CorredorPersonaId = req.CorredorPersonaId,
            CorredorEmpresaId = req.CorredorEmpresaId,
            FechaInicio = req.FechaInicio,
            FechaFin = req.FechaFin,
            ValorPoliza = req.ValorPoliza,
            FormaPagoCuotas = req.FormaPagoCuotas,
            PagoMensual = req.PagoMensual,
            Cobertura = Limpio(req.Cobertura),
            IncluyeZonasUnidades = req.IncluyeZonasUnidades,
            ValoresAgregados = Limpio(req.ValoresAgregados),
            Observaciones = Limpio(req.Observaciones),
            ExpedienteId = req.ExpedienteId,
            PdfOrigenKey = Limpio(req.PdfOrigenKey)
        };
        _db.Polizas.Add(p);
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<bool> ActualizarPolizaAsync(Guid id, ActualizarPolizaRequest req, CancellationToken ct)
    {
        var p = await _db.Polizas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;

        // N-02: snapshot de la vigencia antes del MERGE, para reiniciar la alerta SOLO si cambia (K-08).
        var fechaInicioAntes = p.FechaInicio;
        var fechaFinAntes = p.FechaFin;

        // N-02: el PUT es un MERGE (mismo criterio que contratos): un campo en null = "no enviado" y se
        // conserva. Se valida el ESTADO RESULTANTE (lo que llega o, si no, lo que ya estaba).
        var aseguradoraRes = string.IsNullOrWhiteSpace(req.Aseguradora) ? p.Aseguradora : req.Aseguradora.Trim();
        var numeroRes = req.NumeroPoliza is null ? p.NumeroPoliza : Limpio(req.NumeroPoliza);
        await ValidarPolizaAsync(
            aseguradoraRes,
            req.FechaInicio ?? p.FechaInicio,
            req.FechaFin ?? p.FechaFin,
            req.ValorPoliza ?? p.ValorPoliza,
            req.FormaPagoCuotas ?? p.FormaPagoCuotas,
            req.PagoMensual ?? p.PagoMensual,
            numeroRes, p.Id, ct);

        // MERGE campo a campo: solo se toca lo que vino (null = no enviado). Los textos se vacian con "".
        p.Aseguradora = aseguradoraRes;
        if (req.NumeroPoliza is not null) p.NumeroPoliza = Limpio(req.NumeroPoliza);
        if (req.AseguradoraPersonaId.HasValue) p.AseguradoraPersonaId = req.AseguradoraPersonaId;
        if (req.AseguradoraEmpresaId.HasValue) p.AseguradoraEmpresaId = req.AseguradoraEmpresaId;
        if (req.Corredor is not null) p.Corredor = Limpio(req.Corredor);
        if (req.CorredorPersonaId.HasValue) p.CorredorPersonaId = req.CorredorPersonaId;
        if (req.CorredorEmpresaId.HasValue) p.CorredorEmpresaId = req.CorredorEmpresaId;
        if (req.FechaInicio.HasValue) p.FechaInicio = req.FechaInicio;
        if (req.FechaFin.HasValue) p.FechaFin = req.FechaFin;
        if (req.ValorPoliza.HasValue) p.ValorPoliza = req.ValorPoliza;
        if (req.FormaPagoCuotas.HasValue) p.FormaPagoCuotas = req.FormaPagoCuotas;
        if (req.PagoMensual.HasValue) p.PagoMensual = req.PagoMensual.Value;
        if (req.Cobertura is not null) p.Cobertura = Limpio(req.Cobertura);
        if (req.IncluyeZonasUnidades.HasValue) p.IncluyeZonasUnidades = req.IncluyeZonasUnidades.Value;
        if (req.ValoresAgregados is not null) p.ValoresAgregados = Limpio(req.ValoresAgregados);
        if (req.Observaciones is not null) p.Observaciones = Limpio(req.Observaciones);
        if (req.LimpiarExpediente) p.ExpedienteId = null;
        else if (req.ExpedienteId.HasValue) p.ExpedienteId = req.ExpedienteId;
        if (!string.IsNullOrWhiteSpace(req.PdfOrigenKey)) p.PdfOrigenKey = req.PdfOrigenKey;

        // N-02: reinicia el control de alerta SOLO si cambio la vigencia (antes lo hacia en cada PUT).
        if (p.FechaInicio != fechaInicioAntes || p.FechaFin != fechaFinAntes)
            p.AlertaVencimientoPctNotificado = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarPolizaAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Polizas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        var valores = await _db.PolizaCampoValores.Where(v => v.PolizaId == id).ToListAsync(ct);
        if (valores.Count > 0) _db.PolizaCampoValores.RemoveRange(valores);
        _db.Polizas.Remove(p);   // reclamaciones caen por cascade
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PdfOrigenDescarga?> DescargarPdfOrigenAsync(Guid polizaId, CancellationToken ct)
    {
        var p = await _db.Polizas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == polizaId, ct);
        if (p is null || string.IsNullOrWhiteSpace(p.PdfOrigenKey)) return null;
        var bytes = await _blob.DownloadAsync(p.PdfOrigenKey, ct);
        if (bytes is null) return null;
        var nombre = $"poliza-{(string.IsNullOrWhiteSpace(p.NumeroPoliza) ? p.Id.ToString("N")[..8] : p.NumeroPoliza)}-origen.pdf";
        return new PdfOrigenDescarga(bytes, "application/pdf", nombre);
    }

    // ----------------------------- K-09: validacion de poliza -----------------------------

    /// <summary>
    /// K-09: reglas de negocio de una poliza, compartidas por crear y actualizar. Antes la unica
    /// validacion era "la aseguradora es obligatoria", asi que el API aceptaba y guardaba polizas
    /// imposibles: vigencia al reves, valor asegurado negativo, cero cuotas, numero repetido.
    ///
    /// K-07: la fecha de inicio pasa a ser OBLIGATORIA. Sin ella el semaforo por porcentaje no se
    /// puede calcular y cada consumidor se inventaba un valor distinto (ver SemaforoPoliza).
    ///
    /// Lanza InvalidOperationException con el mensaje en espanol; el controller lo mapea a 400.
    /// </summary>
    private async Task ValidarPolizaAsync(
        string? aseguradora, DateOnly? fechaInicio, DateOnly? fechaFin,
        decimal? valorPoliza, int? formaPagoCuotas, bool pagoMensual,
        string? numeroPoliza, Guid? polizaIdActual, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(aseguradora))
            throw new InvalidOperationException("La aseguradora es obligatoria.");

        if (fechaInicio is not { } ini)
            throw new InvalidOperationException("La fecha de inicio de la vigencia es obligatoria.");

        if (fechaFin is { } fin && fin < ini)
            throw new InvalidOperationException(
                $"La fecha de fin de la vigencia ({fin:dd/MM/yyyy}) no puede ser anterior a la de inicio ({ini:dd/MM/yyyy}).");

        if (valorPoliza is { } vp && vp < 0)
            throw new InvalidOperationException("El valor asegurado no puede ser negativo.");

        // Si no se paga mes a mes, la prima se reparte en cuotas: al menos una.
        if (!pagoMensual && formaPagoCuotas is { } cuotas && cuotas < 1)
            throw new InvalidOperationException("El numero de cuotas debe ser al menos 1.");

        // Unico por copropiedad (el HasQueryFilter ya acota al tenant actual), no global: dos
        // copropiedades distintas pueden tener el mismo numero de poliza.
        if (!string.IsNullOrWhiteSpace(numeroPoliza))
        {
            var numero = numeroPoliza.Trim();
            var repetido = await _db.Polizas.AnyAsync(
                x => x.NumeroPoliza == numero && (polizaIdActual == null || x.Id != polizaIdActual), ct);
            if (repetido)
                throw new InvalidOperationException($"Ya existe una poliza con el numero '{numero}' en esta copropiedad.");
        }
    }

    /// <summary>
    /// K-07: unica definicion del semaforo de una poliza. Habia una por consumidor y no coincidian:
    /// esta pagina pasaba (FechaInicio ?? hoy) -> siempre verde, mientras el job de vencimiento, el
    /// dashboard y los indicadores pasaban (FechaInicio ?? FechaFin) -> total de 0 dias -> rojo
    /// critico. La misma poliza salia verde en /seguros y roja en el panel de la copropiedad.
    ///
    /// Desde K-09 la fecha de inicio es obligatoria, asi que una poliza sin inicio solo puede venir
    /// de datos anteriores: para esas se asume la vigencia anual tipica de una poliza
    /// (inicio = fin - 1 anio), que deja los umbrales 20%/10% en 73 y 36 dias antes del vencimiento
    /// en vez de dar un verde o un rojo falsos.
    /// </summary>
    public static SemaforoContrato SemaforoPoliza(DateOnly? inicio, DateOnly? fin, DateOnly hoy)
    {
        if (fin is not { } f) return SemaforoContrato.Ninguno;
        return MiCopropiedadService.CalcularSemaforoContrato(inicio ?? f.AddYears(-1), f, hoy);
    }

    private static PolizaDto ToDto(Poliza p, IReadOnlyList<PolizaCampoValorDto>? valores = null, int reclCount = 0)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        int? dias = p.FechaFin.HasValue ? p.FechaFin.Value.DayNumber - hoy.DayNumber : null;
        var sem = SemaforoPoliza(p.FechaInicio, p.FechaFin, hoy);
        return new PolizaDto(p.Id, p.NumeroPoliza,
            p.Aseguradora, p.AseguradoraPersonaId, p.AseguradoraEmpresaId,
            p.Corredor, p.CorredorPersonaId, p.CorredorEmpresaId,
            p.FechaInicio, p.FechaFin, p.ValorPoliza, p.FormaPagoCuotas, p.PagoMensual,
            p.Cobertura, p.IncluyeZonasUnidades, p.ValoresAgregados, p.Observaciones,
            p.ExpedienteId, dias, sem, reclCount, valores, !string.IsNullOrEmpty(p.PdfOrigenKey));
    }

    // ----------------------------- Campos EAV -----------------------------
    public async Task<IReadOnlyList<PolizaCampoDto>> ListCamposAsync(CancellationToken ct)
        => await _db.PolizaCampos.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden)
            .Select(c => new PolizaCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.Descripcion, c.Activo))
            .ToListAsync(ct);

    public async Task<PolizaCampoDto> CrearCampoAsync(CrearPolizaCampoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        var orden = (await _db.PolizaCampos.MaxAsync(c => (int?)c.Orden, ct) ?? 0) + 1;
        var c = new PolizaCampo { Label = req.Label.Trim(), Tipo = req.Tipo, Opciones = Limpio(req.Opciones), Descripcion = Limpio(req.Descripcion), Orden = orden };
        _db.PolizaCampos.Add(c);
        await _db.SaveChangesAsync(ct);
        return new PolizaCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.Descripcion, c.Activo);
    }

    public async Task<bool> ActualizarCampoAsync(Guid campoId, ActualizarPolizaCampoRequest req, CancellationToken ct)
    {
        var c = await _db.PolizaCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        c.Label = req.Label.Trim(); c.Tipo = req.Tipo; c.Opciones = Limpio(req.Opciones);
        c.Descripcion = Limpio(req.Descripcion); c.Orden = req.Orden; c.Activo = req.Activo;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct)
    {
        var c = await _db.PolizaCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        c.Activo = false;   // soft-delete: conserva los valores
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> GuardarCampoValorAsync(Guid polizaId, Guid campoId, GuardarPolizaCampoValorRequest req, CancellationToken ct)
    {
        if (!await _db.Polizas.AnyAsync(p => p.Id == polizaId, ct)) return false;
        // K-11: antes no se validaba el campo (se creaba un valor colgando de un campoId inexistente o
        // inactivo) y se guardaba "" en vez de borrar. Mismo patron que los campos de contrato.
        if (!await _db.PolizaCampos.AnyAsync(c => c.Id == campoId && c.Activo, ct)) return false;
        var val = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var v = await _db.PolizaCampoValores.FirstOrDefaultAsync(x => x.PolizaId == polizaId && x.PolizaCampoId == campoId, ct);
        if (v is null)
        {
            if (val is null) return true;   // nada que guardar
            _db.PolizaCampoValores.Add(new PolizaCampoValor { PolizaId = polizaId, PolizaCampoId = campoId, Valor = val });
        }
        else if (val is null)
        {
            _db.PolizaCampoValores.Remove(v);
        }
        else
        {
            v.Valor = val;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Selector de Campos (Fase 1): valores en la forma estandar (espejo de ListTodosCamposValoresEquipoAsync
    // / ListCamposDinEquipoAsync). Proyecciones finas; la logica de campos no se duplica.
    public async Task<IReadOnlyList<PolizaCampoValorFlatDto>> ListTodosCamposValoresPolizaAsync(CancellationToken ct)
        => await _db.PolizaCampoValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new PolizaCampoValorFlatDto(v.PolizaId, v.PolizaCampoId, v.Valor)).ToListAsync(ct);

    public async Task<IReadOnlyList<PolizaCampoDinDto>> ListCamposDinPolizaAsync(Guid polizaId, CancellationToken ct)
    {
        var defs = await _db.PolizaCampos.AsNoTracking().Where(c => c.Activo).OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var vals = await _db.PolizaCampoValores.AsNoTracking().Where(v => v.PolizaId == polizaId).ToListAsync(ct);
        return defs.Select(d => new PolizaCampoDinDto(d.Id, d.Label, d.Orden,
            vals.FirstOrDefault(v => v.PolizaCampoId == d.Id)?.Valor, d.Tipo, d.Opciones)).ToList();
    }

    // ----------------------------- Reclamaciones (Ola 5) -----------------------------
    public async Task<IReadOnlyList<ReclamacionDto>> ListReclamacionesAsync(Guid polizaId, CancellationToken ct)
        => await _db.PolizaReclamaciones.AsNoTracking().Where(r => r.PolizaId == polizaId)
            .OrderByDescending(r => r.Fecha)
            .Select(r => new ReclamacionDto(r.Id, r.Fecha, r.MontoReclamado, r.Descripcion, r.Estado, r.MontoReconocido, r.FechaCierre, r.ExpedienteId))
            .ToListAsync(ct);

    public async Task<ReclamacionDto> CrearReclamacionAsync(Guid polizaId, CrearReclamacionRequest req, CancellationToken ct)
    {
        var poliza = await _db.Polizas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == polizaId, ct);
        if (poliza is null)
            throw new InvalidOperationException("Poliza no encontrada.");
        if (string.IsNullOrWhiteSpace(req.Descripcion))
            throw new InvalidOperationException("Indica que se va a reclamar.");

        // K-09: no se puede reclamar cero (ni un monto negativo), y el siniestro tiene que haber
        // ocurrido dentro de la vigencia: fuera de ella la aseguradora no responde.
        if (req.MontoReclamado <= 0)
            throw new InvalidOperationException("El monto reclamado debe ser mayor que cero.");

        if (poliza.FechaInicio is { } vIni && req.Fecha < vIni)
            throw new InvalidOperationException(
                $"La fecha de la reclamacion ({req.Fecha:dd/MM/yyyy}) es anterior al inicio de la vigencia ({vIni:dd/MM/yyyy}).");

        if (poliza.FechaFin is { } vFin && req.Fecha > vFin)
            throw new InvalidOperationException(
                $"La fecha de la reclamacion ({req.Fecha:dd/MM/yyyy}) es posterior al fin de la vigencia ({vFin:dd/MM/yyyy}).");
        var r = new PolizaReclamacion
        {
            PolizaId = polizaId,
            Fecha = req.Fecha,
            MontoReclamado = req.MontoReclamado,
            Descripcion = req.Descripcion.Trim(),
            Estado = EstadoReclamacion.Vigente,
            ExpedienteId = req.ExpedienteId
        };
        _db.PolizaReclamaciones.Add(r);
        await _db.SaveChangesAsync(ct);
        return new ReclamacionDto(r.Id, r.Fecha, r.MontoReclamado, r.Descripcion, r.Estado, r.MontoReconocido, r.FechaCierre, r.ExpedienteId);
    }

    public async Task<bool> CerrarReclamacionAsync(Guid reclamacionId, CerrarReclamacionRequest req, CancellationToken ct)
    {
        var r = await _db.PolizaReclamaciones.FirstOrDefaultAsync(x => x.Id == reclamacionId, ct);
        if (r is null) return false;
        // K-09: se admitia cerrar reconociendo un monto negativo.
        if (req.MontoReconocido < 0)
            throw new InvalidOperationException("El monto reconocido no puede ser negativo.");
        r.Estado = EstadoReclamacion.Cerrada;
        r.MontoReconocido = req.MontoReconocido;
        r.FechaCierre = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
