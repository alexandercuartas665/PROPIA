namespace Propia.Application.Asambleas;

/// <summary>
/// Envio por correo del enlace self-service (Ola 1.5): un correo por persona habilitada (propietario
/// activo o apoderado con poder aprobado) con todas sus unidades de la sesion. El tenant ya debe estar
/// fijado por quien llama.
/// </summary>
public interface IAsambleaEnlacesService
{
    /// <summary>
    /// Personas habilitadas de una sesion citada o en curso, opcionalmente filtradas. Null si la sesion
    /// no existe; InvalidOperationException si no esta citada ni en curso.
    /// </summary>
    Task<IReadOnlyList<DestinatarioEnlaceDto>?> ResolverDestinatariosAsync(Guid sesionId, IReadOnlyCollection<Guid>? personaIds, CancellationToken ct);

    /// <summary>Emite el token de cada destinatario con correo y le envia su enlace. Sincrono.</summary>
    Task<EnvioEnlacesResultado> EnviarAsync(Guid sesionId, IReadOnlyList<DestinatarioEnlaceDto> destinatarios, string baseUrl, CancellationToken ct);

    /// <summary>
    /// Resuelve los destinatarios, envia en el request hasta 3 correos de prueba y lanza el resto en
    /// segundo plano (scope propio con el tenant fijado). Si ninguno de prueba sale lanza
    /// InvalidOperationException y no programa nada. Null si la sesion no existe.
    /// </summary>
    Task<EnvioEnlacesDto?> ProgramarEnvioAsync(Guid sesionId, IReadOnlyCollection<Guid>? personaIds, string baseUrl, CancellationToken ct);

    /// <summary>Estado del ultimo envio de la sesion (en memoria, 2 dias); null si no hubo envio.</summary>
    EstadoEnvioEnlacesDto? EstadoEnvio(Guid sesionId);
}
