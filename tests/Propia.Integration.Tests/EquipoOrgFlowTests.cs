using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.EquipoOrg;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.EquipoOrg;
using Propia.Infrastructure.Persistence;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests de integracion del modulo 1.3 Gestion de Equipo (spec v1.0).
///
/// Cubre los flujos criticos:
///  - Seed lazy del catalogo de 6 cargos por defecto con plantilla.
///  - Identidad unica (RN-01) en alta de colaborador.
///  - Asignar a todas las PHs de la organizacion (HU-1.3-02).
///  - Desactivar colaborador (RN-04) con reasignacion opcional.
///  - RN-06: cargo con colaboradores activos no se puede eliminar.
///  - Override de permiso individual gana sobre plantilla del cargo.
///  - Historial append-only (trigger SQL).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EquipoOrgFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;

    public EquipoOrgFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = BuildFakeHttpContext(Guid.NewGuid())
        });
        sc.AddDbContext<PropiaDbContext>(opts => opts.UseNpgsql(_fx.AppConnectionString));
        sc.AddIdentityCore<ApplicationUser>(opts => { opts.User.RequireUniqueEmail = true; })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Catalogo_se_siembra_lazy_con_6_cargos_default_y_5_permisos_cada_uno()
    {
        var (orgId, tenantId) = await SeedOrgConTenantAsync("Equipo Seed");
        var (svc, _, _) = BuildService(tenantId);

        var cargos = await svc.ListarCargosAsync(CancellationToken.None);

        Assert.Equal(6, cargos.Count);
        Assert.All(cargos, c => Assert.True(c.EsDefault));
        Assert.Contains(cargos, c => c.Nombre == CargoCatalogoBase.Director);
        Assert.Contains(cargos, c => c.Nombre == CargoCatalogoBase.Coordinador);
        Assert.Contains(cargos, c => c.Nombre == CargoCatalogoBase.Recorredor);

        // Verificar plantilla del Director: 5 modulos en COMPLETO
        var director = cargos.First(c => c.Nombre == CargoCatalogoBase.Director);
        var detalle = await svc.GetCargoDetalleAsync(director.Id, CancellationToken.None);
        Assert.NotNull(detalle);
        Assert.Equal(5, detalle!.Permisos.Count);
        Assert.All(detalle.Permisos, p => Assert.Equal(NivelPermisoCapa1.Completo, p.Nivel));

        await CleanupOrgAsync(orgId, tenantId);
    }

    [Fact]
    public async Task Agregar_colaborador_nuevo_crea_persona_y_lo_vincula_RN01()
    {
        var (orgId, tenantId) = await SeedOrgConTenantAsync("Equipo Alta");
        var (svc, _, _) = BuildService(tenantId);

        var cargo = (await svc.ListarCargosAsync(CancellationToken.None))
            .First(c => c.Nombre == CargoCatalogoBase.Coordinador);

        var docUnico = $"E{Guid.NewGuid():N}".Substring(0, 16);
        var emailUnico = $"col.{Guid.NewGuid():N}@test.co";
        var colab = await svc.AgregarColaboradorAsync(new AgregarColaboradorRequest(
            null, TipoDocumento.CC, docUnico, "Maria", "Test",
            emailUnico, "3001112233", cargo.Id, null, false, null), CancellationToken.None);

        Assert.NotNull(colab);
        Assert.Equal("Maria", colab.Nombres);
        Assert.Equal(EstadoColaborador.Pendiente, colab.Estado);

        // Segundo intento con el mismo documento debe fallar (identidad unica)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AgregarColaboradorAsync(new AgregarColaboradorRequest(
                null, TipoDocumento.CC, docUnico, "Otro", "Persona",
                $"otro.{Guid.NewGuid():N}@test.co", null, cargo.Id, null, false, null), CancellationToken.None));

        await CleanupOrgAsync(orgId, tenantId);
    }

    [Fact]
    public async Task Persona_existente_se_vincula_sin_duplicar()
    {
        var (orgId, tenantId) = await SeedOrgConTenantAsync("Equipo Vincula");
        var personaExistente = await SeedPersonaAsync("Carlos", "Existente");
        var (svc, db, _) = BuildService(tenantId);
        var cargo = (await svc.ListarCargosAsync(CancellationToken.None))
            .First(c => c.Nombre == CargoCatalogoBase.AsistenteAdministrativo);

        var colab = await svc.AgregarColaboradorAsync(new AgregarColaboradorRequest(
            personaExistente.Id, null, null, null, null, null, null, cargo.Id, null, false, null),
            CancellationToken.None);
        Assert.Equal(personaExistente.Id, colab.PersonaId);

        // Solo debe existir una persona con ese documento (no se duplico)
        var personas = await db.Personas.Where(p => p.Documento == personaExistente.Documento).CountAsync();
        Assert.Equal(1, personas);

        await CleanupOrgAsync(orgId, tenantId);
    }

    [Fact]
    public async Task Asignar_a_todas_crea_una_asignacion_por_cada_PH_de_la_organizacion()
    {
        var (orgId, tenantId1) = await SeedOrgConTenantAsync("PH 1 Multi");
        var tenantId2 = await SeedTenantParaOrgAsync(orgId, "PH 2 Multi");
        var tenantId3 = await SeedTenantParaOrgAsync(orgId, "PH 3 Multi");
        var (svc, db, _) = BuildService(tenantId1);

        // Seed roles en cada copropiedad
        var rolGlobalId = await SeedRolGlobalAsync("Asistente Org");

        var cargo = (await svc.ListarCargosAsync(CancellationToken.None))
            .First(c => c.Nombre == CargoCatalogoBase.Coordinador);

        var colab = await svc.AgregarColaboradorAsync(new AgregarColaboradorRequest(
            null, TipoDocumento.CC, $"M{Guid.NewGuid():N}".Substring(0, 16), "Multi", "PH",
            $"multi.{Guid.NewGuid():N}@test.co", null, cargo.Id, null, true, rolGlobalId),
            CancellationToken.None);

        Assert.Equal(3, colab.Asignaciones.Count);
        Assert.Contains(colab.Asignaciones, a => a.TenantId == tenantId1);
        Assert.Contains(colab.Asignaciones, a => a.TenantId == tenantId2);
        Assert.Contains(colab.Asignaciones, a => a.TenantId == tenantId3);

        await CleanupOrgAsync(orgId, tenantId1, tenantId2, tenantId3);
    }

    [Fact]
    public async Task Desactivar_colaborador_revoca_todas_sus_asignaciones_inmediatamente_RN04()
    {
        var (orgId, tenantId) = await SeedOrgConTenantAsync("Equipo Desact");
        var rolGlobalId = await SeedRolGlobalAsync("Rol Desact");
        var (svc, db, _) = BuildService(tenantId);

        var cargo = (await svc.ListarCargosAsync(CancellationToken.None))
            .First(c => c.Nombre == CargoCatalogoBase.Coordinador);
        var colab = await svc.AgregarColaboradorAsync(new AgregarColaboradorRequest(
            null, TipoDocumento.CC, $"D{Guid.NewGuid():N}".Substring(0, 16), "Desact", "Test",
            $"des.{Guid.NewGuid():N}@test.co", null, cargo.Id, null, true, rolGlobalId),
            CancellationToken.None);

        Assert.Single(colab.Asignaciones);  // 1 PH

        await svc.DesactivarColaboradorAsync(colab.Id,
            new DesactivarColaboradorRequest("Test", null), CancellationToken.None);

        var detalle = await svc.GetColaboradorAsync(colab.Id, CancellationToken.None);
        Assert.NotNull(detalle);
        Assert.Equal(EstadoColaborador.Inactivo, detalle!.Estado);
        Assert.Empty(detalle.Asignaciones);  // todas inactivas → no aparecen

        // Verificar en BD que fecha_hasta esta seteada
        var asignacionesBd = await db.OrgColaboradorCopropiedades.AsNoTracking()
            .Where(a => a.ColaboradorId == colab.Id).ToListAsync();
        Assert.All(asignacionesBd, a => Assert.False(a.Activo));
        Assert.All(asignacionesBd, a => Assert.NotNull(a.FechaHasta));

        await CleanupOrgAsync(orgId, tenantId);
    }

    [Fact]
    public async Task RN06_Cargo_con_colaboradores_activos_no_se_puede_eliminar()
    {
        var (orgId, tenantId) = await SeedOrgConTenantAsync("Equipo RN06");
        var (svc, _, _) = BuildService(tenantId);

        var cargos = await svc.ListarCargosAsync(CancellationToken.None);
        var asistente = cargos.First(c => c.Nombre == CargoCatalogoBase.AsistenteCartera);

        await svc.AgregarColaboradorAsync(new AgregarColaboradorRequest(
            null, TipoDocumento.CC, $"R{Guid.NewGuid():N}".Substring(0, 16), "RN06", "Test",
            $"rn06.{Guid.NewGuid():N}@test.co", null, asistente.Id, null, false, null),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.EliminarCargoAsync(asistente.Id, CancellationToken.None));
        Assert.Contains("colaboradores", ex.Message);

        await CleanupOrgAsync(orgId, tenantId);
    }

    [Fact]
    public async Task Override_individual_de_permiso_gana_sobre_plantilla_del_cargo()
    {
        var (orgId, tenantId) = await SeedOrgConTenantAsync("Equipo Permisos");
        var (svc, _, _) = BuildService(tenantId);

        var coordinador = (await svc.ListarCargosAsync(CancellationToken.None))
            .First(c => c.Nombre == CargoCatalogoBase.Coordinador);
        // Plantilla del Coordinador: GestionEquipo = LECTURA
        var colab = await svc.AgregarColaboradorAsync(new AgregarColaboradorRequest(
            null, TipoDocumento.CC, $"P{Guid.NewGuid():N}".Substring(0, 16), "Perm", "Test",
            $"perm.{Guid.NewGuid():N}@test.co", null, coordinador.Id, null, false, null),
            CancellationToken.None);

        // Verifico nivel base
        var antes = await svc.GetColaboradorAsync(colab.Id, CancellationToken.None);
        var permGestionAntes = antes!.PermisosEfectivos.First(p => p.Modulo == ModuloCapa1.GestionEquipo);
        Assert.Equal(NivelPermisoCapa1.Lectura, permGestionAntes.NivelEfectivo);
        Assert.False(permGestionAntes.TieneOverride);

        // Aplico override: Completo
        await svc.AjustarPermisoColaboradorAsync(colab.Id,
            new AjustarPermisoColaboradorRequest(ModuloCapa1.GestionEquipo, NivelPermisoCapa1.Completo),
            CancellationToken.None);

        var despues = await svc.GetColaboradorAsync(colab.Id, CancellationToken.None);
        var permGestion = despues!.PermisosEfectivos.First(p => p.Modulo == ModuloCapa1.GestionEquipo);
        Assert.Equal(NivelPermisoCapa1.Completo, permGestion.NivelEfectivo);
        Assert.True(permGestion.TieneOverride);
        Assert.Equal(NivelPermisoCapa1.Lectura, permGestion.NivelCargo);  // plantilla preservada

        // Reset → vuelve a la plantilla
        await svc.ResetearPermisosColaboradorAsync(colab.Id, CancellationToken.None);
        var reseteado = await svc.GetColaboradorAsync(colab.Id, CancellationToken.None);
        var permFinal = reseteado!.PermisosEfectivos.First(p => p.Modulo == ModuloCapa1.GestionEquipo);
        Assert.Equal(NivelPermisoCapa1.Lectura, permFinal.NivelEfectivo);
        Assert.False(permFinal.TieneOverride);

        await CleanupOrgAsync(orgId, tenantId);
    }

    [Fact]
    public async Task Historial_es_append_only_trigger_bloquea_update_y_delete()
    {
        var (orgId, tenantId) = await SeedOrgConTenantAsync("Equipo Audit");
        var (svc, db, _) = BuildService(tenantId);

        var cargo = (await svc.ListarCargosAsync(CancellationToken.None))
            .First(c => c.Nombre == CargoCatalogoBase.Director);
        var colab = await svc.AgregarColaboradorAsync(new AgregarColaboradorRequest(
            null, TipoDocumento.CC, $"H{Guid.NewGuid():N}".Substring(0, 16), "Hist", "Test",
            $"h.{Guid.NewGuid():N}@test.co", null, cargo.Id, null, false, null),
            CancellationToken.None);

        var eventoId = (await db.OrgColaboradorHistorial.AsNoTracking()
            .FirstAsync(h => h.ColaboradorId == colab.Id)).Id;

        // UPDATE directo en BD debe disparar el trigger append-only
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync(
                $"UPDATE org_colaborador_historial SET descripcion = 'alterado' WHERE id = {eventoId}"));

        // DELETE directo en BD tambien debe fallar
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync(
                $"DELETE FROM org_colaborador_historial WHERE id = {eventoId}"));

        await CleanupOrgAsync(orgId, tenantId);
    }

    // ---------------- Helpers ----------------

    private (IEquipoOrgService svc, PropiaDbContext db, IServiceScope scope) BuildService(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantCtx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var svc = new EquipoOrgService(db, tenantCtx, http, userMgr);
        return (svc, db, scope);
    }

    private static HttpContext BuildFakeHttpContext(Guid userId)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim("persona_id", userId.ToString())
        }, "test"));
        return ctx;
    }

    private async Task<(Guid OrgId, Guid TenantId)> SeedOrgConTenantAsync(string nombre)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var org = new Organizacion
        {
            Nombre = $"Org {nombre}",
            Tipo = TipoOrganizacion.Administradora
        };
        ctx.Organizaciones.Add(org);
        var t = new Tenant
        {
            Nombre = nombre,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.ConAdmin,
            OrganizacionId = org.Id
        };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return (org.Id, t.Id);
    }

    private async Task<Guid> SeedTenantParaOrgAsync(Guid orgId, string nombre)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var t = new Tenant
        {
            Nombre = nombre,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.ConAdmin,
            OrganizacionId = orgId
        };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task<Persona> SeedPersonaAsync(string nombres, string apellidos)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"P{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = nombres,
            Apellidos = apellidos,
            Email = $"p.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p;
    }

    private readonly List<Guid> _rolesCreados = new();

    private async Task<Guid> SeedRolGlobalAsync(string nombre)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var fullName = $"{nombre} {Guid.NewGuid():N}";
        var r = new Rol
        {
            // Personalizado en lugar de Base para NO contaminar el catalogo base global de 2.5
            Nombre = fullName.Length > 80 ? fullName.Substring(0, 80) : fullName,
            Tipo = TipoRol.Personalizado,
            EsEliminable = true,
            Activo = true,
            TenantId = null  // Global para que pueda asignarse a cualquier copropiedad
        };
        ctx.RolesCopropiedad.Add(r);
        await ctx.SaveChangesAsync();
        _rolesCreados.Add(r.Id);
        return r.Id;
    }

    private async Task CleanupOrgAsync(Guid orgId, params Guid[] tenantIds)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        // historial es append-only - los registros quedan huerfanos cuando se borra el colaborador (cascade)
        // pero el trigger bloquea DELETE. Borramos en cascada via colaboradores
        await ctx.Database.ExecuteSqlAsync(
            $"DELETE FROM org_colaborador_copropiedades WHERE colaborador_id IN (SELECT id FROM org_colaboradores WHERE organizacion_id = {orgId})");
        await ctx.Database.ExecuteSqlAsync(
            $"DELETE FROM org_colaborador_permisos WHERE colaborador_id IN (SELECT id FROM org_colaboradores WHERE organizacion_id = {orgId})");
        // el trigger no permite DELETE en historial - lo dejamos. cleanup parcial.
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE org_colaborador_historial DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync(
            $"DELETE FROM org_colaborador_historial WHERE colaborador_id IN (SELECT id FROM org_colaboradores WHERE organizacion_id = {orgId})");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE org_colaborador_historial ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM org_colaboradores WHERE organizacion_id = {orgId}");
        await ctx.Database.ExecuteSqlAsync(
            $"DELETE FROM org_cargo_permisos WHERE cargo_id IN (SELECT id FROM org_cargos WHERE organizacion_id = {orgId})");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM org_cargos WHERE organizacion_id = {orgId}");
        foreach (var tid in tenantIds)
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tid}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM organizaciones WHERE id = {orgId}");

        // Borrar roles creados por los helpers para no contaminar el catalogo entre tests
        foreach (var rid in _rolesCreados)
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM roles_copropiedad WHERE id = {rid}");
        _rolesCreados.Clear();
    }
}
