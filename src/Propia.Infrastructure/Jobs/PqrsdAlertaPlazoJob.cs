using Propia.Infrastructure.Pqrsd;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// G-01 (spec 2.9, 11 y 15): alertas automaticas de plazo de las PQRSD. Corre 2 veces al dia; al cruzar el
/// 80% del plazo legal (dias habiles) avisa al responsable/administracion y al vencer avisa como critico,
/// por todos los canales, ademas de crear la alerta de dashboard y dejar traza en el historial. Idempotente
/// por expediente+umbral (ver PqrsdMantenimientoService.AlertarPlazosAsync).
/// </summary>
public class PqrsdAlertaPlazoJob : IBackgroundJob
{
    public string Nombre => "PqrsdAlertaPlazo";
    public int FrecuenciaMinutos => 60 * 12; // 2 veces al dia

    private readonly PqrsdMantenimientoService _mantenimiento;
    public PqrsdAlertaPlazoJob(PqrsdMantenimientoService mantenimiento) => _mantenimiento = mantenimiento;

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var alertas = await _mantenimiento.AlertarPlazosAsync(ct);
        return new { alertas };
    }
}
