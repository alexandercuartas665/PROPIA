namespace Propia.Application.Pqrsd;

/// <summary>
/// Genera el PDF de la respuesta oficial a un PQRSD (modulo 2.9). El PDF se adjunta al expediente
/// y se comparte con el radicador via el link publico de seguimiento.
/// </summary>
public interface IPqrsdRespuestaPdfService
{
    /// <summary>
    /// Construye el PDF de la respuesta del expediente con el texto dado. Devuelve el binario y el
    /// nombre de archivo sugerido, o null si el expediente no existe.
    /// </summary>
    Task<(byte[] Pdf, string FileName)?> GenerarRespuestaPdfAsync(Guid expedienteId, string textoRespuesta, CancellationToken ct);
}
