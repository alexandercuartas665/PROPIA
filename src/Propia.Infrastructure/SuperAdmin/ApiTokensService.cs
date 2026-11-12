using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Tokens de API de la Consola A&amp;D. Formato "pak_" + 43 caracteres base64url (32 bytes aleatorios). Se guarda
/// solo su sha256; el valor completo se devuelve una vez al crearlo. Cada token actua como el Super Admin que lo
/// creo: deja de servir si se revoca, si vence o si ese Super Admin se desactiva. Crear, revocar y cada escritura
/// hecha con un token quedan en el log del Super Admin (super_admin_logs).
/// </summary>
public sealed class ApiTokensService : IApiTokensService
{
    public const string PrefijoToken = "pak_";
    private const int LargoPrefijoVisible = 8;
    private static readonly TimeSpan IntervaloUltimoUso = TimeSpan.FromMinutes(1);

    private readonly PropiaDbContext _db;

    public ApiTokensService(PropiaDbContext db) => _db = db;

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Nuevo()
        => PrefijoToken + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Estado(SuperAdminApiToken t, bool usuarioActivo)
        => t.RevocadoAt is not null ? "Revocado"
         : t.ExpiraAt is { } e && e <= DateTimeOffset.UtcNow ? "Vencido"
         : !usuarioActivo ? "Inactivo"
         : "Activo";

    public async Task<IReadOnlyList<ApiTokenDto>> ListarAsync(CancellationToken ct)
    {
        var filas = await _db.SuperAdminApiTokens.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { T = t, Email = t.SuperAdminUsuario != null ? t.SuperAdminUsuario.Email : "", Activo = t.SuperAdminUsuario != null && t.SuperAdminUsuario.Activo })
            .ToListAsync(ct);
        return filas.Select(f => ToDto(f.T, f.Email, f.Activo)).ToList();
    }

    public async Task<ApiTokenCreadoDto> CrearAsync(CrearApiTokenRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new InvalidOperationException("Ponle un nombre al token (ej.: Claude Code, script de semillas).");
        if (nombre.Length > 120) nombre = nombre[..120];
        if (req.DiasVigencia is { } d && (d < 1 || d > 3650)) throw new InvalidOperationException("La vigencia va de 1 a 3650 días (o sin vencimiento).");
        var usuario = await _db.SuperAdminUsuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId && u.Activo, ct)
            ?? throw new InvalidOperationException("Solo un Super Admin activo puede crear tokens.");

        var token = Nuevo();
        var t = new SuperAdminApiToken
        {
            Nombre = nombre,
            Prefijo = token[..LargoPrefijoVisible],
            TokenHash = Hash(token),
            SuperAdminUsuarioId = usuario.Id,
            ExpiraAt = req.DiasVigencia is { } dias ? DateTimeOffset.UtcNow.AddDays(dias) : null,
        };
        _db.SuperAdminApiTokens.Add(t);
        _db.SuperAdminLogs.Add(Log(actorId, actorEmail, "API_TOKEN_CREAR", t.Id, $"Nombre={nombre}; Vence={t.ExpiraAt?.ToString("yyyy-MM-dd") ?? "nunca"}", ip));
        await _db.SaveChangesAsync(ct);
        return new ApiTokenCreadoDto(ToDto(t, usuario.Email, true), token);
    }

    public async Task<bool> RevocarAsync(Guid id, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var t = await _db.SuperAdminApiTokens.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        if (t.RevocadoAt is null)
        {
            t.RevocadoAt = DateTimeOffset.UtcNow;
            _db.SuperAdminLogs.Add(Log(actorId, actorEmail, "API_TOKEN_REVOCAR", t.Id, $"Nombre={t.Nombre}", ip));
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task<ApiTokenValidado?> ValidarAsync(string token, string? ip, string? registrarAccion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(PrefijoToken, StringComparison.Ordinal)) return null;
        var hash = Hash(token.Trim());
        var ahora = DateTimeOffset.UtcNow;
        var t = await _db.SuperAdminApiTokens.Include(x => x.SuperAdminUsuario).FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (t is null || t.RevocadoAt is not null || (t.ExpiraAt is { } e && e <= ahora)) return null;
        var u = t.SuperAdminUsuario;
        if (u is null || !u.Activo) return null;

        var guardar = false;
        if (t.UltimoUsoAt is null || ahora - t.UltimoUsoAt.Value >= IntervaloUltimoUso || t.UltimoUsoIp != ip)
        {
            t.UltimoUsoAt = ahora; t.UltimoUsoIp = ip; guardar = true;
        }
        if (registrarAccion is not null)
        {
            _db.SuperAdminLogs.Add(Log(u.Id, u.Email, "API_TOKEN_USO", t.Id, $"Token={t.Nombre}; {registrarAccion}", ip));
            guardar = true;
        }
        if (guardar) await _db.SaveChangesAsync(ct);
        return new ApiTokenValidado(t.Id, t.Nombre, u.Id, u.Email, u.Rol);
    }

    private static ApiTokenDto ToDto(SuperAdminApiToken t, string email, bool usuarioActivo)
        => new(t.Id, t.Nombre, t.Prefijo, t.SuperAdminUsuarioId, email, t.CreatedAt, t.ExpiraAt, t.RevocadoAt, t.UltimoUsoAt, t.UltimoUsoIp,
            Estado(t, usuarioActivo));

    private static SuperAdminLog Log(Guid actorId, string actorEmail, string accion, Guid tokenId, string detalle, string? ip)
        => new() { ActorId = actorId, ActorEmail = actorEmail, Accion = accion, EntidadAfectada = $"ApiToken:{tokenId}", Justificacion = detalle, Ip = ip };
}
