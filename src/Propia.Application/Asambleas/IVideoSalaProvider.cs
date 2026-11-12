namespace Propia.Application.Asambleas;

/// <summary>
/// Sala de video de la asamblea (ola 5 del hand-off). Hoy la implementa LiveKit (Cloud o propio): el servidor solo firma
/// tokens de acceso, valida los webhooks y arranca/detiene la grabacion; el audio/video viaja navegador-LiveKit.
/// Sin configuracion (LiveKit:Url/ApiKey/ApiSecret) la sala no esta disponible y la ficha lo dice.
/// </summary>
public interface IVideoSalaProvider
{
    string Nombre { get; }
    bool Configurada { get; }
    bool GrabacionConfigurada { get; }
    /// <summary>URL del servidor para el cliente (wss://...).</summary>
    string Url { get; }

    /// <summary>Nombre de la sala de una sesion: lleva el tenant para que el webhook fije la copropiedad.</summary>
    string NombreSala(Guid tenantId, Guid sesionId);
    bool TryLeerSala(string? sala, out Guid tenantId, out Guid sesionId);

    /// <summary>Token de acceso firmado para una persona en la sala.</summary>
    string CrearToken(string sala, string identidad, string nombre, bool moderador, TimeSpan vigencia);

    /// <summary>Valida la firma del webhook y lo interpreta. Null = firma invalida o cuerpo ilegible.</summary>
    VideoSalaEvento? LeerWebhook(string cuerpo, string? authorization);

    /// <summary>Inicia la grabacion compuesta de la sala; devuelve el id del egress.</summary>
    Task<string> IniciarGrabacionAsync(string sala, string rutaArchivo, CancellationToken ct);
    Task DetenerGrabacionAsync(string egressId, CancellationToken ct);
}

/// <summary>Evento del proveedor de video ya validado (participant_joined, participant_left, egress_ended, ...).</summary>
public record VideoSalaEvento(string Evento, string? Sala, string? Identidad, string? NombreParticipante,
    string? EgressId, string? EstadoEgress, string? ArchivoUrl, string? ArchivoNombre, long? TamanioBytes, double? DuracionSeg, string? Error);

/// <summary>Identidades de la sala: "u:{usuario}" = equipo de la administracion, "p:{persona}" = copropietario o apoderado.</summary>
public static class IdentidadSala
{
    public static string Usuario(Guid userId) => "u:" + userId.ToString("N");
    public static string Persona(Guid personaId) => "p:" + personaId.ToString("N");
    public static Guid? PersonaDe(string? identidad)
        => identidad is { Length: > 2 } && identidad.StartsWith("p:", StringComparison.Ordinal) && Guid.TryParse(identidad[2..], out var g) ? g : null;
}
