using Propia.Domain.Enums;

namespace Propia.Application.TransferenciaCustodia;

// ===========================================================================
// Transferencia
// ===========================================================================

public record TransferenciaDto(
    Guid Id,
    Guid CopropiedadId,
    string CopropiedadNombre,
    string? CopropiedadNit,
    Guid? OrganizacionSalienteId,
    string? OrganizacionSalienteNombre,
    Guid? OrganizacionEntranteId,
    string? OrganizacionEntranteNombre,
    EscenarioTransferencia Escenario,
    EstadoTransferencia Estado,
    Guid IniciadoPorUsuarioId,
    DateOnly? FechaEfectivaSaliente,
    DateOnly? FechaVencimientoVentana,
    DateTimeOffset? FechaCorte,
    Guid? ActaEntregaDocumentoId,
    DateTimeOffset CreadoAt,
    int NumeroDocumentos,
    int NumeroEventos);

// Escenario A: saliente inicia entrega voluntaria
public record IniciarEntregaVoluntariaRequest(
    Guid CopropiedadId,
    DateOnly FechaEfectivaTerminacion,
    Guid? OrganizacionEntranteId);

// Escenario B: entrante reclama custodia
public record ReclamarCustodiaRequest(
    Guid CopropiedadId,
    bool DeclaracionLegitimidad);

// Escenario C: copropiedad gestiona el cambio
public record IniciarPorCopropiedadRequest(
    Guid CopropiedadId,
    Guid OrganizacionEntranteId);

// ===========================================================================
// Aprobacion/Rechazo del saliente + Acta
// ===========================================================================

public record AprobarTransferenciaRequest(string? Notas);
public record RechazarTransferenciaRequest(string Motivo);

public record SubirActaRequest(
    string NombreArchivo,
    string TipoMime,
    long TamanioBytes,
    /// <summary>Base64 del PDF/imagen.</summary>
    string ContenidoBase64);

public record ActaDocumentoDto(
    Guid Id,
    string NombreArchivo,
    string TipoMime,
    long TamanioBytes,
    string HashSha256,
    ResultadoValidacionActa ResultadoValidacionIa,
    string? DetalleValidacionIaJson,
    Guid SubidoPorUsuarioId,
    DateTimeOffset CreadoAt);

// ===========================================================================
// Eventos del historial
// ===========================================================================

public record EventoHistorialDto(
    Guid Id,
    TipoEventoTransferencia TipoEvento,
    Guid? ActorUsuarioId,
    string? Canal,
    string? DetalleJson,
    DateTimeOffset CreadoAt);

// ===========================================================================
// Expediente completo
// ===========================================================================

public record ExpedienteDto(
    TransferenciaDto Transferencia,
    IReadOnlyList<ActaDocumentoDto> Documentos,
    IReadOnlyList<EventoHistorialDto> Eventos,
    /// <summary>Snapshot del estado al momento del corte (si ya ejecuto).</summary>
    string? SnapshotEstadoJson,
    string? AjusteFacturacionJson);

// ===========================================================================
// Busqueda de copropiedades para reclamacion
// ===========================================================================

public record CopropiedadBusquedaDto(
    Guid Id,
    string Nombre,
    string? Nit,
    string? Ciudad,
    string? AdministradorActualNombre,
    EstadoCustodia EstadoCustodia);

// ===========================================================================
// Resumen
// ===========================================================================

public record ResumenTransferenciasDto(
    int EnCurso,
    int Completadas,
    int PendientesAprobacionMia,        // como saliente
    int PendientesMiActa,               // como entrante esperando acta
    int Canceladas);
