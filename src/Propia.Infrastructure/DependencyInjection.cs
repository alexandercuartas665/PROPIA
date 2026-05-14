using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Application.Common;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Infrastructure.Auth;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.SuperAdmin;

namespace Propia.Infrastructure;

/// <summary>
/// Extension para registrar todos los servicios de Infrastructure en DI.
/// Llamado desde Program.cs de Propia.Api y Propia.Workers.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Propia")
            ?? throw new InvalidOperationException("Falta connection string 'Propia' en configuracion.");

        services.AddScoped<ITenantContext, TenantContext>();

        services.AddDbContext<PropiaDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PropiaDbContext).Assembly.FullName);
            });
        });

        // ASP.NET Core Identity - solo el core (sin cookies, JWT only)
        services.AddIdentityCore<ApplicationUser>(opts =>
            {
                opts.Password.RequiredLength = 10;
                opts.Password.RequireDigit = true;
                opts.Password.RequireUppercase = true;
                opts.Password.RequireNonAlphanumeric = false;
                opts.User.RequireUniqueEmail = true;
                opts.SignIn.RequireConfirmedEmail = false;  // Para MVP - habilitar en Fase 2 con email service
                opts.Lockout.MaxFailedAccessAttempts = 5;
                opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();

        // Auth services
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Super Admin (modulo 0.1) - usa tabla separada super_admin_usuarios
        services.AddScoped<IPasswordHasher<SuperAdminUsuario>, PasswordHasher<SuperAdminUsuario>>();
        services.AddSingleton<ITotpService, TotpService>();  // sin estado
        services.AddScoped<ISuperAdminAuthService, SuperAdminAuthService>();
        services.AddScoped<ISuperAdminService, SuperAdminService>();

        return services;
    }
}
