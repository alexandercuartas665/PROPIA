using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    /// <summary>
    /// Bootstrap del founder SuperAdmin para PRODUCCION. Corre en cualquier entorno pero esta
    /// pensado para prod (donde EnsureDevFounderAsync NO corre). Las credenciales se leen de
    /// configuracion: env vars <c>SuperAdmin__BootstrapEmail</c> / <c>SuperAdmin__BootstrapPassword</c>
    /// (se setean en Railway). Si no estan configuradas, no hace nada.
    /// Idempotente: crea el usuario SOLO si no existe ya uno con ese email; NUNCA sobreescribe la
    /// clave de uno existente. El founder entra solo con email+password y enrola MFA despues desde
    /// la consola (RequiresMfa es false hasta que MfaConfigurado sea true).
    /// </summary>
    public static async Task EnsureBootstrapFounderAsync(IServiceProvider services, IConfiguration config)
    {
        var email = config["SuperAdmin:BootstrapEmail"]?.Trim();
        var password = config["SuperAdmin:BootstrapPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return; // sin bootstrap configurado: no-op

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<SuperAdminUsuario>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        if (await db.SuperAdminUsuarios.AnyAsync(u => u.Email == email))
        {
            logger.LogInformation("Bootstrap SuperAdmin: '{Email}' ya existe; no se modifica.", email);
            return;
        }

        var founder = new SuperAdminUsuario
        {
            Email = email,
            Rol = RolSuperAdmin.SuperAdmin,
            Activo = true
        };
        founder.PasswordHash = hasher.HashPassword(founder, password);
        db.SuperAdminUsuarios.Add(founder);
        try
        {
            await db.SaveChangesAsync();
            logger.LogWarning("Bootstrap SuperAdmin CREADO: {Email}. Configure MFA tras el primer login.", email);
        }
        catch (DbUpdateException)
        {
            // Carrera entre instancias: otra ya lo creo. Idempotente, seguimos.
            logger.LogInformation("Bootstrap SuperAdmin: '{Email}' creado en paralelo por otra instancia.", email);
        }
    }
}
