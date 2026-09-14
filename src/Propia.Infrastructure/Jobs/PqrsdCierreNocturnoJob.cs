namespace Propia.Infrastructure.Jobs;

/// <summary>
/// DESACTIVADO (decision de Alex 2026-09-13): un PQRSD NUNCA se cierra automaticamente ni se avisa; el cierre
/// es siempre manual por el usuario. Este job ya no hace nada.
///
/// No se puede borrar aqui sin dejar el build roto: su registro vive en el archivo compartido
/// DependencyInjection.cs (no es de FARO). Queda como no-op transitorio hasta que la sesion dev principal
/// quite el registro y elimine este archivo -> SOLICITUD_FARO_01. NO "arreglar" para que vuelva a cerrar.
/// </summary>
public class PqrsdCierreNocturnoJob : IBackgroundJob
{
    public string Nombre => "PqrsdCierreNocturno";
    public int FrecuenciaMinutos => 60 * 6; // 6 horas

    public Task<object?> EjecutarAsync(CancellationToken ct)
        => Task.FromResult<object?>(new { desactivado = true, motivo = "El cierre de PQRSD es manual (decision de Alex 2026-09-13)." });
}
