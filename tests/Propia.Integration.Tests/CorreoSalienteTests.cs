using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.Common;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Infrastructure.Email;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Correo saliente de la plataforma: limite de tiempo duro (un SMTP que no responde ya no cuelga el request
/// ~5 min), proveedor SendGrid por API HTTPS seleccionable por configuracion y el diagnostico por API.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CorreoSalienteTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private Factory _factory = null!;
    private EmailConfig? _original;
    private Guid? _tokenId;

    public CorreoSalienteTests(PostgresFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        _factory = new Factory(_fx.OwnerConnectionString);
        await using var db = OwnerDb();
        // Guarda: la API de pruebas debe apuntar al contenedor, nunca a propia_dev (ver ApiFactory).
        await using (var scope = _factory.Services.CreateAsyncScope())
            Assert.Equal(new Npgsql.NpgsqlConnectionStringBuilder(_fx.OwnerConnectionString).Port,
                new Npgsql.NpgsqlConnectionStringBuilder(scope.ServiceProvider.GetRequiredService<PropiaDbContext>().Database.GetConnectionString()).Port);
        _original = await db.EmailConfigs.AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var db = OwnerDb())
        {
            await db.EmailConfigs.ExecuteDeleteAsync();
            if (_original is not null) { db.EmailConfigs.Add(_original); await db.SaveChangesAsync(); }
            if (_tokenId is Guid t) await db.Database.ExecuteSqlRawAsync("DELETE FROM super_admin_api_tokens WHERE id = {0}", t);
        }
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Smtp_que_acepta_la_conexion_y_no_responde_falla_por_timeout_dentro_del_limite()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var abiertos = new List<TcpClient>();
        _ = Task.Run(async () => { try { while (true) abiertos.Add(await listener.AcceptTcpClientAsync()); } catch { } });
        var puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            await GuardarConfigAsync(ProveedorCorreo.Smtp, "127.0.0.1", puerto, "apikey", "clave-falsa");
            await using var scope = _factory.Services.CreateAsyncScope();
            var sender = Sender(scope, new FakeHttp(_ => throw new InvalidOperationException("no debe usar HTTP")));

            var sw = Stopwatch.StartNew();
            var r = await sender.ProbarAsync("destino@example.com");
            sw.Stop();

            Assert.False(r.Ok);
            Assert.Equal(ProveedorCorreo.Smtp, r.Proveedor);
            Assert.Contains($"SMTP timeout (127.0.0.1:{puerto})", r.Error);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"tardo {sw.Elapsed}");
            Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(1.5), $"no espero el limite: {sw.Elapsed}");
            Assert.Equal(puerto, r.Puerto);

            // IEmailSender (lo que usan invitaciones, olvide mi clave, etc.) falla igual de rapido y sin lanzar.
            var envio = await sender.SendAsync("destino@example.com", "x", "<p>x</p>");
            Assert.False(envio.Success);
            Assert.Contains("SMTP timeout", envio.Error);
        }
        finally
        {
            listener.Stop();
            foreach (var c in abiertos) c.Dispose();
        }
    }

    [Fact]
    public async Task Proveedor_sendgrid_api_envia_por_https_con_la_api_key_y_reporta_errores()
    {
        await GuardarConfigAsync(ProveedorCorreo.SendGridApi, null, 587, null, "SG.key-de-prueba");
        HttpRequestMessage? capturado = null;
        string? json = null;
        var http = new FakeHttp(req =>
        {
            capturado = req;
            json = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var r = await Sender(scope, http).ProbarAsync("destino@example.com");
            Assert.True(r.Ok, r.Error);
            Assert.Equal(ProveedorCorreo.SendGridApi, r.Proveedor);
            Assert.Equal("api.sendgrid.com", r.Host);
            Assert.Equal(443, r.Puerto);
        }
        Assert.Equal("https://api.sendgrid.com/v3/mail/send", capturado!.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "SG.key-de-prueba"), capturado.Headers.Authorization);
        Assert.Contains("\"email\":\"destino@example.com\"", json);
        Assert.Contains("\"email\":\"no-reply@propia.test\"", json);
        Assert.Contains("text/html", json);

        // Rechazo de SendGrid (key sin permiso): error claro, sin lanzar.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var r = await Sender(scope, new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            { Content = new StringContent("{\"errors\":[{\"message\":\"permission denied\"}]}") })).SendAsync("destino@example.com", "x", "<p>x</p>");
            Assert.False(r.Success);
            Assert.Contains("SendGrid API respondio 401", r.Error);
        }

        // API que no contesta: tambien corta en el limite.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sw = Stopwatch.StartNew();
            var r = await Sender(scope, new FakeHttp(null, colgar: true)).ProbarAsync("destino@example.com");
            Assert.False(r.Ok);
            Assert.Contains("SendGrid API timeout", r.Error);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"tardo {sw.Elapsed}");
        }
    }

    [Fact]
    public async Task Diagnostico_por_api_acepta_token_pak_y_devuelve_proveedor_y_error()
    {
        await using (var db = OwnerDb())
        {
            await db.EmailConfigs.ExecuteDeleteAsync();
            var founder = await db.SuperAdminUsuarios.FirstAsync(u => u.Email == "founder@adgroup.com.co");
            if (founder.MfaConfigurado || !founder.Activo)
            {
                founder.MfaConfigurado = false; founder.MfaSecret = null; founder.Activo = true;
                await db.SaveChangesAsync();
            }
        }
        var sesion = _factory.CreateClient();
        var login = await (await sesion.PostAsJsonAsync("/admin/login", new SuperAdminLoginRequest("founder@adgroup.com.co", "PropiaFounder2026!")))
            .Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        sesion.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        var token = (await (await sesion.PostAsJsonAsync("/api/admin/api-tokens", new CrearApiTokenRequest("[TEST] correo", 1)))
            .Content.ReadFromJsonAsync<ApiTokenCreadoDto>())!;
        _tokenId = token.Info.Id;
        var api = _factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonAsync("/api/admin/email/probar", new { to = "" })).StatusCode);
        var r = await api.PostAsJsonAsync("/api/admin/email/probar", new { to = "destino@example.com" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var dto = (await r.Content.ReadFromJsonAsync<EmailProbeResult>())!;
        Assert.False(dto.Ok);
        Assert.Equal(ProveedorCorreo.Smtp, dto.Proveedor);
        Assert.Contains("no esta configurado", dto.Error);

        // Sin autenticacion no se puede usar.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/api/admin/email/probar", new { to = "a@b.co" })).StatusCode);
    }

    // ===================== Helpers =====================

    private PlataformaEmailSender Sender(AsyncServiceScope scope, IHttpClientFactory http)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Email:TimeoutSegundos"] = "2" }).Build();
        return new PlataformaEmailSender(OwnerDb(), scope.ServiceProvider.GetRequiredService<ISecretProtector>(), http, config,
            NullLogger<PlataformaEmailSender>.Instance);
    }

    private async Task GuardarConfigAsync(string proveedor, string? host, int puerto, string? usuario, string clave)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        await using var db = OwnerDb();
        await db.EmailConfigs.ExecuteDeleteAsync();
        db.EmailConfigs.Add(new EmailConfig
        {
            Proveedor = proveedor,
            SmtpHost = host,
            SmtpPort = puerto,
            SmtpUser = usuario,
            UseSsl = false,
            SmtpPasswordEncrypted = protector.Protect(clave),
            FromEmail = "no-reply@propia.test",
            FromName = "PROPIA test",
            IsEnabled = true
        });
        await db.SaveChangesAsync();
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private sealed class Factory : AuthFlowTests.ApiFactory
    {
        private readonly string _conn;
        public Factory(string conn) : base(conn) => _conn = conn;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ConnectionStrings:Propia", _conn);
            builder.UseSetting("Jobs:Enabled", "false");
            builder.UseSetting("Email:TimeoutSegundos", "2");
        }
    }

    private sealed class FakeHttp : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage>? _responder;
        private readonly bool _colgar;
        public FakeHttp(Func<HttpRequestMessage, HttpResponseMessage>? responder, bool colgar = false)
        { _responder = responder; _colgar = colgar; }
        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler : HttpMessageHandler
        {
            private readonly FakeHttp _f;
            public Handler(FakeHttp f) => _f = f;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (_f._colgar) await Task.Delay(Timeout.Infinite, ct);
                return _f._responder!(request);
            }
        }
    }
}
