namespace Propia.Application.Asambleas;

/// <summary>
/// Motor de voz a texto de la sala (ola 4). Decision de Alex (2026-11-12): por ahora SOLO el navegador (Web Speech API,
/// "Mi voz a texto"): el texto lo reconoce el navegador de quien habla y llega como linea por POST /transcripcion.
/// Un proveedor de pago (Azure Speech, Deepgram, Whisper) se conecta despues implementando esta interfaz: recibira el
/// audio por pista de la videollamada (ola 5) y publicara las lineas con el hablante (diarizacion por participante).
/// </summary>
public interface ITranscripcionProvider
{
    /// <summary>Clave que queda en el origen de cada linea ("navegador", "azure", "deepgram"...).</summary>
    string Clave { get; }
    /// <summary>true = transcribe en el servidor a partir del audio de la sala; false = el navegador envia el texto.</summary>
    bool EnServidor { get; }
}
