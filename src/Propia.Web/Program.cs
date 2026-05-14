using Propia.Web.Client.Pages;
using Propia.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// HttpClient apuntando al API REST de PROPIA.
// En dev: https://localhost:7100 (default del Propia.Api). En produccion: env var.
// El Login.razor inyecta IHttpClientFactory y pide CreateClient("PropiaApi").
builder.Services.AddHttpClient("PropiaApi", client =>
{
    var baseUrl = builder.Configuration["PropiaApi:BaseUrl"] ?? "https://localhost:7100";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // En dev: confiar en el cert self-signed del Api. En produccion: quitar esto.
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Propia.Web.Client._Imports).Assembly);

app.Run();
