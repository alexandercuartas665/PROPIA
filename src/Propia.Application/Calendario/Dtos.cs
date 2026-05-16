using Propia.Domain.Enums;

namespace Propia.Application.Calendario;

// ===========================================================================
// Eventos agregados del calendario (de Capa 2 modulos productores + internos)
// ===========================================================================

/// <summary>Evento unificado que aparece en el calendario - viene de cualquier modulo productor o de eventos internos.</summary>
public record EventoCalendarioDto(
    string Id,                       // "interno:{guid}" o "asamblea:{guid}" etc.
    CategoriaEvento Categoria,
    string Titulo,
    string? Descripcion,
    Guid? TenantId,
    string? CopropiedadNombre,
    string? CopropiedadColor,
    DateTimeOffset FechaInicio,
    DateTimeOffset? FechaFin,
    bool EsDiaCompleto,
    string ZonaHoraria,
    /// <summary>URL al modulo origen para navegar (ej. /asambleas/{id}).</summary>
    string? UrlModuloOrigen,
    /// <summary>True si es evento interno (icono lapiz + borde punteado).</summary>
    bool EsInterno);

public record CriticoDto(
    string Id,
    SeveridadCritico Severidad,
    string Titulo,
    string Descripcion,
    Guid? TenantId,
    string? CopropiedadNombre,
    DateTimeOffset Vencimiento,
    int DiasRestantes,
    string? UrlModuloOrigen);

// ===========================================================================
// Eventos internos (CRUD)
// ===========================================================================

public record EventoInternoDto(
    Guid Id,
    Guid OrganizacionId,
    Guid? TenantId,
    string? CopropiedadNombre,
    string Titulo,
    string? Descripcion,
    TipoEventoInterno Tipo,
    DateTimeOffset FechaInicio,
    DateTimeOffset? FechaFin,
    bool EsDiaCompleto,
    int? RecordatorioMinutos,
    Guid CreadoPorUsuarioId,
    DateTimeOffset CreadoAt);

public record CrearEventoInternoRequest(
    Guid? TenantId,
    string Titulo,
    string? Descripcion,
    TipoEventoInterno Tipo,
    DateTimeOffset FechaInicio,
    DateTimeOffset? FechaFin,
    bool EsDiaCompleto,
    int? RecordatorioMinutos);

public record ActualizarEventoInternoRequest(
    string Titulo,
    string? Descripcion,
    TipoEventoInterno Tipo,
    DateTimeOffset FechaInicio,
    DateTimeOffset? FechaFin,
    bool EsDiaCompleto,
    int? RecordatorioMinutos);

// ===========================================================================
// Filtros y configuracion del usuario
// ===========================================================================

public record FiltroCalendarioDto(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<Guid>? Copropiedades,
    IReadOnlyList<CategoriaEvento>? Categorias);

public record CalendarioConfigDto(
    VistaCalendario VistaDefault,
    VistaCalendario UltimaVista,
    IReadOnlyList<Guid>? FiltroCopropiedades,
    IReadOnlyList<CategoriaEvento>? FiltroTipos,
    Guid? IcalToken,
    int AnticipacionAsamblea,
    int AnticipacionTarea,
    int AnticipacionMantenimiento,
    int AnticipacionPqrsd);

public record ActualizarConfigCalendarioRequest(
    VistaCalendario VistaDefault,
    IReadOnlyList<Guid>? FiltroCopropiedades,
    IReadOnlyList<CategoriaEvento>? FiltroTipos,
    int AnticipacionAsamblea,
    int AnticipacionTarea,
    int AnticipacionMantenimiento,
    int AnticipacionPqrsd);

// ===========================================================================
// Resumen
// ===========================================================================

public record ResumenCalendarioDto(
    int TotalEventosProximos30Dias,
    int Criticos,
    int Asambleas,
    int Mantenimientos,
    int TareasVencen,
    int EventosInternos);
