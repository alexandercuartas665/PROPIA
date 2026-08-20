using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Propia.Api.Controllers;
using Propia.Api.Middleware;
using Propia.Infrastructure;
using Propia.Infrastructure.Auth;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.SuperAdmin;
using Propia.Web.Components;
using Serilog;
using Serilog.Formatting.Compact;

// ============================================================================
// HOST UNIFICADO: la Web (Blazor Server + WASM) hostea TAMBIEN la API REST,
// SignalR, el servidor MCP, los jobs y /uploads. Un solo proceso, un solo
// servicio Railway, un solo origen -> las URLs /uploads/* caen en la misma app
// que las guardo (arregla las imagenes de raiz). Fusion de los dos Program.cs.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// ---- Logging estructurado (Serilog): stdout + file rotativo ----
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Propia")
        .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(new CompactJsonFormatter());

    var logsDir = ctx.Configuration["Serilog:FileSink:Directory"] ?? "logs";
    try
    {
        if (!Path.IsPathRooted(logsDir))
            logsDir = Path.Combine(ctx.HostingEnvironment.ContentRootPath, logsDir);
        Directory.CreateDirectory(logsDir);
        lc.WriteTo.File(
            new CompactJsonFormatter(),
            Path.Combine(logsDir, "propia-.log"),
            rollingInterval: Serilog.RollingInterval.Day,
            retainedFileCountLimit: 7,
            fileSizeLimitBytes: 50 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            shared: false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Serilog file sink] no se pudo configurar (se sigue con consola): {ex.Message}");
    }
});

// ==================== Servicios: UI (Blazor) ====================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o =>
    {
        if (builder.Environment.IsDevelopment()) o.DetailedErrors = true;
        // Retener el circuito desconectado varios minutos: si el usuario deja la pestana quieta
        // o hay un microcorte de red, al volver se reengancha al MISMO circuito (conserva estado)
        // en vez de fallar y mostrar "Recargar pagina".
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
        o.DisconnectedCircuitMaxRetained = 200;
    })
    .AddHubOptions(o =>
    {
        // Tolerar pestanas en segundo plano (timers throttled por el navegador) sin cortar el
        // circuito tan rapido: es la causa mas comun del "Reconectando..." al dejar quieta la app.
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
        o.HandshakeTimeout = TimeSpan.FromSeconds(30);
        o.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    })
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddScoped<Propia.Web.Components.Shared.Modals.IModalService,
                          Propia.Web.Components.Shared.Modals.ModalService>();

// HttpClient hacia la propia API. Ahora vive en el MISMO proceso; en dev apunta a
// la propia app (loopback), en prod a PropiaApi:BaseUrl (su propio origen interno).
builder.Services.AddHttpClient("PropiaApi", client =>
{
    var baseUrl = builder.Configuration["PropiaApi:BaseUrl"] ?? "https://localhost:7113";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var handler = new HttpClientHandler();
    if (env.IsDevelopment())
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

// ==================== Servicios: API (controllers, infra, signalr, mcp, jobs) ====================
// AddApplicationPart: los controllers viven en el ensamblado Propia.Api (referenciado),
// no en el ensamblado de entrada, asi que hay que registrarlos explicitamente.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AdminController).Assembly);
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<Propia.Application.InfraestructuraIa.IChatBroadcaster, Propia.Api.Hubs.ChatBroadcaster>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PropiaDbContext>("postgresql", tags: new[] { "ready" });

// Background jobs (scheduler como IHostedService + Singleton tipado).
builder.Services.AddSingleton<Propia.Infrastructure.Jobs.BackgroundJobScheduler>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<Propia.Infrastructure.Jobs.BackgroundJobScheduler>());

// Cola de despacho del agente IA (auto-respuesta a entrantes). El Singleton lo registra
// AddInfrastructure; aqui lo arrancamos como IHostedService (el bucle que agrupa rafagas y despacha).
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<Propia.Infrastructure.InfraestructuraIa.AgentDispatchQueue>());

// Forwarded Headers (proxy de Railway / cualquier reverse proxy).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// CORS: con host unificado el navegador llama al MISMO origen (no hace falta), pero
// se mantiene para origenes configurados por si algun cliente externo consume la API.
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
    else
    {
        policy.WithOrigins(
                "https://localhost:7113", "http://localhost:5105",
                "https://app.propia.cubot.com.co")
              .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

// Autenticacion JWT (para la API). No interfiere con Blazor: las peticiones de pagina
// no llevan Bearer, quedan anonimas y las paginas validan sesion client-side (SessionHelper).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((bearer, jwtOpts) =>
    {
        var jwt = jwtOpts.Value;
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey ausente o muy corta (min 32 chars).");

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // SignalR over WebSocket no permite headers: el token viaja en query string.
        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminController.SuperAdminPolicy, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("is_super_admin", "true"));
});

// ---- Servidor MCP (tool-layer para agentes) montado en /mcp ----
var mcpJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
};
mcpJsonOptions.Converters.Add(new JsonStringEnumConverter());
builder.Services.AddMcpServer()
    .WithHttpTransport(o => { o.Stateless = true; })
    .WithTools<Propia.Api.Mcp.MiCopropiedadConsultaTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.MiCopropiedadCreacionTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.MiCopropiedadEdicionTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.ServiciosConsultaTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.ServiciosCreacionTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.VerificarResidenciaTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.PqrsdAgenteTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.TareasAgenteTools>(mcpJsonOptions);

var app = builder.Build();

// ==================== Pipeline ====================
app.UseForwardedHeaders();

// Bootstrap del founder SuperAdmin (prod, idempotente desde env vars). No-op si no estan.
await SuperAdminSeeder.EnsureBootstrapFounderAsync(app.Services, app.Configuration);

if (app.Environment.IsDevelopment())
{
    await SuperAdminSeeder.EnsureDevFounderAsync(app.Services);
    await DemoSeeder.EnsureDemoDataAsync(app.Services);
    await AgenteDocumentalSeeder.EnsureAsync(app.Services);
    await AuxiliarAdministrativoSeeder.EnsureAsync(app.Services);
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
    app.UseHttpsRedirection();  // dev usa cert self-signed
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();  // TLS lo termina el proxy de Railway; el contenedor escucha HTTP.
}

// Servir imagenes subidas via /uploads/* en TODOS los entornos (la Web unificada las sirve).
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"));
app.UseStaticFiles();

// Routing explicito antes de metricas/auth.
app.UseRouting();
app.UseHttpMetrics();  // Prometheus: nombre de ruta normalizada
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Antiforgery (requerido por Blazor). Va tras routing/auth y antes de los endpoints.
// No bloquea los controllers [ApiController] (no tienen metadata de antiforgery).
app.UseAntiforgery();

// TenantMiddleware: NO en health ni en la infraestructura de Blazor (_blazor / _framework).
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/health")
        && !ctx.Request.Path.StartsWithSegments("/_blazor")
        && !ctx.Request.Path.StartsWithSegments("/_framework"),
    branch =>
    {
        branch.UseMiddleware<TenantMiddleware>();
        // Lee X-Contact-Phone / X-Conversation-Id (server-set por el McpGateway) para las tools MCP.
        branch.UseMiddleware<Propia.Api.Middleware.AgentCallContextMiddleware>();
    });

// ---- Endpoints: API ----
app.MapControllers();
app.MapHub<Propia.Api.Hubs.ChatHub>("/hubs/chat");
app.MapMcp("/mcp").RequireAuthorization();
app.MapMetrics("/metrics");

// ---- Endpoints: Blazor ----
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Propia.Web.Client._Imports).Assembly);

// ---- Health checks ----
var startedAt = DateTimeOffset.UtcNow;
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0-pilot",
    uptimeSeconds = (int)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
    serverTime = DateTimeOffset.UtcNow
}));

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                durationMs = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(json);
    }
});

app.Run();
