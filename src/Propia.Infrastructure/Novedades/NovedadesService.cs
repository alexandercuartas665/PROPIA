using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Novedades;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Novedades;

/// <summary>
/// Muro de novedades generico. La logica salio de MiCopropiedadService (donde solo servia a
/// zonas comunes) para que cualquier entidad pueda tener muro sin duplicar tablas ni codigo.
/// TenantId se asigna en SaveChanges (TenantEntity) + RLS.
/// </summary>
public class NovedadesService : INovedadesService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IBlobStorage _blob;

    public NovedadesService(PropiaDbContext db, ITenantContext tenant, IBlobStorage blob)
    {
        _db = db;
        _tenant = tenant;
        _blob = blob;
    }

    public async Task<IReadOnlyList<NovedadDto>> ListarAsync(TipoEntidadNovedad tipo, Guid entidadId, Guid? personaId, CancellationToken ct)
    {
        var novedades = await _db.Novedades.AsNoTracking()
            .Where(n => n.EntidadTipo == tipo && n.EntidadId == entidadId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
        if (novedades.Count == 0) return Array.Empty<NovedadDto>();

        var ids = novedades.Select(n => n.Id).ToList();
        var comentarios = await _db.NovedadComentarios.AsNoTracking()
            .Where(c => ids.Contains(c.NovedadId)).OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var misLikes = personaId is Guid pid
            ? await _db.NovedadLikes.AsNoTracking().Where(l => ids.Contains(l.NovedadId) && l.PersonaId == pid)
                .Select(l => l.NovedadId).ToListAsync(ct)
            : new List<Guid>();

        return novedades.Select(n => new NovedadDto(
            n.Id, n.Titulo, n.Texto, _blob.ResolveUrl(n.ImagenUrl), n.AutorNombre, Iniciales(n.AutorNombre), FechaRel(n.CreatedAt),
            n.LikesCount, misLikes.Contains(n.Id),
            comentarios.Where(c => c.NovedadId == n.Id)
                .Select(c => new NovedadComentarioDto(c.Id, c.AutorNombre, Iniciales(c.AutorNombre), c.Texto, FechaRel(c.CreatedAt)))
                .ToList()
        )).ToList();
    }

    public async Task<NovedadDto?> PublicarAsync(TipoEntidadNovedad tipo, Guid entidadId, PublicarNovedadRequest req, Guid? personaId, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(req.Titulo)) return null;
        if (!await ExisteEntidadAsync(tipo, entidadId, ct)) return null;

        var autor = await ResolverNombrePersonaAsync(personaId, "Administracion", ct);
        var n = new Novedad
        {
            TenantId = tid,
            EntidadTipo = tipo,
            EntidadId = entidadId,
            Titulo = req.Titulo.Trim(),
            Texto = string.IsNullOrWhiteSpace(req.Texto) ? null : req.Texto.Trim(),
            ImagenUrl = string.IsNullOrWhiteSpace(req.ImagenUrl) ? null : req.ImagenUrl.Trim(),
            AutorNombre = autor,
            AutorPersonaId = personaId,
            LikesCount = 0
        };
        _db.Novedades.Add(n);
        await _db.SaveChangesAsync(ct);
        return new NovedadDto(n.Id, n.Titulo, n.Texto, _blob.ResolveUrl(n.ImagenUrl), n.AutorNombre, Iniciales(n.AutorNombre),
            FechaRel(n.CreatedAt), 0, false, new List<NovedadComentarioDto>());
    }

    public async Task<bool> EliminarAsync(Guid novedadId, CancellationToken ct)
    {
        var n = await _db.Novedades.FirstOrDefaultAsync(x => x.Id == novedadId, ct);
        if (n is null) return false;
        _db.NovedadComentarios.RemoveRange(_db.NovedadComentarios.Where(c => c.NovedadId == novedadId));
        _db.NovedadLikes.RemoveRange(_db.NovedadLikes.Where(l => l.NovedadId == novedadId));
        _db.Novedades.Remove(n);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<NovedadComentarioDto?> ComentarAsync(Guid novedadId, ComentarNovedadRequest req, Guid? personaId, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(req.Texto)) return null;
        if (!await _db.Novedades.AnyAsync(n => n.Id == novedadId, ct)) return null;

        var autor = await ResolverNombrePersonaAsync(personaId, "Residente", ct);
        var c = new NovedadComentario
        {
            TenantId = tid,
            NovedadId = novedadId,
            AutorNombre = autor,
            AutorPersonaId = personaId,
            Texto = req.Texto.Trim()
        };
        _db.NovedadComentarios.Add(c);
        await _db.SaveChangesAsync(ct);
        return new NovedadComentarioDto(c.Id, c.AutorNombre, Iniciales(c.AutorNombre), c.Texto, FechaRel(c.CreatedAt));
    }

    public async Task<int> ToggleLikeAsync(Guid novedadId, Guid? personaId, CancellationToken ct)
    {
        var n = await _db.Novedades.FirstOrDefaultAsync(x => x.Id == novedadId, ct);
        if (n is null) return 0;

        if (personaId is Guid pid)
        {
            var existing = await _db.NovedadLikes.FirstOrDefaultAsync(l => l.NovedadId == novedadId && l.PersonaId == pid, ct);
            if (existing is null)
            {
                if (_tenant.CurrentTenantId is Guid tid)
                    _db.NovedadLikes.Add(new NovedadLike { TenantId = tid, NovedadId = novedadId, PersonaId = pid });
                n.LikesCount += 1;
            }
            else
            {
                _db.NovedadLikes.Remove(existing);
                n.LikesCount = Math.Max(0, n.LikesCount - 1);
            }
        }
        else { n.LikesCount += 1; }

        await _db.SaveChangesAsync(ct);
        return n.LikesCount;
    }

    // ----------------------------- Helpers -----------------------------

    /// <summary>Evita muros huerfanos: la entidad dueña tiene que existir en este tenant.</summary>
    private Task<bool> ExisteEntidadAsync(TipoEntidadNovedad tipo, Guid entidadId, CancellationToken ct) => tipo switch
    {
        TipoEntidadNovedad.ZonaComun => _db.ZonasComunes.AnyAsync(z => z.Id == entidadId, ct),
        TipoEntidadNovedad.EquipoActivo => _db.EquiposActivos.AnyAsync(e => e.Id == entidadId, ct),
        _ => Task.FromResult(false)
    };

    private async Task<string> ResolverNombrePersonaAsync(Guid? personaId, string fallback, CancellationToken ct)
    {
        if (personaId is not Guid pid) return fallback;
        var n = await _db.Personas.AsNoTracking().Where(p => p.Id == pid)
            .Select(p => p.Nombres + " " + p.Apellidos).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(n) ? fallback : n.Trim();
    }

    private static string Iniciales(string? nombre)
    {
        var parts = (nombre ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        return ("" + parts[0][0] + parts[1][0]).ToUpperInvariant();
    }

    private static string FechaRel(DateTimeOffset dt)
    {
        var d = DateTimeOffset.UtcNow - dt;
        if (d.TotalMinutes < 1) return "ahora";
        if (d.TotalMinutes < 60) return "hace " + (int)d.TotalMinutes + " min";
        if (d.TotalHours < 24) return "hace " + (int)d.TotalHours + " h";
        if (d.TotalDays < 30) return "hace " + (int)d.TotalDays + " d";
        return dt.ToLocalTime().ToString("dd MMM yyyy");
    }
}
