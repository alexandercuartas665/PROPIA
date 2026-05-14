using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Propia.Infrastructure.Persistence;

/// <summary>
/// Factory usado SOLO por dotnet ef migrations (design time).
/// En runtime, el DbContext lo construye DI desde Propia.Api/Program.cs.
/// La connection string aqui es la de desarrollo (puerto 5433 - Docker compose).
/// </summary>
public class PropiaDbContextFactory : IDesignTimeDbContextFactory<PropiaDbContext>
{
    public PropiaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=propia_dev;Username=propia;Password=PropiaDev2026!")
            .Options;

        // En design time no hay tenant - se usa un context vacio
        var tenantContext = new TenantContext();
        return new PropiaDbContext(options, tenantContext);
    }
}
