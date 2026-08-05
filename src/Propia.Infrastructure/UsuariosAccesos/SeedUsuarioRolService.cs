using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.UsuariosAccesos;

/// <summary>
/// Implementa la siembra automatica de usuario+directorio por rol semilla (ver
/// <see cref="ISeedUsuarioRolService"/>). Un rol Personalizado del tenant puede declarar facetas
/// semilla (RolUnidadPersona) en <c>Rol.FacetasSemilla</c> (CSV de valores int). Al sembrar:
///  - se asegura el UsuarioTenant (rol) de la persona en el tenant activo,
///  - se crea el ApplicationUser (login) si la persona tiene email y aun no hay cuenta
///    (sin password: se activa por invitacion/reset).
/// El contacto de Directorio ya lo asegura el flujo de distribucion antes de sembrar.
/// </summary>
public sealed class SeedUsuarioRolService : ISeedUsuarioRolService
{
    private readonly PropiaDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantContext _tenant;

    public SeedUsuarioRolService(PropiaDbContext db, UserManager<ApplicationUser> userManager, ITenantContext tenant)
    {
        _db = db;
        _userManager = userManager;
        _tenant = tenant;
    }

    public async Task SembrarPorFacetaAsync(Guid personaId, RolUnidadPersona faceta, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return;

        var rolesTenant = await RolesSemillaAsync(ct);
        var match = rolesTenant.FirstOrDefault(z => ParseFacetas(z.Facetas).Contains((int)faceta));
        if (match.Rol is null) return;

        await EnsureUsuarioAsync(personaId, match.Rol, tenantId.Value, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> BackfillFacetasAsync(IEnumerable<int> facetasInt, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return 0;
        var set = facetasInt.ToHashSet();
        if (set.Count == 0) return 0;

        var rolesTenant = await RolesSemillaAsync(ct);
        if (rolesTenant.Count == 0) return 0;

        // Personas ya vinculadas a alguna unidad con una faceta del conjunto (una vez por persona+faceta).
        var links = await _db.UnidadPersonas
            .Where(up => up.PersonaId != null && up.EntidadTipo == EntidadDirectorio.Persona)
            .Select(up => new { PersonaId = up.PersonaId!.Value, up.Rol })
            .Distinct()
            .ToListAsync(ct);

        var sembradas = 0;
        foreach (var l in links)
        {
            if (!set.Contains((int)l.Rol)) continue;
            var match = rolesTenant.FirstOrDefault(z => ParseFacetas(z.Facetas).Contains((int)l.Rol));
            if (match.Rol is null) continue;
            if (await EnsureUsuarioAsync(l.PersonaId, match.Rol, tenantId.Value, ct)) sembradas++;
        }
        await _db.SaveChangesAsync(ct);
        return sembradas;
    }

    // Roles semilla del tenant activo, resueltos desde el override RolSemillaTenant (aplica a
    // cualquier tipo de rol). El query filter de RolSemillaTenant ya limita a la copropiedad activa.
    private async Task<List<(Rol Rol, string Facetas)>> RolesSemillaAsync(CancellationToken ct)
    {
        var rows = await _db.RolesSemillaTenant
            .Where(x => x.FacetasSemilla != null)
            .Join(_db.RolesCopropiedad, x => x.RolId, r => r.Id,
                  (x, r) => new { Rol = r, x.FacetasSemilla })
            .Where(z => z.Rol.Activo)
            .ToListAsync(ct);
        return rows.Select(z => (z.Rol, z.FacetasSemilla!)).ToList();
    }

    /// <summary>Asegura UsuarioTenant + ApplicationUser. Devuelve true si creo/cambio algo.</summary>
    private async Task<bool> EnsureUsuarioAsync(Guid personaId, Rol rol, Guid tenantId, CancellationToken ct)
    {
        var cambio = false;

        var ut = await _db.UsuariosTenant
            .FirstOrDefaultAsync(u => u.PersonaId == personaId && u.TenantId == tenantId, ct);
        if (ut is null)
        {
            _db.UsuariosTenant.Add(new UsuarioTenant
            {
                TenantId = tenantId,
                PersonaId = personaId,
                RolId = rol.Id,
                Rol = rol.Nombre,
                Estado = EstadoUsuarioTenant.Activo
            });
            cambio = true;
        }
        else if (ut.RolId is null)
        {
            // Solo completa si no tenia rol; no pisa un rol ya asignado a proposito.
            ut.RolId = rol.Id;
            ut.Rol = rol.Nombre;
            cambio = true;
        }

        // Login (ApplicationUser) si la persona tiene email y aun no hay cuenta.
        var persona = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personaId, ct);
        var email = persona?.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                // Sin password: la cuenta existe (login) pero se activa via invitacion/reset.
                var appUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    PersonaId = personaId,
                    EmailConfirmed = false
                };
                var res = await _userManager.CreateAsync(appUser);
                if (res.Succeeded) cambio = true;
            }
            else if (existing.PersonaId is null)
            {
                existing.PersonaId = personaId;
                await _userManager.UpdateAsync(existing);
                cambio = true;
            }
        }

        return cambio;
    }

    private static IEnumerable<int> ParseFacetas(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) yield break;
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, out var n)) yield return n;
    }
}
