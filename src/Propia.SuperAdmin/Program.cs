using Propia.SuperAdmin.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HttpClient hacia el API REST (endpoints /admin/*)
builder.Services.AddHttpClient("PropiaApi", client =>
{
    var baseUrl = builder.Configuration["PropiaApi:BaseUrl"] ?? "https://localhost:7100";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

// Estado de sesion en memoria (server-side) - SuperAdmin no soporta multi-instancia aun.
// En produccion: cookie HttpOnly + sesion distribuida (Redis).
builder.Services.AddScoped<Propia.SuperAdmin.Services.SuperAdminSession>();

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
