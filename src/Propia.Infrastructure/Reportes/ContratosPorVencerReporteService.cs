using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Reportes;
using Propia.Domain.Enums;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Reportes;

/// <summary>
/// Reporte "Contratos proximos a vencer" multi-copropiedad. Recorre las copropiedades que la
/// persona administra (get_tenants_for_persona), cambia el contexto de tenant por cada una
/// (SetTenantSqlAsync: EF query filter + RLS) y acumula los contratos con semaforo amarillo/rojo
/// o vencidos. Restaura el tenant activo al terminar.
/// </summary>
public sealed class ContratosPorVencerReporteService : IContratosPorVencerReporteService
{
    private static readonly XLColor Brand = XLColor.FromHtml("#6D4FE3");
    private static readonly XLColor Ink = XLColor.FromHtml("#1B2A3A");

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextAccessor _http;

    public ContratosPorVencerReporteService(PropiaDbContext db, ITenantContext tenant, IHttpContextAccessor http)
    {
        _db = db;
        _tenant = tenant;
        _http = http;
    }

    public async Task<ContratosPorVencerReporteDto> GetAsync(IReadOnlyList<Guid>? tenantIds, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var copros = await CopropiedadesDelClienteAsync(ct);   // id -> (nombre, codigo)

        // Filtra a las copropiedades pedidas (interseccion con las que administra); vacio = todas.
        var ids = (tenantIds is { Count: > 0 })
            ? copros.Keys.Where(tenantIds.Contains).ToList()
            : copros.Keys.ToList();

        var filas = new List<ContratoPorVencerFila>();
        // TODOS los contratos (para la analitica de costos), no solo los por vencer.
        var todos = new List<(DateOnly Ini, DateOnly? Fin, decimal? Mensual, decimal? Total, string Cat)>();
        var tenantOriginal = _tenant.CurrentTenantId;
        try
        {
            foreach (var tid in ids)
            {
                await SetTenantSqlAsync(tid, ct);
                var (nombre, codigo) = copros[tid];

                var contratos = await _db.ContratosServicio.AsNoTracking()
                    .Select(c => new
                    {
                        c.Id, c.NumeroContrato, c.Proveedor, c.Categoria, c.TipoContrato,
                        c.FechaInicio, c.FechaFin, c.ValorTotal, c.ValorMensual
                    })
                    .ToListAsync(ct);

                foreach (var c in contratos)
                {
                    todos.Add((c.FechaInicio, c.FechaFin, c.ValorMensual, c.ValorTotal,
                        c.Categoria?.ToString() ?? "Sin categoria"));

                    // Tabla "por vencer": requiere FechaFin y semaforo amarillo/rojo o vencido.
                    if (c.FechaFin is null) continue;
                    var sem = MiCopropiedadService.CalcularSemaforoContrato(c.FechaInicio, c.FechaFin, hoy);
                    var vencido = c.FechaFin is { } f && f < hoy;
                    if (!vencido && sem is not (SemaforoContrato.Amarillo or SemaforoContrato.Rojo)) continue;

                    var dias = c.FechaFin is { } ff ? ff.DayNumber - hoy.DayNumber : (int?)null;
                    var pct = CalcularPct(c.FechaInicio, c.FechaFin, hoy);

                    filas.Add(new ContratoPorVencerFila(
                        tid, nombre, codigo, c.Id, c.NumeroContrato,
                        string.IsNullOrWhiteSpace(c.Proveedor) ? "(sin proveedor)" : c.Proveedor,
                        c.Categoria?.ToString(), c.TipoContrato?.ToString(),
                        c.FechaInicio, c.FechaFin, dias, pct,
                        vencido ? "rojo" : (sem == SemaforoContrato.Rojo ? "rojo" : "amarillo"),
                        vencido, c.ValorTotal ?? c.ValorMensual));
                }
            }
        }
        finally
        {
            if (tenantOriginal is { } to) await SetTenantSqlAsync(to, ct);
        }

        // Orden: mas urgente primero (vencidos, luego menos dias restantes).
        filas = filas.OrderBy(f => f.Vencido ? -1 : (f.DiasRestantes ?? int.MaxValue)).ToList();

        var resumen = Resumir(filas);
        var analitica = Analizar(todos, hoy);
        return new ContratosPorVencerReporteDto(filas, resumen, analitica);
    }

    private static int CalcularPct(DateOnly inicio, DateOnly? fin, DateOnly hoy)
    {
        if (fin is not { } f) return 0;
        var total = f.DayNumber - inicio.DayNumber;
        if (total <= 0) return 100;
        var transcurrido = hoy.DayNumber - inicio.DayNumber;
        var pct = (int)Math.Round(100.0 * transcurrido / total);
        return Math.Clamp(pct, 0, 100);
    }

    private static ContratosPorVencerResumen Resumir(List<ContratoPorVencerFila> filas)
    {
        var amarillo = filas.Count(f => !f.Vencido && f.Semaforo == "amarillo");
        var rojo = filas.Count(f => !f.Vencido && f.Semaforo == "rojo");
        var vencidos = filas.Count(f => f.Vencido);
        var valor = filas.Sum(f => f.Valor ?? 0m);

        var porCopro = filas
            .GroupBy(f => new { f.TenantId, f.Copropiedad, f.CodigoCorto })
            .Select(g => new ContratosPorCopropiedad(
                g.Key.TenantId, g.Key.Copropiedad, g.Key.CodigoCorto,
                g.Count(),
                g.Count(x => !x.Vencido && x.Semaforo == "amarillo"),
                g.Count(x => !x.Vencido && x.Semaforo == "rojo"),
                g.Count(x => x.Vencido),
                g.Sum(x => x.Valor ?? 0m)))
            .OrderByDescending(x => x.Cantidad)
            .ToList();

        return new ContratosPorVencerResumen(filas.Count, amarillo, rojo, vencidos, valor, porCopro);
    }

    private static readonly string[] _meses =
        { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

    // Analitica de costos sobre TODOS los contratos: costo mensual/anual, por categoria y
    // proyeccion del costo comprometido para los proximos 12 meses (baja al vencer contratos).
    private static ContratosAnalitica Analizar(
        List<(DateOnly Ini, DateOnly? Fin, decimal? Mensual, decimal? Total, string Cat)> todos, DateOnly hoy)
    {
        var activos = todos.Where(t => t.Fin is null || t.Fin >= hoy).ToList();
        var costoMensual = activos.Sum(t => t.Mensual ?? 0m);
        var valorContratado = activos.Sum(t => t.Total ?? 0m);

        var porCategoria = activos
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Cat) ? "Sin categoria" : t.Cat)
            .Select(g => new CategoriaCosto(g.Key, g.Count(), g.Sum(x => x.Mensual ?? 0m), g.Sum(x => x.Mensual ?? 0m) * 12))
            .OrderByDescending(x => x.ValorMensual).ThenByDescending(x => x.Cantidad)
            .ToList();

        var proy = new List<MesCosto>();
        var primerMes = new DateOnly(hoy.Year, hoy.Month, 1);
        for (var m = 0; m < 12; m++)
        {
            var ini = primerMes.AddMonths(m);
            var fin = ini.AddMonths(1).AddDays(-1);
            var costo = todos
                .Where(t => t.Ini <= fin && (t.Fin is null || t.Fin >= ini))
                .Sum(t => t.Mensual ?? 0m);
            proy.Add(new MesCosto(_meses[ini.Month - 1], ini.Year, costo));
        }

        return new ContratosAnalitica(activos.Count, costoMensual, costoMensual * 12, valorContratado, porCategoria, proy);
    }

    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarExcelAsync(IReadOnlyList<Guid>? tenantIds, CancellationToken ct)
    {
        var data = await GetAsync(tenantIds, ct);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Contratos por vencer");

        var headers = new[] { "COPROPIEDAD", "CODIGO", "CONTRATO", "PROVEEDOR", "CATEGORIA", "TIPO",
            "INICIO", "FIN", "% TRANSCURRIDO", "DIAS RESTANTES", "SEMAFORO", "VALOR" };

        // Banner
        var banner = ws.Range(1, 1, 1, headers.Length).Merge();
        banner.Value = "PROPIA   |   Contratos proximos a vencer";
        banner.Style.Fill.BackgroundColor = Brand;
        banner.Style.Font.FontColor = XLColor.White;
        banner.Style.Font.Bold = true;
        banner.Style.Font.FontSize = 13;
        banner.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 24;

        for (var i = 0; i < headers.Length; i++)
        {
            var h = ws.Cell(2, i + 1);
            h.Value = headers[i];
            h.Style.Font.Bold = true;
            h.Style.Fill.BackgroundColor = Ink;
            h.Style.Font.FontColor = XLColor.White;
        }

        var r = 3;
        foreach (var f in data.Filas)
        {
            ws.Cell(r, 1).Value = f.Copropiedad;
            ws.Cell(r, 2).Value = f.CodigoCorto ?? "";
            ws.Cell(r, 3).Value = f.NumeroContrato ?? "";
            ws.Cell(r, 4).Value = f.Proveedor;
            ws.Cell(r, 5).Value = f.Categoria ?? "";
            ws.Cell(r, 6).Value = f.TipoContrato ?? "";
            ws.Cell(r, 7).Value = f.FechaInicio?.ToString("yyyy-MM-dd") ?? "";
            ws.Cell(r, 8).Value = f.FechaFin?.ToString("yyyy-MM-dd") ?? "";
            ws.Cell(r, 9).Value = f.PctTranscurrido;
            ws.Cell(r, 10).Value = f.Vencido ? "Vencido" : (f.DiasRestantes?.ToString() ?? "");
            ws.Cell(r, 11).Value = f.Vencido ? "Vencido" : f.Semaforo;
            if (f.Valor is { } v) ws.Cell(r, 12).Value = v;
            // Color del semaforo en la fila
            var col = f.Vencido || f.Semaforo == "rojo" ? XLColor.FromHtml("#FDECEC")
                    : XLColor.FromHtml("#FFF6E6");
            ws.Cell(r, 11).Style.Fill.BackgroundColor = col;
            r++;
        }

        ws.Columns(1, headers.Length).AdjustToContents();
        ws.SheetView.FreezeRows(2);

        // ---- Hoja 2: Analisis de costos ----
        var a = data.Analitica;
        var wa = wb.AddWorksheet("Analisis");
        var b2 = wa.Range(1, 1, 1, 4).Merge();
        b2.Value = "PROPIA   |   Analisis de costos de contratos";
        b2.Style.Fill.BackgroundColor = Brand; b2.Style.Font.FontColor = XLColor.White;
        b2.Style.Font.Bold = true; b2.Style.Font.FontSize = 13; wa.Row(1).Height = 24;

        wa.Cell(3, 1).Value = "Contratos activos"; wa.Cell(3, 2).Value = a.ContratosActivos;
        wa.Cell(4, 1).Value = "Costo mensual"; wa.Cell(4, 2).Value = a.CostoMensual;
        wa.Cell(5, 1).Value = "Costo anual proyectado"; wa.Cell(5, 2).Value = a.CostoAnualProyectado;
        wa.Cell(6, 1).Value = "Valor contratado"; wa.Cell(6, 2).Value = a.ValorContratado;
        foreach (var rr in new[] { 3, 4, 5, 6 }) wa.Cell(rr, 1).Style.Font.Bold = true;

        wa.Cell(8, 1).Value = "COSTO POR CATEGORIA"; wa.Cell(8, 1).Style.Font.Bold = true; wa.Cell(8, 1).Style.Font.FontColor = Brand;
        wa.Cell(9, 1).Value = "Categoria"; wa.Cell(9, 2).Value = "Cantidad"; wa.Cell(9, 3).Value = "Valor mensual"; wa.Cell(9, 4).Value = "Valor anual";
        foreach (var c in new[] { 1, 2, 3, 4 }) { wa.Cell(9, c).Style.Font.Bold = true; wa.Cell(9, c).Style.Fill.BackgroundColor = Ink; wa.Cell(9, c).Style.Font.FontColor = XLColor.White; }
        var rc = 10;
        foreach (var cat in a.PorCategoria)
        {
            wa.Cell(rc, 1).Value = cat.Categoria; wa.Cell(rc, 2).Value = cat.Cantidad;
            wa.Cell(rc, 3).Value = cat.ValorMensual; wa.Cell(rc, 4).Value = cat.ValorAnual; rc++;
        }

        var rp = rc + 1;
        wa.Cell(rp, 1).Value = "PROYECCION MENSUAL (12 MESES)"; wa.Cell(rp, 1).Style.Font.Bold = true; wa.Cell(rp, 1).Style.Font.FontColor = Brand; rp++;
        wa.Cell(rp, 1).Value = "Mes"; wa.Cell(rp, 2).Value = "Costo comprometido";
        wa.Cell(rp, 1).Style.Font.Bold = true; wa.Cell(rp, 2).Style.Font.Bold = true; rp++;
        foreach (var mc in a.ProyeccionMensual)
        {
            wa.Cell(rp, 1).Value = $"{mc.Mes} {mc.Anio}"; wa.Cell(rp, 2).Value = mc.Costo; rp++;
        }
        wa.Columns(1, 4).AdjustToContents();

        wb.Properties.Author = "PROPIA";
        wb.Properties.Title = "Contratos proximos a vencer";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var nombre = $"Contratos por vencer {DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}.xlsx";
        return (ms.ToArray(), nombre);
    }

    // ===================== Multi-copropiedad =====================
    private async Task SetTenantSqlAsync(Guid tenantId, CancellationToken ct)
    {
        _tenant.SetTenant(tenantId);
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @t, false)";
        var p = cmd.CreateParameter(); p.ParameterName = "@t"; p.Value = tenantId.ToString(); cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<Dictionary<Guid, (string Nombre, string? Codigo)>> CopropiedadesDelClienteAsync(CancellationToken ct)
    {
        var personaId = Guid.TryParse(_http.HttpContext?.User?.FindFirst("persona_id")?.Value, out var pid) ? pid : (Guid?)null;
        var ids = new List<Guid>();
        if (personaId is not null)
        {
            var conn = _db.Database.GetDbConnection();
            var abierta = conn.State != System.Data.ConnectionState.Open;
            if (abierta) await conn.OpenAsync(ct);
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT tenant_id FROM get_tenants_for_persona(@p)";
                var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = personaId.Value; cmd.Parameters.Add(p);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0));
            }
            finally { if (abierta) await conn.CloseAsync(); }
        }
        if (ids.Count == 0 && _tenant.CurrentTenantId is { } curr) ids.Add(curr);

        var lista = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Nombre, t.CodigoCorto })
            .ToListAsync(ct);
        return lista.ToDictionary(x => x.Id, x => (x.Nombre, (string?)x.CodigoCorto));
    }
}
