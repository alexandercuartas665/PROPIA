using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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

// ---- Logging estructurado a stdout (Railway captura nativamente) ----
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// ---- Servicios ----
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

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
// En Prod la API recibe Cors:AllowedOrigins=https://app.propia.cubot.com.co (env var Railway).
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
    });

builder.Services.AddAuthorization(options =>
{
    // Policy "SuperAdmin": requiere claim is_super_admin=true emitido por SuperAdminAuthService.
    options.AddPolicy(AdminController.SuperAdminPolicy, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("is_super_admin", "true"));
});

var app = builder.Build();

// ---- Pipeline ----

// ForwardedHeaders DEBE ir antes de cualquier middleware que use HttpContext.Request.Scheme
app.UseForwardedHeaders();

// Seed dev del founder SuperAdmin (solo en Development)
if (app.Environment.IsDevelopment())
{
    await SuperAdminSeeder.EnsureDevFounderAsync(app.Services);
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

// CORS antes de Authentication para preflight OPTIONS.
// UseHttpsRedirection y UseStaticFiles (/uploads) se aplican condicionalmente
// en el bloque if (IsDevelopment) arriba: en Production, Railway termina TLS
// fuera del contenedor y los adjuntos van a Cloudflare R2 (IBlobStorage).
app.UseCors();

// IMPORTANTE: Authentication PRIMERO, despues Authorization, despues TenantMiddleware
// (necesita el claim ya validado), despues MapControllers.
app.UseAuthentication();
app.UseAuthorization();

// TenantMiddleware no debe correr en endpoints de health (no requieren DB ni JWT).
// UseWhen aplica el middleware solo cuando el path NO empieza por /health.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseMiddleware<TenantMiddleware>());

app.MapControllers();

// ---- Health checks ----
// /health: liveness simple (responde si el proceso esta vivo)
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.1.0-pilot" }));

// /health/ready: readiness con check de DB
app.MapGet("/health/ready", async (PropiaDbContext db, CancellationToken ct) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        return canConnect
            ? Results.Ok(new { status = "ok", db = "ok", version = "0.1.0-pilot" })
            : Results.StatusCode(503);
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

app.Run();

// Marker para integration tests (WebApplicationFactory<Program>)
public partial class Program { }
