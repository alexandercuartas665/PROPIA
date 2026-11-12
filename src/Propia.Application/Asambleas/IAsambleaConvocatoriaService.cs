namespace Propia.Application.Asambleas;

/// <summary>
/// Genera la Convocatoria de una sesion al citarla (PROPUESTAS.md 8.1, hueco #6): PDF con el membrete de la
/// copropiedad (encabezado y pie predeterminados de Plantillas de documentos), tipo, fecha, hora, lugar o
/// videollamada y el orden del dia. Queda como documento de la sesion (tipo Convocatoria), que no se elimina.
/// </summary>
public interface IAsambleaConvocatoriaService
{
    /// <summary>Devuelve el documento creado, o null si la sesion no existe.</summary>
    Task<SesionDocumentoDto?> GenerarAsync(Guid sesionId, CancellationToken ct);
}
