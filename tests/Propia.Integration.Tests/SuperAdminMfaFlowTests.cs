using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del flujo MFA TOTP del SuperAdmin Console (modulo 0.1 - regla critica de seguridad).
/// Cubre: enroll, verify-enroll, login con MFA (2 pasos), codigo invalido, ticket expirado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SuperAdminMfaFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private ApiFactory _factory = null!;
    private string _testEmail = string.Empty;
    private string _testPassword = "MfaTestPwd2026!";
    private Guid _userId;

    public SuperAdminMfaFlowTests(PostgresFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(_fx.OwnerConnectionString);
        _testEmail = $"mfa-{Guid.NewGuid():N}@adgroup.com.co";

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<SuperAdminUsuario>>();
        var user = new SuperAdminUsuario
        {
            Email = _testEmail,
            Rol = RolSuperAdmin.SuperAdmin,
            Activo = true
        };
        user.PasswordHash = hasher.HashPassword(user, _testPassword);
        db.SuperAdminUsuarios.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;
    }

    public async Task DisposeAsync()
    {
        // Cleanup del usuario de test
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM super_admin_usuarios WHERE id = {_userId}");
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Flujo_completo_enroll_MFA_y_login_con_codigo_TOTP()
    {
        var client = _factory.CreateClient();

        // 1) Login sin MFA -> JWT directo (porque aun no esta configurado)
        var login1 = await client.PostAsJsonAsync("/admin/login", new SuperAdminLoginRequest(_testEmail, _testPassword));
        var login1Body = await login1.Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        Assert.False(login1Body!.RequiresMfa);
        Assert.NotNull(login1Body.AccessToken);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login1Body.AccessToken);

        // 2) Enroll MFA -> secret + URI
        var enrollResp = await client.PostAsync("/admin/mfa/enroll", null);
        Assert.Equal(HttpStatusCode.OK, enrollResp.StatusCode);
        var enroll = await enrollResp.Content.ReadFromJsonAsync<MfaEnrollResponse>();
        Assert.NotNull(enroll);
        Assert.StartsWith("otpauth://totp/", enroll!.OtpAuthUri);
        Assert.False(string.IsNullOrEmpty(enroll.Secret));

        // 3) Generar codigo TOTP valido usando el mismo secret (simula la app autenticadora)
        var code = GenerateTotpCode(enroll.Secret);

        // 4) Verify enroll -> MfaConfigurado = true
        var verifyEnroll = await client.PostAsJsonAsync("/admin/mfa/verify-enroll", new VerifyMfaEnrollRequest(code));
        Assert.Equal(HttpStatusCode.OK, verifyEnroll.StatusCode);

        // 5) Nuevo login con cliente limpio - ahora debe pedir MFA
        var freshClient = _factory.CreateClient();
        var login2 = await freshClient.PostAsJsonAsync("/admin/login", new SuperAdminLoginRequest(_testEmail, _testPassword));
        var login2Body = await login2.Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        Assert.True(login2Body!.RequiresMfa);
        Assert.Null(login2Body.AccessToken);
        Assert.NotNull(login2Body.MfaTicket);

        // 6) Verify mfa-login con codigo TOTP fresco -> JWT final
        var code2 = GenerateTotpCode(enroll.Secret);
        var verifyLogin = await freshClient.PostAsJsonAsync("/admin/mfa/verify-login",
            new VerifyMfaLoginRequest(login2Body.MfaTicket!, code2));
        Assert.Equal(HttpStatusCode.OK, verifyLogin.StatusCode);
        var loginFinal = await verifyLogin.Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        Assert.False(loginFinal!.RequiresMfa);
        Assert.NotNull(loginFinal.AccessToken);
        Assert.Equal(_testEmail, loginFinal.Email);

        // 7) JWT final permite acceso a endpoints admin
        freshClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginFinal.AccessToken);
        var tenantsResp = await freshClient.GetAsync("/admin/tenants");
        Assert.Equal(HttpStatusCode.OK, tenantsResp.StatusCode);
    }

    [Fact]
    public async Task MFA_verify_login_con_codigo_invalido_retorna_401()
    {
        var client = _factory.CreateClient();

        // Setup: enroll y verify
        var login1 = await client.PostAsJsonAsync("/admin/login", new SuperAdminLoginRequest(_testEmail, _testPassword));
        var jwt = (await login1.Content.ReadFromJsonAsync<SuperAdminLoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        var enroll = (await (await client.PostAsync("/admin/mfa/enroll", null)).Content.ReadFromJsonAsync<MfaEnrollResponse>())!;
        await client.PostAsJsonAsync("/admin/mfa/verify-enroll", new VerifyMfaEnrollRequest(GenerateTotpCode(enroll.Secret)));

        // Login -> ticket MFA
        var fresh = _factory.CreateClient();
        var loginMfa = await fresh.PostAsJsonAsync("/admin/login", new SuperAdminLoginRequest(_testEmail, _testPassword));
        var loginMfaBody = await loginMfa.Content.ReadFromJsonAsync<SuperAdminLoginResponse>();

        // Verify con codigo INCORRECTO
        var resp = await fresh.PostAsJsonAsync("/admin/mfa/verify-login",
            new VerifyMfaLoginRequest(loginMfaBody!.MfaTicket!, "000000"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task MFA_verify_login_con_ticket_falso_retorna_401()
    {
        var client = _factory.CreateClient();
        // Ticket inventado (no firmado por nuestro servidor)
        var resp = await client.PostAsJsonAsync("/admin/mfa/verify-login",
            new VerifyMfaLoginRequest("invalid-ticket-fake", "123456"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private static string GenerateTotpCode(string secret)
    {
        var bytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(bytes);
        return totp.ComputeTotp();
    }

    // Reuso del patron ApiFactory
    private class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connString;
        public ApiFactory(string connString) => _connString = connString;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((ctx, conf) =>
            {
                conf.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Propia"] = _connString,
                    ["Jwt:SigningKey"] = "DEV-ONLY-PropIA-Jwt-SigningKey-Min32CharsForHS256-AuthTest",
                    ["Jwt:Issuer"] = "propia-api",
                    ["Jwt:Audience"] = "propia-clients",
                    ["Jwt:AccessTokenMinutes"] = "60"
                });
            });
        }
    }
}
