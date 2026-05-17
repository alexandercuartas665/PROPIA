using Propia.Application.Monitoria;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Job nocturno que calcula y persiste el snapshot diario de metricas globales
/// del modulo 0.3 Monitoria. Corre cada 12h para que el dashboard SuperAdmin
/// tenga datos frescos sin sobrecargar el sistema (los conteos cross-tenant
/// son baratos pero no gratis).
/// </summary>
public class MetricasDiariasJob : IBackgroundJob
{
    public string Nombre => "MetricasDiarias";
    public int FrecuenciaMinutos => 60 * 12; // 12 horas

    private readonly IMonitoriaService _monitoria;
    public MetricasDiariasJob(IMonitoriaService monitoria) => _monitoria = monitoria;

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var snapshot = await _monitoria.CalcularYGuardarMetricasHoyAsync(ct);
        return new
        {
            fecha = snapshot.Fecha,
            tenants = snapshot.TotalTenants,
            tareas24h = snapshot.TareasCreadas24h,
            pqrsd24h = snapshot.PqrsdsRadicadas24h,
            notis24h = snapshot.NotificacionesDespachadas24h,
            incidentesAbiertos = snapshot.IncidentesAbiertos
        };
    }
}
