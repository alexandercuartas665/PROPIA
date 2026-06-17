using Microsoft.AspNetCore.HttpOverrides;
using Propia.Web.Client.Pages;
using Propia.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o =>
    {
        if (builder.Environment.IsDevelopment()) o.DetailedErrors = true;
    })
    .AddInteractiveWebAssemblyComponents();

// Forwarded Headers para el proxy de Railway (necesario para que el scheme HTTPS
// se respete tras la terminacion TLS del PaaS).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// HttpClient apuntando al API REST de PROPIA.
// Dev: https://localhost:7100 (cert self-signed). Prod: PropiaApi__BaseUrl (env Railway)
// apunta a http://propia-api.railway.internal:8080 (red privada Railway).
builder.Services.AddHttpClient("PropiaApi", client =>
{
    var baseUrl = builder.Configuration["PropiaApi:BaseUrl"] ?? "https://localhost:7100";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var handler = new HttpClientHandler();
    // En Development confiamos en el cert self-signed del Api local.
    // En Production NUNCA - se valida el cert correctamente.
    if (env.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// Servicio de modales globales. Scoped por circuit Blazor Server.
// Ver Components/Shared/Modals/IModalService.cs y [[00.3. COMPONENTES GLOBALES]].
builder.Services.AddScoped<Propia.Web.Components.Shared.Modals.IModalService,
                          Propia.Web.Components.Shared.Modals.ModalService>();

var app = builder.Build();

// ForwardedHeaders DEBE ir primero en el pipeline.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseHttpsRedirection();  // Local dev usa cert self-signed
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    // NO usamos UseHttpsRedirection en Production: Railway hace TLS termination
    // en el proxy y el contenedor solo escucha HTTP en el puerto 8080.
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Propia.Web.Client._Imports).Assembly);

// Liveness para Railway probe.
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.1.0-pilot" }));

app.Run();
