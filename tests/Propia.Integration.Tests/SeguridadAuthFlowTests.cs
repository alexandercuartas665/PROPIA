using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// S-03 (lockout) y S-04 (login exige EmailConfirmed) - auditoria ciclo 3.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SeguridadAuthFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private AuthFlowTests.ApiFactory _factory = null!;

    public SeguridadAuthFlowTests(PostgresFixture fx) => _fx = fx;
    public Task InitializeAsync() { _factory = new AuthFlowTests.ApiFactory(_fx.OwnerConnectionString); return Task.CompletedTask; }
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Login_con_email_no_confirmado_retorna_401()  // S-04
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var email = $"nc-{Guid.NewGuid():N}@propia.com.co";
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = false };
        await userManager.CreateAsync(user, "Password1234!");

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/connect/token", new LoginRequest(email, "Password1234!"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        await db.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {user.Id}");
    }

    [Fact]
    public async Task Login_se_bloquea_tras_5_intentos_fallidos()  // S-03
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var email = $"lock-{Guid.NewGuid():N}@propia.com.co";
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, "Password1234!");

        var client = _factory.CreateClient();
        // 5 intentos con clave incorrecta -> AccessFailedAsync bloquea al 5o.
        for (var i = 0; i < 5; i++)
        {
            var bad = await client.PostAsJsonAsync("/connect/token", new LoginRequest(email, "wrong-pass"));
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }
        // Ahora la clave CORRECTA tambien falla: la cuenta esta bloqueada (IsLockedOutAsync).
        var ok = await client.PostAsJsonAsync("/connect/token", new LoginRequest(email, "Password1234!"));
        Assert.Equal(HttpStatusCode.Unauthorized, ok.StatusCode);

        var reloaded = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.True(await userManager.IsLockedOutAsync(reloaded!));

        await db.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {user.Id}");
    }
}
