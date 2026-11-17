using Propia.Application.Integraciones;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Lector de buzon (Ola 1): cada 10 minutos lee los correos nuevos de las copropiedades con la lectura activa y los
/// clasifica con el agente "Coordinador de correo". Idempotente por Message-ID y por UID IMAP.
/// </summary>
public class CorreoEntranteJob : IBackgroundJob
{
    public string Nombre => "CorreoEntrante";
    public int FrecuenciaMinutos => 10;

    private readonly ICorreoEntranteService _svc;
    public CorreoEntranteJob(ICorreoEntranteService svc) => _svc = svc;

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var nuevos = await _svc.RevisarTodasAsync(ct);
        return new { nuevos };
    }
}
