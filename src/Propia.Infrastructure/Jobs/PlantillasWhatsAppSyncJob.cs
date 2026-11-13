using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Sincroniza cada 30 minutos las plantillas de WhatsApp de las lineas YCloud de cada copropiedad: asi las que se
/// crean en YCloud (o cambian de estado en Meta: aprobada, rechazada) caen solas al modulo Plantillas de WhatsApp
/// sin importar a mano. Corre sin request: itera los tenants (tabla global) con SetTenant + CloseConnectionAsync
/// para que RLS y el query filter apliquen a cada copropiedad.
/// </summary>
public class PlantillasWhatsAppSyncJob : IBackgroundJob
{
    public string Nombre => "PlantillasWhatsAppSync";
    public int FrecuenciaMinutos => 30;

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPlantillasWhatsAppService _plantillas;
    private readonly ILogger<PlantillasWhatsAppSyncJob> _log;

    public PlantillasWhatsAppSyncJob(PropiaDbContext db, ITenantContext tenant, IPlantillasWhatsAppService plantillas, ILogger<PlantillasWhatsAppSyncJob> log)
    {
        _db = db;
        _tenant = tenant;
        _plantillas = plantillas;
        _log = log;
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
        int copropiedades = 0, lineas = 0, importadas = 0, actualizadas = 0, errores = 0;

        foreach (var tid in tenantIds)
        {
            try
            {
                _tenant.SetTenant(tid);
                await _db.Database.CloseConnectionAsync();   // el interceptor aplica app.tenant_id al reabrir
                var tieneYCloud = await _db.WhatsAppLines.AsNoTracking()
                    .AnyAsync(l => l.Provider == WhatsAppProvider.YCloud && l.YCloudApiKeyEncrypted != null, ct);
                if (!tieneYCloud) continue;

                var r = await _plantillas.SincronizarAsync(forzar: true, ct);
                copropiedades++;
                lineas += r.Lineas; importadas += r.Importadas; actualizadas += r.Actualizadas; errores += r.Errores.Count;
                foreach (var e in r.Errores) _log.LogWarning("[PlantillasWhatsAppSync] tenant {Tenant}: {Error}", tid, e);
            }
            catch (Exception ex)
            {
                errores++;
                _log.LogWarning(ex, "[PlantillasWhatsAppSync] fallo en tenant {Tenant}", tid);
            }
            finally
            {
                _db.ChangeTracker.Clear();
            }
        }
        return new { copropiedades, lineas, importadas, actualizadas, errores };
    }
}
