using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Prometheus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Propia.Api.Controllers;
using Propia.Api.Middleware;
using Propia.Infrastructure;
using Propia.Infrastructure.Auth;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.SuperAdmin;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging estructurado: stdout (Railway captura nativamente) + file rotativo ----
// Frente C observabilidad: el archivo rotativo da resilience offline (si el agregador
// externo cae, no perdemos logs). En piloto se rota diario + se mantienen 7 dias.
// En produccion se cambia o anade sink a Grafana Loki / Better Stack.
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Propia.Api")
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
            Path.Combine(logsDir, "propia-api-.log"),
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

// ---- Servicios ----
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<Propia.Application.InfraestructuraIa.IChatBroadcaster, Propia.Api.Hubs.ChatBroadcaster>();

// ---- Health checks enriquecidos (Frente C) ----
// Mas alla de /health (liveness simple), agregamos /health/ready con check real
// del DbContext via Microsoft.Extensions.Diagnostics.HealthChecks.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PropiaDbContext>("postgresql", tags: new[] { "ready" });

// Background jobs (BackgroundJobScheduler como IHostedService + Singleton tipado).
// Solo en la API porque Web/SuperAdmin no necesitan ejecutar jobs - los consumen
// via API. El Singleton tipado permite inyectarlo en controllers (admin manual
// trigger) y tests.
builder.Services.AddSingleton<Propia.Infrastructure.Jobs.BackgroundJobScheduler>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<Propia.Infrastructure.Jobs.BackgroundJobScheduler>());

// Cola de despacho del agente IA (auto-respuesta a entrantes). El Singleton lo registra
// AddInfrastructure; aqui lo arrancamos como IHostedService (el bucle que agrupa y despacha).
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<Propia.Infrastructure.InfraestructuraIa.AgentDispatchQueue>());

// ---- Forwarded Headers (proxy de Railway o cualquier reverse proxy) ----
// Necesario para que el HTTPS redirect y los esquemas de URL respeten el origen.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // En PaaS no conocemos el rango de IP del proxy, asi que limpiamos los defaults
    // y confiamos en todos los proxies (terminacion TLS del PaaS).
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---- CORS para el frontend Web (Blazor Web App) ----
// En Prod la API recibe Cors:AllowedOrigins=https://app.propia-ad.com (env var Railway).
// En Dev sin config explicita: fallback a localhost + dominio piloto.
// Nota: AllowAnyOrigin no es compatible con AllowCredentials en navegadores.
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    }
    else
    {
        policy.WithOrigins(
                "https://localhost:7113", "http://localhost:5105",
                "https://app.propia.cubot.com.co")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    }
}));


// Autenticacion JWT - configurada via IOptions<JwtSettings> (resolucion lazy)
// para que coincida exactamente con lo que TokenService usa al firmar.
// Sin esto: en tests el JwtBearer lee la SigningKey en construccion del host,
// mientras TokenService la lee perezosamente despues de aplicada la config del test.
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

        // SignalR over WebSocket no permite headers: el token viaja en query string ?access_token=...
        bearer.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Policy "SuperAdmin": requiere claim is_super_admin=true emitido por SuperAdminAuthService.
    options.AddPolicy(AdminController.SuperAdminPolicy, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("is_super_admin", "true"));
});

// ---- Capa MCP de 2.3 Mi Copropiedad (tool-layer para agentes) ----
// Servidor MCP sobre HTTP montado en /mcp, detras del MISMO JWT + TenantMiddleware
// que el resto de la API. El agente presenta el token del usuario: hereda su tenant
// (RLS) y sus permisos (RBAC). No puede ver ni hacer mas que el usuario.
// Enums serializados como string para que el agente lea valores legibles (no numeros).
var mcpJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    // Reflection-based resolver: requerido para que el SDK pueda hacer MakeReadOnly().
    TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
};
mcpJsonOptions.Converters.Add(new JsonStringEnumConverter());
builder.Services.AddMcpServer()
    .WithHttpTransport(o =>
    {
        // Stateless: cada POST se procesa en el scope DI del request HTTP (no en una
        // sesion persistente). CLAVE para multi-tenant: la tool resuelve ITenantContext,
        // IMiCopropiedadService y PropiaDbContext del MISMO scope donde el TenantMiddleware
        // ya seteo el tenant del JWT. Sin esto, las tools correrian en el scope de la sesion
        // (creada en initialize) y RLS bloquearia todo por falta de tenant.
        o.Stateless = true;
    })
    .WithTools<Propia.Api.Mcp.MiCopropiedadConsultaTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.MiCopropiedadCreacionTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.MiCopropiedadEdicionTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.ServiciosConsultaTools>(mcpJsonOptions)
    .WithTools<Propia.Api.Mcp.ServiciosCreacionTools>(mcpJsonOptions);

var app = builder.Build();

// ---- Pipeline ----

// ForwardedHeaders DEBE ir antes de cualquier middleware que use HttpContext.Request.Scheme
app.UseForwardedHeaders();

// Bootstrap del founder SuperAdmin para PRODUCCION (idempotente, desde env vars
// SuperAdmin__BootstrapEmail / SuperAdmin__BootstrapPassword). No-op si no estan configuradas.
// En Development el founder dev se crea abajo; en prod este es el unico camino de aprovisionamiento.
await SuperAdminSeeder.EnsureBootstrapFounderAsync(app.Services, app.Configuration);

// Seed dev del founder SuperAdmin (solo en Development)
if (app.Environment.IsDevelopment())
{
    await SuperAdminSeeder.EnsureDevFounderAsync(app.Services);
    await DemoSeeder.EnsureDemoDataAsync(app.Services);
    await AgenteDocumentalSeeder.EnsureAsync(app.Services);
    app.MapOpenApi();
    app.UseHttpsRedirection();  // Local dev usa cert self-signed en puerto HTTPS

    // Servir imagenes subidas via /uploads/* (solo Development, en Production R2 sirve directo).
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"));
    app.UseStaticFiles();
}
else
{
    app.UseHsts();  // HSTS solo en !Development. NO usamos UseHttpsRedirection en Production:
                    // el proxy de Railway termina TLS afuera, el contenedor solo escucha HTTP.
}

// ---- Frente C observabilidad: Prometheus + Routing explicitos ----
// UseRouting va ANTES de Auth/Authorization. UseHttpMetrics se cuelga del routing
// para capturar nombre de ruta normalizada (en vez de URLs con UUIDs como series).
app.UseRouting();
app.UseHttpMetrics();

// CORS antes de Authentication para preflight OPTIONS.
app.UseCors();

// IMPORTANTE: Authentication PRIMERO, despues Authorization, despues TenantMiddleware
// (necesita el claim ya validado), despues MapXxx.
app.UseAuthentication();
app.UseAuthorization();

// TenantMiddleware no debe correr en endpoints de health (no requieren DB ni JWT).
// UseWhen aplica el middleware solo cuando el path NO empieza por /health.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseMiddleware<TenantMiddleware>());

app.MapControllers();
app.MapHub<Propia.Api.Hubs.ChatHub>("/hubs/chat");

// Endpoint MCP en /mcp. RequireAuthorization fuerza JWT valido; el TenantMiddleware
// (ya en el pipeline) setea el tenant del claim antes de ejecutar cualquier tool.
app.MapMcp("/mcp").RequireAuthorization();

// Endpoint /metrics expone counters/histograms en formato Prometheus.
// En Railway: configurar scrape externo apuntando a /metrics (Grafana Cloud free tier OK).
app.MapMetrics("/metrics");

// ---- Health checks ----
var startedAt = DateTimeOffset.UtcNow;

// /health: liveness enriquecido con uptime + version
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0-pilot",
    uptimeSeconds = (int)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
    serverTime = DateTimeOffset.UtcNow
}));

// /health/ready: readiness con check real de DbContext via HealthChecks framework.
// Devuelve 200 con JSON detallado si OK, 503 si algun check falla.
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

// Marker para integration tests (WebApplicationFactory<Program>)
public partial class Program { }
