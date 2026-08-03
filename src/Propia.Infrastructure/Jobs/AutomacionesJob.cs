using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Evalua las reglas de automatizacion activas de cada tenant (Infraestructura IA). Como corre sin
/// contexto de request, itera los tenants (tabla global) y para cada uno fija ITenantContext +
/// reabre la conexion (el TenantConnectionInterceptor aplica app.tenant_id) para que RLS permita
/// leer/escribir. Delega la logica en IAutomationService.RunNowAsync (hoy solo ejecuta de verdad
/// ChatSinRespuesta -> NotificarAdministracion; el resto es scaffolding).
/// </summary>
public sealed class AutomacionesJob : IBackgroundJob
{
    public string Nombre => "Automatizaciones";
    public int FrecuenciaMinutos => 15;

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAutomationService _automations;

    public AutomacionesJob(PropiaDbContext db, ITenantContext tenant, IAutomationService automations)
    {
        _db = db;
        _tenant = tenant;
        _automations = automations;
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
        int reglasEvaluadas = 0, accionesDisparadas = 0, tenantsConTrabajo = 0, errores = 0;

        foreach (var tid in tenantIds)
        {
            try
            {
                _tenant.SetTenant(tid);
                await _db.Database.CloseConnectionAsync(); // el interceptor aplica app.tenant_id al reabrir

                var r = await _automations.RunNowAsync(ct);
                if (r.RulesEvaluated > 0) tenantsConTrabajo++;
                reglasEvaluadas += r.RulesEvaluated;
                accionesDisparadas += r.ActionsFired;
            }
            catch
            {
                errores++;
                _db.ChangeTracker.Clear();
            }
        }

        _tenant.Clear();
        return new { tenants = tenantIds.Count, tenantsConTrabajo, reglasEvaluadas, accionesDisparadas, errores };
    }
}
