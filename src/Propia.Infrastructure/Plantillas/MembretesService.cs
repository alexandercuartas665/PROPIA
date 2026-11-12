using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Plantillas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Documents;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Plantillas;

/// <summary>
/// Encabezados y pies de pagina (membretes) de la copropiedad activa (query filter + RLS). La primera vez que se
/// consultan, la copropiedad nace con "Institucional" (encabezado y pie, predeterminados), armados con su config de
/// membrete (MembreteTokens). Un solo predeterminado por tipo; el predeterminado no se elimina; al eliminar uno
/// fijado en plantillas, esas plantillas pasan a Predeterminado.
/// </summary>
public sealed class MembretesService : IMembretesService
{
    public const string NombreSemilla = "Institucional";

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;

    public MembretesService(PropiaDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<MembretesDto> ListarAsync(CancellationToken ct)
    {
        var tenant = await AsegurarSemillaAsync(ct);
        var ms = await _db.MembretesDocumento.AsNoTracking().OrderBy(m => m.Tipo).ThenByDescending(m => m.Predeterminado).ThenBy(m => m.Nombre).ToListAsync(ct);
        var ps = await _db.PlantillasDocumento.AsNoTracking()
            .Select(p => new { p.EncabezadoModo, p.EncabezadoId, p.PieModo, p.PieId }).ToListAsync(ct);
        int Usos(MembreteDocumento m) => m.Tipo == TipoMembrete.Encabezado
            ? ps.Count(p => (p.EncabezadoModo == ModoMembrete.Fijo && p.EncabezadoId == m.Id) || (m.Predeterminado && p.EncabezadoModo == ModoMembrete.Predeterminado))
            : ps.Count(p => (p.PieModo == ModoMembrete.Fijo && p.PieId == m.Id) || (m.Predeterminado && p.PieModo == ModoMembrete.Predeterminado));
        var items = ms.Select(m => ToDto(m, Usos(m))).ToList();
        var logo = tenant is null ? "" : MembreteTokens.LogoHtml(tenant);
        var ejemplo = MembreteTokens.DocumentoDatosHtml("Respuesta PQRSD", "Radicado", "PQRSD-2026-0042",
            (string.IsNullOrWhiteSpace(tenant?.Ciudad) ? "" : tenant!.Ciudad!.Trim() + ", ") + MembreteTokens.FechaLarga(DateTimeOffset.Now),
            tenant is null ? MembreteTokens.AccentDefault : MembreteTokens.Accent(tenant));
        return new MembretesDto(items, logo, ejemplo);
    }

    public async Task<MembreteDto> CrearAsync(GuardarMembreteRequest req, CancellationToken ct)
    {
        if (req.Tipo is not { } tipo || !Enum.IsDefined(tipo)) throw new InvalidOperationException("Indica si es encabezado o pie de página.");
        var nombre = Nombre(req.Nombre);
        await AsegurarSemillaAsync(ct);
        var m = new MembreteDocumento { Tipo = tipo, Nombre = nombre, Html = Common.HtmlSanitization.Clean(req.Html) ?? "" };   // S-19
        _db.MembretesDocumento.Add(m);
        if (req.Predeterminado == true) await HacerPredeterminadoAsync(m, ct);
        else await _db.SaveChangesAsync(ct);
        return ToDto(m, 0);
    }

    public async Task<MembreteDto?> ActualizarAsync(Guid id, GuardarMembreteRequest req, CancellationToken ct)
    {
        var m = await _db.MembretesDocumento.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return null;
        // PUT MERGE: solo lo enviado. El tipo no cambia (las plantillas lo tienen como encabezado o como pie).
        if (req.Nombre is not null) m.Nombre = Nombre(req.Nombre);
        if (req.Html is not null) m.Html = Common.HtmlSanitization.Clean(req.Html) ?? "";   // S-19
        if (req.Predeterminado == true && !m.Predeterminado) await HacerPredeterminadoAsync(m, ct);
        else await _db.SaveChangesAsync(ct);
        return ToDto(m, 0);
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct)
    {
        var m = await _db.MembretesDocumento.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return false;
        if (m.Predeterminado) throw new InvalidOperationException("Es el predeterminado: marca otro como predeterminado antes de eliminarlo.");
        // Las plantillas que lo tenian fijo pasan al predeterminado.
        if (m.Tipo == TipoMembrete.Encabezado)
            await _db.PlantillasDocumento.Where(p => p.EncabezadoModo == ModoMembrete.Fijo && p.EncabezadoId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.EncabezadoModo, ModoMembrete.Predeterminado).SetProperty(p => p.EncabezadoId, (Guid?)null), ct);
        else
            await _db.PlantillasDocumento.Where(p => p.PieModo == ModoMembrete.Fijo && p.PieId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.PieModo, ModoMembrete.Predeterminado).SetProperty(p => p.PieId, (Guid?)null), ct);
        _db.MembretesDocumento.Remove(m);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(string? Encabezado, string? Pie)> ParaDocumentoAsync(Guid? plantillaId, IReadOnlyDictionary<string, string>? datos, CancellationToken ct)
    {
        var tenant = await AsegurarSemillaAsync(ct);
        if (tenant is null) return (null, null);
        PlantillaDocumento? p = plantillaId is { } pid ? await _db.PlantillasDocumento.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct) : null;
        var ms = await _db.MembretesDocumento.AsNoTracking().ToListAsync(ct);
        MembreteDocumento? Elegido(TipoMembrete tipo, ModoMembrete modo, Guid? id)
            => modo == ModoMembrete.Ninguno ? null
             : (modo == ModoMembrete.Fijo ? ms.FirstOrDefault(m => m.Id == id && m.Tipo == tipo) : null)
               ?? ms.FirstOrDefault(m => m.Tipo == tipo && m.Predeterminado);
        var enc = Elegido(TipoMembrete.Encabezado, p?.EncabezadoModo ?? ModoMembrete.Predeterminado, p?.EncabezadoId);
        var pie = Elegido(TipoMembrete.Pie, p?.PieModo ?? ModoMembrete.Predeterminado, p?.PieId);
        var mapa = MembreteTokens.DatosCopropiedad(tenant, datos is not null && datos.TryGetValue("administrador.nombre", out var adm) ? adm : null);
        if (datos is not null)
            foreach (var (k, v) in datos)
                if (k != "administrador.nombre" || string.IsNullOrWhiteSpace(tenant.MembreteFirmanteNombre))
                    mapa[k] = System.Net.WebUtility.HtmlEncode(v ?? "");
        return (enc is null ? null : MembreteTokens.Resolver(enc.Html, mapa), pie is null ? null : MembreteTokens.Resolver(pie.Html, mapa));
    }

    // "Nace con" Institucional: si la copropiedad no tiene NINGUN membrete, crea encabezado y pie predeterminados
    // con su config de membrete. Idempotente. Devuelve el tenant activo (null fuera de una copropiedad).
    private async Task<Tenant?> AsegurarSemillaAsync(CancellationToken ct)
    {
        if (_tenantContext.CurrentTenantId is not Guid tid) return null;
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid, ct);
        if (tenant is null) return null;
        if (await _db.MembretesDocumento.AnyAsync(ct)) return tenant;
        _db.MembretesDocumento.Add(new MembreteDocumento { TenantId = tid, Tipo = TipoMembrete.Encabezado, Nombre = NombreSemilla, Predeterminado = true, Html = MembreteTokens.EncabezadoInstitucional(tenant) });
        _db.MembretesDocumento.Add(new MembreteDocumento { TenantId = tid, Tipo = TipoMembrete.Pie, Nombre = NombreSemilla, Predeterminado = true, Html = MembreteTokens.PieInstitucional(tenant) });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Otra peticion sembro a la vez (indice unico del predeterminado): se descarta esta siembra.
            foreach (var e in _db.ChangeTracker.Entries<MembreteDocumento>().Where(e => e.State == EntityState.Added).ToList()) e.State = EntityState.Detached;
        }
        return tenant;
    }

    // Vuelve predeterminado a m y quita la marca al anterior de su tipo. Dos pasos: el indice unico filtrado no
    // admite dos predeterminados a la vez dentro del mismo SaveChanges.
    private async Task HacerPredeterminadoAsync(MembreteDocumento m, CancellationToken ct)
    {
        var anteriores = await _db.MembretesDocumento.Where(x => x.Tipo == m.Tipo && x.Predeterminado && x.Id != m.Id).ToListAsync(ct);
        foreach (var a in anteriores) a.Predeterminado = false;
        await _db.SaveChangesAsync(ct);
        m.Predeterminado = true;
        await _db.SaveChangesAsync(ct);
    }

    private static string Nombre(string? nombre)
    {
        var n = (nombre ?? "").Trim();
        if (n.Length == 0) throw new InvalidOperationException("Ponle un nombre (ej.: Navidad).");
        return n.Length > 120 ? n[..120] : n;
    }

    private static MembreteDto ToDto(MembreteDocumento m, int usos)
        => new(m.Id, m.Tipo, m.Nombre, m.Html, m.Predeterminado, usos, m.UpdatedAt ?? m.CreatedAt);
}
