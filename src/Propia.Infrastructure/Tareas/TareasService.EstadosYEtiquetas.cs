using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Tareas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Tareas;

// Particion de TareasService por area (clase parcial: comparte _db/_tenantContext/_http/_noti
// y GetUsuarioActualId del archivo principal). Mismo comportamiento.
public partial class TareasService
{
    // Seed lazy de estados, estados (columnas) y etiquetas.
    // ===================== Seed lazy de estados =====================

    // Fase 2: asegura las 4 prioridades base (independiente de los estados). Idempotente: no re-siembra si
    // ya hay prioridades (las existentes las siembra la migracion por tablero; las nuevas, aqui/SembrarPrioridades).
    private async Task AsegurarPrioridadesBaseAsync(CancellationToken ct)
    {
        if (await _db.TareasPrioridades.AnyAsync(ct)) return;
        foreach (var (enumV, nombre, orden, color) in PrioridadTareaBase.Base)
            _db.TareasPrioridades.Add(new TareaPrioridad
            {
                Nombre = nombre, Orden = orden, Color = color, EsBase = true, BaseValor = (int)enumV, Activo = true
            });
        await _db.SaveChangesAsync(ct);
    }

    private async Task AsegurarEstadosBaseAsync(CancellationToken ct)
    {
        await AsegurarPrioridadesBaseAsync(ct);
        var hay = await _db.TareasEstados.AnyAsync(ct);
        if (hay) return;
        foreach (var (nombre, orden, esTerminal) in EstadoTareaBase.Base)
        {
            _db.TareasEstados.Add(new TareaEstado
            {
                Nombre = nombre,
                Orden = orden,
                EsTerminal = esTerminal,
                EsBase = true,
                Activo = true,
                Color = nombre switch
                {
                    EstadoTareaBase.Pendiente => "#94a3b8",
                    EstadoTareaBase.EnProgreso => "#3b82f6",
                    EstadoTareaBase.EnRevision => "#f59e0b",
                    EstadoTareaBase.Bloqueada => "#ef4444",
                    EstadoTareaBase.Completada => "#22c55e",
                    EstadoTareaBase.Cancelada => "#6b7280",
                    _ => null
                }
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Estados =====================

    public async Task<IReadOnlyList<EstadoTareaDto>> ListarEstadosAsync(CancellationToken ct)
    {
        await AsegurarEstadosBaseAsync(ct);
        return await _db.TareasEstados.AsNoTracking()
            .OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new EstadoTareaDto(e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal, e.EsBase, e.Activo))
            .ToListAsync(ct);
    }

    private static readonly string[] _estadoPalette = { "#6D4FE3", "#0EA5E9", "#EC4899", "#14B8A6", "#A855F7", "#F97316" };

    public async Task<EstadoTareaDto> CrearEstadoAsync(CrearEstadoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre) || req.Nombre.Trim().Length < 2)
            throw new InvalidOperationException("Nombre minimo 2 caracteres.");
        var nom = req.Nombre.Trim();
        var tableroId = req.TableroId ?? await AsegurarTableroDefaultAsync(ct);
        if (await _db.TareasEstados.AnyAsync(e => e.TableroId == tableroId && e.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe un estado con este nombre en el tablero.");

        // Insertar el nuevo estado justo antes de los estados terminales (Completada/Cancelada).
        var enBoard = await _db.TareasEstados.Where(e => e.TableroId == tableroId).ToListAsync(ct);
        var terminales = enBoard.Where(e => e.EsTerminal).ToList();
        int orden;
        if (req.Orden > 0) orden = req.Orden;
        else if (terminales.Count > 0)
        {
            orden = terminales.Min(e => e.Orden);
            foreach (var term in terminales) { term.Orden += 1; term.UpdatedAt = DateTimeOffset.UtcNow; }
        }
        else orden = (enBoard.Count > 0 ? enBoard.Max(e => e.Orden) : 0) + 1;

        var color = string.IsNullOrWhiteSpace(req.Color)
            ? _estadoPalette[enBoard.Count(e => !e.EsBase) % _estadoPalette.Length]
            : req.Color;
        var e = new TareaEstado { TableroId = tableroId, Nombre = nom, Color = color, Orden = orden, EsTerminal = false, EsBase = false, Activo = true };
        _db.TareasEstados.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EstadoTareaDto(e.Id, e.Nombre, e.Color, e.Orden, false, false, true);
    }

    public async Task<bool> ActualizarEstadoAsync(Guid id, ActualizarEstadoRequest req, CancellationToken ct)
    {
        var e = await _db.TareasEstados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        if (e.EsTerminal) throw new InvalidOperationException("Los estados terminales no son editables.");
        var nom = req.Nombre.Trim();
        if (e.Nombre != nom && await _db.TareasEstados.AnyAsync(x => x.Nombre == nom && x.Id != id, ct))
            throw new InvalidOperationException("Ya existe un estado con este nombre.");
        e.Nombre = nom;
        e.Color = req.Color;
        e.Orden = req.Orden;
        e.Activo = req.Activo;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarEstadoAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.TareasEstados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        if (e.EsTerminal || e.EsBase) throw new InvalidOperationException("Estados base o terminales no eliminables.");
        if (await _db.Tareas.AnyAsync(t => t.EstadoId == id && !t.Eliminada, ct))
            throw new InvalidOperationException("No puedes eliminar un estado con tareas asociadas. Reasignalas primero.");
        _db.TareasEstados.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Prioridades (Fase 2: lista configurable por tablero) =====================

    public async Task<IReadOnlyList<PrioridadTareaDto>> ListarPrioridadesAsync(Guid? tableroId, CancellationToken ct)
    {
        await AsegurarPrioridadesBaseAsync(ct);
        var q = _db.TareasPrioridades.AsNoTracking().AsQueryable();
        if (tableroId.HasValue) q = q.Where(p => p.TableroId == tableroId.Value);
        return await q.OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new PrioridadTareaDto(p.Id, p.Nombre, p.Color, p.Orden, p.EsBase, p.Activo))
            .ToListAsync(ct);
    }

    private static readonly string[] _prioridadPalette = { "#6D4FE3", "#0EA5E9", "#EC4899", "#14B8A6", "#A855F7", "#F97316" };

    public async Task<PrioridadTareaDto> CrearPrioridadAsync(CrearPrioridadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre) || req.Nombre.Trim().Length < 2)
            throw new InvalidOperationException("Nombre minimo 2 caracteres.");
        var nom = req.Nombre.Trim();
        var tableroId = req.TableroId ?? await AsegurarTableroDefaultAsync(ct);
        if (await _db.TareasPrioridades.AnyAsync(p => p.TableroId == tableroId && p.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe una prioridad con este nombre en el tablero.");
        var enBoard = await _db.TareasPrioridades.Where(p => p.TableroId == tableroId).ToListAsync(ct);
        int orden = req.Orden > 0 ? req.Orden : (enBoard.Count > 0 ? enBoard.Max(p => p.Orden) : 0) + 1;
        var color = string.IsNullOrWhiteSpace(req.Color)
            ? _prioridadPalette[enBoard.Count(p => !p.EsBase) % _prioridadPalette.Length]
            : req.Color;
        var p = new TareaPrioridad { TableroId = tableroId, Nombre = nom, Color = color, Orden = orden, EsBase = false, BaseValor = null, Activo = true };
        _db.TareasPrioridades.Add(p);
        await _db.SaveChangesAsync(ct);
        return new PrioridadTareaDto(p.Id, p.Nombre, p.Color, p.Orden, false, true);
    }

    public async Task<bool> ActualizarPrioridadAsync(Guid id, ActualizarPrioridadRequest req, CancellationToken ct)
    {
        var p = await _db.TareasPrioridades.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        var nom = req.Nombre.Trim();
        if (string.IsNullOrWhiteSpace(nom) || nom.Length < 2) throw new InvalidOperationException("Nombre minimo 2 caracteres.");
        if (p.Nombre != nom && await _db.TareasPrioridades.AnyAsync(x => x.TableroId == p.TableroId && x.Nombre == nom && x.Id != id, ct))
            throw new InvalidOperationException("Ya existe una prioridad con este nombre.");
        p.Nombre = nom;
        p.Color = req.Color;
        p.Orden = req.Orden;
        p.Activo = req.Activo;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarPrioridadAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.TareasPrioridades.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (p.EsBase) throw new InvalidOperationException("Las prioridades base (Urgente/Alta/Normal/Baja) no se pueden eliminar.");
        // Reasigna las tareas que la usan a la prioridad base Normal del mismo tablero (o null si no hay).
        var fallback = await _db.TareasPrioridades
            .Where(x => x.TableroId == p.TableroId && x.EsBase && x.BaseValor == (int)PrioridadTarea.Normal)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        await _db.Tareas.Where(t => t.PrioridadId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.PrioridadId, fallback), ct);
        _db.TareasPrioridades.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Resuelve la opcion de prioridad al crear/editar: si viene PrioridadId valido del tablero, ese; si no,
    // la prioridad BASE del tablero que corresponde al enum (creaciones desde modulos/jobs por enum).
    // Devuelve (Id de opcion o null, enum "equivalente" para mantener Tarea.Prioridad en sync/respaldo).
    private async Task<(Guid? Id, PrioridadTarea Enum)> ResolverPrioridadAsync(Guid? tableroId, Guid? prioridadId, PrioridadTarea enumV, CancellationToken ct)
    {
        if (prioridadId is Guid pid)
        {
            var op = await _db.TareasPrioridades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct);
            if (op is not null && (tableroId is null || op.TableroId == tableroId))
                return (op.Id, op.EsBase && op.BaseValor is int bv ? (PrioridadTarea)bv : enumV);
        }
        if (tableroId is Guid tid)
        {
            var baseOp = await _db.TareasPrioridades.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TableroId == tid && x.EsBase && x.BaseValor == (int)enumV, ct);
            if (baseOp is not null) return (baseOp.Id, enumV);
        }
        return (null, enumV);
    }

    // ===================== Etiquetas =====================

    public async Task<IReadOnlyList<EtiquetaTareaDto>> ListarEtiquetasAsync(Guid? tableroId, CancellationToken ct)
    {
        var q = _db.TareaEtiquetas.AsNoTracking().AsQueryable();
        // Etiquetas EXCLUSIVAS del tablero (no hay globales). Sin tablero: todas (admin/legacy).
        if (tableroId.HasValue) q = q.Where(e => e.TableroId == tableroId.Value);
        var rows = await q.OrderBy(e => e.Nombre).ToListAsync(ct);
        var counts = await _db.TareaEtiquetaAsignaciones.AsNoTracking()
            .GroupBy(a => a.EtiquetaId)
            .Select(g => new { g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cant, ct);
        return rows.Select(e => new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, e.Activo, counts.GetValueOrDefault(e.Id, 0), e.TableroId)).ToList();
    }

    public async Task<EtiquetaTareaDto> CrearEtiquetaAsync(CrearEtiquetaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        if (req.TableroId is null) throw new InvalidOperationException("La etiqueta debe pertenecer a un tablero.");
        var nom = req.Nombre.Trim();
        // Unicidad dentro del mismo tablero.
        if (await _db.TareaEtiquetas.AnyAsync(e => e.Nombre == nom && e.TableroId == req.TableroId, ct))
            throw new InvalidOperationException("Ya existe una etiqueta con este nombre en este tablero.");
        var e = new TareaEtiqueta { Nombre = nom, Color = req.Color, Activo = true, TableroId = req.TableroId };
        _db.TareaEtiquetas.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, true, 0, e.TableroId);
    }

    public async Task<bool> ActualizarEtiquetaAsync(Guid id, ActualizarEtiquetaRequest req, CancellationToken ct)
    {
        var e = await _db.TareaEtiquetas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        e.Nombre = req.Nombre.Trim();
        e.Color = req.Color;
        e.Activo = req.Activo;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarEtiquetaAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.TareaEtiquetas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        _db.TareaEtiquetas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

}
