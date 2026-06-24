using Microsoft.EntityFrameworkCore;
using Propia.Application.ServiciosPublicos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.ServiciosPublicos;

/// <summary>Modulo 2.17 Servicios publicos. CRUD de cuentas, consumos y reclamaciones.</summary>
public class ServiciosPublicosService : IServiciosPublicosService
{
    private readonly PropiaDbContext _db;
    public ServiciosPublicosService(PropiaDbContext db) => _db = db;

    private static readonly string[] _meses =
        { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };
    private static string PeriodoLabel(int anio, int mes)
        => $"{(mes >= 1 && mes <= 12 ? _meses[mes - 1] : mes.ToString())} {anio}";
    private static int Orden(RegistroConsumoServicio r) => r.Anio * 100 + r.Mes;

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
            if (Variacion(ords) is { } v && v > c.UmbralAlertaPct) conAlerta++;
        }

        return new ServiciosPublicosResumenDto(
            cuentas.Count(c => c.Activa), gasto, conAlerta, reclAbiertas);
    }

    public async Task<IReadOnlyList<CuentaServicioDto>> ListCuentasAsync(CancellationToken ct)
    {
        var cuentas = await _db.CuentasServicioPublico.AsNoTracking()
            .Include(c => c.Registros).OrderBy(c => c.Alias).ToListAsync(ct);
        return cuentas.Select(ToCuentaDto).ToList();
    }

    public async Task<CuentaServicioDetalleDto?> GetCuentaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.CuentasServicioPublico.AsNoTracking()
            .Include(x => x.Registros).Include(x => x.Reclamaciones)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        var regsOrden = c.Registros.OrderBy(Orden).ToList();   // cronologico ascendente
        var registros = new List<RegistroConsumoDto>();
        for (int i = 0; i < regsOrden.Count; i++)
        {
            var r = regsOrden[i];
            decimal? varPct = i > 0 && regsOrden[i - 1].Consumo != 0
                ? Math.Round((r.Consumo - regsOrden[i - 1].Consumo) / regsOrden[i - 1].Consumo * 100, 1)
                : null;
            registros.Add(new RegistroConsumoDto(r.Id, r.CuentaServicioId, r.Anio, r.Mes,
                PeriodoLabel(r.Anio, r.Mes), r.Consumo, r.Valor, r.Estado,
                varPct, varPct is { } vp && vp > c.UmbralAlertaPct, r.NotaAdmin));
        }
        registros.Reverse(); // mostrar mas reciente primero

        var recl = c.Reclamaciones.OrderByDescending(x => x.Fecha)
            .Select(ToReclDto).ToList();
        decimal? prom = c.Registros.Count > 0
            ? Math.Round(c.Registros.Average(x => x.Consumo), 1) : null;

        return new CuentaServicioDetalleDto(ToCuentaDto(c), registros, recl, prom);
    }

    public async Task<CuentaServicioDto> CrearCuentaAsync(CrearCuentaServicioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Alias)) throw new InvalidOperationException("Alias obligatorio.");
        var c = new CuentaServicioPublico
        {
            Tipo = req.Tipo,
            Alias = req.Alias.Trim(),
            Prestador = (req.Prestador ?? "").Trim(),
            NumeroCuenta = req.NumeroCuenta?.Trim(),
            MetodoPago = req.MetodoPago?.Trim(),
            UnidadMedida = req.UnidadMedida?.Trim(),
            UmbralAlertaPct = req.UmbralAlertaPct <= 0 ? 25 : req.UmbralAlertaPct
        };
        _db.CuentasServicioPublico.Add(c);
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
        var r = new RegistroConsumoServicio
        {
            CuentaServicioId = cuentaId,
            Anio = req.Anio,
            Mes = req.Mes,
            Consumo = req.Consumo,
            Valor = req.Valor,
            Estado = req.Estado,
            NotaAdmin = req.NotaAdmin?.Trim()
        };
        _db.RegistrosConsumoServicio.Add(r);
        await _db.SaveChangesAsync(ct);

        var previos = c.Registros.Where(x => Orden(x) < Orden(r)).OrderByDescending(Orden).FirstOrDefault();
        decimal? varPct = previos is not null && previos.Consumo != 0
            ? Math.Round((r.Consumo - previos.Consumo) / previos.Consumo * 100, 1) : null;
        return new RegistroConsumoDto(r.Id, r.CuentaServicioId, r.Anio, r.Mes,
            PeriodoLabel(r.Anio, r.Mes), r.Consumo, r.Valor, r.Estado,
            varPct, varPct is { } vp && vp > c.UmbralAlertaPct, r.NotaAdmin);
    }

    public async Task<bool> EliminarRegistroAsync(Guid registroId, CancellationToken ct)
    {
        var r = await _db.RegistrosConsumoServicio.FirstOrDefaultAsync(x => x.Id == registroId, ct);
        if (r is null) return false;
        _db.RegistrosConsumoServicio.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }

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

    // ----- helpers -----
    private static decimal? Variacion(List<RegistroConsumoServicio> ordsDesc)
    {
        if (ordsDesc.Count < 2 || ordsDesc[1].Consumo == 0) return null;
        return Math.Round((ordsDesc[0].Consumo - ordsDesc[1].Consumo) / ordsDesc[1].Consumo * 100, 1);
    }

    private static CuentaServicioDto ToCuentaDto(CuentaServicioPublico c)
    {
        var ords = c.Registros.OrderByDescending(Orden).ToList();
        var varPct = Variacion(ords);
        return new CuentaServicioDto(c.Id, c.Tipo, c.Alias, c.Prestador, c.NumeroCuenta, c.MetodoPago,
            c.UnidadMedida, c.UmbralAlertaPct, c.Activa,
            varPct is { } v && v > c.UmbralAlertaPct,
            ords.Count > 0 ? ords[0].Valor : null, varPct);
    }

    private static ReclamacionServicioDto ToReclDto(ReclamacionServicio r)
        => new(r.Id, r.CuentaServicioId, r.Motivo, r.Radicado, r.Descripcion, r.Estado, r.Fecha);
}
