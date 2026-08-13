using Propia.Domain.Enums;

namespace Propia.Application.Tareas;

public record EstadoTareaDto(Guid Id, string Nombre, string? Color, int Orden, bool EsTerminal, bool EsBase, bool Activo);
public record EtiquetaTareaDto(Guid Id, string Nombre, string? Color, bool Activo, int CantidadTareas, Guid? TableroId = null);

public record TareaListaDto(
    Guid Id,
    string NumeroTarea,
    string Titulo,
    PrioridadTarea Prioridad,
    Guid EstadoId,
    string EstadoNombre,
    string? EstadoColor,
    bool EstadoEsTerminal,
    Guid? AsignadoPersonaId,
    string? AsignadoNombre,
    DateOnly? FechaVencimiento,
    bool Vencida,
    Guid? PadreId,
    int CantidadSubtareas,
    int CantidadComentarios,
    IReadOnlyList<EtiquetaTareaDto> Etiquetas,
    int Progreso = 0,
    string? Color = null,
    bool EsProyecto = false,
    decimal? Valor = null,
    DateOnly? FechaInicio = null,
    IReadOnlyList<TareaCampoValorDto>? CamposValores = null,
    IReadOnlyList<ResponsableMiniDto>? Responsables = null,
    string? Descripcion = null,
    string? OrigenTipo = null,
    string? OrigenReferencia = null,
    DateTimeOffset? EstadoDesde = null,
    // ----- Cierre (para la bandeja "Cerrados") -----
    string? MotivoCierre = null,
    DateTimeOffset? CerradaAt = null);

// Responsable de una tarea (asignado principal + colaboradores) con foto para la vista tabla.
public record ResponsableMiniDto(Guid PersonaId, string Nombre, string? FotoUrl);

// Edicion inline de UN campo de la tarea (vista tabla tipo Excel). Campo indica cual.
// TextoAux se usa para campos con dos valores (ej. "origen": Texto=tipo, TextoAux=referencia).
public record InlineUpdateTareaRequest(string Campo, string? Texto, decimal? Numero, DateOnly? Fecha, IReadOnlyList<Guid>? Guids, string? TextoAux = null);

// Set del valor de UN campo personalizado (TableroCampo) de la tarea.
public record SetCampoValorRequest(string? Valor);

public record TareaDetalleDto(
    Guid Id,
    string NumeroTarea,
    string Titulo,
    string? Descripcion,
    PrioridadTarea Prioridad,
    EstadoTareaDto Estado,
    Guid? AsignadoPersonaId,
    string? AsignadoNombre,
    DateOnly? FechaInicio,
    DateOnly? FechaVencimiento,
    DateTimeOffset? FechaCompletada,
    Guid? PadreId,
    string? PadreTitulo,
    OrigenTarea Origen,
    string? ModuloOrigenCodigo,
    Guid? ModuloOrigenEntidadId,
    DateTimeOffset CreatedAt,
    Guid? CreadoPorUsuarioId,
    IReadOnlyList<EtiquetaTareaDto> Etiquetas,
    IReadOnlyList<TareaListaDto> Subtareas,
    IReadOnlyList<TareaComentarioDto> Comentarios,
    IReadOnlyList<TareaHistorialDto> Historial,
    IReadOnlyList<TareaColaboradorDto> Colaboradores,
    // ----- Campos de la tarjeta rica (2.10) -----
    string? Color = null,
    bool EsProyecto = false,
    decimal? Valor = null,
    int Progreso = 0,
    TimeOnly? HoraInicio = null,
    TimeOnly? HoraFin = null,
    string? OrigenTipo = null,
    string? OrigenReferencia = null,
    Guid? TableroId = null,
    IReadOnlyList<TareaAdjuntoDto>? Adjuntos = null,
    IReadOnlyList<SubtareaCheckDto>? Checklist = null,
    IReadOnlyList<TareaCampoValorDto>? CamposValores = null,
    // ----- Traza de copias (copia independiente, NO subtarea) -----
    Guid? CopiadaDeTareaId = null,
    string? CopiadaDeNumero = null,
    string? CopiadaDeTitulo = null,
    IReadOnlyList<TareaListaDto>? Copias = null);

/// <summary>Opciones para copiar una tarea (mini-modal). La copia es independiente, no subtarea.</summary>
public record CopiarTareaRequest(
    string? Titulo = null,
    Guid? EstadoId = null,
    int Cantidad = 1,
    bool ConservarResponsables = true,
    bool ConservarEtiquetas = true,
    bool ConservarRelacion = true,
    bool ConservarCampos = true);

public record TareaComentarioDto(Guid Id, Guid AutorUsuarioId, string Texto, DateTimeOffset CreatedAt, string? AutorNombre = null);
public record TareaHistorialDto(TipoEventoTarea TipoEvento, string Descripcion, Guid RealizadoPorUsuarioId, DateTimeOffset OcurridoAt);
public record TareaColaboradorDto(Guid Id, Guid PersonaId, string Nombre);

// ----- Adjuntos y checklist de la tarjeta -----
public record TareaAdjuntoDto(Guid Id, string Nombre, string Url, string? SubidoPorNombre = null, DateTimeOffset? SubidoAt = null, Guid? SubidoPorUsuarioId = null, string? Texto = null);
public record SubtareaCheckDto(Guid Id, string Titulo, bool Hecho, int Orden);
public record SubtareaCheckItem(string Titulo, bool Hecho);

public record CrearTareaRequest(
    string Titulo,
    string? Descripcion,
    PrioridadTarea Prioridad,
    Guid? EstadoId,
    Guid? AsignadoPersonaId,
    DateOnly? FechaInicio,
    DateOnly? FechaVencimiento,
    Guid? PadreId,
    IReadOnlyList<Guid>? EtiquetaIds,
    Guid? TableroId = null,
    string? Color = null,
    bool EsProyecto = false,
    decimal? Valor = null,
    TimeOnly? HoraInicio = null,
    TimeOnly? HoraFin = null,
    string? OrigenTipo = null,
    string? OrigenReferencia = null,
    IReadOnlyList<Guid>? ResponsablePersonaIds = null,
    IReadOnlyList<SubtareaCheckItem>? Checklist = null,
    IReadOnlyList<TareaCampoValorDto>? CamposValores = null);

public record ActualizarTareaRequest(
    string Titulo,
    string? Descripcion,
    PrioridadTarea Prioridad,
    Guid? AsignadoPersonaId,
    DateOnly? FechaInicio,
    DateOnly? FechaVencimiento,
    Guid? EstadoId = null,
    string? Color = null,
    bool EsProyecto = false,
    decimal? Valor = null,
    int? Progreso = null,
    TimeOnly? HoraInicio = null,
    TimeOnly? HoraFin = null,
    string? OrigenTipo = null,
    string? OrigenReferencia = null,
    IReadOnlyList<Guid>? ResponsablePersonaIds = null,
    IReadOnlyList<SubtareaCheckItem>? Checklist = null,
    IReadOnlyList<TareaCampoValorDto>? CamposValores = null);

public record CambiarEstadoRequest(Guid EstadoId, string? MotivoCancelacion, Guid? MotivoCierreId = null);
public record CrearComentarioRequest(string Texto);
public record AsignarEtiquetaRequest(Guid EtiquetaId);
public record AgregarColaboradorRequest(Guid PersonaId);

public record CrearEstadoRequest(string Nombre, string? Color, int Orden, Guid? TableroId = null);
public record ActualizarEstadoRequest(string Nombre, string? Color, int Orden, bool Activo);

public record CrearEtiquetaRequest(string Nombre, string? Color, Guid? TableroId = null);
public record ActualizarEtiquetaRequest(string Nombre, string? Color, bool Activo);

public record ResumenTareasDto(
    int Total,
    int Pendientes,
    int EnProgreso,
    int Vencidas,
    int CompletadasUltimoMes,
    IReadOnlyList<(string Estado, int Cantidad)> PorEstado,
    IReadOnlyList<(string Prioridad, int Cantidad)> PorPrioridad,
    // KPIs del tablero (prototipo v2): tareas activas (no terminales), que vencen hoy,
    // total completadas y porcentaje de avance global.
    int Activas = 0,
    int VencenHoy = 0,
    int Completadas = 0,
    int AvancePct = 0);

// ===========================================================================
// Dependencias entre tareas (Fase 2)
// ===========================================================================

public record TareaDependenciaDto(
    Guid Id,
    Guid TareaId,
    Guid DependeDeTareaId,
    string DependeDeTareaNumero,
    string DependeDeTareaTitulo,
    string DependeDeTareaEstadoNombre,
    bool DependeDeTareaEsTerminal,
    TipoDependenciaTarea Tipo,
    DateTimeOffset CreadoAt);

public record AgregarDependenciaRequest(
    Guid DependeDeTareaId,
    TipoDependenciaTarea Tipo = TipoDependenciaTarea.Bloqueante);

// ===========================================================================
// Bulk actions (Fase 2)
// ===========================================================================

public record BulkCambiarEstadoRequest(
    IReadOnlyList<Guid> TareaIds,
    Guid NuevoEstadoId,
    string? Nota = null);

public record BulkCambiarPrioridadRequest(
    IReadOnlyList<Guid> TareaIds,
    PrioridadTarea Prioridad);

public record BulkAsignarPersonaRequest(
    IReadOnlyList<Guid> TareaIds,
    Guid? AsignadoPersonaId);

public record BulkResultDto(
    int Solicitados,
    int Aplicados,
    int Omitidos,
    IReadOnlyList<string> Errores);

// ----- Tableros de trabajo (2.10) -----
public record TableroUsuarioDto(Guid PersonaId, string Nombre, string Iniciales);
public record TableroCampoDto(
    Guid Id, string Label, int Orden,
    TipoCampoTablero Tipo = TipoCampoTablero.Texto,
    string? Opciones = null,
    bool MostrarEnFiltro = false,
    int Columna = 1,
    string? Descripcion = null,
    bool Requerido = false,
    string? ValorPorDefecto = null,
    bool PermiteVarios = false,
    string? CamposSuma = null,
    bool Activo = true);

/// <summary>Valor de un campo personalizado para una tarjeta (campoId -> valor).</summary>
public record TareaCampoValorDto(Guid CampoId, string? Valor);
public record TableroDto(
    Guid Id, string Nombre, string? Descripcion, string Color, int Orden,
    int NCards, IReadOnlyList<TableroUsuarioDto> Usuarios,
    IReadOnlyList<TableroCampoDto>? Campos = null);
public record GuardarTableroRequest(
    string Nombre, string? Descripcion, string Color, IReadOnlyList<Guid> UsuarioPersonaIds);
public record GuardarCampoRequest(
    string Label,
    TipoCampoTablero Tipo = TipoCampoTablero.Texto,
    string? Opciones = null,
    bool MostrarEnFiltro = false,
    int Columna = 1,
    string? Descripcion = null,
    bool Requerido = false,
    string? ValorPorDefecto = null,
    bool PermiteVarios = false,
    string? CamposSuma = null);

/// <summary>Vista completa de un tablero: el tablero + sus columnas/estados + sus tarjetas.</summary>
public record TableroBoardDto(
    TableroDto Tablero,
    IReadOnlyList<EstadoTareaDto> Estados,
    IReadOnlyList<TareaListaDto> Tareas);

public record ActualizarProgresoRequest(int Progreso);
