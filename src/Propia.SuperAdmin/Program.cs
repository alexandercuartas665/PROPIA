using Propia.SuperAdmin.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HttpClient hacia el API REST (endpoints /admin/*)
builder.Services.AddHttpClient("PropiaApi", client =>
{
    var baseUrl = builder.Configuration["PropiaApi:BaseUrl"] ?? "https://localhost:7205";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

// Estado de sesion en memoria del proceso. Singleton porque las pages del SuperAdmin
// se acceden via Blazor SSR + full-load navigation, y un scoped service perderia
// el JWT entre circuitos. En produccion: cookie HttpOnly + Redis para multi-instancia.
builder.Services.AddSingleton<Propia.SuperAdmin.Services.SuperAdminSession>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
