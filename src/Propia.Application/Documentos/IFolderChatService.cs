namespace Propia.Application.Documentos;

public record FolderChatMensajeDto(Guid Id, string Rol, string Texto, DateTimeOffset Cuando, bool Error);

/// <summary>Estado del chat de un folder raiz para el usuario actual.</summary>
public record FolderChatDto(Guid FolderId, bool IaDisponible, string? Proveedor, string? Modelo, IReadOnlyList<FolderChatMensajeDto> Mensajes);

public record EnviarFolderChatRequest(string Texto, Guid? FolderContextoId = null);

/// <summary>Respuesta a un envio: el mensaje del usuario y el del asistente (ya persistidos).</summary>
public record FolderChatRespuestaDto(FolderChatMensajeDto Usuario, FolderChatMensajeDto Asistente);

public record FolderDocumentoTextoDto(Guid DocumentoId, int NumeroVersion, string Fuente, int Caracteres, string? Error, string? Extracto);

/// <summary>
/// Chat "Conversar con los documentos" (Documentos v2, Ola 3). Conversacion persistente por copropiedad +
/// folder raiz + usuario. El contexto que recibe la IA = estructura del folder raiz + por documento:
/// nombre, archivo, metadatos (con su descripcion) y el texto extraido del archivo (indexado bajo demanda).
/// Proveedor: el habilitado en Super Admin (AiProviderConfigs). Consumo: IAiUsageService (cuota del plan).
/// </summary>
public interface IFolderChatService
{
    Task<FolderChatDto?> ListarAsync(Guid folderRaizId, CancellationToken ct);
    Task<FolderChatRespuestaDto?> EnviarAsync(Guid folderRaizId, EnviarFolderChatRequest req, CancellationToken ct);
    Task<bool> LimpiarAsync(Guid folderRaizId, CancellationToken ct);
    /// <summary>Extrae (o reutiliza) el texto del archivo actual de un documento. Null si no existe.</summary>
    Task<FolderDocumentoTextoDto?> IndexarDocumentoAsync(Guid documentoId, bool forzar, CancellationToken ct);
}
