using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Propia.Api.Controllers;
using Propia.Application.Auth;
using Propia.Application.SuperAdmin;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Gate del atajo de desarrollo /connect/dev-login: fuera de Development NUNCA emite token (404,
/// fail-closed); en Development emite el JWT del admin demo. Es un test unitario del controller (sin
/// host) para que el gate de seguridad se verifique de forma rapida y determinista.
/// </summary>
public class DevLoginGateTests
{
    private sealed class FakeEnv : IWebHostEnvironment
    {
        public FakeEnv(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeAuth : IAuthService
    {
        private readonly LoginResponse? _login;
        public FakeAuth(LoginResponse? login) => _login = login;
        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct, string? ip = null, string? userAgent = null)
            => Task.FromResult(_login);
        public Task<MeResponse?> GetMeAsync(Guid userId, Guid? activeTenantId, CancellationToken ct) => Task.FromResult<MeResponse?>(null);
        public Task<LoginResponse?> SwitchTenantAsync(Guid userId, Guid newTenantId, CancellationToken ct) => Task.FromResult<LoginResponse?>(null);
        public Task<LoginResponse?> RefreshAsync(string rawJwt, CancellationToken ct) => Task.FromResult<LoginResponse?>(null);
        public Task<(bool Ok, string? Error)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct) => Task.FromResult((true, (string?)null));
    }

    private static AuthController Controller(IAuthService auth)
    {
        // HttpContext para que Ip()/UserAgent() no revienten en el camino Development.
        var c = new AuthController(auth, null!);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return c;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task DevLogin_fuera_de_Development_devuelve_404(string envName)
    {
        // _auth = null: si el gate fallara y siguiera, reventaria; NotFound confirma que corta antes.
        var ctrl = Controller(null!);
        var res = await ctrl.DevLogin(new FakeEnv(envName), CancellationToken.None);
        Assert.IsType<NotFoundResult>(res);
    }

    [Fact]
    public async Task DevLogin_en_Development_emite_el_token_del_demo()
    {
        var login = new LoginResponse(
            AccessToken: "jwt-demo",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(8),
            UserId: Guid.NewGuid(),
            Email: "admin@demo.propia",
            ActiveTenantId: null,
            AvailableTenants: new List<TenantInfo> { new(Guid.NewGuid(), "Conjunto Altos del Bosque", "Administrador") });
        var ctrl = Controller(new FakeAuth(login));

        var res = await ctrl.DevLogin(new FakeEnv("Development"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("jwt-demo", body.AccessToken);
    }

    [Fact]
    public async Task DevLogin_en_Development_sin_demo_seedeado_devuelve_404()
    {
        var ctrl = Controller(new FakeAuth(null));
        var res = await ctrl.DevLogin(new FakeEnv("Development"), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(res);
    }
}
