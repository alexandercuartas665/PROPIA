using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Reactivacion/seguimiento automatico de conversaciones inactivas (Infraestructura IA). Como corre sin
/// contexto de request, itera los tenants (tabla global) y para cada uno fija ITenantContext + reabre la
/// conexion (el TenantConnectionInterceptor aplica app.tenant_id) para que RLS permita leer/escribir.
/// Delega en IAgentReactivacionService.RunTenantAsync. Mismo patron que AutomacionesJob. Portado de ECOREX.
/// </summary>
public sealed class ReactivacionAgenteJob : IBackgroundJob
{
    public string Nombre => "ReactivacionAgente";
    // Los offsets de los pasos son en HORAS; barrer cada 15 min da granularidad de sobra sin recargar.
    public int FrecuenciaMinutos => 15;

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAgentReactivacionService _reactivacion;

    public ReactivacionAgenteJob(PropiaDbContext db, ITenantContext tenant, IAgentReactivacionService reactivacion)
    {
        _db = db;
        _tenant = tenant;
        _reactivacion = reactivacion;
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
        int enviados = 0, tenantsConEnvio = 0, errores = 0;

        foreach (var tid in tenantIds)
        {
            try
            {
                _tenant.SetTenant(tid);
                await _db.Database.CloseConnectionAsync(); // el interceptor aplica app.tenant_id al reabrir

                var n = await _reactivacion.RunTenantAsync(ct);
                if (n > 0) { tenantsConEnvio++; enviados += n; }
            }
            catch
            {
                errores++;
                _db.ChangeTracker.Clear();
            }
        }

        _tenant.Clear();
        return new { tenants = tenantIds.Count, tenantsConEnvio, enviados, errores };
    }
}
