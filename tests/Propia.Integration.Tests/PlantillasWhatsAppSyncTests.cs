using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.InfraestructuraIa;
using Propia.Infrastructure.Jobs;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Plantillas de WhatsApp como fuente unica (2026-11-13): las de YCloud caen solas (sincronizar al abrir sin repetir
/// antes de 2 min, y el job cada 30 min por copropiedad con RLS), y cada modulo que usa una plantilla (PQRSD) se
/// reporta con su estado: bien, no existe, no aprobada o archivada. YCloud es falso.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PlantillasWhatsAppSyncTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private static readonly List<YCloudTemplateItem> Plantillas = new();

    public PlantillasWhatsAppSyncTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddScoped<ITenantContext, TenantContext>();
        sc.AddSingleton<ISecretProtector, SecretoPlano>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, o) => o.UseNpgsql(_fx.AppConnectionString).AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddSingleton(DispatchProxy.Create<IYCloudApiClient, YCloudFalso>());
        sc.AddSingleton(DispatchProxy.Create<IWhatsAppConnectorService, ConectorNoUsado>());
        sc.AddScoped<IPlantillasWhatsAppService, PlantillasWhatsAppService>();
        sc.AddScoped<PlantillasWhatsAppSyncJob>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static YCloudTemplateItem Item(string nombre, string estado, string? motivo = null)
        => new(nombre, "es", estado, "tpl-" + nombre, motivo, "UTILITY", null, null, "Hola {{1}}, su solicitud " + nombre, null, new[] { "Ana" }, null);

    [Fact]
    public async Task Las_plantillas_caen_solas_y_cada_uso_reporta_su_estado()
    {
        var tA = await SeedTenantAsync("[PWA] A");
        var tB = await SeedTenantAsync("[PWA] B");
        await SeedLineaAsync(tA, "Linea A");
        await SeedLineaAsync(tB, "Linea B");
        lock (Plantillas)
        {
            Plantillas.Clear();
            Plantillas.Add(Item("respuesta_pqrsd", "APPROVED"));
            Plantillas.Add(Item("porteria_solicitud_ingreso", "PENDING"));
        }
        try
        {
            // 1) Al abrir el modulo: importa todas las de la linea; un segundo llamado inmediato no vuelve a YCloud.
            using (var s = Scope(tA))
            {
                var svc = s.ServiceProvider.GetRequiredService<IPlantillasWhatsAppService>();
                var r = await svc.SincronizarAsync(false, default);
                Assert.Equal((1, 2, 0, 0, false), (r.Lineas, r.Importadas, r.Actualizadas, r.Errores.Count, r.Omitida));
                Assert.True((await svc.SincronizarAsync(false, default)).Omitida);
            }

            // 2) Usos de PQRSD: bien enlazada, no aprobada, no existe y archivada.
            await SetPqrsdAsync(tA, "respuesta_pqrsd");
            using (var s = Scope(tA))
            {
                var svc = s.ServiceProvider.GetRequiredService<IPlantillasWhatsAppService>();
                var uso = Assert.Single(await svc.UsosAsync(default));
                Assert.Equal(("PQRSD", EstadoUsoPlantilla.Ok), (uso.Modulo, uso.Estado));
                Assert.NotNull(uso.PlantillaId);
            }
            await SetPqrsdAsync(tA, "porteria_solicitud_ingreso");
            Assert.Equal(EstadoUsoPlantilla.NoAprobada, (await UsoAsync(tA)).Estado);
            await SetPqrsdAsync(tA, "no_existe_en_ycloud");
            Assert.Equal(EstadoUsoPlantilla.NoExiste, (await UsoAsync(tA)).Estado);
            await SetPqrsdAsync(tA, "respuesta_pqrsd");
            using (var s = Scope(tA))
            {
                var svc = s.ServiceProvider.GetRequiredService<IPlantillasWhatsAppService>();
                var id = (await svc.UsosAsync(default))[0].PlantillaId!.Value;
                await svc.ArchivarAsync(id, false, default);
            }
            Assert.Equal(EstadoUsoPlantilla.Archivada, (await UsoAsync(tA)).Estado);

            // 3) Meta aprueba la de porteria: el job (sin request) la actualiza en CADA copropiedad, cada una con lo suyo.
            lock (Plantillas) { Plantillas[1] = Item("porteria_solicitud_ingreso", "APPROVED"); }
            using (var s = _services.CreateScope())
            {
                var job = s.ServiceProvider.GetRequiredService<PlantillasWhatsAppSyncJob>();
                await job.EjecutarAsync(default);
            }
            await using (var ctx = Owner())
            {
                var a = await ctx.PlantillasWhatsApp.IgnoreQueryFilters().Where(p => p.TenantId == tA).ToListAsync();
                var b = await ctx.PlantillasWhatsApp.IgnoreQueryFilters().Where(p => p.TenantId == tB).ToListAsync();
                Assert.Equal(2, a.Count);
                Assert.Equal(2, b.Count);
                Assert.Equal(PlantillaWaEstado.Approved, a.Single(p => p.Name == "porteria_solicitud_ingreso").Estado);
                Assert.Equal(PlantillaWaEstado.Approved, b.Single(p => p.Name == "porteria_solicitud_ingreso").Estado);
            }
        }
        finally
        {
            await CleanAsync(tA);
            await CleanAsync(tB);
        }
    }

    private IServiceScope Scope(Guid tenantId)
    {
        var s = _services.CreateScope();
        s.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        return s;
    }

    private async Task<UsoPlantillaWhatsAppDto> UsoAsync(Guid tenantId)
    {
        using var s = Scope(tenantId);
        return Assert.Single(await s.ServiceProvider.GetRequiredService<IPlantillasWhatsAppService>().UsosAsync(default));
    }

    private async Task SetPqrsdAsync(Guid tenantId, string plantilla)
    {
        await using var ctx = Owner();
        var c = await ctx.PqrsdWhatsAppConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId);
        if (c is null) { c = new PqrsdWhatsAppConfig { TenantId = tenantId }; ctx.PqrsdWhatsAppConfigs.Add(c); }
        c.PlantillaNombre = plantilla;
        c.PlantillaIdioma = "es";
        await ctx.SaveChangesAsync();
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = Owner();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task SeedLineaAsync(Guid tenantId, string nombre)
    {
        await using var ctx = Owner();
        ctx.WhatsAppLines.Add(new WhatsAppLine
        {
            TenantId = tenantId, InstanceName = nombre, Provider = WhatsAppProvider.YCloud,
            YCloudFromNumber = "+57300" + Random.Shared.Next(1000000, 9999999), YCloudApiKeyEncrypted = "enc:key"
        });
        await ctx.SaveChangesAsync();
    }

    private async Task CleanAsync(Guid tenantId)
    {
        await using var ctx = Owner();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM plantillas_whatsapp WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_whatsapp_configs WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM whats_app_lines WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    private PropiaDbContext Owner() => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private sealed class SecretoPlano : ISecretProtector
    {
        public string Protect(string plain) => "enc:" + plain;
        public string Unprotect(string cipher) => cipher.StartsWith("enc:") ? cipher[4..] : throw new InvalidOperationException("llave anterior");
    }

    /// <summary>YCloud falso: solo lista plantillas (las de <see cref="Plantillas"/>); cualquier otra llamada falla.</summary>
    public class YCloudFalso : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? m, object?[]? args)
        {
            if (m!.Name == nameof(IYCloudApiClient.ListTemplatesAsync))
            {
                List<YCloudTemplateItem> copia;
                lock (Plantillas) copia = Plantillas.ToList();
                return Task.FromResult(new YCloudTemplateListResult(true, copia, null));
            }
            throw new NotSupportedException(m.Name);
        }
    }

    public class ConectorNoUsado : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? m, object?[]? args) => throw new NotSupportedException(m!.Name);
    }
}
