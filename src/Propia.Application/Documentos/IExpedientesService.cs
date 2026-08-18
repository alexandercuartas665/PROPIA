namespace Propia.Application.Documentos;

/// <summary>
/// Vista "Expedientes" del modulo 2.15 Documentos (Tabla de Retencion Documental).
/// Configuracion TRD (series/subseries/tipologias/campos) + expedientes con su checklist
/// de documentos esperados. Fiel al prototipo PROPIA.dc.html. Tenant-scoped (RLS).
/// El archivo de cada tipologia vive en IBlobStorage.
/// </summary>
public interface IExpedientesService
{
    // ----- Configuracion documental (TRD) -----
    /// <summary>Arbol completo serie/subserie/tipologia/campo. Siembra la TRD base si el tenant no tiene ninguna.</summary>
    Task<IReadOnlyList<TrdSerieDto>> ListarTrdAsync(CancellationToken ct);
    Task<TrdSerieDto> CrearSerieAsync(CrearSerieRequest req, CancellationToken ct);
    Task<bool> EliminarSerieAsync(Guid id, CancellationToken ct);
    Task<TrdSubserieDto> CrearSubserieAsync(CrearSubserieRequest req, CancellationToken ct);
    Task<bool> EliminarSubserieAsync(Guid id, CancellationToken ct);
    Task<TrdTipologiaDto> CrearTipologiaCfgAsync(CrearTipologiaCfgRequest req, CancellationToken ct);
    Task<bool> EliminarTipologiaCfgAsync(Guid id, CancellationToken ct);
    Task<TrdCampoDto> CrearCampoCfgAsync(CrearCampoCfgRequest req, CancellationToken ct);
    Task<bool> EliminarCampoCfgAsync(Guid id, CancellationToken ct);

    // ----- Expedientes -----
    Task<IReadOnlyList<ExpedienteListaDto>> ListarExpedientesAsync(CancellationToken ct);
    Task<ExpedienteDetalleDto?> GetExpedienteAsync(Guid id, CancellationToken ct);
    /// <summary>Crea un expediente copiando tipologias + campos de la subserie. Genera codigo EXP-XXX-NNN.</summary>
    Task<ExpedienteDetalleDto> CrearExpedienteAsync(CrearExpedienteRequest req, CancellationToken ct);

    Task<bool> AgregarTipologiaAsync(Guid expedienteId, AgregarTipologiaExpRequest req, CancellationToken ct);
    Task<bool> EliminarTipologiaAsync(Guid tipologiaId, CancellationToken ct);
    Task<bool> AgregarCampoAsync(Guid expedienteId, AgregarCampoExpRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct);

    /// <summary>Carga (o reemplaza) el archivo de una tipologia + guarda sus metadatos.</summary>
    Task<bool> CargarTipologiaAsync(Guid tipologiaId, CargarTipologiaRequest req, CancellationToken ct);
    /// <summary>Quita el archivo y los metadatos de una tipologia (la deja vacia).</summary>
    Task<bool> QuitarArchivoAsync(Guid tipologiaId, CancellationToken ct);
    /// <summary>Descarga el archivo (version actual) de una tipologia (base64).</summary>
    Task<DescargaDocumentoDto?> DescargarTipologiaAsync(Guid tipologiaId, CancellationToken ct);

    /// <summary>Lista el historial de versiones cargadas de una tipologia (mas reciente primero).</summary>
    Task<IReadOnlyList<TipologiaVersionDto>> ListarVersionesTipologiaAsync(Guid tipologiaId, CancellationToken ct);

    /// <summary>Descarga una version historica especifica del archivo de una tipologia (base64).</summary>
    Task<DescargaDocumentoDto?> DescargarVersionTipologiaAsync(Guid tipologiaId, Guid versionId, CancellationToken ct);

    /// <summary>Vista previa renderizable (version actual): imagen/pdf/texto/html segun el tipo de archivo.</summary>
    Task<TipologiaPreviewDto?> PreviewTipologiaAsync(Guid tipologiaId, CancellationToken ct);
    /// <summary>Vista previa renderizable de una version historica.</summary>
    Task<TipologiaPreviewDto?> PreviewVersionTipologiaAsync(Guid tipologiaId, Guid versionId, CancellationToken ct);

    /// <summary>Duplica una tipologia del expediente (misma etiqueta, casilla vacia) para permitir varios documentos del mismo tipo.</summary>
    Task<bool> DuplicarTipologiaAsync(Guid tipologiaId, CancellationToken ct);
}
