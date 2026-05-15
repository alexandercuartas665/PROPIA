using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Propia.Api.Controllers;
using Propia.Api.Middleware;
using Propia.Infrastructure;
using Propia.Infrastructure.Auth;
using Propia.Infrastructure.SuperAdmin;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

// CORS para el frontend Web (Blazor Web App). En Dev permitimos localhost.
// En produccion el frontend hace fetch via IHttpClientFactory server-side (no necesita CORS),
// pero algunos scripts cliente (theme toggle) llaman directamente al Api.
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(p => p
        .WithOrigins(
            "https://localhost:7113", "http://localhost:5105",
            "https://app.propia.cubot.com.co")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

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

// Seed dev del founder SuperAdmin (solo en Development)
if (app.Environment.IsDevelopment())
{
    await SuperAdminSeeder.EnsureDevFounderAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// CORS antes de Authentication para preflight OPTIONS
app.UseCors();

// Servir imagenes subidas via /uploads/* (modulo 2.3 - logos, fachadas, portadas).
// En produccion esto se mueve a un bucket S3/Azure con CDN.
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"));
app.UseStaticFiles();

// IMPORTANTE: Authentication PRIMERO, despues Authorization, despues TenantMiddleware
// (necesita el claim ya validado), despues MapControllers.
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();
app.MapControllers();

// Endpoint smoke test - no requiere auth
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.1.0-dev" }));

app.Run();

// Marker para integration tests (WebApplicationFactory<Program>)
public partial class Program { }
