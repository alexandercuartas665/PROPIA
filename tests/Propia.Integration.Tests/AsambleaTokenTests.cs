using Microsoft.AspNetCore.DataProtection;
using Propia.Application.Asambleas;
using Propia.Infrastructure.Asambleas;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Asambleas Ola 1.1: token del enlace self-service (AsambleaTokenService). Prueba pura con un
/// proveedor de Data Protection efimero: no necesita BD. Fija que solo un token autentico, vigente
/// y emitido para este proposito devuelve los datos; todo lo demas devuelve null.
/// </summary>
public class AsambleaTokenTests
{
    private static readonly AsambleaEnlaceToken Datos = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static AsambleaTokenService Nuevo(IDataProtectionProvider? provider = null)
        => new(provider ?? new EphemeralDataProtectionProvider());

    [Fact]
    public void Token_valido_devuelve_los_mismos_datos()
    {
        var svc = Nuevo();
        var token = svc.Emitir(Datos, DateTimeOffset.UtcNow.AddHours(1));

        Assert.Equal(Datos, svc.Validar(token));
        // base64url: apto para ir como segmento de ruta sin escapar
        Assert.DoesNotContain(token, c => c is '+' or '/' or '=');
    }

    [Fact]
    public void Token_vencido_no_valida()
    {
        var svc = Nuevo();
        var token = svc.Emitir(Datos, DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.Null(svc.Validar(token));
    }

    [Fact]
    public void Token_manipulado_no_valida()
    {
        var svc = Nuevo();
        var token = svc.Emitir(Datos, DateTimeOffset.UtcNow.AddHours(1));
        var mid = token.Length / 2;
        var alterado = token[..mid] + (token[mid] == 'A' ? 'B' : 'A') + token[(mid + 1)..];

        Assert.Null(svc.Validar(alterado));
    }

    [Fact]
    public void Token_de_otra_llave_no_valida()
    {
        var token = Nuevo().Emitir(Datos, DateTimeOffset.UtcNow.AddHours(1));

        Assert.Null(Nuevo().Validar(token));
    }

    [Fact]
    public void Payload_protegido_para_otro_proposito_no_valida()
    {
        // Mismo proveedor, mismo texto, pero con el proposito de los secretos de plataforma.
        var provider = new EphemeralDataProtectionProvider();
        var ajeno = provider.CreateProtector("Propia.PlatformSecrets.v1")
            .Protect($"{Datos.TenantId:N}|{Datos.SesionId:N}|{Datos.PersonaId:N}");

        Assert.Null(Nuevo(provider).Validar(ajeno));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-token")]
    [InlineData("%%%")]
    public void Token_vacio_o_basura_no_valida(string? token)
        => Assert.Null(Nuevo().Validar(token));
}
