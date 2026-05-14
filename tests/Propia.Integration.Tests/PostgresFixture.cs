using Microsoft.EntityFrameworkCore;
using Propia.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Levanta un contenedor PostgreSQL 17 efimero, aplica TODAS las migraciones
/// y deja la BD lista para los tests.
/// La conexion expone DOS connection strings:
///  - Owner (superuser, usado para migraciones y setup)
///  - App (rol propia_app, usado por los tests que validan RLS)
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private const string OwnerUser = "test_owner";
    private const string OwnerPwd = "OwnerPwd123!";
    private const string AppUser = "propia_app";
    private const string AppPwd = "AppPwd123!";

    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("propia_test")
        .WithUsername(OwnerUser)
        .WithPassword(OwnerPwd)
        .Build();

    public string OwnerConnectionString => Container.GetConnectionString();

    public string AppConnectionString =>
        Container.GetConnectionString().Replace(OwnerUser, AppUser).Replace(OwnerPwd, AppPwd);

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        // Crear extensiones requeridas (la imagen base no las crea sola)
        await ExecAsAdminAsync(@"
            CREATE EXTENSION IF NOT EXISTS ""uuid-ossp"";
            CREATE EXTENSION IF NOT EXISTS ""pg_trgm"";
            CREATE EXTENSION IF NOT EXISTS ""citext"";
        ");

        // Aplicar las migraciones EF (que tambien crean el rol propia_app)
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        await ctx.Database.MigrateAsync();

        // Reset password del rol propia_app al valor que usaremos en los tests
        await ExecAsAdminAsync($"ALTER ROLE {AppUser} WITH PASSWORD '{AppPwd}';");
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }

    private async Task ExecAsAdminAsync(string sql)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        await ctx.Database.ExecuteSqlRawAsync(sql);
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture> { }
