using Propia.Infrastructure.Pqrsd;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Job nocturno que cierra automaticamente expedientes PQRSD cuya ventana de
/// inconformidad ya vencio (RN-06 spec 2.9). Corre cada 6h (4 veces al dia)
/// para que un expediente respondido se cierre a mas tardar 6h despues de
/// vencer su ventana.
/// </summary>
public class PqrsdCierreNocturnoJob : IBackgroundJob
{
    public string Nombre => "PqrsdCierreNocturno";
    public int FrecuenciaMinutos => 60 * 6; // 6 horas

    private readonly PqrsdMantenimientoService _mantenimiento;
    public PqrsdCierreNocturnoJob(PqrsdMantenimientoService mantenimiento)
        => _mantenimiento = mantenimiento;

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var cerrados = await _mantenimiento.CerrarVencidosTrasInconformidadAsync(ct);
        return new { cerrados };
    }
}
