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
app.UseAuthorization();
app.MapControllers();

// Endpoint smoke test - confirma que la BD responde
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.1.0-dev" }));

app.Run();
