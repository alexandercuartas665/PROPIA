using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Auth;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PropiaDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        PropiaDbContext db,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null) return null;

        var ok = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!ok) return null;

        var tenants = await LoadAvailableTenantsAsync(user.Id, ct);
        // Si solo hay una copropiedad vinculada, se selecciona auto. Caso comun para residentes.
        Guid? activeTenant = tenants.Count == 1 ? tenants[0].TenantId : null;

        var (token, expires) = _tokenService.IssueAccessToken(user, activeTenant);

        // Marcamos ultimo acceso del UsuarioTenant si hay tenant activo
        if (activeTenant.HasValue)
        {
            await UpdateUltimoAccesoAsync(user.PersonaId, activeTenant.Value, ct);
        }

        return new LoginResponse(token, expires, user.Id, user.Email!, activeTenant, tenants);
    }

    public async Task<MeResponse?> GetMeAsync(Guid userId, Guid? activeTenantId, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Persona)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;

        var tenants = await LoadAvailableTenantsAsync(userId, ct);
        return new MeResponse(
            user.Id,
            user.Email!,
            user.PersonaId,
            user.Persona?.Nombres,
            user.Persona?.Apellidos,
            activeTenantId,
            tenants);
    }

    public async Task<LoginResponse?> SwitchTenantAsync(Guid userId, Guid newTenantId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;

        var tenants = await LoadAvailableTenantsAsync(userId, ct);
        var valid = tenants.Any(t => t.TenantId == newTenantId);
        if (!valid) return null;  // El usuario no tiene acceso a ese tenant - rechazar

        var (token, expires) = _tokenService.IssueAccessToken(user, newTenantId);
        await UpdateUltimoAccesoAsync(user.PersonaId, newTenantId, ct);

        return new LoginResponse(token, expires, user.Id, user.Email!, newTenantId, tenants);
    }

    // ---------- Helpers ----------

    /// <summary>
    /// Lista las copropiedades a las que tiene acceso la persona vinculada al usuario.
    /// Usa la funcion SQL SECURITY DEFINER `get_tenants_for_persona` para bypassar RLS
    /// (necesario porque /connect/token corre antes de tener tenant activo).
    /// La seguridad la garantiza el filtro por persona_id del usuario autenticado.
    /// </summary>
    private async Task<List<TenantInfo>> LoadAvailableTenantsAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.PersonaId is null) return new();

        var personaId = user.PersonaId.Value;
        var rows = new List<TenantInfo>();
        var conn = _db.Database.GetDbConnection();
        var openedHere = conn.State != System.Data.ConnectionState.Open;
        if (openedHere) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT tenant_id, nombre, rol FROM get_tenants_for_persona(@p_persona_id)";
            var p = cmd.CreateParameter();
            p.ParameterName = "@p_persona_id";
            p.Value = personaId;
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add(new TenantInfo(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
            }
        }
        finally
        {
            if (openedHere) await conn.CloseAsync();
        }
        return rows;
    }

    private async Task UpdateUltimoAccesoAsync(Guid? personaId, Guid tenantId, CancellationToken ct)
    {
        if (personaId is null) return;
        // Actualizamos sin pasar por el HasQueryFilter (no hay tenant activo aun en el scope auth)
        var vinculo = await _db.UsuariosTenant.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.PersonaId == personaId && x.TenantId == tenantId, ct);
        if (vinculo is null) return;
        vinculo.UltimoAcceso = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
