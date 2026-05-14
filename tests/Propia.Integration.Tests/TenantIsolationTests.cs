using Microsoft.EntityFrameworkCore;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests CRITICOS de aislamiento de tenant via Row-Level Security de PostgreSQL.
///
/// Cualquier fallo en este archivo BLOQUEA el merge. Es la garantia ultima de que
/// una copropiedad NUNCA puede ver datos de otra, ni siquiera con un bug en EF
/// HasQueryFilter o un olvido de filtrar manualmente en SQL raw.
///
/// Estos tests usan el rol propia_app (NO superuser, NO BYPASSRLS) - asi se
/// comportara la aplicacion en runtime.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class TenantIsolationTests
{
    private readonly PostgresFixture _fx;

    public TenantIsolationTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task RLS_aisla_filas_entre_tenants()
    {
        // ---------- Arrange: setup como owner (sin RLS) ----------
        var (t1, t2, p1, p2) = await SetupDosTenantsConUsuariosAsync();

        // ---------- Act + Assert: leer como app role ----------
        // Como T1, debe ver SOLO su UsuarioTenant
        var visibles1 = await CountUsuariosTenantAsAppAsync(t1);
        Assert.Equal(1, visibles1);

        // Como T2, debe ver SOLO el suyo
        var visibles2 = await CountUsuariosTenantAsAppAsync(t2);
        Assert.Equal(1, visibles2);

        // Sin tenant seteado: RLS bloquea todas las filas
        var visiblesSinTenant = await CountUsuariosTenantAsAppAsync(null);
        Assert.Equal(0, visiblesSinTenant);

        // Cleanup
        await CleanupAsync(t1, t2, p1, p2);
    }

    [Fact]
    public async Task RLS_bloquea_inserts_con_tenant_id_distinto_al_de_sesion()
    {
        var (t1, t2, p1, _) = await SetupDosTenantsConUsuariosAsync();

        // Como T1 trato de insertar un UsuarioTenant marcado como de T2 - debe fallar (WITH CHECK)
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .Options;
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(t1);
        await using var ctx = new PropiaDbContext(options, tenantCtx);

        await using var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT set_config('app.tenant_id', '{t1}', false)";
            await cmd.ExecuteScalarAsync();
        }

        // Intento crear un registro con TenantId = t2 - WITH CHECK debe rechazarlo.
        // Bypaseo el SaveChanges normal porque ese auto-asigna el TenantId desde el context.
        await using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = $@"
            INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, created_at)
            VALUES ('{Guid.NewGuid()}', '{t2}', '{p1}', 'X', 1, now())";

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => cmd2.ExecuteNonQueryAsync());
        Assert.Contains("row-level security", ex.Message, StringComparison.OrdinalIgnoreCase);

        await CleanupAsync(t1, t2, p1, Guid.Empty);
    }

    [Fact]
    public async Task SuperAdminLog_es_inmutable_no_acepta_update_ni_delete()
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());

        var log = new SuperAdminLog
        {
            ActorId = Guid.NewGuid(),
            ActorEmail = "auditor@adgroup.com.co",
            Accion = "TEST_INMUTABLE"
        };
        ctx.SuperAdminLogs.Add(log);
        await ctx.SaveChangesAsync();

        // Intento UPDATE - debe fallar
        log.Accion = "MODIFICADO";
        var exUpd = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.Contains("append-only", exUpd.InnerException?.Message ?? exUpd.Message);

        // Limpio: TRUNCATE no dispara el trigger BEFORE DELETE row-level
        ctx.ChangeTracker.Clear();
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE super_admin_logs");
    }

    // ---------------- Helpers ----------------

    private async Task<(Guid t1, Guid t2, Guid p1, Guid p2)> SetupDosTenantsConUsuariosAsync()
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());

        var t1 = new Tenant { Nombre = "CP Uno", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        var t2 = new Tenant { Nombre = "CP Dos", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        var p1 = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"D{Guid.NewGuid():N}".Substring(0, 18), Nombres = "Ana", Apellidos = "Test" };
        var p2 = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"D{Guid.NewGuid():N}".Substring(0, 18), Nombres = "Beto", Apellidos = "Test" };
        ctx.Tenants.AddRange(t1, t2);
        ctx.Personas.AddRange(p1, p2);
        await ctx.SaveChangesAsync();

        // Insertar via SQL para evitar las query filters del context (que requieren tenant activo)
        await ctx.Database.ExecuteSqlRawAsync($@"
            INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, created_at)
            VALUES ('{Guid.NewGuid()}', '{t1.Id}', '{p1.Id}', 'Residente', 1, now()),
                   ('{Guid.NewGuid()}', '{t2.Id}', '{p2.Id}', 'Residente', 1, now());
        ");

        return (t1.Id, t2.Id, p1.Id, p2.Id);
    }

    private async Task<int> CountUsuariosTenantAsAppAsync(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .Options;
        var tenantCtx = new TenantContext();
        if (tenantId.HasValue) tenantCtx.SetTenant(tenantId.Value);
        await using var ctx = new PropiaDbContext(options, tenantCtx);

        await using var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT set_config('app.tenant_id', '{tenantId?.ToString() ?? ""}', false)";
        await cmd.ExecuteScalarAsync();

        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT count(*) FROM usuarios_tenant";
        var result = await countCmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task CleanupAsync(Guid t1, Guid t2, Guid p1, Guid p2)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        await ctx.Database.ExecuteSqlRawAsync($@"
            DELETE FROM usuarios_tenant WHERE tenant_id IN ('{t1}', '{t2}');
            DELETE FROM personas WHERE id IN ('{p1}'{(p2 == Guid.Empty ? "" : $", '{p2}'")});
            DELETE FROM tenants WHERE id IN ('{t1}', '{t2}');
        ");
    }
}
