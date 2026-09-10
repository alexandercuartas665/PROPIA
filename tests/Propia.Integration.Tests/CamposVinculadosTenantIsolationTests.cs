using Microsoft.EntityFrameworkCore;
using Npgsql;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests de aislamiento de tenant de los 8 catalogos de campos dinamicos de las entidades
/// vinculadas a una unidad (Personas / Vehiculos / Mascotas / Terceros).
///
/// Cubre las 4 familias con el mismo cuerpo parametrizado: la tabla de definiciones,
/// la de valores y el nombre de la columna que apunta a la entidad vinculada.
///
/// Igual que TenantIsolationTests: el setup se hace con el owner (superuser, evade RLS) y
/// las verificaciones con el rol propia_app (sin BYPASSRLS), que es como corre la aplicacion.
/// Los nombres de tabla/columna no se pueden parametrizar en SQL, asi que se validan contra
/// la whitelist Familias antes de interpolarlos; todos los valores si van parametrizados.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CamposVinculadosTenantIsolationTests
{
    private readonly PostgresFixture _fx;

    public CamposVinculadosTenantIsolationTests(PostgresFixture fx) => _fx = fx;

    /// <summary>Whitelist de tablas/columnas admitidas por estos tests.</summary>
    private static readonly (string Definiciones, string Valores, string ColumnaEntidad)[] Familias =
    [
        ("persona_campos_definiciones", "persona_campos_valores", "unidad_persona_id"),
        ("vehiculo_campos_definiciones", "vehiculo_campos_valores", "unidad_placa_id"),
        ("mascota_campos_definiciones", "mascota_campos_valores", "unidad_mascota_id"),
        ("tercero_campos_definiciones", "tercero_campos_valores", "unidad_empleada_id")
    ];

    [Theory]
    [InlineData("persona_campos_definiciones", "persona_campos_valores", "unidad_persona_id")]
    [InlineData("vehiculo_campos_definiciones", "vehiculo_campos_valores", "unidad_placa_id")]
    [InlineData("mascota_campos_definiciones", "mascota_campos_valores", "unidad_mascota_id")]
    [InlineData("tercero_campos_definiciones", "tercero_campos_valores", "unidad_empleada_id")]
    public async Task RLS_aisla_catalogo_de_campos_entre_tenants(
        string tablaDefiniciones, string tablaValores, string columnaEntidad)
    {
        ValidarNombres(tablaDefiniciones, tablaValores, columnaEntidad);

        var (t1, t2) = await CrearDosTenantsAsync();
        var defT1 = Guid.NewGuid();
        var defT2 = Guid.NewGuid();
        var valT1 = Guid.NewGuid();
        var valT2 = Guid.NewGuid();

        try
        {
            await SembrarComoOwnerAsync(
                tablaDefiniciones, tablaValores, columnaEntidad,
                t1, t2, defT1, defT2, valT1, valT2);

            // ---------- Definiciones ----------
            // Como T1 solo se ve la definicion de T1
            var idsT1 = await LeerIdsComoAppAsync(tablaDefiniciones, t1);
            Assert.Equal(new[] { defT1 }, idsT1);

            // Como T2 solo se ve la de T2 (la de T1 es invisible)
            var idsT2 = await LeerIdsComoAppAsync(tablaDefiniciones, t2);
            Assert.Equal(new[] { defT2 }, idsT2);

            // Sin tenant en sesion, RLS oculta todo
            Assert.Empty(await LeerIdsComoAppAsync(tablaDefiniciones, null));

            // ---------- Valores ----------
            Assert.Equal(new[] { valT1 }, await LeerIdsComoAppAsync(tablaValores, t1));
            Assert.Equal(new[] { valT2 }, await LeerIdsComoAppAsync(tablaValores, t2));
            Assert.Empty(await LeerIdsComoAppAsync(tablaValores, null));
        }
        finally
        {
            await LimpiarComoOwnerAsync(tablaDefiniciones, tablaValores, t1, t2);
        }
    }

    [Theory]
    [InlineData("persona_campos_definiciones", "persona_campos_valores", "unidad_persona_id")]
    [InlineData("vehiculo_campos_definiciones", "vehiculo_campos_valores", "unidad_placa_id")]
    [InlineData("mascota_campos_definiciones", "mascota_campos_valores", "unidad_mascota_id")]
    [InlineData("tercero_campos_definiciones", "tercero_campos_valores", "unidad_empleada_id")]
    public async Task RLS_bloquea_insert_de_campo_con_tenant_id_ajeno(
        string tablaDefiniciones, string tablaValores, string columnaEntidad)
    {
        ValidarNombres(tablaDefiniciones, tablaValores, columnaEntidad);

        var (t1, t2) = await CrearDosTenantsAsync();

        try
        {
            await using var conn = await AbrirComoAppAsync(t1);

            // Definicion marcada como de T2 mientras la sesion es T1: WITH CHECK debe rechazarla
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    INSERT INTO {tablaDefiniciones} (id, tenant_id, label, orden, tipo, created_at)
                    VALUES (@id, @tenant, 'Intruso', 1, @tipo, now())";
                cmd.Parameters.AddWithValue("id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("tenant", t2);
                cmd.Parameters.AddWithValue("tipo", (int)TipoCampoTablero.Texto);

                var ex = await Assert.ThrowsAnyAsync<Exception>(() => cmd.ExecuteNonQueryAsync());
                Assert.Contains("row-level security", ex.Message, StringComparison.OrdinalIgnoreCase);
            }

            // Idem para la tabla de valores. Se crea primero la definicion en T1 (legal) para
            // no chocar con la FK, y luego se intenta colgar el valor bajo el tenant T2.
            var defLegal = Guid.NewGuid();
            await using (var cmdDef = conn.CreateCommand())
            {
                cmdDef.CommandText = $@"
                    INSERT INTO {tablaDefiniciones} (id, tenant_id, label, orden, tipo, created_at)
                    VALUES (@id, @tenant, 'Legal T1', 1, @tipo, now())";
                cmdDef.Parameters.AddWithValue("id", defLegal);
                cmdDef.Parameters.AddWithValue("tenant", t1);
                cmdDef.Parameters.AddWithValue("tipo", (int)TipoCampoTablero.Texto);
                await cmdDef.ExecuteNonQueryAsync();
            }

            await using (var cmdVal = conn.CreateCommand())
            {
                cmdVal.CommandText = $@"
                    INSERT INTO {tablaValores} (id, tenant_id, definicion_id, {columnaEntidad}, valor, created_at)
                    VALUES (@id, @tenant, @def, @entidad, 'x', now())";
                cmdVal.Parameters.AddWithValue("id", Guid.NewGuid());
                cmdVal.Parameters.AddWithValue("tenant", t2);
                cmdVal.Parameters.AddWithValue("def", defLegal);
                cmdVal.Parameters.AddWithValue("entidad", Guid.NewGuid());

                var exVal = await Assert.ThrowsAnyAsync<Exception>(() => cmdVal.ExecuteNonQueryAsync());
                Assert.Contains("row-level security", exVal.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await LimpiarComoOwnerAsync(tablaDefiniciones, tablaValores, t1, t2);
        }
    }

    // ---------------- Helpers ----------------

    private static void ValidarNombres(string definiciones, string valores, string columnaEntidad)
    {
        Assert.Contains((definiciones, valores, columnaEntidad), Familias);
    }

    private async Task<(Guid t1, Guid t2)> CrearDosTenantsAsync()
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());

        var t1 = new Tenant
        {
            Nombre = $"CP Campos A {Guid.NewGuid():N}"[..24],
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.SinAdmin
        };
        var t2 = new Tenant
        {
            Nombre = $"CP Campos B {Guid.NewGuid():N}"[..24],
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.SinAdmin
        };
        ctx.Tenants.AddRange(t1, t2);
        await ctx.SaveChangesAsync();

        return (t1.Id, t2.Id);
    }

    /// <summary>
    /// Siembra una definicion y un valor en cada tenant. El owner es superuser: evade RLS,
    /// asi que puede insertar filas de los dos tenants en la misma sesion.
    /// </summary>
    private async Task SembrarComoOwnerAsync(
        string tablaDefiniciones, string tablaValores, string columnaEntidad,
        Guid t1, Guid t2, Guid defT1, Guid defT2, Guid valT1, Guid valT2)
    {
        await using var conn = new NpgsqlConnection(_fx.OwnerConnectionString);
        await conn.OpenAsync();

        await using (var cmdDef = conn.CreateCommand())
        {
            // Mismo label en los dos tenants: el indice unico es (tenant_id, label)
            cmdDef.CommandText = $@"
                INSERT INTO {tablaDefiniciones} (id, tenant_id, label, orden, tipo, created_at)
                VALUES (@d1, @t1, 'Campo compartido', 1, @tipo, now()),
                       (@d2, @t2, 'Campo compartido', 1, @tipo, now())";
            cmdDef.Parameters.AddWithValue("d1", defT1);
            cmdDef.Parameters.AddWithValue("t1", t1);
            cmdDef.Parameters.AddWithValue("d2", defT2);
            cmdDef.Parameters.AddWithValue("t2", t2);
            cmdDef.Parameters.AddWithValue("tipo", (int)TipoCampoTablero.Texto);
            await cmdDef.ExecuteNonQueryAsync();
        }

        await using (var cmdVal = conn.CreateCommand())
        {
            cmdVal.CommandText = $@"
                INSERT INTO {tablaValores} (id, tenant_id, definicion_id, {columnaEntidad}, valor, created_at)
                VALUES (@v1, @t1, @d1, @e1, 'valor de T1', now()),
                       (@v2, @t2, @d2, @e2, 'valor de T2', now())";
            cmdVal.Parameters.AddWithValue("v1", valT1);
            cmdVal.Parameters.AddWithValue("t1", t1);
            cmdVal.Parameters.AddWithValue("d1", defT1);
            cmdVal.Parameters.AddWithValue("e1", Guid.NewGuid());
            cmdVal.Parameters.AddWithValue("v2", valT2);
            cmdVal.Parameters.AddWithValue("t2", t2);
            cmdVal.Parameters.AddWithValue("d2", defT2);
            cmdVal.Parameters.AddWithValue("e2", Guid.NewGuid());
            await cmdVal.ExecuteNonQueryAsync();
        }
    }

    private async Task<List<Guid>> LeerIdsComoAppAsync(string tabla, Guid? tenantId)
    {
        await using var conn = await AbrirComoAppAsync(tenantId);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM {tabla} ORDER BY id";

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ids.Add(reader.GetGuid(0));
        return ids;
    }

    /// <summary>
    /// Abre una conexion con el rol propia_app (respeta RLS, no tiene BYPASSRLS) y fija el
    /// tenant de sesion via set_config('app.tenant_id', ...), que es lo que lee current_tenant_id().
    /// </summary>
    private async Task<NpgsqlConnection> AbrirComoAppAsync(Guid? tenantId)
    {
        var conn = new NpgsqlConnection(_fx.AppConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tenant, false)";
        cmd.Parameters.AddWithValue("tenant", tenantId?.ToString() ?? string.Empty);
        await cmd.ExecuteScalarAsync();
        return conn;
    }

    private async Task LimpiarComoOwnerAsync(string tablaDefiniciones, string tablaValores, Guid t1, Guid t2)
    {
        await using var conn = new NpgsqlConnection(_fx.OwnerConnectionString);
        await conn.OpenAsync();

        // Los valores cuelgan de las definiciones (FK cascade), pero se borran explicitamente
        // para no depender del orden de cascada.
        foreach (var tabla in new[] { tablaValores, tablaDefiniciones, "tenants" })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = tabla == "tenants"
                ? "DELETE FROM tenants WHERE id IN (@t1, @t2)"
                : $"DELETE FROM {tabla} WHERE tenant_id IN (@t1, @t2)";
            cmd.Parameters.AddWithValue("t1", t1);
            cmd.Parameters.AddWithValue("t2", t2);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
