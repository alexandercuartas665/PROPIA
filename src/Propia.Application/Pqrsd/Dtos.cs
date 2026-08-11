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
    DateTimeOffset CreatedAt,
    /// <summary>Columna del tablero configurable donde se ubica el expediente.</summary>
    Guid? EstadoId = null,
    bool Archivado = false,
    Guid? UnidadPrivadaId = null,
    Guid? RadicadorPersonaId = null,
    IReadOnlyList<PqrsdCampoValorDto>? Campos = null,
    /// <summary>Nombre del tipo configurable elegido (fallback al label del enum).</summary>
    string? TipoNombre = null);

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
    PqrsdComiteSesionDto? Comite,
    Guid? EstadoId = null,
    Guid? UnidadPrivadaId = null,
    bool Archivado = false,
    IReadOnlyList<PqrsdCampoValorDto>? Campos = null,
    Guid? TipoId = null,
    string? TipoNombre = null,
    Guid? AsignadoPersonaId = null,
    string? AsignadoNombre = null,
    int Progreso = 0,
    IReadOnlyList<PqrsdComentarioDto>? Comentarios = null);

public record PqrsdComentarioDto(Guid Id, string Texto, string? AutorNombre, DateTimeOffset CreatedAt);

// ===================== Tablero configurable (columnas + campos dinamicos) =====================

/// <summary>Columna/estado configurable del tablero PQRS. Port de Tareas.</summary>
public record PqrsdEstadoDto(
    Guid Id, string Nombre, string? Color, int Orden,
    bool EsTerminal, bool EsBase, bool Activo, EstadoPqrsd? SemanticaLegal);

public record CrearEstadoPqrsdRequest(string Nombre, string? Color);
public record ActualizarEstadoPqrsdRequest(string Nombre, string? Color, int Orden);
public record MoverExpedienteEstadoRequest(Guid EstadoId);

/// <summary>Campo personalizado del tablero PQRS. Port de TableroCampo.</summary>
public record PqrsdCampoDto(
    Guid Id, string Label, int Orden, TipoCampoTablero Tipo, string? Opciones,
    bool MostrarEnFiltro, int Columna, string? Descripcion, bool Requerido,
    string? ValorPorDefecto, bool PermiteVarios, string? CamposSuma, bool Activo);

public record GuardarCampoPqrsdRequest(
    string Label, TipoCampoTablero Tipo, string? Opciones, bool MostrarEnFiltro,
    int Columna, string? Descripcion, bool Requerido, string? ValorPorDefecto,
    bool PermiteVarios, string? CamposSuma);

public record PqrsdCampoValorDto(Guid CampoId, string? Valor);

/// <summary>Payload para archivar/restaurar un expediente (clic tipo checkbox).</summary>
public record ArchivarExpedienteRequest(bool Archivar);

// ===================== Tipos configurables =====================

/// <summary>Tipo de PQRS configurable por copropiedad (catalogo editable).</summary>
public record PqrsdTipoDto(
    Guid Id, string Nombre, int DiasHabiles, int DiasInconformidad,
    NivelUrgenciaPqrsd NivelUrgencia, TipoPqrsd Legal, bool EsBase, bool Activo, int Orden);

public record GuardarTipoPqrsdRequest(
    string Nombre, int DiasHabiles, int DiasInconformidad, NivelUrgenciaPqrsd NivelUrgencia, TipoPqrsd Legal);

// ===================== Requests =====================

public record RadicarPqrsdRequest(
    TipoPqrsd Tipo,
    Guid CategoriaId,
    string Descripcion,
    bool IdentidadReservada,
    IReadOnlyList<AdjuntoInicialDto>? Adjuntos,
    /// <summary>Unidad privada con la que se relaciona el PQR (opcional).</summary>
    Guid? UnidadPrivadaId = null,
    /// <summary>Persona del directorio como radicador (admin radicando en nombre de otro). Null = usuario actual.</summary>
    Guid? RadicadorPersonaId = null,
    IReadOnlyList<PqrsdCampoValorDto>? Campos = null,
    /// <summary>Tipo configurable elegido. Si viene, define el nombre + plazo (y su Legal fija el enum Tipo).</summary>
    Guid? TipoId = null);

/// <summary>Actualiza datos editables del expediente desde el modal de detalle (unidad, radicador, campos dinamicos).</summary>
public record ActualizarExpedienteRequest(
    Guid? UnidadPrivadaId,
    Guid? RadicadorPersonaId,
    string? Descripcion,
    IReadOnlyList<PqrsdCampoValorDto>? Campos,
    Guid? AsignadoPersonaId = null,
    int? Progreso = null);

public record ReportarActividadPqrsdRequest(string Texto);

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
