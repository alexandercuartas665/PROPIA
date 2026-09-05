namespace Propia.Application.Seguros;

/// <summary>Modulo Seguros (Ola 4): polizas dedicadas con aseguradora/corredor del Directorio,
/// vigencia con semaforo, campos dinamicos y (Ola 5) reclamaciones.</summary>
public interface ISegurosService
{
    Task<IReadOnlyList<PolizaDto>> ListPolizasAsync(CancellationToken ct);
    Task<PolizaDto?> ObtenerPolizaAsync(Guid id, CancellationToken ct);
    Task<PolizaDto> CrearPolizaAsync(CrearPolizaRequest req, CancellationToken ct);
    Task<bool> ActualizarPolizaAsync(Guid id, ActualizarPolizaRequest req, CancellationToken ct);
    Task<bool> EliminarPolizaAsync(Guid id, CancellationToken ct);

    /// <summary>Descarga el PDF ORIGEN (blob R2) de la poliza. Null si no tiene o no existe. Gateado por el controller.</summary>
    Task<PdfOrigenDescarga?> DescargarPdfOrigenAsync(Guid polizaId, CancellationToken ct);

    // Campos personalizados (EAV)
    Task<IReadOnlyList<PolizaCampoDto>> ListCamposAsync(CancellationToken ct);
    Task<PolizaCampoDto> CrearCampoAsync(CrearPolizaCampoRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid campoId, ActualizarPolizaCampoRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct);
    Task<bool> GuardarCampoValorAsync(Guid polizaId, Guid campoId, GuardarPolizaCampoValorRequest req, CancellationToken ct);

    // Reclamaciones (Ola 5)
    Task<IReadOnlyList<ReclamacionDto>> ListReclamacionesAsync(Guid polizaId, CancellationToken ct);
    Task<ReclamacionDto> CrearReclamacionAsync(Guid polizaId, CrearReclamacionRequest req, CancellationToken ct);
    Task<bool> CerrarReclamacionAsync(Guid reclamacionId, CerrarReclamacionRequest req, CancellationToken ct);
}
