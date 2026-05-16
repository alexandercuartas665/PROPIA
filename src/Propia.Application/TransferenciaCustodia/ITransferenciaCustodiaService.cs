namespace Propia.Application.TransferenciaCustodia;

/// <summary>
/// Modulo 1.5 Transferencia de Custodia - servicio de aplicacion (spec v1.0 MVP).
///
/// Alcance MVP:
///  - 3 escenarios: A entrega voluntaria + B reclamacion entrante + C copropiedad gestiona.
///  - State machine: Iniciado -> PendienteAprobacion -> ActaEnValidacion -> AlertasActivas -> Ejecutado | Cancelado.
///  - Subida de acta de asamblea (almacenamiento via IBlobStorage).
///  - Aprobacion/Rechazo del saliente.
///  - Ejecucion del corte: snapshot + revocacion vinculos + cambio estado custodia + acta entrega.
///  - Expediente completo con historial inmutable.
///  - RN-16: unicidad de proceso activo por copropiedad (UNIQUE parcial en migracion).
///
/// Diferido a Fase 2:
///  - Validacion IA del acta via T.1 Claude (en MVP queda en estado RequiereRevision para revision manual).
///  - Notificaciones reales via T.2 (in-app + email + WhatsApp).
///  - Alertas progresivas dia 1/7/13 con cron job.
///  - Ajuste real de facturacion en 0.2 (en MVP calcula prorrateo y lo guarda en JSON).
///  - Generacion automatica del acta de entrega PDF en 2.15 Documentos (en MVP genera estructura JSON).
///  - Suspension de integraciones contables (Siigo/Alegra).
///  - Reasignacion de tareas abiertas a "Sin asignar" (hook 2.10 listo).
///  - Revocacion en tiempo real de tokens JWT del saliente via Redis.
/// </summary>
public interface ITransferenciaCustodiaService
{
    // ----- Busqueda y listado -----

    Task<IReadOnlyList<CopropiedadBusquedaDto>> BuscarCopropiedadesAsync(string? query, CancellationToken ct);

    Task<IReadOnlyList<TransferenciaDto>> ListarMisTransferenciasAsync(CancellationToken ct);
    Task<TransferenciaDto?> GetTransferenciaAsync(Guid id, CancellationToken ct);
    Task<ExpedienteDto?> GetExpedienteAsync(Guid id, CancellationToken ct);

    // ----- Escenarios -----

    Task<TransferenciaDto> IniciarEntregaVoluntariaAsync(IniciarEntregaVoluntariaRequest req, CancellationToken ct);
    Task<TransferenciaDto> ReclamarCustodiaAsync(ReclamarCustodiaRequest req, CancellationToken ct);
    Task<TransferenciaDto> IniciarPorCopropiedadAsync(IniciarPorCopropiedadRequest req, CancellationToken ct);

    // ----- Acta de asamblea -----

    Task<ActaDocumentoDto> SubirActaAsync(Guid transferenciaId, SubirActaRequest req, CancellationToken ct);

    // ----- Aprobacion/Rechazo del saliente -----

    Task<bool> AprobarComoSalienteAsync(Guid transferenciaId, AprobarTransferenciaRequest req, CancellationToken ct);
    Task<bool> RechazarComoSalienteAsync(Guid transferenciaId, RechazarTransferenciaRequest req, CancellationToken ct);

    // ----- Cancelacion (por el iniciador o admin) -----

    Task<bool> CancelarAsync(Guid transferenciaId, string motivo, CancellationToken ct);

    // ----- Ejecucion del corte (manual en MVP - en Fase 2 automatico al vencer ventana) -----

    Task<TransferenciaDto> EjecutarCorteAsync(Guid transferenciaId, CancellationToken ct);

    // ----- Resumen -----

    Task<ResumenTransferenciasDto> GetResumenAsync(CancellationToken ct);
}
