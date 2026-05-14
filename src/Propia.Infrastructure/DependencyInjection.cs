using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Infrastructure.Persistence;

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

        return services;
    }
}
