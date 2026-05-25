using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Billing;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 0.2 Billing y Suscripciones (MVP).
/// Cubre: CRUD planes con regla del plan con suscripciones activas, crear suscripcion,
/// cambiar plan/estado, generar factura, registrar pago, billing_config singleton,
/// trigger inmutable de suscripcion_historial.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class BillingFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public BillingFlowTests(PostgresFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(_fx.OwnerConnectionString);
        await using var scope = _factory.Services.CreateAsyncScope();
        // Founder seedeado por SuperAdminSeeder al levantar el host. Solo aseguramos
        // que esta sin MFA (otros tests del MFA pueden haber tocado este registro).
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var founder = await db.SuperAdminUsuarios.FirstOrDefaultAsync(u => u.Email == "founder@adgroup.com.co");
        if (founder is not null && (founder.MfaConfigurado || !founder.Activo))
        {
            founder.MfaConfigurado = false;
            founder.MfaSecret = null;
            founder.Activo = true;
            await db.SaveChangesAsync();
        }

        _client = _factory.CreateClient();
        var loginResp = await _client.PostAsJsonAsync("/admin/login",
            new SuperAdminLoginRequest("founder@adgroup.com.co", "PropiaFounder2026!"));
        var login = await loginResp.Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Crear_plan_y_suscripcion_genera_factura_y_marca_pagada()
    {
        // 1) Crear plan
        var planResp = await _client.PostAsJsonAsync("/admin/billing/planes",
            new CrearPlanRequest("Plan Test", "Desc", 150000m, 5000m, true, true, 10m, null, null, null, null, null, 0));
        var planBody = await planResp.Content.ReadAsStringAsync();
        Assert.True(planResp.StatusCode == HttpStatusCode.Created,
            $"Plan create returned {(int)planResp.StatusCode}: {planBody}");
        var plan = System.Text.Json.JsonSerializer.Deserialize<PlanDto>(planBody,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(plan);

        // 2) Crear organizacion (via /admin/organizaciones que ya existe)
        var orgResp = await _client.PostAsJsonAsync("/admin/organizaciones",
            new CrearOrganizacionRequest($"Org Billing {Guid.NewGuid():N}", TipoOrganizacion.Administradora, $"9{Random.Shared.Next(100000000, 999999999)}", null, null));
        var org = await orgResp.Content.ReadFromJsonAsync<OrganizacionDto>();

        // 3) Crear suscripcion ligada a la org
        var subResp = await _client.PostAsJsonAsync("/admin/billing/suscripciones",
            new CrearSuscripcionRequest(org!.Id, null, plan!.Id, CicloFacturacion.Mensual));
        Assert.Equal(HttpStatusCode.Created, subResp.StatusCode);
        var sub = await subResp.Content.ReadFromJsonAsync<SuscripcionDto>();
        Assert.NotNull(sub);
        Assert.Equal(EstadoSuscripcion.Activa, sub!.Estado);  // sin trial -> Activa

        // 4) Generar factura del periodo actual
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var facResp = await _client.PostAsJsonAsync("/admin/billing/facturas/generar",
            new GenerarFacturaRequest(sub.Id, hoy.AddDays(-30), hoy));
        Assert.Equal(HttpStatusCode.Created, facResp.StatusCode);
        var fac = await facResp.Content.ReadFromJsonAsync<FacturaDto>();
        Assert.Equal(EstadoFactura.Pendiente, fac!.Estado);
        Assert.True(fac.Total > 0);

        // 5) Marcar pagada - numero de factura unico por run (constraint unique)
        var numeroFactura = $"F-{Guid.NewGuid():N}";
        var pagoResp = await _client.PostAsJsonAsync($"/admin/billing/facturas/{fac.Id}/registrar-pago",
            new RegistrarPagoFacturaRequest("WMP-TEST-001", numeroFactura));
        Assert.Equal(HttpStatusCode.OK, pagoResp.StatusCode);
        var pagada = await pagoResp.Content.ReadFromJsonAsync<FacturaDto>();
        Assert.Equal(EstadoFactura.Pagada, pagada!.Estado);
        Assert.Equal(numeroFactura, pagada.NumeroFactura);

        // 6) Historial registra activacion
        var hist = await _client.GetFromJsonAsync<List<SuscripcionHistorialDto>>($"/admin/billing/suscripciones/{sub.Id}/historial");
        Assert.NotNull(hist);
        Assert.Contains(hist!, h => h.Tipo == TipoEventoSuscripcion.Activacion);
    }

    [Fact]
    public async Task No_se_puede_desactivar_plan_con_suscripciones_activas()
    {
        // Crear plan
        var planResp = await _client.PostAsJsonAsync("/admin/billing/planes",
            new CrearPlanRequest("Plan Bloqueado", null, 100000m, 0m, true, false, 0m, null, null, null, null, null, 0));
        var plan = await planResp.Content.ReadFromJsonAsync<PlanDto>();

        // Crear org y suscripcion
        var orgResp = await _client.PostAsJsonAsync("/admin/organizaciones",
            new CrearOrganizacionRequest($"Org B {Guid.NewGuid():N}", TipoOrganizacion.Administradora, null, null, null));
        var org = await orgResp.Content.ReadFromJsonAsync<OrganizacionDto>();
        await _client.PostAsJsonAsync("/admin/billing/suscripciones",
            new CrearSuscripcionRequest(org!.Id, null, plan!.Id, CicloFacturacion.Mensual));

        // Intentar archivar el plan -> debe fallar
        var resp = await _client.PutAsJsonAsync($"/admin/billing/planes/{plan.Id}",
            new ActualizarPlanRequest(plan.Nombre, null, plan.FeeBase, plan.FeeVariablePorUnidad,
                true, false, 0m, null, null, null, null, null, 0, EstadoPlan.Archivado));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("suscripcion", body);
    }

    [Fact]
    public async Task Cambio_de_plan_se_registra_como_upgrade_o_downgrade_en_historial()
    {
        var basicoResp = await _client.PostAsJsonAsync("/admin/billing/planes",
            new CrearPlanRequest("Basico", null, 100000m, 0m, true, false, 0m, null, null, null, null, null, 0));
        var premiumResp = await _client.PostAsJsonAsync("/admin/billing/planes",
            new CrearPlanRequest("Premium", null, 500000m, 0m, true, false, 0m, null, null, null, null, null, 0));
        var basico = await basicoResp.Content.ReadFromJsonAsync<PlanDto>();
        var premium = await premiumResp.Content.ReadFromJsonAsync<PlanDto>();

        var orgResp = await _client.PostAsJsonAsync("/admin/organizaciones",
            new CrearOrganizacionRequest($"Org Up {Guid.NewGuid():N}", TipoOrganizacion.Administradora, null, null, null));
        var org = await orgResp.Content.ReadFromJsonAsync<OrganizacionDto>();
        var subResp = await _client.PostAsJsonAsync("/admin/billing/suscripciones",
            new CrearSuscripcionRequest(org!.Id, null, basico!.Id, CicloFacturacion.Mensual));
        var sub = await subResp.Content.ReadFromJsonAsync<SuscripcionDto>();

        var upgResp = await _client.PutAsJsonAsync($"/admin/billing/suscripciones/{sub!.Id}/plan",
            new CambiarPlanRequest(premium!.Id, "Validacion test upgrade"));
        Assert.Equal(HttpStatusCode.OK, upgResp.StatusCode);

        var hist = await _client.GetFromJsonAsync<List<SuscripcionHistorialDto>>($"/admin/billing/suscripciones/{sub.Id}/historial");
        Assert.Contains(hist!, h => h.Tipo == TipoEventoSuscripcion.Upgrade && h.Notas!.Contains("upgrade"));
    }

    [Fact]
    public async Task BillingConfig_singleton_es_legible_y_modificable()
    {
        var c = await _client.GetFromJsonAsync<BillingConfigDto>("/admin/billing/config");
        Assert.NotNull(c);
        Assert.Equal("COP", c!.Moneda);
        Assert.Equal(0m, c.ImpuestoPct);  // IVA SaaS excluido por defecto

        var nuevoIva = 0m;  // dejamos en 0 - solo validamos que el endpoint acepta cambios
        var nuevoMoneda = "COP";
        var req = new ActualizarBillingConfigRequest(
            c.DiasGracia, c.DiaAlertaMora1, c.DiaAlertaMora2, c.DiaSuspension,
            c.DiaAlertaCancelacion, c.DiaCancelacion, c.ReintentosCobro,
            c.DiasEntreReintentos, c.DiasPreavisoCobro, c.RetencionDatosMeses,
            c.RetencionFacturasAnios, nuevoIva, nuevoMoneda, c.ProveedorContable);
        var resp = await _client.PutAsJsonAsync("/admin/billing/config", req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SuscripcionHistorial_es_inmutable_no_acepta_update_ni_delete()
    {
        // Setup minimo: crear plan, org, suscripcion -> genera entrada en historial
        var planResp = await _client.PostAsJsonAsync("/admin/billing/planes",
            new CrearPlanRequest("Plan Imm", null, 50000m, 0m, true, false, 0m, null, null, null, null, null, 0));
        var plan = await planResp.Content.ReadFromJsonAsync<PlanDto>();
        var orgResp = await _client.PostAsJsonAsync("/admin/organizaciones",
            new CrearOrganizacionRequest($"Org Imm {Guid.NewGuid():N}", TipoOrganizacion.Administradora, null, null, null));
        var org = await orgResp.Content.ReadFromJsonAsync<OrganizacionDto>();
        await _client.PostAsJsonAsync("/admin/billing/suscripciones",
            new CrearSuscripcionRequest(org!.Id, null, plan!.Id, CicloFacturacion.Mensual));

        // Intentar UPDATE/DELETE directo via SQL -> debe fallar por trigger
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var exUpd = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE suscripcion_historial SET notas = 'tampered'"));
        Assert.Contains("append-only", exUpd.InnerException?.Message ?? exUpd.Message);

        var exDel = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM suscripcion_historial"));
        Assert.Contains("append-only", exDel.InnerException?.Message ?? exDel.Message);
    }

    private class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connString;
        public ApiFactory(string connString) => _connString = connString;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((ctx, conf) =>
            {
                conf.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Propia"] = _connString,
                    ["Jwt:SigningKey"] = "DEV-ONLY-PropIA-Jwt-SigningKey-Min32CharsForHS256-AuthTest",
                    ["Jwt:Issuer"] = "propia-api",
                    ["Jwt:Audience"] = "propia-clients",
                    ["Jwt:AccessTokenMinutes"] = "60"
                });
            });
        }
    }
}
