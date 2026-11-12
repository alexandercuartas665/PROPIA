namespace Propia.Application.Asambleas;

/// <summary>
/// Acta legible de la sesion (ola 3 del hand-off de Asambleas, PROPUESTAS.md 8.3): al cerrar con quorum se crea el
/// borrador base con los datos del sistema (numero AAAA-NNN) y el agente "Redactor de actas" lo redacta en segundo
/// plano con la plantilla "Acta de asamblea" (categoria Asambleas) y la transcripcion. La administracion lo edita o lo
/// regenera mientras nadie haya firmado; firman presidente y secretario (cuenta o codigo al correo) y con las dos
/// firmas queda inmutable (hash SHA-256 del HTML final). Publicar = PDF con membrete en Documentos + comunicado en
/// Borrador para avisar a los propietarios (plazo legal: 20 dias habiles, Ley 675 art. 47).
/// </summary>
public interface IAsambleaActaService
{
    /// <summary>Asegura numero y borrador base del acta de una sesion Cerrada y marca la redaccion como pendiente.
    /// Devuelve el id del acta (null si la sesion no tiene acta).</summary>
    Task<Guid?> PrepararAsync(Guid sesionId, CancellationToken ct);
    /// <summary>Lanza la redaccion por IA en segundo plano (tenant actual).</summary>
    void ProgramarRedaccion(Guid actaId);
    /// <summary>Redacta con la IA y guarda el borrador (si en el intermedio no se edito ni se firmo).</summary>
    Task RedactarAsync(Guid actaId, CancellationToken ct);
    Task<bool> RegenerarAsync(Guid actaId, CancellationToken ct);
    Task<bool> GuardarBorradorAsync(Guid actaId, GuardarBorradorActaRequest req, CancellationToken ct);
    Task<CodigoFirmaDto> SolicitarCodigoAsync(Guid actaId, SolicitarCodigoFirmaRequest req, CancellationToken ct);
    Task<bool> FirmarAsync(Guid actaId, FirmarActaRolRequest req, CancellationToken ct);
    Task<PublicacionActaDto> PublicarAsync(Guid actaId, string? baseUrl, CancellationToken ct);
}
