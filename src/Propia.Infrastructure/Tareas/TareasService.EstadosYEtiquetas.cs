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

    private async Task AsegurarEstadosBaseAsync(CancellationToken ct)
    {
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
