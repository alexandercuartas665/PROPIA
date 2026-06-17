using Propia.Domain.Enums;

namespace Propia.Application.Pqrsd;

// ===================== Categorias y plazos =====================

public record PqrsdCategoriaDto(Guid Id, string Nombre, bool EsPredeterminada, bool Activa, int Orden);
public record CrearCategoriaRequest(string Nombre, int Orden);
public record ActualizarCategoriaRequest(string Nombre, bool Activa, int Orden);

public record PqrsdPlazoDto(TipoPqrsd Tipo, int DiasHabiles, int DiasInconformidad, NivelUrgenciaPqrsd NivelUrgencia);
public record ActualizarPlazoRequest(int DiasHabiles, int DiasInconformidad, NivelUrgenciaPqrsd NivelUrgencia);

// ===================== Expediente =====================

public record PqrsdAdjuntoDto(Guid Id, string NombreArchivo, string TipoMime, long TamanioBytes, string UrlStorage, DateTimeOffset CreatedAt);

public record PqrsdHistorialDto(
    EstadoPqrsd? EstadoAnterior,
    EstadoPqrsd EstadoNuevo,
    Guid? ActorUsuarioId,
    OrigenCambioEstado Origen,
    string? Nota,
    DateTimeOffset CreatedAt);

public record PqrsdBandejaItemDto(
    Guid Id,
    string NumeroRadicado,
    TipoPqrsd Tipo,
    string CategoriaNombre,
    string DescripcionResumen,
    EstadoPqrsd Estado,
    SemaforoPqrsd Semaforo,
    /// <summary>Si el expediente tiene reserva de identidad, este es null para el admin.</summary>
    string? RadicadorNombre,
    string? UnidadNumero,
    bool IdentidadReservada,
    bool TutelaActiva,
    DateOnly FechaVencimiento,
    int DiasHastaVencimiento,
    NivelUrgenciaPqrsd NivelUrgencia,
    bool TieneComiteActivo,
    DateTimeOffset CreatedAt);

public record PqrsdKpisDto(
    int Total, int Recibidas, int EnGestion, int Respondidas, int Cerradas,
    int PorVencer, int Vencidas, int ConTutela, int ConComite);

public record PqrsdBandejaDto(PqrsdKpisDto Kpis, IReadOnlyList<PqrsdBandejaItemDto> Items);

public record PqrsdComiteSesionDto(
    Guid Id,
    DateTimeOffset? FechaSesion,
    ModalidadComite Modalidad,
    string? EnlaceReunion,
    ResultadoComite? Resultado,
    string? BorradorActa,
    string? ActaFinal,
    Guid ActivadaPorUsuarioId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PqrsdComiteMiembroDto> Miembros);

public record PqrsdComiteMiembroDto(Guid Id, Guid PersonaId, string Nombre);

public record PqrsdExpedienteDetalleDto(
    Guid Id,
    string NumeroRadicado,
    TipoPqrsd Tipo,
    Guid CategoriaId,
    string CategoriaNombre,
    string Descripcion,
    EstadoPqrsd Estado,
    SemaforoPqrsd Semaforo,
    /// <summary>Null si IdentidadReservada y el solicitante es el Admin sin override.</summary>
    string? RadicadorNombre,
    Guid? RadicadorPersonaId,
    string? UnidadNumero,
    bool IdentidadReservada,
    bool TutelaActiva,
    DateTimeOffset? TutelaActivadaAt,
    DateOnly FechaVencimiento,
    int DiasHastaVencimiento,
    NivelUrgenciaPqrsd NivelUrgencia,
    string? RespuestaAdmin,
    DateTimeOffset? RespuestaAdminAt,
    string? InconformidadTexto,
    DateTimeOffset? InconformidadAt,
    string? RespuestaDefinitiva,
    DateTimeOffset? RespuestaDefinitivaAt,
    DateTimeOffset? FechaCierre,
    Guid? TareaId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PqrsdAdjuntoDto> Adjuntos,
    IReadOnlyList<PqrsdHistorialDto> Historial,
    PqrsdComiteSesionDto? Comite);

// ===================== Requests =====================

public record RadicarPqrsdRequest(
    TipoPqrsd Tipo,
    Guid CategoriaId,
    string Descripcion,
    bool IdentidadReservada,
    IReadOnlyList<AdjuntoInicialDto>? Adjuntos);

public record AdjuntoInicialDto(string NombreArchivo, string TipoMime, long TamanioBytes, string UrlStorage);

public record TomarExpedienteRequest(string? Nota);

public record ResponderExpedienteRequest(string Texto);

public record ManifestarInconformidadRequest(string Texto);

public record CerrarDefinitivoRequest(string RespuestaDefinitiva);

/// <summary>Contexto humano del expediente: personas asociadas a la unidad del radicador.</summary>
public record PqrsdContextoPersonaDto(
    Guid PersonaId, string Nombre, string Documento,
    string? Email, string? Telefono, string Rol, bool EsRadicador);

public record PqrsdContextoUnidadDto(
    Guid? UnidadId, string? UnidadNumero, string? TorreNombre, int? Piso,
    string? Tipo, decimal? CoeficientePropiedad);

public record PqrsdContextoDto(
    PqrsdContextoUnidadDto Unidad,
    IReadOnlyList<PqrsdContextoPersonaDto> Personas);

public record AsignarUnidadPqrsdRequest(Guid? UnidadId);

public record AgregarAdjuntoPqrsdRequest(
    string NombreArchivo, string TipoMime, long TamanioBytes, string UrlStorage);

public record ActivarTutelaRequest(string Justificacion);

public record EscalarAComiteRequest(
    DateTimeOffset? FechaPropuestaSesion,
    ModalidadComite Modalidad,
    string? EnlaceReunion,
    IReadOnlyList<Guid> PersonaIds);

public record RegistrarSesionComiteRequest(
    DateTimeOffset FechaSesion,
    string? Acta,
    ResultadoComite Resultado);
