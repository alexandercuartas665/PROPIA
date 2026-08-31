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
public interface ITableroCompartidoService
{
    /// <summary>Null cuando el usuario no administra ninguna copropiedad (sin acceso).</summary>
    Task<TableroCompartidoDto?> ObtenerAsync(Guid userId, CancellationToken ct);

    Task<MoverTarjetaCompartidaResultado> MoverAsync(Guid userId, MoverTarjetaCompartidaRequest req, CancellationToken ct);
}

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
