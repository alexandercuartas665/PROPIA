using Propia.Domain.Enums;

namespace Propia.Application.Pqrsd;

/// <summary>Modulo 2.9 PQRSD y Convivencia - spec v1.0 MVP.</summary>
public interface IPqrsdService
{
    // Tablero configurable: columnas (estados) editables
    Task<IReadOnlyList<PqrsdEstadoDto>> ListarEstadosAsync(CancellationToken ct);
    Task<PqrsdEstadoDto> CrearEstadoAsync(CrearEstadoPqrsdRequest req, CancellationToken ct);
    Task<bool> ActualizarEstadoAsync(Guid id, ActualizarEstadoPqrsdRequest req, CancellationToken ct);
    Task<bool> EliminarEstadoAsync(Guid id, CancellationToken ct);
    Task<bool> ReordenarEstadoAsync(Guid id, string direccion, CancellationToken ct);
    Task<bool> MoverAEstadoAsync(Guid expedienteId, Guid estadoId, CancellationToken ct);

    // Tablero configurable: campos dinamicos (port de Tareas)
    Task<IReadOnlyList<PqrsdCampoDto>> ListarCamposAsync(CancellationToken ct);
    Task<IReadOnlyList<PqrsdCampoDto>> ListarCamposArchivadosAsync(CancellationToken ct);
    Task<PqrsdCampoDto> CrearCampoAsync(GuardarCampoPqrsdRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid id, GuardarCampoPqrsdRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid id, CancellationToken ct);
    Task<bool> SetCampoActivoAsync(Guid id, bool activo, CancellationToken ct);
    Task<bool> ReordenarCampoAsync(Guid id, string direccion, CancellationToken ct);

    // Expediente: archivar, asignar unidad/persona, campos dinamicos
    Task<bool> ArchivarExpedienteAsync(Guid id, bool archivar, CancellationToken ct);
    Task<bool> ActualizarExpedienteAsync(Guid id, ActualizarExpedienteRequest req, CancellationToken ct);

    /// <summary>Agrega un reporte de actividad (comentario libre) al expediente. Devuelve el comentario creado o null si el expediente no existe.</summary>
    Task<PqrsdComentarioDto?> ReportarActividadAsync(Guid id, ReportarActividadPqrsdRequest req, CancellationToken ct);

    /// <summary>Notifica las @menciones escritas en un comentario/caption de adjunto (canal InApp).</summary>
    Task NotificarMencionComentarioAsync(Guid expedienteId, string? texto, CancellationToken ct);

    /// <summary>Crea una tarea interna (2.10) a partir del PQR y la vincula. Idempotente (devuelve la existente).</summary>
    Task<Guid?> GenerarTareaAsync(Guid id, CancellationToken ct);

    // Categorias y plazos
    Task<IReadOnlyList<PqrsdCategoriaDto>> ListarCategoriasAsync(CancellationToken ct);
    Task<PqrsdCategoriaDto> CrearCategoriaAsync(CrearCategoriaRequest req, CancellationToken ct);
    Task<bool> ActualizarCategoriaAsync(Guid id, ActualizarCategoriaRequest req, CancellationToken ct);
    Task<bool> EliminarCategoriaAsync(Guid id, CancellationToken ct);
    Task<int> RestablecerCategoriasBaseAsync(CancellationToken ct);

    Task<IReadOnlyList<PqrsdPlazoDto>> ListarPlazosAsync(CancellationToken ct);
    Task<bool> ActualizarPlazoAsync(TipoPqrsd tipo, ActualizarPlazoRequest req, CancellationToken ct);

    // Tipos configurables (catalogo editable por copropiedad)
    Task<IReadOnlyList<PqrsdTipoDto>> ListarTiposAsync(bool incluirInactivos, CancellationToken ct);
    Task<PqrsdTipoDto> CrearTipoAsync(GuardarTipoPqrsdRequest req, CancellationToken ct);
    Task<bool> ActualizarTipoAsync(Guid id, GuardarTipoPqrsdRequest req, CancellationToken ct);
    Task<bool> EliminarTipoAsync(Guid id, CancellationToken ct);

    // Bandeja + ficha
    Task<PqrsdBandejaDto> GetBandejaAsync(EstadoPqrsd? estado, TipoPqrsd? tipo, Guid? categoriaId, string? query, bool incluirArchivados, CancellationToken ct);
    Task<PqrsdExpedienteDetalleDto?> GetExpedienteAsync(Guid id, CancellationToken ct);

    // Radicacion (residente o admin)
    Task<PqrsdExpedienteDetalleDto> RadicarAsync(RadicarPqrsdRequest req, CancellationToken ct);

    // Formulario publico (sin login): branding + catalogos, y radicacion externa
    /// <summary>Config del formulario publico (nombre + logo de la copropiedad + tipos/categorias activos). Null si el tenant no existe o no esta activo.</summary>
    Task<PqrsdPublicoConfigDto?> GetConfigPublicoAsync(Guid tenantId, CancellationToken ct);
    /// <summary>Radica un PQRSD desde el formulario publico. Resuelve/crea la persona por documento y matchea la unidad por numero exacto.</summary>
    Task<RadicarPublicoResultDto> RadicarPublicoAsync(Guid tenantId, RadicarPublicoRequest req, string? ipOrigen, CancellationToken ct);

    /// <summary>Config editable (admin) del formulario publico: que campos opcionales se muestran.</summary>
    Task<PqrsdFormularioPublicoConfigDto> GetFormularioPublicoConfigAsync(CancellationToken ct);
    /// <summary>Guarda (upsert) la config del formulario publico para la copropiedad activa.</summary>
    Task<bool> GuardarFormularioPublicoConfigAsync(PqrsdFormularioPublicoConfigDto req, CancellationToken ct);
    /// <summary>Marca/desmarca un campo dinamico para que se pida en el formulario publico.</summary>
    Task<bool> SetCampoPublicoAsync(Guid campoId, bool mostrar, CancellationToken ct);

    // Vista del residente
    Task<IReadOnlyList<PqrsdBandejaItemDto>> ListarMisPqrsdAsync(CancellationToken ct);

    // Ciclo de gestion
    Task<bool> TomarExpedienteAsync(Guid id, TomarExpedienteRequest req, CancellationToken ct);
    Task<bool> ResponderAsync(Guid id, ResponderExpedienteRequest req, CancellationToken ct);
    Task<bool> ManifestarInconformidadAsync(Guid id, ManifestarInconformidadRequest req, CancellationToken ct);
    Task<bool> CerrarDefinitivoAsync(Guid id, CerrarDefinitivoRequest req, CancellationToken ct);

    // Tutela
    Task<bool> ActivarTutelaAsync(Guid id, ActivarTutelaRequest req, CancellationToken ct);

    /// <summary>Prorroga: amplia el plazo en N dias habiles, pide motivo y lo deja en la traza (historial).</summary>
    Task<bool> AmpliarPlazoAsync(Guid id, AmpliarPlazoRequest req, CancellationToken ct);

    // Comite de Convivencia (solo Denuncia)
    Task<PqrsdComiteSesionDto> EscalarAComiteAsync(Guid expedienteId, EscalarAComiteRequest req, CancellationToken ct);
    Task<bool> RegistrarSesionComiteAsync(Guid sesionId, RegistrarSesionComiteRequest req, CancellationToken ct);

    // Tareas enlazadas al PQR (tablero "PQRSD" del modulo Tareas)
    Task<PqrTareasDto> ListTareasDePqrAsync(Guid pqrId, CancellationToken ct);
    /// <summary>Crea la tarea enlazada y devuelve su Id (null si el PQR no existe o el titulo esta vacio).</summary>
    Task<Guid?> CrearTareaDePqrAsync(Guid pqrId, CrearPqrTareaRequest req, CancellationToken ct);
}
