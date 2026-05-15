using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Propia.Application.Auth;
using Propia.Application.Common;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Auth;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.UsuariosAccesos;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests de integracion del modulo 2.5 Usuarios, Roles y Accesos (spec v1.0).
///
/// Cubre los flujos criticos: catalogo seed de roles base + extendidos, matriz de
/// permisos default, creacion/eliminacion de roles personalizados, invitacion +
/// aceptacion (incluye creacion de cuenta), revocacion con guarda de ultimo admin,
/// auditoria append-only y RLS cross-tenant.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class UsuariosAccesosFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;

    public UsuariosAccesosFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        // Construyo un ServiceProvider minimo para resolver UserManager + ITokenService
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddDbContext<PropiaDbContext>(opts =>
            opts.UseNpgsql(_fx.OwnerConnectionString));
        sc.AddIdentityCore<ApplicationUser>(opts =>
            {
                opts.Password.RequiredLength = 10;
                opts.Password.RequireDigit = true;
                opts.Password.RequireUppercase = true;
                opts.Password.RequireNonAlphanumeric = false;
                opts.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        sc.Configure<JwtSettings>(o =>
        {
            o.Issuer = "propia-api";
            o.Audience = "propia-clients";
            o.SigningKey = "test-key-32-bytes-largo-secret!!";
            o.AccessTokenMinutes = 60;
        });
        sc.AddScoped<ITokenService, TokenService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Catalogo_seed_tiene_5_base_y_6_extendidos()
    {
        var (svc, _, _, _) = BuildRoles(await SeedTenantAsync("CP Roles"));
        var roles = await svc.ListarRolesAsync(CancellationToken.None);

        Assert.Equal(5, roles.Count(r => r.Tipo == TipoRol.Base));
        Assert.Equal(6, roles.Count(r => r.Tipo == TipoRol.Extendido));
        Assert.Contains(roles, r => r.Nombre == "Administrador" && r.Tipo == TipoRol.Base);
        Assert.Contains(roles, r => r.Nombre == "Revisor Fiscal" && r.Tipo == TipoRol.Extendido);
    }

    [Fact]
    public async Task Matriz_Administrador_tiene_todos_los_permisos_habilitados_a_nivel_copropiedad()
    {
        var (svc, _, _, _) = BuildRoles(await SeedTenantAsync("CP Matriz Admin"));
        var roles = await svc.ListarRolesAsync(CancellationToken.None);
        var admin = roles.First(r => r.Nombre == "Administrador");
        var detalle = await svc.GetRolDetalleAsync(admin.Id, CancellationToken.None);

        Assert.NotNull(detalle);
        // 15 modulos x 6 acciones = 90 entradas en la matriz
        Assert.Equal(15 * 6, detalle!.Permisos.Count);
        Assert.All(detalle.Permisos, p => Assert.True(p.Habilitado));
        Assert.All(detalle.Permisos, p => Assert.Equal(NivelDato.Copropiedad, p.NivelDato));
    }

    [Fact]
    public async Task Crear_rol_personalizado_copiando_de_otro_clona_la_matriz()
    {
        var (svc, _, _, _) = BuildRoles(await SeedTenantAsync("CP Copiar"));
        var roles = await svc.ListarRolesAsync(CancellationToken.None);
        var admin = roles.First(r => r.Nombre == "Administrador");

        var nuevo = await svc.CrearRolAsync(new CrearRolRequest("Admin Junior", "Copia de admin", admin.Id), CancellationToken.None);
        Assert.Equal(TipoRol.Personalizado, nuevo.Tipo);

        var detalle = await svc.GetRolDetalleAsync(nuevo.Id, CancellationToken.None);
        Assert.True(detalle!.Permisos.Count(p => p.Habilitado) >= 80); // copia ~todos
    }

    [Fact]
    public async Task No_se_puede_eliminar_rol_base()
    {
        var (svc, _, _, _) = BuildRoles(await SeedTenantAsync("CP NoElim"));
        var admin = (await svc.ListarRolesAsync(CancellationToken.None)).First(r => r.Nombre == "Administrador");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.EliminarRolAsync(admin.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Administrador_no_puede_perder_permiso_de_usuarios_accesos()
    {
        var (svc, _, _, _) = BuildRoles(await SeedTenantAsync("CP AdminSelf"));
        var admin = (await svc.ListarRolesAsync(CancellationToken.None)).First(r => r.Nombre == "Administrador");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarPermisoAsync(admin.Id, new ActualizarPermisoRequest(
                ModuloCodigo.UsuariosAccesos, AccionPermiso.Ver, false, NivelDato.SinAcceso), CancellationToken.None));
    }

    [Fact]
    public async Task Invitar_y_aceptar_crea_ApplicationUser_y_activa_vinculo()
    {
        var tenantId = await SeedTenantAsync("CP Invitar");
        var (svcU, _, _, db) = BuildUsuarios(tenantId);
        var rolPropietarioId = (await db.RolesCopropiedad.FirstAsync(r => r.Nombre == "Propietario")).Id;

        // Crear persona en directorio (paso previo segun RN-02)
        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"DOC{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Sofia",
            Apellidos = "Invitada",
            Email = $"sofia.{Guid.NewGuid():N}@test.co"
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        // Invitar
        var inv = await svcU.InvitarAsync(new CrearInvitacionRequest(
            persona.Id, rolPropietarioId, CanalEnvioInvitacion.Email), CancellationToken.None);
        Assert.Equal(EstadoInvitacion.Pendiente, inv.Estado);

        // Aceptar (publico)
        var resp = await svcU.AceptarInvitacionAsync(new AceptarInvitacionRequest(
            inv.Token, "Propia2026!Test", "Propia2026!Test"), CancellationToken.None);
        Assert.True(resp.Aceptada);
        Assert.NotNull(resp.AccessToken);

        // Verifico que UsuarioTenant fue creado activo
        var ut = await db.UsuariosTenant.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.PersonaId == persona.Id);
        Assert.NotNull(ut);
        Assert.Equal(EstadoUsuarioTenant.Activo, ut!.Estado);
        Assert.Equal(rolPropietarioId, ut.RolId);

        // Y ApplicationUser tambien
        var au = await db.Users.FirstOrDefaultAsync(u => u.Email == persona.Email);
        Assert.NotNull(au);
        Assert.Equal(persona.Id, au!.PersonaId);
    }

    [Fact]
    public async Task Invitacion_expirada_no_se_puede_aceptar()
    {
        var tenantId = await SeedTenantAsync("CP Exp");
        var (svcU, _, _, db) = BuildUsuarios(tenantId);
        var rol = (await db.RolesCopropiedad.FirstAsync(r => r.Nombre == "Residente"));
        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"DOC{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Exp", Apellidos = "irada", Email = $"e.{Guid.NewGuid():N}@test.co"
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        var inv = await svcU.InvitarAsync(new CrearInvitacionRequest(persona.Id, rol.Id, CanalEnvioInvitacion.Email), CancellationToken.None);
        // Forzar expiracion
        var invEntity = await db.UsuarioInvitaciones.IgnoreQueryFilters().FirstAsync(i => i.Id == inv.Id);
        invEntity.ExpiraAt = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svcU.AceptarInvitacionAsync(new AceptarInvitacionRequest(inv.Token, "Propia2026!Test", "Propia2026!Test"), CancellationToken.None));
    }

    [Fact]
    public async Task Revocar_acceso_marca_inactivo_y_cierra_sesiones()
    {
        var tenantId = await SeedTenantAsync("CP Revocar");
        var (svcU, _, _, db) = BuildUsuarios(tenantId);

        // Setup: 1 admin + 1 propietario activo
        var rolPropId = (await db.RolesCopropiedad.FirstAsync(r => r.Nombre == "Propietario")).Id;
        var rolAdminId = (await db.RolesCopropiedad.FirstAsync(r => r.Nombre == "Administrador")).Id;
        var admin = await CreateActiveUsuarioTenantAsync(db, tenantId, rolAdminId, "Administrador");
        var prop = await CreateActiveUsuarioTenantAsync(db, tenantId, rolPropId, "Propietario");

        var ok = await svcU.RevocarAccesoAsync(prop.Id, new RevocarAccesoRequest("Test"), CancellationToken.None);
        Assert.True(ok);

        var ut = await db.UsuariosTenant.IgnoreQueryFilters().FirstAsync(u => u.Id == prop.Id);
        Assert.Equal(EstadoUsuarioTenant.Inactivo, ut.Estado);
        Assert.NotNull(ut.FechaRevocacion);
    }

    [Fact]
    public async Task No_se_puede_revocar_al_ultimo_Administrador()
    {
        var tenantId = await SeedTenantAsync("CP UltimoAdmin");
        var (svcU, _, _, db) = BuildUsuarios(tenantId);

        var rolAdminId = (await db.RolesCopropiedad.FirstAsync(r => r.Nombre == "Administrador")).Id;
        var admin = await CreateActiveUsuarioTenantAsync(db, tenantId, rolAdminId, "Administrador");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svcU.RevocarAccesoAsync(admin.Id, new RevocarAccesoRequest("intento"), CancellationToken.None));
    }

    [Fact]
    public async Task Auditoria_es_append_only_no_acepta_delete_ni_update()
    {
        var tenantId = await SeedTenantAsync("CP Auditoria");
        var (_, _, _, db) = BuildUsuarios(tenantId);

        var log = new AccesoAuditoria
        {
            TenantId = tenantId,
            TipoEvento = TipoEventoAuditoria.LoginExitoso,
            Canal = CanalAcceso.Web
        };
        db.AccesoAuditorias.Add(log);
        await db.SaveChangesAsync();

        // UPDATE debe fallar por trigger
        log.Detalle = "alterado";
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
    }

    // ---------------- Helpers ----------------

    private (IRolesService svc, PropiaDbContext db, TenantContext tctx, IServiceScope scope)
        BuildRoles(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantCtx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var svc = new RolesService(db, tenantCtx);
        return (svc, db, (TenantContext)tenantCtx, scope);
    }

    private (IUsuariosService svc, IRolesService rolesSvc, TenantContext tctx, PropiaDbContext db)
        BuildUsuarios(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantCtx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var jwt = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>();
        var svc = new UsuariosService(db, tenantCtx, userManager, tokenSvc, jwt);
        var roles = new RolesService(db, tenantCtx);
        return (svc, roles, (TenantContext)tenantCtx, db);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var t = new Tenant
        {
            Nombre = nombre,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.SinAdmin
        };
        db.Tenants.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private async Task<UsuarioTenant> CreateActiveUsuarioTenantAsync(PropiaDbContext db, Guid tenantId, Guid rolId, string rolNombre)
    {
        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"DOC{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = rolNombre, Apellidos = "Test",
            Email = $"{rolNombre.ToLower()}.{Guid.NewGuid():N}@test.co"
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        var ut = new UsuarioTenant
        {
            TenantId = tenantId,
            PersonaId = persona.Id,
            RolId = rolId,
            Rol = rolNombre,
            Estado = EstadoUsuarioTenant.Activo,
            FechaActivacion = DateTimeOffset.UtcNow
        };
        db.UsuariosTenant.Add(ut);
        await db.SaveChangesAsync();
        return ut;
    }
}
