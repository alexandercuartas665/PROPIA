using Microsoft.EntityFrameworkCore;
using Propia.Application.Billing;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Job de cobro recurrente (modulo 0.2, antes Fase 2). Cada dia busca las suscripciones activas
/// cuyo FechaProximoCobro ya vencio, genera la factura del periodo, registra un intento de cobro
/// y avanza la fecha del proximo cobro segun el ciclo. En dev/MVP el intento queda Pendiente
/// (cobro manual via pasarela); cuando el WompiApiClient este cableado, este job ejecutara el
/// debito automatico contra la fuente de pago guardada. Portado del concepto de CUBOT.travels.
/// </summary>
public class CobroRecurrenteJob : IBackgroundJob
{
    public string Nombre => "CobroRecurrente";
    public int FrecuenciaMinutos => 60 * 24; // diario

    private readonly PropiaDbContext _db;
    private readonly IBillingService _billing;

    public CobroRecurrenteJob(PropiaDbContext db, IBillingService billing)
    {
        _db = db;
        _billing = billing;
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var pendientes = await _db.Suscripciones
            .Where(s => s.Estado == EstadoSuscripcion.Activa && s.FechaProximoCobro <= hoy
                        && !s.Plan!.EsPromocional)  // planes promocionales/cortesia no se facturan
            .OrderBy(s => s.FechaProximoCobro)
            .Take(500) // cota por tick para no saturar
            .ToListAsync(ct);

        int facturasGeneradas = 0, intentos = 0;

        foreach (var s in pendientes)
        {
            var desde = s.FechaProximoCobro;
            var hasta = s.Ciclo == CicloFacturacion.Anual
                ? desde.AddYears(1).AddDays(-1)
                : desde.AddMonths(1).AddDays(-1);

            // Reusa el calculo de monto/impuesto probado de BillingService (mismo DbContext scoped).
            var factura = await _billing.GenerarFacturaAsync(
                new GenerarFacturaRequest(s.Id, desde, hasta),
                Guid.Empty, "system@propia", null, ct);
            facturasGeneradas++;

            _db.IntentosCobro.Add(new IntentoCobro
            {
                FacturaId = factura.Id,
                SuscripcionId = s.Id,
                NumeroIntento = 1,
                FechaIntento = DateTimeOffset.UtcNow,
                Resultado = ResultadoIntentoCobro.Pendiente,
                DescripcionError = "Cobro automatico encolado (pasarela pendiente de cableado).",
            });
            intentos++;

            // Avanza el proximo cobro segun ciclo.
            s.FechaProximoCobro = s.Ciclo == CicloFacturacion.Anual
                ? hasta.AddDays(1).AddYears(0)
                : hasta.AddDays(1);
        }

        await _db.SaveChangesAsync(ct);
        return new { procesadas = pendientes.Count, facturasGeneradas, intentos, fecha = hoy.ToString("yyyy-MM-dd") };
    }
}
