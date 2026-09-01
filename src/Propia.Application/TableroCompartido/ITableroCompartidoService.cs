namespace Propia.Application.TableroCompartido;

/// <summary>
/// Tablero compartido entre las copropiedades que el usuario ADMINISTRA: un Kanban cross-tenant
/// que ESPEJA las tareas reales de cada tenant (no hay copias ni tablas nuevas). Es una feature
/// de plataforma (existe para todos los clientes); cada administrador ve solo su tajada.
///
/// Seguridad: las tablas de Tareas tienen RLS, asi que el servicio lee tenant por tenant con el
/// patron de impersonacion del Admin Agent API (SetTenant + set_config) y la lista de tenants
/// SIEMPRE se deriva en el servidor de get_tenants_for_persona (rol Administrador) - jamas del
/// cliente. Mover una tarjeta ejecuta el CambiarEstadoAsync REAL del tenant (historial intacto).
/// </summary>
/// <summary>Identidad fija del tablero virtual "Todas mis copropiedades" (compartida API/UI).</summary>
public static class TableroCompartidoConstantes
{
    public static readonly Guid BoardId = Guid.Parse("c0417a5d-0000-4000-8000-7ab1e0c09a77");
    public const string BoardNombre = "Todas mis copropiedades";
}

public interface ITableroCompartidoService
{
    /// <summary>
    /// Board VIRTUAL con el mismo contrato de un tablero normal (TableroBoardDto): por cada
    /// copropiedad administrada se llama el GetTableroBoardAsync REAL bajo impersonacion y se
    /// fusionan estados (por nombre) y tareas (con TenantId/TenantNombre). Asi la pagina de
    /// Tareas lo renderiza con sus vistas existentes sin codigo nuevo. Null = sin acceso.
    /// </summary>
    Task<Propia.Application.Tareas.TableroBoardDto?> ObtenerBoardVirtualAsync(Guid userId, CancellationToken ct);

    /// <summary>Null cuando el usuario no administra ninguna copropiedad (sin acceso).</summary>
    Task<TableroCompartidoDto?> ObtenerAsync(Guid userId, CancellationToken ct);

    Task<MoverTarjetaCompartidaResultado> MoverAsync(Guid userId, MoverTarjetaCompartidaRequest req, CancellationToken ct);

    /// <summary>
    /// Busca personas en el directorio de TODAS las copropiedades que el usuario administra
    /// (para invitar usuarios cross-tenant a un tablero de Tareas). Solo devuelve USUARIOS DEL
    /// SISTEMA (personas con cuenta/login): por ahora es la regla del producto para trabajar en
    /// tableros. Deduplica por persona priorizando la copropiedad activa; vacio si el usuario
    /// no administra ninguna.
    /// </summary>
    Task<IReadOnlyList<PersonaCrossTenantDto>> BuscarPersonasAsync(Guid userId, string q, CancellationToken ct);
}

/// <summary>Persona encontrada en el directorio de una de mis copropiedades administradas.</summary>
public sealed record PersonaCrossTenantDto(
    Guid Id, string Nombres, string Apellidos, string Documento, string? FotoUrl,
    Guid TenantId, string TenantNombre);

/// <summary>Una tarea real de un tenant, proyectada como tarjeta del tablero compartido.</summary>
public sealed record TarjetaCompartidaDto(
    Guid TareaId,
    Guid TenantId,
    string TenantNombre,
    string NumeroTarea,
    string Titulo,
    string? Color,
    Guid EstadoId,
    string EstadoNombre,
    string? EstadoColor,
    int EstadoOrden,
    string? TableroNombre,
    string? ResponsableNombre,
    DateOnly? FechaVencimiento,
    int Progreso,
    string Prioridad,
    bool EsProyecto);

public sealed record CopropiedadTableroDto(Guid TenantId, string Nombre, string? LogoUrl, int Tarjetas);

public sealed record TableroCompartidoDto(
    IReadOnlyList<CopropiedadTableroDto> Copropiedades,
    IReadOnlyList<TarjetaCompartidaDto> Tarjetas);

/// <summary>El destino es el NOMBRE de la columna: la tarea se mueve a la etapa con ese nombre
/// EN SU PROPIO tenant (si su tablero no la tiene, la operacion se rechaza con mensaje).</summary>
public sealed record MoverTarjetaCompartidaRequest(Guid TenantId, Guid TareaId, string EstadoNombreDestino);

public sealed record MoverTarjetaCompartidaResultado(bool Ok, string? Error);
