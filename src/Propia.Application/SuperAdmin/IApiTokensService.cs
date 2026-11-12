namespace Propia.Application.SuperAdmin;

/// <summary>Token de API tal como se lista (nunca trae el valor completo).</summary>
public sealed record ApiTokenDto(Guid Id, string Nombre, string Prefijo, Guid CreadoPorId, string CreadoPorEmail,
    DateTimeOffset CreadoAt, DateTimeOffset? ExpiraAt, DateTimeOffset? RevocadoAt, DateTimeOffset? UltimoUsoAt, string? UltimoUsoIp,
    string Estado);

/// <summary>DiasVigencia null = no vence.</summary>
public sealed record CrearApiTokenRequest(string Nombre, int? DiasVigencia);

/// <summary>Respuesta de crear: el Token completo solo viaja aqui, una vez.</summary>
public sealed record ApiTokenCreadoDto(ApiTokenDto Info, string Token);

/// <summary>Resultado de validar un token presentado en una peticion.</summary>
public sealed record ApiTokenValidado(Guid TokenId, string Nombre, Guid UsuarioId, string Email, Domain.Enums.RolSuperAdmin Rol);

/// <summary>Tokens de API de la Consola A&amp;D (Super Admin).</summary>
public interface IApiTokensService
{
    Task<IReadOnlyList<ApiTokenDto>> ListarAsync(CancellationToken ct);
    /// <summary>Crea el token. InvalidOperationException si falta el nombre o la vigencia no es valida.</summary>
    Task<ApiTokenCreadoDto> CrearAsync(CrearApiTokenRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
    /// <summary>false si no existe; revocar uno ya revocado es idempotente.</summary>
    Task<bool> RevocarAsync(Guid id, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
    /// <summary>
    /// Valida el token presentado (vigente, no revocado, Super Admin activo) y registra el uso. null = no valido.
    /// Si <paramref name="registrarAccion"/> no es null se deja en el log del Super Admin (escrituras).
    /// </summary>
    Task<ApiTokenValidado?> ValidarAsync(string token, string? ip, string? registrarAccion, CancellationToken ct);
}
