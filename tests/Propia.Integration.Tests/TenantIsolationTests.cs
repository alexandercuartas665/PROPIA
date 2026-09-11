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

        // Insertar via SQL para evitar las query filters del context (que requieren tenant activo).
        // ExecuteSqlAsync parametriza valores - los Guid van SIN comillas (EF los parametriza como uuid).
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await ctx.Database.ExecuteSqlAsync($@"
            INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, created_at)
            VALUES ({id1}, {t1.Id}, {p1.Id}, 'Residente', 1, now()),
                   ({id2}, {t2.Id}, {p2.Id}, 'Residente', 1, now());");

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

    /// <summary>
    /// T-05. Las seis entidades del modulo 2.10 (tablero, usuarios y campos del tablero, valores de
    /// campo, adjuntos y subtareas) heredan de TenantEntity pero se habian quedado sin
    /// HasQueryFilter: la unica red era la RLS. Aqui se comprueban las DOS por separado:
    ///  - la de la BASE (RLS), leyendo con el rol propia_app, que no tiene BYPASSRLS;
    ///  - la de la APLICACION (HasQueryFilter), leyendo con el rol owner, que SI tiene BYPASSRLS y
    ///    por tanto deja al filtro de EF como lo unico que puede acotar la consulta.
    /// Sin el filtro de EF, las cuentas del bloque "red de la aplicacion" darian 2 en vez de 1.
    /// </summary>
    [Fact]
    public async Task Tareas_las_seis_entidades_del_modulo_se_aislan_por_tenant()
    {
        var (t1, t2) = await CrearDosTenantsT05Async();
        await SembrarTableroCompletoAsync(t1, "A");
        await SembrarTableroCompletoAsync(t2, "B");

        // Las filas de las dos copropiedades existen de verdad: sin ninguna red se ven las 2.
        Assert.Equal(2, await ContarComoOwnerSinRedesAsync("tableros", t1, t2));
        Assert.Equal(2, await ContarComoOwnerSinRedesAsync("tarea_adjuntos", t1, t2));
        Assert.Equal(2, await ContarComoOwnerSinRedesAsync("tarea_subtareas", t1, t2));

        // ---------- Red de la APLICACION (EF HasQueryFilter) ----------
        await using (var ef = NuevoContextoT05(_fx.OwnerConnectionString, t1))
        {
            Assert.Equal(1, await ef.Tableros.CountAsync());
            Assert.Equal(1, await ef.TableroUsuarios.CountAsync());
            Assert.Equal(1, await ef.TableroCampos.CountAsync());
            Assert.Equal(1, await ef.TareaCampoValores.CountAsync());
            Assert.Equal(1, await ef.TareaAdjuntos.CountAsync());
            Assert.Equal(1, await ef.TareaSubtareas.CountAsync());
            // Y lo que ve es lo suyo, no "una cualquiera de las dos".
            Assert.Equal("T05-A", (await ef.Tableros.SingleAsync()).Nombre);
            Assert.All(await ef.TareaAdjuntos.ToListAsync(), a => Assert.Equal(t1, a.TenantId));
        }

        // ---------- Red de la BASE (RLS) ----------
        Assert.Equal(1, await ContarComoAppT05Async("tableros", t2));
        Assert.Equal(1, await ContarComoAppT05Async("tarea_adjuntos", t2));
        Assert.Equal(1, await ContarComoAppT05Async("tablero_campos", t2));
        // Sin tenant en la sesion, la RLS no deja pasar nada.
        Assert.Equal(0, await ContarComoAppT05Async("tableros", null));
        Assert.Equal(0, await ContarComoAppT05Async("tarea_subtareas", null));
        Assert.Equal(0, await ContarComoAppT05Async("tarea_campo_valores", null));

        await LimpiarT05Async(t1, t2);
    }

    // ---------------- Helpers T-05 ----------------

    private PropiaDbContext NuevoContextoT05(string connectionString, Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var tenantCtx = new TenantContext();
        if (tenantId.HasValue) tenantCtx.SetTenant(tenantId.Value);
        return new PropiaDbContext(options, tenantCtx);
    }

    private async Task<(Guid t1, Guid t2)> CrearDosTenantsT05Async()
    {
        await using var ctx = NuevoContextoT05(_fx.OwnerConnectionString, null);
        var t1 = new Tenant { Nombre = "CP T05 Uno", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        var t2 = new Tenant { Nombre = "CP T05 Dos", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.AddRange(t1, t2);
        await ctx.SaveChangesAsync();
        return (t1.Id, t2.Id);
    }

    /// <summary>Un tablero con una fila de cada una de las seis entidades bajo prueba. El TenantId
    /// lo estampa el propio SaveChanges desde el ITenantContext, igual que en runtime.</summary>
    private async Task SembrarTableroCompletoAsync(Guid tenantId, string sufijo)
    {
        await using var ctx = NuevoContextoT05(_fx.OwnerConnectionString, tenantId);

        var tablero = new Tablero { Nombre = "T05-" + sufijo };
        var estado = new TareaEstado { Nombre = "Pendiente " + sufijo, Orden = 1, TableroId = tablero.Id };
        var tarea = new Tarea
        {
            NumeroTarea = "T05-" + sufijo,
            Titulo = "Tarea T05 " + sufijo,
            EstadoId = estado.Id,
            TableroId = tablero.Id
        };
        var campo = new TableroCampo { TableroId = tablero.Id, Label = "Campo T05", Orden = 1 };

        ctx.Tableros.Add(tablero);
        ctx.TareasEstados.Add(estado);
        ctx.Tareas.Add(tarea);
        ctx.TableroCampos.Add(campo);
        ctx.TableroUsuarios.Add(new TableroUsuario { TableroId = tablero.Id, PersonaId = Guid.NewGuid() });
        ctx.TareaCampoValores.Add(new TareaCampoValor { TareaId = tarea.Id, TableroCampoId = campo.Id, Valor = "valor " + sufijo });
        ctx.TareaAdjuntos.Add(new TareaAdjunto { TareaId = tarea.Id, Nombre = "adjunto-" + sufijo + ".pdf", Url = "blob://t05/" + sufijo });
        ctx.TareaSubtareas.Add(new TareaSubtarea { TareaId = tarea.Id, Titulo = "Subtarea " + sufijo, Orden = 1 });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Cuenta con el rol owner y SQL directo: ni RLS (tiene BYPASSRLS) ni filtro de EF.</summary>
    private async Task<int> ContarComoOwnerSinRedesAsync(string tabla, Guid t1, Guid t2)
    {
        await using var ctx = NuevoContextoT05(_fx.OwnerConnectionString, null);
        await using var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // La tabla sale de una lista fija escrita en este archivo, nunca de una entrada externa.
        cmd.CommandText = $"SELECT count(*) FROM {tabla} WHERE tenant_id IN ('{t1}', '{t2}')";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>Cuenta con el rol propia_app: aqui la que filtra es la RLS de PostgreSQL.</summary>
    private async Task<int> ContarComoAppT05Async(string tabla, Guid? tenantId)
    {
        await using var ctx = NuevoContextoT05(_fx.AppConnectionString, tenantId);
        await using var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using (var set = conn.CreateCommand())
        {
            set.CommandText = $"SELECT set_config('app.tenant_id', '{tenantId?.ToString() ?? ""}', false)";
            await set.ExecuteScalarAsync();
        }
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT count(*) FROM {tabla}";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task LimpiarT05Async(Guid t1, Guid t2)
    {
        await using var ctx = NuevoContextoT05(_fx.OwnerConnectionString, null);
        // Orden hijo -> padre por las FK de tablero_campos y tablero_usuarios hacia tableros.
        var tablas = new[]
        {
            "tarea_subtareas", "tarea_adjuntos", "tarea_campo_valores",
            "tablero_campos", "tablero_usuarios", "tareas", "tarea_estados", "tableros"
        };
        foreach (var tabla in tablas)
        {
            // El nombre de tabla sale de la lista fija de arriba; los tenant van parametrizados.
            var borrado = "DELETE FROM " + tabla + " WHERE tenant_id = {0} OR tenant_id = {1}";
            await ctx.Database.ExecuteSqlRawAsync(borrado, t1, t2);
        }
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id IN ({t1}, {t2})");
    }

    private async Task CleanupAsync(Guid t1, Guid t2, Guid p1, Guid p2)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        // ExecuteSqlAsync parametriza - Guid van SIN comillas. Statements separados.
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE tenant_id IN ({t1}, {t2})");
        if (p2 != Guid.Empty)
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id IN ({p1}, {p2})");
        else
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {p1}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id IN ({t1}, {t2})");
    }
}
