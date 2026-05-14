using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Propia.Api.Middleware;
using Propia.Infrastructure;
using Propia.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

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

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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
