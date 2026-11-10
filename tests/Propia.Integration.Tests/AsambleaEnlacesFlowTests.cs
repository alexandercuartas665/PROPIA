using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.Asambleas;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Asambleas;
using Propia.Infrastructure.Persistence;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Asambleas Ola 1.5: envio por correo del enlace self-service. Fija a quien le llega (propietario
/// activo y apoderado con poder aprobado; no residente ni ex-dueno), que va un correo por persona con
/// todas sus unidades, que el enlace del correo valida para esa persona/sesion/tenant y que un fallo de
/// SMTP se reporta sin cortar el lote. Correo falso que captura; RLS activo (usuario de runtime).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AsambleaEnlacesFlowTests : IAsyncLifetime
{
    private const string BaseUrl = "https://propia.test";
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _adminUserId;
    private Guid _adminPersonaId;
    private readonly List<Guid> _personas = new();
    public AsambleaEnlacesFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _adminUserId = Guid.NewGuid();
        _adminPersonaId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddMemoryCache();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext(_adminUserId, _adminPersonaId) });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Destinatarios_son_propietarios_activos_y_apoderados_aprobados()
    {
        var c = await EscenarioAsync("Asam Enl Dest");
        var (_, enlaces, _) = Build(c.TenantId, new CorreoFalso());

        var dest = await enlaces.ResolverDestinatariosAsync(c.SesionId, null, CancellationToken.None);

        Assert.NotNull(dest);
        Assert.Equal(new[] { c.Apoderado, c.Dueno, c.OtroDueno }.OrderBy(x => x), dest!.Select(d => d.PersonaId).OrderBy(x => x));
        var dueno = dest.Single(d => d.PersonaId == c.Dueno);
        Assert.Equal(new[] { "100", "101" }, dueno.Unidades);
        Assert.Equal(new[] { "102" }, dest.Single(d => d.PersonaId == c.Apoderado).Unidades);
        Assert.Null(dest.Single(d => d.PersonaId == c.OtroDueno).Email);

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Un_correo_por_persona_con_enlace_valido_para_ella()
    {
        var c = await EscenarioAsync("Asam Enl Envio");
        var correo = new CorreoFalso();
        var (tokens, enlaces, _) = Build(c.TenantId, correo);

        var dest = await enlaces.ResolverDestinatariosAsync(c.SesionId, null, CancellationToken.None);
        var r = await enlaces.EnviarAsync(c.SesionId, dest!, BaseUrl, CancellationToken.None);

        Assert.Equal(2, r.Enviados);
        Assert.Equal(0, r.Fallidos);
        Assert.Equal(2, correo.Enviados.Count);
        Assert.Equal(new[] { c.EmailApoderado, c.EmailDueno }.OrderBy(x => x), correo.Enviados.Select(e => e.To).OrderBy(x => x));

        var alDueno = correo.Enviados.Single(e => e.To == c.EmailDueno);
        Assert.Contains("100, 101", alDueno.Html);
        Assert.Contains("Asamblea &lt;enlaces&gt;", alDueno.Html);
        var m = Regex.Match(alDueno.Html, $@"{Regex.Escape(BaseUrl)}/publico/asambleas/{c.TenantId}/([A-Za-z0-9_-]+)");
        Assert.True(m.Success, "El correo no trae el enlace publico.");
        var enlace = tokens.Validar(m.Groups[1].Value);
        Assert.Equal(new AsambleaEnlaceToken(c.TenantId, c.SesionId, c.Dueno), enlace);

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Filtro_por_persona_envia_solo_a_ella()
    {
        var c = await EscenarioAsync("Asam Enl Filtro");
        var correo = new CorreoFalso();
        var (_, enlaces, _) = Build(c.TenantId, correo);

        var dest = await enlaces.ResolverDestinatariosAsync(c.SesionId, new[] { c.Apoderado, c.Residente }, CancellationToken.None);
        var unico = Assert.Single(dest!);
        Assert.Equal(c.Apoderado, unico.PersonaId);

        await enlaces.EnviarAsync(c.SesionId, dest!, BaseUrl, CancellationToken.None);
        Assert.Equal(c.EmailApoderado, Assert.Single(correo.Enviados).To);

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Fallo_de_smtp_se_reporta_sin_cortar_el_lote()
    {
        var c = await EscenarioAsync("Asam Enl Fallo");
        var correo = new CorreoFalso { FallaPara = c.EmailDueno };
        var (_, enlaces, _) = Build(c.TenantId, correo);

        var dest = await enlaces.ResolverDestinatariosAsync(c.SesionId, null, CancellationToken.None);
        var r = await enlaces.EnviarAsync(c.SesionId, dest!, BaseUrl, CancellationToken.None);

        Assert.Equal(1, r.Enviados);
        Assert.Equal(1, r.Fallidos);
        Assert.Contains("SMTP caido", Assert.Single(r.Errores));

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Borrador_no_envia_y_sesion_inexistente_da_null()
    {
        var tenantId = await SeedTenantAsync("Asam Enl Borrador");
        await SeedAdminAsync();
        var (_, enlaces, svc) = Build(tenantId, new CorreoFalso());
        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "Borrador",
            DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "P", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            enlaces.ResolverDestinatariosAsync(s.Id, null, CancellationToken.None));
        Assert.Null(await enlaces.ResolverDestinatariosAsync(Guid.NewGuid(), null, CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Smtp_caido_se_detecta_en_el_momento_y_no_programa_nada()
    {
        var c = await EscenarioAsync("Asam Enl Smtp");
        var correo = new CorreoFalso { FallaTodo = true };
        var (_, enlaces, _) = Build(c.TenantId, correo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            enlaces.ProgramarEnvioAsync(c.SesionId, null, BaseUrl, CancellationToken.None));

        Assert.Contains("SMTP caido", ex.Message);
        var estado = enlaces.EstadoEnvio(c.SesionId);
        Assert.NotNull(estado);
        Assert.Equal(0, estado!.Enviados);
        Assert.Equal(2, estado.Fallidos);   // los 2 con correo se intentaron en el momento
        Assert.False(estado.EnCurso);

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Primer_correo_sale_en_el_momento_y_queda_estado()
    {
        var c = await EscenarioAsync("Asam Enl Estado");
        var correo = new CorreoFalso();
        var (_, enlaces, _) = Build(c.TenantId, correo);

        var r = await enlaces.ProgramarEnvioAsync(c.SesionId, null, BaseUrl, CancellationToken.None);

        Assert.Equal(2, r!.ConCorreo);
        Assert.NotEmpty(correo.Enviados);   // la prueba salio dentro del request
        var estado = enlaces.EstadoEnvio(c.SesionId);
        Assert.NotNull(estado);
        Assert.Equal(2, estado!.Total);
        Assert.True(estado.Enviados >= 1);

        await CleanTenant(c.TenantId);
    }

    // ===================== Escenario =====================

    /// <summary>
    /// 3 unidades: Dueno (con correo) es Propietario de la 100 y la 101 y Residente vive en la 100;
    /// OtroDueno (sin correo) es Propietario de la 102 y ExDueno tiene vinculo inactivo en ella; el
    /// Apoderado tiene poder digital (aprobado) de la 102. Sesion citada.
    /// </summary>
    private async Task<Escenario> EscenarioAsync(string nombre)
    {
        var tenantId = await SeedTenantAsync(nombre);
        await SeedAdminAsync();
        var unidades = await SeedUnidadesAsync(tenantId, new[] { 0.5m, 0.3m, 0.2m });
        var emailDueno = $"dueno.{Guid.NewGuid():N}@test.co";
        var emailApoderado = $"apoderado.{Guid.NewGuid():N}@test.co";
        var dueno = await SeedPersonaAsync("Dueno", emailDueno);
        var otroDueno = await SeedPersonaAsync("OtroDueno", null);
        var exDueno = await SeedPersonaAsync("ExDueno", $"ex.{Guid.NewGuid():N}@test.co");
        var residente = await SeedPersonaAsync("Residente", $"res.{Guid.NewGuid():N}@test.co");
        var apoderado = await SeedPersonaAsync("Apoderado", emailApoderado);
        await SeedVinculoAsync(tenantId, unidades[0], dueno, RolUnidadPersona.Propietario, true);
        await SeedVinculoAsync(tenantId, unidades[1], dueno, RolUnidadPersona.Propietario, true);
        await SeedVinculoAsync(tenantId, unidades[0], residente, RolUnidadPersona.Residente, true);
        await SeedVinculoAsync(tenantId, unidades[2], otroDueno, RolUnidadPersona.Propietario, true);
        await SeedVinculoAsync(tenantId, unidades[2], exDueno, RolUnidadPersona.Propietario, false);

        var (_, _, svc) = Build(tenantId, new CorreoFalso());
        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "Asamblea <enlaces>",
            DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "Aprobar presupuesto", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);
        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), CancellationToken.None);
        await svc.OtorgarPoderAsync(s.Id,
            new OtorgarPoderRequest(unidades[2], apoderado, TipoPoder.Digital, null), CancellationToken.None);

        return new Escenario(tenantId, s.Id, dueno, otroDueno, residente, apoderado, emailDueno, emailApoderado);
    }

    private record Escenario(Guid TenantId, Guid SesionId, Guid Dueno, Guid OtroDueno, Guid Residente,
        Guid Apoderado, string EmailDueno, string EmailApoderado);

    /// <summary>IEmailSender que captura los correos; puede fallar para una direccion.</summary>
    private sealed class CorreoFalso : IEmailSender
    {
        public List<(string To, string Subject, string Html)> Enviados { get; } = new();
        public string? FallaPara { get; init; }
        public bool FallaTodo { get; init; }

        public Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            if (FallaTodo || string.Equals(toEmail, FallaPara, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new EmailSendResult(false, "SMTP caido"));
            Enviados.Add((toEmail, subject, htmlBody));
            return Task.FromResult(new EmailSendResult(true, null));
        }
    }

    // ===================== Helpers =====================

    private (IAsambleaTokenService tokens, IAsambleaEnlacesService enlaces, IAsambleaService svc) Build(Guid tenantId, IEmailSender correo)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var tokens = new AsambleaTokenService(scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
        var enlaces = new AsambleaEnlacesService(db, ctx, tokens, correo,
            scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            scope.ServiceProvider.GetRequiredService<IMemoryCache>(), NullLogger<AsambleaEnlacesService>.Instance);
        return (tokens, enlaces, new AsambleaService(db, ctx, http));
    }

    private static HttpContext BuildFakeHttpContext(Guid userId, Guid personaId)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim("persona_id", personaId.ToString())
        }, "test"));
        return ctx;
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task SeedAdminAsync()
    {
        await using var ctx = OwnerDb();
        var email = $"admin.{Guid.NewGuid():N}@test.co";
        ctx.Personas.Add(new Persona
        {
            Id = _adminPersonaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"A{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Admin",
            Apellidos = "Test",
            Email = email,
            PerfilIncompleto = false
        });
        ctx.Users.Add(new ApplicationUser
        {
            Id = _adminUserId,
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpper(),
            NormalizedEmail = email.ToUpper(),
            EmailConfirmed = true,
            PersonaId = _adminPersonaId,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await ctx.SaveChangesAsync();
    }

    private async Task<Guid> SeedPersonaAsync(string nombre, string? email)
    {
        await using var ctx = OwnerDb();
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"E{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = nombre,
            Apellidos = "Test",
            Email = email,
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        _personas.Add(p.Id);
        return p.Id;
    }

    private async Task<Guid[]> SeedUnidadesAsync(Guid tenantId, decimal[] coeficientes)
    {
        await using var ctx = OwnerDb();
        var ids = new List<Guid>();
        for (int i = 0; i < coeficientes.Length; i++)
        {
            var u = new UnidadPrivada { TenantId = tenantId, Numero = $"{100 + i}", CoeficientePropiedad = coeficientes[i] };
            ctx.UnidadesPrivadas.Add(u);
            ids.Add(u.Id);
        }
        await ctx.SaveChangesAsync();
        return ids.ToArray();
    }

    private async Task SeedVinculoAsync(Guid tenantId, Guid unidadId, Guid personaId, RolUnidadPersona rol, bool activo)
    {
        await using var ctx = OwnerDb();
        ctx.UnidadPersonas.Add(new UnidadPersona
        {
            TenantId = tenantId,
            UnidadId = unidadId,
            PersonaId = personaId,
            Rol = rol,
            Activo = activo
        });
        await ctx.SaveChangesAsync();
    }

    private async Task CleanTenant(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE sesion_quorum_log DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_quorum_log WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE sesion_quorum_log ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM votos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM votaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM actas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_poderes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_participantes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_puntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesiones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asamblea_config WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_personas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_adminUserId}");
        foreach (var id in _personas.Append(_adminPersonaId))
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {id}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
