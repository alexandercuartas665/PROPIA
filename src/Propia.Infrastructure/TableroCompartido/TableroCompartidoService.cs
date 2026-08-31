using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.TableroCompartido;
using Propia.Application.Tareas;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.TableroCompartido;

/// <summary>
/// Tablero compartido (espejo cross-tenant de Tareas). Lee tenant por tenant impersonando
/// (SetTenant + set_config, patron del Admin Agent API) y restaura el tenant original al salir.
/// Sin tablas propias ni migracion: el estado SIEMPRE vive en el tenant dueno de la tarea.
/// </summary>
public class TableroCompartidoService : ITableroCompartidoService
{
    private const string RolAdministrador = "Administrador";

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ITareasService _tareas;
    private readonly ILogger<TableroCompartidoService> _logger;

    public TableroCompartidoService(
        PropiaDbContext db,
        ITenantContext tenant,
        ITareasService tareas,
        ILogger<TableroCompartidoService> logger)
    {
        _db = db;
        _tenant = tenant;
        _tareas = tareas;
        _logger = logger;
    }

    public async Task<TableroCompartidoDto?> ObtenerAsync(Guid userId, CancellationToken ct)
    {
        var tenantIds = await TenantsAdministradosAsync(userId, ct);
        if (tenantIds.Count == 0) return null;

        // La tabla tenants no tiene RLS: los nombres se leen sin impersonar.
        var copros = await _db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nombre, t.LogoUrl })
            .OrderBy(t => t.Nombre)
            .ToListAsync(ct);

        var tarjetas = new List<TarjetaCompartidaDto>();
        var original = _tenant.CurrentTenantId;
        try
        {
            foreach (var copro in copros)
            {
                await ImpersonarAsync(copro.Id, ct);
                try
                {
                    // Solo trabajo vivo: sin eliminadas, sin cerradas y sin etapas terminales
                    // (las cerradas viven en la pestana Cerrados de su propio tenant).
                    var rows = await _db.Tareas.AsNoTracking()
                        .Where(t => !t.Eliminada && !t.Cerrada)
                        .Where(t => t.Estado != null && t.Estado.Activo && !t.Estado.EsTerminal)
                        .Select(t => new
                        {
                            t.Id,
                            t.NumeroTarea,
                            t.Titulo,
                            t.Color,
                            t.EstadoId,
                            EstadoNombre = t.Estado!.Nombre,
                            EstadoColor = t.Estado.Color,
                            EstadoOrden = t.Estado.Orden,
                            t.TableroId,
                            Responsable = t.AsignadoPersona != null ? (t.AsignadoPersona.Nombres + " " + t.AsignadoPersona.Apellidos) : null,
                            t.FechaVencimiento,
                            t.Progreso,
                            t.Prioridad,
                            t.EsProyecto
                        })
                        .ToListAsync(ct);

                    var tableros = await _db.Tableros.AsNoTracking()
                        .Select(b => new { b.Id, b.Nombre })
                        .ToDictionaryAsync(b => b.Id, b => b.Nombre, ct);

                    tarjetas.AddRange(rows.Select(r => new TarjetaCompartidaDto(
                        r.Id, copro.Id, copro.Nombre, r.NumeroTarea, r.Titulo, r.Color,
                        r.EstadoId, r.EstadoNombre, r.EstadoColor, r.EstadoOrden,
                        r.TableroId is Guid tb && tableros.TryGetValue(tb, out var tn) ? tn : null,
                        string.IsNullOrWhiteSpace(r.Responsable) ? null : r.Responsable!.Trim(),
                        r.FechaVencimiento, r.Progreso, r.Prioridad.ToString(), r.EsProyecto)));
                }
                catch (Exception ex)
                {
                    // Best-effort: una copropiedad con problema no tumba el tablero completo.
                    _logger.LogError(ex, "Tablero compartido: fallo leyendo tareas del tenant {TenantId}", copro.Id);
                }
            }
        }
        finally
        {
            await RestaurarAsync(original, ct);
        }

        var porTenant = tarjetas.GroupBy(t => t.TenantId).ToDictionary(g => g.Key, g => g.Count());
        var coprosDto = copros
            .Select(c => new CopropiedadTableroDto(c.Id, c.Nombre, c.LogoUrl, porTenant.GetValueOrDefault(c.Id)))
            .ToList();

        return new TableroCompartidoDto(coprosDto, tarjetas);
    }

    public async Task<MoverTarjetaCompartidaResultado> MoverAsync(Guid userId, MoverTarjetaCompartidaRequest req, CancellationToken ct)
    {
        var destinoNombre = (req.EstadoNombreDestino ?? "").Trim();
        if (destinoNombre.Length == 0)
            return new MoverTarjetaCompartidaResultado(false, "Falta la etapa destino.");

        // La pertenencia del tenant se revalida SIEMPRE en el servidor (sin RLS de red aqui).
        var tenantIds = await TenantsAdministradosAsync(userId, ct);
        if (!tenantIds.Contains(req.TenantId))
            return new MoverTarjetaCompartidaResultado(false, "No administras esa copropiedad.");

        var original = _tenant.CurrentTenantId;
        try
        {
            await ImpersonarAsync(req.TenantId, ct);

            var tarea = await _db.Tareas.AsNoTracking()
                .Where(t => t.Id == req.TareaId && !t.Eliminada && !t.Cerrada)
                .Select(t => new { t.Id, t.EstadoId, t.TableroId })
                .FirstOrDefaultAsync(ct);
            if (tarea is null)
                return new MoverTarjetaCompartidaResultado(false, "La tarea ya no existe o esta cerrada.");

            // La etapa destino se resuelve POR NOMBRE dentro del tablero de la tarea. Solo etapas
            // activas y no terminales (cerrar con motivo se hace en el tenant, no desde aqui).
            var candidatas = await _db.TareasEstados.AsNoTracking()
                .Where(e => e.Activo && !e.EsTerminal && e.TableroId == tarea.TableroId)
                .Select(e => new { e.Id, e.Nombre })
                .ToListAsync(ct);
            var destino = candidatas.FirstOrDefault(e =>
                string.Equals(e.Nombre.Trim(), destinoNombre, StringComparison.OrdinalIgnoreCase));
            if (destino is null)
                return new MoverTarjetaCompartidaResultado(false,
                    $"Esa copropiedad no tiene la etapa '{destinoNombre}' en el tablero de la tarea.");

            if (destino.Id == tarea.EstadoId)
                return new MoverTarjetaCompartidaResultado(true, null);

            // Servicio REAL de Tareas bajo el tenant impersonado: historial y reglas intactos.
            var ok = await _tareas.CambiarEstadoAsync(tarea.Id, new CambiarEstadoRequest(destino.Id, null), ct);
            return ok
                ? new MoverTarjetaCompartidaResultado(true, null)
                : new MoverTarjetaCompartidaResultado(false, "No se pudo mover la tarea.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tablero compartido: fallo moviendo la tarea {TareaId} del tenant {TenantId}", req.TareaId, req.TenantId);
            return new MoverTarjetaCompartidaResultado(false, "Error moviendo la tarea. Intenta de nuevo.");
        }
        finally
        {
            await RestaurarAsync(original, ct);
        }
    }

    // ---------- impersonacion (patron Admin Agent API) ----------

    private async Task ImpersonarAsync(Guid tenantId, CancellationToken ct)
    {
        _tenant.SetTenant(tenantId);
        await _db.Database.ExecuteSqlRawAsync("SELECT set_config('app.tenant_id', {0}, false)", new object[] { tenantId.ToString() }, ct);
    }

    private async Task RestaurarAsync(Guid? original, CancellationToken ct)
    {
        try
        {
            if (original is Guid g)
            {
                await ImpersonarAsync(g, ct);
            }
            else
            {
                await _db.Database.ExecuteSqlRawAsync("SELECT set_config('app.tenant_id', '', false)", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tablero compartido: no se pudo restaurar el tenant original");
        }
    }

    /// <summary>Copropiedades donde la persona del usuario es Administrador (SECURITY DEFINER,
    /// mismo criterio de MisCopropiedadesService).</summary>
    private async Task<List<Guid>> TenantsAdministradosAsync(Guid userId, CancellationToken ct)
    {
        var personaId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PersonaId)
            .FirstOrDefaultAsync(ct);
        if (personaId is not Guid pid) return new List<Guid>();

        var ids = new List<Guid>();
        var conn = _db.Database.GetDbConnection();
        var abiertaAqui = conn.State != System.Data.ConnectionState.Open;
        if (abiertaAqui) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT tenant_id, rol FROM get_tenants_for_persona(@p_persona_id)";
            var p = cmd.CreateParameter();
            p.ParameterName = "@p_persona_id";
            p.Value = pid;
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var rol = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                if (string.Equals(rol, RolAdministrador, StringComparison.OrdinalIgnoreCase))
                    ids.Add(reader.GetGuid(0));
            }
        }
        finally
        {
            if (abiertaAqui) await conn.CloseAsync();
        }
        return ids;
    }
}
