using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.Directorio;
using Propia.Application.TableroCompartido;
using Propia.Application.Tareas;
using Propia.Domain.Enums;
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
    private readonly IDirectorioService _directorio;
    private readonly ILogger<TableroCompartidoService> _logger;

    public TableroCompartidoService(
        PropiaDbContext db,
        ITenantContext tenant,
        ITareasService tareas,
        IDirectorioService directorio,
        ILogger<TableroCompartidoService> logger)
    {
        _db = db;
        _tenant = tenant;
        _tareas = tareas;
        _directorio = directorio;
        _logger = logger;
    }

    public async Task<Propia.Application.Tareas.TableroBoardDto?> ObtenerBoardVirtualAsync(Guid userId, CancellationToken ct)
    {
        var tenantIds = await TenantsAdministradosAsync(userId, ct);
        if (tenantIds.Count == 0) return null;

        var copros = await _db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nombre })
            .OrderBy(t => t.Nombre)
            .ToListAsync(ct);

        // Estados virtuales unificados por NOMBRE (guid determinista por nombre normalizado).
        var estadosVirtuales = new Dictionary<string, EstadoTareaDto>(StringComparer.OrdinalIgnoreCase);
        var tareas = new List<TareaListaDto>();
        var original = _tenant.CurrentTenantId;
        try
        {
            foreach (var copro in copros)
            {
                await ImpersonarAsync(copro.Id, ct);
                try
                {
                    var tableros = await _tareas.ListarTablerosAsync(ct);
                    foreach (var tb in tableros)
                    {
                        // El board REAL de cada tablero: mismas proyecciones (etiquetas, subtareas,
                        // responsables, campos) que ve el tenant.
                        var board = await _tareas.GetTableroBoardAsync(tb.Id, ct, verCerradas: false);
                        if (board is null) continue;

                        var mapaEstado = new Dictionary<Guid, EstadoTareaDto>();
                        foreach (var est in board.Estados)
                        {
                            var clave = est.Nombre.Trim();
                            if (!estadosVirtuales.TryGetValue(clave, out var v))
                            {
                                v = new EstadoTareaDto(GuidPorNombre(clave), clave, est.Color, est.Orden,
                                    est.EsTerminal, EsBase: true, Activo: true);
                                estadosVirtuales[clave] = v;
                            }
                            else if (est.Orden < v.Orden)
                            {
                                v = v with { Orden = est.Orden };
                                estadosVirtuales[clave] = v;
                            }
                            mapaEstado[est.Id] = estadosVirtuales[clave];
                        }

                        foreach (var t in board.Tareas)
                        {
                            if (!mapaEstado.TryGetValue(t.EstadoId, out var v)) continue;
                            tareas.Add(t with
                            {
                                EstadoId = v.Id,
                                EstadoNombre = v.Nombre,
                                EstadoColor = v.Color,
                                TenantId = copro.Id,
                                TenantNombre = copro.Nombre
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tablero compartido: fallo armando el board del tenant {TenantId}", copro.Id);
                }
            }
        }
        finally
        {
            await RestaurarAsync(original, ct);
        }

        var estados = estadosVirtuales.Values
            .OrderBy(e => e.EsTerminal ? 1 : 0).ThenBy(e => e.Orden).ThenBy(e => e.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tablero = new TableroDto(
            TableroCompartidoConstantes.BoardId,
            TableroCompartidoConstantes.BoardNombre,
            "Espejo de las tareas reales de las copropiedades que administras.",
            "#5955D1", 999, tareas.Count,
            Array.Empty<TableroUsuarioDto>(), null);
        return new Propia.Application.Tareas.TableroBoardDto(tablero, estados, tareas);
    }

    /// <summary>Guid determinista por nombre de etapa (MD5): estable entre requests y tenants.</summary>
    private static Guid GuidPorNombre(string nombre)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("tablero-compartido:" + nombre.Trim().ToLowerInvariant()));
        return new Guid(bytes);
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
                .Where(e => e.Activo && e.TableroId == tarea.TableroId)
                .Select(e => new { e.Id, e.Nombre, e.EsTerminal })
                .ToListAsync(ct);
            var destino = candidatas.FirstOrDefault(e =>
                string.Equals(e.Nombre.Trim(), destinoNombre, StringComparison.OrdinalIgnoreCase));
            if (destino is null)
                return new MoverTarjetaCompartidaResultado(false,
                    $"Esa copropiedad no tiene la etapa '{destinoNombre}' en el tablero de la tarea.");
            if (destino.EsTerminal)
                return new MoverTarjetaCompartidaResultado(false,
                    "Cerrar la tarea (etapa terminal) pide motivo: abrela en su copropiedad para cerrarla.");

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

    // ===================== Invitados sin vinculo (tableros donde me invitaron) =====================

    private sealed record InvitacionRaw(Guid TenantId, string TenantNombre, Guid TableroId, string TableroNombre, string? Color, string? Descripcion);

    /// <summary>Filas crudas de get_tableros_invitado (SECURITY DEFINER) para la persona.</summary>
    private async Task<List<InvitacionRaw>> InvitacionesRawAsync(Guid personaId, CancellationToken ct)
    {
        var filas = new List<InvitacionRaw>();
        var conn = _db.Database.GetDbConnection();
        var abiertaAqui = conn.State != System.Data.ConnectionState.Open;
        if (abiertaAqui) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT tenant_id, tenant_nombre, tablero_id, tablero_nombre, tablero_color, tablero_descripcion FROM get_tableros_invitado(@p_persona_id)";
            var p = cmd.CreateParameter();
            p.ParameterName = "@p_persona_id";
            p.Value = personaId;
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                filas.Add(new InvitacionRaw(
                    reader.GetGuid(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.GetGuid(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5)));
            }
        }
        finally
        {
            if (abiertaAqui) await conn.CloseAsync();
        }
        return filas;
    }

    private async Task<Guid?> PersonaDelUsuarioAsync(Guid userId, CancellationToken ct)
        => await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TableroInvitacionDto>> InvitacionesAsync(Guid userId, CancellationToken ct)
    {
        if (await PersonaDelUsuarioAsync(userId, ct) is not Guid personaId)
            return Array.Empty<TableroInvitacionDto>();

        var filas = await InvitacionesRawAsync(personaId, ct);
        if (filas.Count == 0) return Array.Empty<TableroInvitacionDto>();

        // El tenant activo ya muestra sus tableros en la galeria, y los administrados los cubre
        // "Todas mis copropiedades": aqui solo van las invitaciones de OTRAS copropiedades.
        var activo = _tenant.CurrentTenantId;
        var administrados = (await TenantsAdministradosAsync(userId, ct)).ToHashSet();

        return filas
            .Where(f => f.TenantId != activo && !administrados.Contains(f.TenantId))
            .OrderBy(f => f.TenantNombre).ThenBy(f => f.TableroNombre)
            .Select(f => new TableroInvitacionDto(f.TenantId, f.TenantNombre, f.TableroId, f.TableroNombre, f.Color, f.Descripcion))
            .ToList();
    }

    public async Task<Propia.Application.Tareas.TableroBoardDto?> ObtenerBoardInvitadoAsync(Guid userId, Guid tenantId, Guid tableroId, CancellationToken ct)
    {
        if (await PersonaDelUsuarioAsync(userId, ct) is not Guid personaId) return null;

        // La autorizacion ES la invitacion: sin fila en tablero_usuarios no hay board.
        var filas = await InvitacionesRawAsync(personaId, ct);
        var inv = filas.FirstOrDefault(f => f.TenantId == tenantId && f.TableroId == tableroId);
        if (inv is null) return null;

        var original = _tenant.CurrentTenantId;
        try
        {
            await ImpersonarAsync(tenantId, ct);
            var board = await _tareas.GetTableroBoardAsync(tableroId, ct, verCerradas: false);
            if (board is null) return null;
            // Estados REALES tal cual; solo se estampa la copropiedad en cada tarea (chip + mover).
            var tareas = board.Tareas
                .Select(t => t with { TenantId = tenantId, TenantNombre = inv.TenantNombre })
                .ToList();
            return board with { Tareas = tareas };
        }
        finally
        {
            await RestaurarAsync(original, ct);
        }
    }

    public async Task<MoverTarjetaCompartidaResultado> MoverInvitadoAsync(Guid userId, Guid tenantId, Guid tableroId, Guid tareaId, Guid estadoId, CancellationToken ct)
    {
        if (await PersonaDelUsuarioAsync(userId, ct) is not Guid personaId)
            return new MoverTarjetaCompartidaResultado(false, "Usuario sin persona asociada.");

        var filas = await InvitacionesRawAsync(personaId, ct);
        if (!filas.Any(f => f.TenantId == tenantId && f.TableroId == tableroId))
            return new MoverTarjetaCompartidaResultado(false, "No estas invitado a ese tablero.");

        var original = _tenant.CurrentTenantId;
        try
        {
            await ImpersonarAsync(tenantId, ct);

            var tarea = await _db.Tareas.AsNoTracking()
                .Where(t => t.Id == tareaId && t.TableroId == tableroId && !t.Eliminada && !t.Cerrada)
                .Select(t => new { t.Id, t.EstadoId })
                .FirstOrDefaultAsync(ct);
            if (tarea is null)
                return new MoverTarjetaCompartidaResultado(false, "La tarea ya no existe en ese tablero o esta cerrada.");

            var destino = await _db.TareasEstados.AsNoTracking()
                .Where(e => e.Id == estadoId && e.Activo && e.TableroId == tableroId)
                .Select(e => new { e.Id, e.EsTerminal })
                .FirstOrDefaultAsync(ct);
            if (destino is null)
                return new MoverTarjetaCompartidaResultado(false, "La etapa destino no existe en ese tablero.");
            if (destino.EsTerminal)
                return new MoverTarjetaCompartidaResultado(false,
                    "Cerrar la tarea (etapa terminal) pide motivo: se hace dentro de la copropiedad.");
            if (destino.Id == tarea.EstadoId)
                return new MoverTarjetaCompartidaResultado(true, null);

            var ok = await _tareas.CambiarEstadoAsync(tareaId, new CambiarEstadoRequest(estadoId, null), ct);
            return ok
                ? new MoverTarjetaCompartidaResultado(true, null)
                : new MoverTarjetaCompartidaResultado(false, "No se pudo mover la tarea.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tablero invitado: fallo moviendo la tarea {TareaId} del tenant {TenantId}", tareaId, tenantId);
            return new MoverTarjetaCompartidaResultado(false, "Error moviendo la tarea. Intenta de nuevo.");
        }
        finally
        {
            await RestaurarAsync(original, ct);
        }
    }

    public async Task<IReadOnlyList<PersonaCrossTenantDto>> BuscarPersonasAsync(Guid userId, string q, CancellationToken ct)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return Array.Empty<PersonaCrossTenantDto>();

        // Alcance del buscador = TODAS las copropiedades del usuario (mismo criterio que el check de la
        // UI, que se muestra por acceso, no solo por rol Administrador). El resto del tablero compartido
        // sigue restringido a las administradas; aqui solo buscamos a quien invitar.
        var tenantIds = await TenantsDelUsuarioAsync(userId, soloAdmin: false, ct);
        if (tenantIds.Count == 0) return Array.Empty<PersonaCrossTenantDto>();

        var copros = await _db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nombre })
            .ToListAsync(ct);
        // La copropiedad activa primero: en el dedup gana su etiqueta.
        var original = _tenant.CurrentTenantId;
        copros = copros.OrderBy(c => c.Id == original ? 0 : 1).ThenBy(c => c.Nombre).ToList();
        var qLower = q.ToLowerInvariant();

        var candidatos = new List<PersonaCrossTenantDto>();
        var vistos = new HashSet<Guid>();
        try
        {
            foreach (var copro in copros)
            {
                await ImpersonarAsync(copro.Id, ct);
                try
                {
                    // 1) Personas del DIRECTORIO de la copropiedad que casan la busqueda.
                    var personas = await _directorio.ListarPersonasDelTenantAsync(q, ct);
                    foreach (var p in personas)
                    {
                        if (!vistos.Add(p.Id)) continue;
                        candidatos.Add(new PersonaCrossTenantDto(
                            p.Id, p.Nombres, p.Apellidos, p.Documento, p.FotoUrl, copro.Id, copro.Nombre));
                    }

                    // 2) USUARIOS DEL SISTEMA de la copropiedad (miembros activos en usuarios_tenant).
                    // Un usuario con login de otra copropiedad puede NO estar en su directorio; aun asi
                    // debe poder invitarse a un tablero. El filtro de cuenta de mas abajo confirma el login.
                    var usuariosSis = await _db.UsuariosTenant.AsNoTracking()
                        .Where(ut => ut.Estado == EstadoUsuarioTenant.Activo)
                        .Join(_db.Personas.IgnoreQueryFilters(), ut => ut.PersonaId, p => p.Id, (ut, p) => p)
                        .Where(p => p.Nombres.ToLower().Contains(qLower) || p.Apellidos.ToLower().Contains(qLower)
                                 || p.Documento.Contains(q) || (p.Email != null && p.Email.ToLower().Contains(qLower)))
                        .Select(p => new { p.Id, p.Nombres, p.Apellidos, p.Documento, p.FotoUrl })
                        .Take(60).ToListAsync(ct);
                    foreach (var p in usuariosSis)
                    {
                        if (!vistos.Add(p.Id)) continue;
                        candidatos.Add(new PersonaCrossTenantDto(
                            p.Id, p.Nombres, p.Apellidos, p.Documento, p.FotoUrl, copro.Id, copro.Nombre));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tablero compartido: fallo buscando personas en el tenant {TenantId}", copro.Id);
                }
                if (candidatos.Count >= 80) break;
            }
        }
        finally
        {
            await RestaurarAsync(original, ct);
        }

        // Regla del producto: por ahora solo USUARIOS DEL SISTEMA (personas con cuenta/login)
        // pueden trabajar en un tablero. El directorio trae residentes y terceros sin cuenta;
        // aqui se filtran contra la tabla global de usuarios.
        if (candidatos.Count == 0) return candidatos;
        var candidatoIds = candidatos.Select(c => c.Id).ToList();
        var conCuenta = await _db.Users.AsNoTracking()
            .Where(u => u.PersonaId != null && candidatoIds.Contains(u.PersonaId.Value))
            .Select(u => u.PersonaId!.Value)
            .ToListAsync(ct);
        var conCuentaSet = conCuenta.ToHashSet();
        return candidatos.Where(c => conCuentaSet.Contains(c.Id)).Take(30).ToList();
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
    /// mismo criterio de MisCopropiedadesService). Usada por el espejo/movimiento del tablero compartido.</summary>
    private Task<List<Guid>> TenantsAdministradosAsync(Guid userId, CancellationToken ct)
        => TenantsDelUsuarioAsync(userId, soloAdmin: true, ct);

    /// <summary>Copropiedades del usuario (SECURITY DEFINER get_tenants_for_persona). Con
    /// <paramref name="soloAdmin"/> = true solo las que administra; = false TODAS a las que pertenece
    /// (mismo criterio que el selector/check de la UI). El buscador de invitados usa el conjunto amplio.</summary>
    private async Task<List<Guid>> TenantsDelUsuarioAsync(Guid userId, bool soloAdmin, CancellationToken ct)
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
                if (!soloAdmin || string.Equals(rol, RolAdministrador, StringComparison.OrdinalIgnoreCase))
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
