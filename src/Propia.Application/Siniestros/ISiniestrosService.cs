namespace Propia.Application.Siniestros;

/// <summary>
/// Modulo Siniestros (2026-11-16): siniestros de la copropiedad ligados a un equipo, zona comun, unidad u
/// "otro", con poliza opcional, estados editables por copropiedad, campos propios, documentos y actividad.
/// Las validaciones lanzan InvalidOperationException con el mensaje en espanol (el controller -> 400).
/// </summary>
public interface ISiniestrosService
{
    Task<IReadOnlyList<SiniestroDto>> ListAsync(CancellationToken ct);
    Task<SiniestroDto?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<SiniestroDto> CrearAsync(CrearSiniestroRequest req, CancellationToken ct);
    Task<bool> ActualizarAsync(Guid id, ActualizarSiniestroRequest req, CancellationToken ct);
    Task<bool> CambiarEstadoAsync(Guid id, CambiarEstadoSiniestroRequest req, CancellationToken ct);
    Task<bool> EliminarAsync(Guid id, CancellationToken ct);

    /// <summary>S3: crea la tarea de reparacion del siniestro en el tablero General, ligada al elemento
    /// afectado (aparece en su historial) y al siniestro (Origen = Siniestros). Una por siniestro: si ya tiene
    /// una tarea vigente, falla. Devuelve el siniestro actualizado (null si no existe).</summary>
    Task<SiniestroDto?> CrearTareaReparacionAsync(Guid id, CancellationToken ct);

    // Estados (columnas, editables por copropiedad)
    Task<IReadOnlyList<SiniestroEstadoDto>> ListEstadosAsync(CancellationToken ct);
    Task<SiniestroEstadoDto> CrearEstadoAsync(CrearSiniestroEstadoRequest req, CancellationToken ct);
    Task<bool> ActualizarEstadoAsync(Guid estadoId, ActualizarSiniestroEstadoRequest req, CancellationToken ct);
    Task<bool> EliminarEstadoAsync(Guid estadoId, CancellationToken ct);
    Task ReordenarEstadosAsync(ReordenarSiniestroEstadosRequest req, CancellationToken ct);

    // Campos personalizados (EAV)
    Task<IReadOnlyList<SiniestroCampoDto>> ListCamposAsync(CancellationToken ct);
    Task<SiniestroCampoDto> CrearCampoAsync(CrearSiniestroCampoRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid campoId, ActualizarSiniestroCampoRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct);
    Task<bool> GuardarCampoValorAsync(Guid siniestroId, Guid campoId, GuardarSiniestroCampoValorRequest req, CancellationToken ct);
    Task<IReadOnlyList<SiniestroCampoValorFlatDto>> ListTodosCamposValoresAsync(CancellationToken ct);
    Task<IReadOnlyList<SiniestroCampoDinDto>> ListCamposDinAsync(Guid siniestroId, CancellationToken ct);

    // Documentos
    Task<IReadOnlyList<SiniestroDocumentoDto>> ListDocumentosAsync(Guid siniestroId, CancellationToken ct);
    Task<SiniestroDocumentoDto?> AgregarDocumentoAsync(Guid siniestroId, string nombre, string url, CancellationToken ct);
    Task<bool> EliminarDocumentoAsync(Guid docId, CancellationToken ct);

    // Actividad (comentarios + eventos del sistema)
    Task<IReadOnlyList<SiniestroComentarioDto>> ListComentariosAsync(Guid siniestroId, CancellationToken ct);
    Task<SiniestroComentarioDto> AgregarComentarioAsync(Guid siniestroId, AgregarComentarioSiniestroRequest req, CancellationToken ct);
}
