using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Documents;
using Propia.Application.ServiciosPublicos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.ServiciosPublicos;

/// <summary>
/// Recibos de EJEMPLO para "Prueba con uno de ejemplo" (pestana Recibos y consumo). Se arman al vuelo con los
/// servicios REALES de la copropiedad (su referencia, titular y topes) y se rinden a PDF con Chromium, para que la
/// IA los lea de verdad por el mismo camino que un recibo subido:
///  - "alto":  energia (o el primer servicio con topes) con un valor ~26% sobre el maximo;
///  - "ok":    acueducto (u otro servicio) con el valor dentro de los topes;
///  - "ajeno": un recibo de otra copropiedad (referencia que no es de ningun servicio).
/// Los datos imitan un recibo colombiano: numero de factura que cambia cada mes, NIT del prestador, periodo que
/// cruza dos meses, saldos y desglose de cobros que cuadra con el total.
/// </summary>
public sealed class ReciboEjemploService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHtmlToPdfService _pdf;
    private static readonly CultureInfo Es = new("es-CO");

    public ReciboEjemploService(PropiaDbContext db, ITenantContext tenant, IHtmlToPdfService pdf) { _db = db; _tenant = tenant; _pdf = pdf; }

    public sealed record Ejemplo(byte[] Pdf, string NombreArchivo);

    // Desglose tipico por tipo (participacion sobre el valor del periodo), el mismo de la replica.
    private static readonly Dictionary<TipoServicioPublico, (string Concepto, decimal Peso)[]> Desglose = new()
    {
        [TipoServicioPublico.EnergiaElectrica] = new[] { ("Consumo de energía", .78m), ("Cargo de comercialización", .06m), ("Alumbrado público", .09m), ("Contribución (20%)", .07m) },
        [TipoServicioPublico.Acueducto] = new[] { ("Consumo acueducto", .42m), ("Cargo fijo acueducto", .08m), ("Consumo alcantarillado", .38m), ("Cargo fijo alcantarillado", .07m), ("Tasa ambiental", .05m) },
        [TipoServicioPublico.Gas] = new[] { ("Consumo de gas", .82m), ("Cargo fijo", .1m), ("Contribución", .08m) },
        [TipoServicioPublico.Aseo] = new[] { ("Recolección y transporte", .55m), ("Barrido y limpieza de vías", .25m), ("Disposición final", .2m) },
        [TipoServicioPublico.Internet] = new[] { ("Plan de internet", .84m), ("IVA (19%)", .16m) },
        [TipoServicioPublico.TelefoniaFija] = new[] { ("Cargo básico", .7m), ("Consumo", .14m), ("IVA (19%)", .16m) },
    };

    public async Task<Ejemplo?> GenerarAsync(string clase, CancellationToken ct)
    {
        clase = (clase ?? "").Trim().ToLowerInvariant();
        if (clase is not ("alto" or "ok" or "ajeno")) return null;
        var copropiedad = _tenant.CurrentTenantId is Guid tid
            ? await _db.Tenants.AsNoTracking().Where(t => t.Id == tid).Select(t => t.Nombre).FirstOrDefaultAsync(ct) : null;
        var activas = await _db.CuentasServicioPublico.AsNoTracking().Include(c => c.Registros)
            .Where(c => c.Activa).OrderBy(c => c.Alias).ToListAsync(ct);
        var rnd = new Random();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

        CuentaServicioPublico? c = clase switch
        {
            "alto" => activas.FirstOrDefault(x => x.Tipo == TipoServicioPublico.EnergiaElectrica && x.NumeroCuenta != null)
                      ?? activas.FirstOrDefault(x => x.NumeroCuenta != null && (x.ValorMax != null || x.ConsumoMax != null)),
            "ok" => activas.FirstOrDefault(x => x.Tipo == TipoServicioPublico.Acueducto && x.NumeroCuenta != null)
                    ?? activas.FirstOrDefault(x => x.NumeroCuenta != null && x.Tipo != TipoServicioPublico.EnergiaElectrica)
                    ?? activas.FirstOrDefault(x => x.NumeroCuenta != null),
            _ => null
        };
        if (clase != "ajeno" && c is null) return null;

        var tipo = c?.Tipo ?? TipoServicioPublico.EnergiaElectrica;
        var unidad = c?.UnidadMedida ?? ReglasServicioPublico.UnidadPorDefecto(tipo) ?? "";
        var u = c?.Registros.OrderByDescending(r => r.Anio * 100 + r.Mes).FirstOrDefault();
        // Periodo: el siguiente al ultimo recibo (segun la periodicidad) o el mes pasado.
        var mesPer = u is not null
            ? new DateOnly(u.Anio, u.Mes, 1).AddMonths(ReglasServicioPublico.Meses(c!.Periodicidad))
            : new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        // Un periodo que cruza dos meses (dia 17 al 16): se registra en el mes de periodo_hasta.
        var hasta = new DateOnly(mesPer.Year, mesPer.Month, 16);
        var desde = hasta.AddMonths(-ReglasServicioPublico.Meses(c?.Periodicidad ?? PeriodicidadServicio.Mensual)).AddDays(1);
        var expedicion = hasta.AddDays(4);
        var dia = Math.Min(c?.DiaPago ?? 15, 28);
        var limite = new DateOnly(mesPer.Year, mesPer.Month, 1).AddMonths(1).AddDays(dia - 1);

        var hist = c?.Registros.Where(r => r.Consumo > 0).ToList() ?? new();
        decimal prom = hist.Count > 0 ? hist.Average(r => r.Consumo) : tipo switch
        {
            TipoServicioPublico.EnergiaElectrica => 4200, TipoServicioPublico.Acueducto => 300, TipoServicioPublico.Gas => 180, _ => 0
        };
        decimal tarifa = hist.Count > 0 ? hist[^1].Valor / hist[^1].Consumo : tipo switch
        {
            TipoServicioPublico.EnergiaElectrica => 890, TipoServicioPublico.Acueducto => 6200, TipoServicioPublico.Gas => 3100, _ => 0
        };
        decimal consumo, valor;
        static decimal Cien(decimal v) => Math.Round(v / 100m, MidpointRounding.AwayFromZero) * 100m;
        if (clase == "alto")
        {
            consumo = Math.Round((c!.ConsumoMax ?? prom) * 1.24m);
            valor = c.ValorMax is { } vmax ? Cien(vmax * 1.26m) : Cien(consumo * (tarifa > 0 ? tarifa : 890));
        }
        else if (clase == "ok")
        {
            consumo = Math.Round(prom * 1.01m);
            valor = c!.ValorMin is { } a && c.ValorMax is { } b ? Cien((a + b) / 2)
                  : u is not null ? Cien(u.Valor) : Cien(consumo * (tarifa > 0 ? tarifa : 6200));
        }
        else { consumo = 4310; valor = 3_836_000; }
        if (prom == 0 && clase != "ajeno") consumo = 0;
        if (valor <= 0) valor = 410_000;

        var lectAnt = 10000 + rnd.Next(80000);
        var conceptos = Repartir(Desglose.TryGetValue(tipo, out var d) ? d : new[] { ("Valor del servicio", 1m) }, valor);
        var saldo = clase == "alto" ? 0m : 0m;
        var referencia = clase == "ajeno" ? "4455667788" : c!.NumeroCuenta!;
        var prestador = clase == "ajeno" ? "EPM" : (string.IsNullOrWhiteSpace(c!.Prestador) ? "Empresa de servicios" : c.Prestador);
        var titular = clase == "ajeno" ? "Conjunto Residencial Las Palmas" : (copropiedad ?? "Copropiedad");
        var direccion = clase == "ajeno" ? "Calle 48 Sur # 39-120, Envigado" : "Zonas comunes - " + (copropiedad ?? "copropiedad");
        var factura = rnd.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        var nit = prestador.Contains("EPM", StringComparison.OrdinalIgnoreCase) ? "890.904.996-1"
                : prestador.Contains("Vanti", StringComparison.OrdinalIgnoreCase) ? "800.007.813-5"
                : prestador.Contains("Claro", StringComparison.OrdinalIgnoreCase) ? "800.153.993-7" : "900.123.456-7";

        var html = Html(prestador, nit, NombreTipo(tipo), titular, direccion, referencia, factura, desde, hasta, expedicion, limite,
            consumo > 0 ? lectAnt : null, consumo > 0 ? lectAnt + consumo : null, consumo, unidad, conceptos, valor, saldo);
        var pdf = await _pdf.RenderAsync(html, ct);
        var nombre = clase switch
        {
            "alto" => "Factura_EPM_energia.pdf",
            "ok" => "Factura_EPM_aguas.pdf",
            _ => "Recibo_energia_otro_conjunto.pdf"
        };
        return new Ejemplo(pdf, nombre);
    }

    private static string NombreTipo(TipoServicioPublico t) => ServiciosPublicosService.NombreTipo(t);

    // Reparte el valor en conceptos redondeados a 100; el ultimo absorbe la diferencia (cuadra exacto).
    private static List<(string Concepto, decimal Valor)> Repartir((string Concepto, decimal Peso)[] base_, decimal total)
    {
        var outp = new List<(string, decimal)>(); decimal acum = 0;
        for (var i = 0; i < base_.Length; i++)
        {
            var v = i == base_.Length - 1 ? total - acum : Math.Round(total * base_[i].Peso / 100m, MidpointRounding.AwayFromZero) * 100m;
            acum += v; outp.Add((base_[i].Concepto, v));
        }
        return outp;
    }

    private static string F(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static string M(decimal v) => "$ " + v.ToString("#,##0", Es);
    private static string E(string s) => WebUtility.HtmlEncode(s);

    private static string Html(string prestador, string nit, string servicio, string titular, string direccion, string referencia,
        string factura, DateOnly desde, DateOnly hasta, DateOnly expedicion, DateOnly limite, decimal? lectAnt, decimal? lectAct,
        decimal consumo, string unidad, List<(string Concepto, decimal Valor)> conceptos, decimal valor, decimal saldo)
    {
        var filas = string.Concat(conceptos.Select(k => $"<tr><td>{E(k.Concepto)}</td><td class=r>{M(k.Valor)}</td></tr>"));
        var lecturas = lectAnt is { } la && lectAct is { } lc
            ? $@"<table class=t><tr><th>Lectura anterior</th><th>Lectura actual</th><th>Consumo</th></tr>
                 <tr><td>{la.ToString("#,##0", Es)}</td><td>{lc.ToString("#,##0", Es)}</td><td>{consumo.ToString("#,##0", Es)} {E(unidad)}</td></tr></table>"
            : "<p class=s>Servicio de cargo fijo: no tiene medidor.</p>";
        return $@"<!doctype html><html><head><meta charset=utf-8><style>
@page {{ size: Letter; margin: 16mm; }}
body {{ font-family: Arial, Helvetica, sans-serif; font-size: 11px; color: #222; }}
.h {{ display: flex; justify-content: space-between; border-bottom: 3px solid #0a6b3a; padding-bottom: 8px; }}
.h b {{ font-size: 20px; color: #0a6b3a; }} .s {{ color: #555; }}
.box {{ border: 1px solid #bbb; border-radius: 6px; padding: 8px 10px; margin-top: 10px; }}
.g {{ display: grid; grid-template-columns: 1fr 1fr; gap: 4px 18px; }}
.t {{ width: 100%; border-collapse: collapse; margin-top: 8px; }} .t th, .t td {{ border: 1px solid #ccc; padding: 4px 6px; text-align: left; }}
.r {{ text-align: right; }} .tot td {{ font-weight: bold; font-size: 13px; }}
.pague {{ background: #0a6b3a; color: #fff; padding: 8px 10px; border-radius: 6px; margin-top: 10px; display: flex; justify-content: space-between; font-size: 13px; }}
</style></head><body>
<div class=h><div><b>{E(prestador)}</b><div class=s>NIT {E(nit)} &middot; Empresa de servicios públicos domiciliarios</div></div>
<div style='text-align:right'><div>Factura de servicios públicos</div><div>Factura electrónica No. <b style='font-size:12px;color:#222'>FE-{factura}</b></div></div></div>
<div class=box><div class=g>
<div>Suscriptor / Titular: <b>{E(titular)}</b></div><div>Servicio: <b>{E(servicio)}</b></div>
<div>Dirección del suministro: {E(direccion)}</div><div>Contrato / Referencia de pago: <b>{E(referencia)}</b></div>
<div>Periodo facturado: {F(desde)} a {F(hasta)}</div><div>Fecha de expedición: {F(expedicion)}</div>
</div></div>
<div class=box><b>Consumo del periodo</b>{lecturas}</div>
<div class=box><b>Detalle de cobros</b><table class=t><tr><th>Concepto</th><th class=r>Valor</th></tr>{filas}
<tr class=tot><td>Total servicios del periodo</td><td class=r>{M(valor)}</td></tr>
<tr><td>Saldo anterior</td><td class=r>{M(saldo)}</td></tr>
<tr class=tot><td>TOTAL A PAGAR</td><td class=r>{M(valor + saldo)}</td></tr></table></div>
<div class=pague><span>Pague hasta: <b>{F(limite)}</b></span><span>Referencia de pago {E(referencia)}</span><span><b>{M(valor + saldo)}</b></span></div>
<p class=s>Recibo de ejemplo generado por PROPIA para probar la lectura con IA. No es un documento real.</p>
</body></html>";
    }
}
