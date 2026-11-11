using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Propia.Application.Asambleas;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Token del enlace self-service de Asambleas sobre ASP.NET Core Data Protection con vencimiento
/// (ITimeLimitedDataProtector). Las llaves ya se persisten en la BD (AddDataProtection en
/// DependencyInjection), asi que los enlaces sobreviven redeploys y multi-instancia sin un secreto
/// nuevo en config. El proposito aisla estos tokens: un payload protegido para otro fin (secretos
/// de plataforma, cookies) no valida aqui.
/// </summary>
public sealed class AsambleaTokenService : IAsambleaTokenService
{
    private const string Proposito = "Propia.Asambleas.EnlaceResidente.v1";
    private readonly ITimeLimitedDataProtector _protector;

    public AsambleaTokenService(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Proposito).ToTimeLimitedDataProtector();

    public string Emitir(AsambleaEnlaceToken datos, DateTimeOffset expira)
        => _protector.Protect($"{datos.TenantId:N}|{datos.SesionId:N}|{datos.PersonaId:N}", expira);

    public AsambleaEnlaceToken? Validar(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        string payload;
        try { payload = _protector.Unprotect(token, out _); }
        catch (CryptographicException) { return null; }  // manipulado, vencido o de otra llave/proposito
        catch (FormatException) { return null; }         // no es base64url

        var partes = payload.Split('|');
        if (partes.Length != 3
            || !Guid.TryParseExact(partes[0], "N", out var tenantId)
            || !Guid.TryParseExact(partes[1], "N", out var sesionId)
            || !Guid.TryParseExact(partes[2], "N", out var personaId))
            return null;

        return new AsambleaEnlaceToken(tenantId, sesionId, personaId);
    }
}
