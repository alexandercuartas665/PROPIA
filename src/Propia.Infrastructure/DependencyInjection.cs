using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Application.Billing;
using Propia.Application.Common;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Infrastructure.Auth;
using Propia.Infrastructure.Billing;
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
        services.AddScoped<Persistence.TenantConnectionInterceptor>();
        // Necesario para EquipoOrgService que lee claims del JWT en runtime.
        services.AddHttpContextAccessor();

        services.AddDbContext<PropiaDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PropiaDbContext).Assembly.FullName);
            });
            options.AddInterceptors(sp.GetRequiredService<Persistence.TenantConnectionInterceptor>());
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

        // Modulo 0.2 Billing y Suscripciones
        services.AddScoped<IBillingService, BillingService>();

        // Modulo 2.3 Mi Copropiedad
        services.AddScoped<Application.MiCopropiedad.IMiCopropiedadService, MiCopropiedad.MiCopropiedadService>();

        // Modulo 2.1 Onboarding y Activacion
        services.AddScoped<Application.Onboarding.IOnboardingService, Onboarding.OnboardingService>();

        // Modulo 2.4 Directorio
        services.AddScoped<Application.Directorio.IDirectorioService, Directorio.DirectorioService>();

        // Modulo 2.5 Usuarios, Roles y Accesos
        services.AddScoped<Application.UsuariosAccesos.IUsuariosService, UsuariosAccesos.UsuariosService>();
        services.AddScoped<Application.UsuariosAccesos.IRolesService, UsuariosAccesos.RolesService>();

        // Modulo 2.6 Presupuesto, Cuotas y Pagos
        services.AddScoped<Application.Presupuesto.IPresupuestoService, Presupuesto.PresupuestoService>();

        // Modulo 1.3 Gestion de Equipo
        services.AddScoped<Application.EquipoOrg.IEquipoOrgService, EquipoOrg.EquipoOrgService>();

        // Modulo 1.1 Panel y Dashboard Consolidado
        services.AddScoped<Application.PanelConsolidado.IPanelConsolidadoService, PanelConsolidado.PanelConsolidadoService>();

        // Modulo 2.2 Dashboard de la Copropiedad
        services.AddScoped<Application.DashboardCopropiedad.IDashboardCopropiedadService, DashboardCopropiedad.DashboardCopropiedadService>();

        // Modulo 2.10 Tareas y Proyectos
        services.AddScoped<Application.Tareas.ITareasService, Tareas.TareasService>();

        // Modulo 2.7 Cartera y Estado de Cuenta
        services.AddScoped<Application.Cartera.ICarteraService, Cartera.CarteraService>();

        // Modulo 2.9 PQRSD y Convivencia
        services.AddScoped<Application.Pqrsd.IPqrsdService, Pqrsd.PqrsdService>();

        return services;
    }
}
