using Propia.Domain.Enums;

namespace Propia.Application.Calendario;

/// <summary>
/// Modulo 1.2 Calendario Multi-Copropiedad - servicio de aplicacion (spec v1.0 MVP).
///
/// Alcance MVP:
///  - Vista Agenda + Criticos (Mes/Semana/Dia se pueden derivar de la misma data).
///  - Eventos internos CRUD (RecordatorioPersonal/Equipo/Bloqueo).
///  - Agregador cross-modulo (RN: nunca consulta tablas operativas directo).
///    Lee de 2.8 asambleas, 2.9 pqrsd, 2.10 tareas, 2.11 mantenimiento, 2.13 bloqueos.
///  - Filtros por copropiedad y categoria.
///  - Configuracion personal por usuario + organizacion.
///  - Feed iCal con token (RFC 5545).
///
/// Diferido a Fase 2:
///  - Notificaciones reales via T.2 (recordatorios email/WhatsApp).
///  - Sincronizacion bidireccional Google/Outlook (OAuth + webhooks).
///  - Vista Semana/Dia con drag & drop completo.
///  - Acciones rapidas: crear tarea desde slot vacio (hook 2.10).
///  - Acta export .ics individual por evento.
/// </summary>
public interface ICalendarioService
{
    // ----- Vista Agenda (consume modulos productores) -----

    /// <summary>Lista cronologica de todos los eventos visibles en el rango con los filtros del usuario.</summary>
    Task<IReadOnlyList<EventoCalendarioDto>> ListarEventosAsync(FiltroCalendarioDto filtro, CancellationToken ct);

    /// <summary>Pestania Criticos: eventos con consecuencia legal o contractual urgente.</summary>
    Task<IReadOnlyList<CriticoDto>> ListarCriticosAsync(CancellationToken ct);

    // ----- Eventos internos -----

    Task<IReadOnlyList<EventoInternoDto>> ListarEventosInternosAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct);
    Task<EventoInternoDto?> GetEventoInternoAsync(Guid id, CancellationToken ct);
    Task<EventoInternoDto> CrearEventoInternoAsync(CrearEventoInternoRequest req, CancellationToken ct);
    Task<bool> ActualizarEventoInternoAsync(Guid id, ActualizarEventoInternoRequest req, CancellationToken ct);
    Task<bool> EliminarEventoInternoAsync(Guid id, CancellationToken ct);

    // ----- Configuracion del usuario -----

    Task<CalendarioConfigDto> GetConfigAsync(CancellationToken ct);
    Task<CalendarioConfigDto> ActualizarConfigAsync(ActualizarConfigCalendarioRequest req, CancellationToken ct);
    Task<Guid> GenerarOReGenerarIcalTokenAsync(CancellationToken ct);
    Task<bool> RevocarIcalTokenAsync(CancellationToken ct);

    // ----- Feed iCal publico -----

    /// <summary>Genera el contenido RFC 5545 del feed iCal para un token de usuario. Sin auth de sesion.</summary>
    Task<string?> GenerarIcsAsync(Guid token, CancellationToken ct);

    // ----- Resumen para Panel 1.1 -----

    Task<ResumenCalendarioDto> GetResumenAsync(CancellationToken ct);
}
