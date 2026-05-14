using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Seed inicial de un SuperAdmin "founder" para entornos dev.
/// En produccion: NO se ejecuta - el founder se crea manualmente por DBA con un
/// script una sola vez al instalar la plataforma.
/// </summary>
public static class SuperAdminSeeder
{
    public const string DevFounderEmail = "founder@adgroup.com.co";
    public const string DevFounderPassword = "PropiaFounder2026!";

    public static async Task EnsureDevFounderAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<SuperAdminUsuario>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        var exists = await db.SuperAdminUsuarios.AnyAsync(u => u.Email == DevFounderEmail);
        if (exists) return;

        var founder = new SuperAdminUsuario
        {
            Email = DevFounderEmail,
            Rol = RolSuperAdmin.SuperAdmin,
            Activo = true
        };
        founder.PasswordHash = hasher.HashPassword(founder, DevFounderPassword);
        db.SuperAdminUsuarios.Add(founder);
        await db.SaveChangesAsync();
        logger.LogWarning("[DEV] Founder SuperAdmin seeded: {Email} / {Password} - cambiar antes de prod.",
            DevFounderEmail, DevFounderPassword);
    }
}
