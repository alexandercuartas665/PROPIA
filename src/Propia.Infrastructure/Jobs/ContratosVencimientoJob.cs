using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Seguros;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Job de vencimiento de contratos (Ola 3). Recorre los contratos con fecha de finalizacion,
/// calcula el semaforo por % de dias totales y genera una AlertaCopropiedad al cruzar el 20%
/// (amarillo) y el 10% (rojo). Cada umbral alerta una sola vez (AlertaVencimientoPctNotificado);
/// al renovar (vuelve a verde) el contador se resetea. Corre sin contexto de request, por lo que
/// itera los tenants (tabla global) y hace SetTenant para respetar RLS/query filter.
/// </summary>
public class ContratosVencimientoJob : IBackgroundJob
{
    public string Nombre => "ContratosVencimiento";
    public int FrecuenciaMinutos => 60 * 12; // 2 veces al dia

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ContratosVencimientoJob> _log;
    public ContratosVencimientoJob(PropiaDbContext db, ITenantContext tenant, ILogger<ContratosVencimientoJob> log)
    {
        _db = db;
        _tenant = tenant;
        _log = log;
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);

        int alertas = 0, tenantsConTrabajo = 0, errores = 0;

        foreach (var tid in tenantIds)
        {
            try
            {
                _tenant.SetTenant(tid);
                await _db.Database.CloseConnectionAsync(); // el interceptor aplica app.tenant_id al reabrir

                var contratos = await _db.ContratosServicio.Where(c => c.FechaFin != null).ToListAsync(ct);
                if (contratos.Count == 0) continue;

                bool cambios = false;
                foreach (var c in contratos)
                {
                    var sem = MiCopropiedadService.CalcularSemaforoContrato(c.FechaInicio, c.FechaFin, hoy);
                    if (sem is SemaforoContrato.Verde or SemaforoContrato.Ninguno)
                    {
                        if (c.AlertaVencimientoPctNotificado != null) { c.AlertaVencimientoPctNotificado = null; cambios = true; }
                        continue;
                    }

                    var umbral = sem == SemaforoContrato.Rojo ? 10 : 20;
                    var ya = c.AlertaVencimientoPctNotificado;
                    // Alerta si no se ha notificado nunca, o si escalo de 20% (amarillo) a 10% (rojo).
                    var debe = ya is null || (umbral == 10 && ya != 10);
                    if (!debe) continue;

                    var dias = c.FechaFin!.Value.DayNumber - hoy.DayNumber;
                    _db.AlertasCopropiedad.Add(new AlertaCopropiedad
                    {
                        Tipo = TipoAlertaDashboard.ContratoPorVencer,
                        Severidad = sem == SemaforoContrato.Rojo ? SeveridadAlerta.Critica : SeveridadAlerta.Advertencia,
                        Titulo = dias < 0 ? "Contrato vencido" : $"Contrato por vencer ({dias} dias)",
                        Descripcion = dias < 0
                            ? $"El contrato con '{c.Proveedor}' esta vencido."
                            : $"Faltan {dias} dias para finalizar el contrato con '{c.Proveedor}'.",
                        UrlAccion = "/contratos",
                        ModuloOrigenCodigo = "2.3",
                        EntidadId = c.Id,
                        Activa = true
                    });
                    c.AlertaVencimientoPctNotificado = umbral;
                    alertas++;
                    cambios = true;
                }

                // ----- Polizas (Ola 4c): mismo semaforo y alertas de vencimiento -----
                var polizas = await _db.Polizas.Where(p => p.FechaFin != null).ToListAsync(ct);
                foreach (var p in polizas)
                {
                    // K-07: el semaforo de una poliza lo define SegurosService y nadie mas. Aqui se
                    // pasaba (FechaInicio ?? FechaFin) -> total 0 dias -> rojo critico, mientras la
                    // pagina /seguros la pintaba verde.
                    var sem = SegurosService.SemaforoPoliza(p.FechaInicio, p.FechaFin, hoy);
                    if (sem is SemaforoContrato.Verde or SemaforoContrato.Ninguno)
                    {
                        if (p.AlertaVencimientoPctNotificado != null) { p.AlertaVencimientoPctNotificado = null; cambios = true; }
                        continue;
                    }
                    var umbral = sem == SemaforoContrato.Rojo ? 10 : 20;
                    var ya = p.AlertaVencimientoPctNotificado;
                    if (!(ya is null || (umbral == 10 && ya != 10))) continue;
                    var dias = p.FechaFin!.Value.DayNumber - hoy.DayNumber;
                    _db.AlertasCopropiedad.Add(new AlertaCopropiedad
                    {
                        Tipo = TipoAlertaDashboard.ContratoPorVencer,
                        Severidad = sem == SemaforoContrato.Rojo ? SeveridadAlerta.Critica : SeveridadAlerta.Advertencia,
                        Titulo = dias < 0 ? "Poliza vencida" : $"Poliza por vencer ({dias} dias)",
                        Descripcion = dias < 0
                            ? $"La poliza de '{p.Aseguradora}' esta vencida."
                            : $"Faltan {dias} dias para el vencimiento de la poliza de '{p.Aseguradora}'.",
                        UrlAccion = "/seguros",
                        ModuloOrigenCodigo = "2.3",
                        EntidadId = p.Id,
                        Activa = true
                    });
                    p.AlertaVencimientoPctNotificado = umbral;
                    alertas++; cambios = true;
                }

                if (cambios) { await _db.SaveChangesAsync(ct); tenantsConTrabajo++; }
            }
            catch (Exception ex)
            {
                // K-12: antes este catch se comia el error en silencio; un tenant que fallaba no dejaba
                // rastro. No se corta el resto de tenants, pero queda en el log.
                errores++;
                _log.LogError(ex, "ContratosVencimiento fallo en el tenant {TenantId}", tid);
            }
        }

        return new { alertas, tenantsConTrabajo, errores, tenants = tenantIds.Count };
    }
}
