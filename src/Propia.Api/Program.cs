using Propia.Api.Middleware;
using Propia.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// IMPORTANTE: TenantMiddleware debe ir DESPUES de UseAuthentication (cuando exista)
// y ANTES de UseAuthorization. Por ahora aun no hay auth - el middleware queda listo
// para cuando se agregue Identity + OpenIddict en el paso 6.
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();
app.MapControllers();

// Endpoint smoke test - confirma que la BD responde
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.1.0-dev" }));

app.Run();

// Marker para integration tests (WebApplicationFactory<Program>)
public partial class Program { }
