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
    public SegurosService(PropiaDbContext db) => _db = db;

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
        if (string.IsNullOrWhiteSpace(req.Aseguradora))
            throw new InvalidOperationException("La aseguradora es obligatoria.");
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
            ExpedienteId = req.ExpedienteId
        };
        _db.Polizas.Add(p);
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<bool> ActualizarPolizaAsync(Guid id, ActualizarPolizaRequest req, CancellationToken ct)
    {
        var p = await _db.Polizas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (string.IsNullOrWhiteSpace(req.Aseguradora))
            throw new InvalidOperationException("La aseguradora es obligatoria.");
        p.Aseguradora = req.Aseguradora.Trim();
        p.NumeroPoliza = Limpio(req.NumeroPoliza);
        p.AseguradoraPersonaId = req.AseguradoraPersonaId;
        p.AseguradoraEmpresaId = req.AseguradoraEmpresaId;
        p.Corredor = Limpio(req.Corredor);
        p.CorredorPersonaId = req.CorredorPersonaId;
        p.CorredorEmpresaId = req.CorredorEmpresaId;
        p.FechaInicio = req.FechaInicio;
        p.FechaFin = req.FechaFin;
        p.ValorPoliza = req.ValorPoliza;
        p.FormaPagoCuotas = req.FormaPagoCuotas;
        p.PagoMensual = req.PagoMensual;
        p.Cobertura = Limpio(req.Cobertura);
        p.IncluyeZonasUnidades = req.IncluyeZonasUnidades;
        p.ValoresAgregados = Limpio(req.ValoresAgregados);
        p.Observaciones = Limpio(req.Observaciones);
        if (req.LimpiarExpediente) p.ExpedienteId = null;
        else if (req.ExpedienteId.HasValue) p.ExpedienteId = req.ExpedienteId;
        // Vigencia cambiada: reinicia el control de alerta para que el job reevalue.
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

    private static PolizaDto ToDto(Poliza p, IReadOnlyList<PolizaCampoValorDto>? valores = null, int reclCount = 0)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        int? dias = p.FechaFin.HasValue ? p.FechaFin.Value.DayNumber - hoy.DayNumber : null;
        var sem = MiCopropiedadService.CalcularSemaforoContrato(p.FechaInicio ?? hoy, p.FechaFin, hoy);
        return new PolizaDto(p.Id, p.NumeroPoliza,
            p.Aseguradora, p.AseguradoraPersonaId, p.AseguradoraEmpresaId,
            p.Corredor, p.CorredorPersonaId, p.CorredorEmpresaId,
            p.FechaInicio, p.FechaFin, p.ValorPoliza, p.FormaPagoCuotas, p.PagoMensual,
            p.Cobertura, p.IncluyeZonasUnidades, p.ValoresAgregados, p.Observaciones,
            p.ExpedienteId, dias, sem, reclCount, valores);
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
        var v = await _db.PolizaCampoValores.FirstOrDefaultAsync(x => x.PolizaId == polizaId && x.PolizaCampoId == campoId, ct);
        if (v is null)
        {
            v = new PolizaCampoValor { PolizaId = polizaId, PolizaCampoId = campoId, Valor = req.Valor };
            _db.PolizaCampoValores.Add(v);
        }
        else v.Valor = req.Valor;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Reclamaciones (Ola 5) -----------------------------
    public async Task<IReadOnlyList<ReclamacionDto>> ListReclamacionesAsync(Guid polizaId, CancellationToken ct)
        => await _db.PolizaReclamaciones.AsNoTracking().Where(r => r.PolizaId == polizaId)
            .OrderByDescending(r => r.Fecha)
            .Select(r => new ReclamacionDto(r.Id, r.Fecha, r.MontoReclamado, r.Descripcion, r.Estado, r.MontoReconocido, r.FechaCierre, r.ExpedienteId))
            .ToListAsync(ct);

    public async Task<ReclamacionDto> CrearReclamacionAsync(Guid polizaId, CrearReclamacionRequest req, CancellationToken ct)
    {
        if (!await _db.Polizas.AnyAsync(p => p.Id == polizaId, ct))
            throw new InvalidOperationException("Poliza no encontrada.");
        if (string.IsNullOrWhiteSpace(req.Descripcion))
            throw new InvalidOperationException("Indica que se va a reclamar.");
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
        r.Estado = EstadoReclamacion.Cerrada;
        r.MontoReconocido = req.MontoReconocido;
        r.FechaCierre = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
