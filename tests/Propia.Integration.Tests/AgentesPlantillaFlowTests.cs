using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.InfraestructuraIa;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Agentes semilla del Super Admin: la plantilla sube de version al cambiar el prompt; "Actualizar
/// copropiedades" actualiza las desactualizadas, RESPETA las editadas (salvo forzar) y crea donde falta;
/// los agentes del sistema se resuelven por clave (no por nombre) y se crean al primer uso; la copropiedad
/// puede volver a la version de A&amp;D. Todo pasando por RLS (propia_app) como en produccion.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AgentesPlantillaFlowTests
{
    private readonly PostgresFixture _fx;
    public AgentesPlantillaFlowTests(PostgresFixture fx) => _fx = fx;

    private static SaveAiAgentTemplateRequest Req(string nombre, string prompt) =>
        new(nombre, "Pruebas", null, AiProvider.Claude, null, prompt, true, false, 900, new List<AiAgentTemplateToolDto>());

    [Fact]
    public async Task Version_actualizar_respeta_editadas_forzar_y_crea_faltantes()
    {
        var tA = await SeedTenantAsync("[AGT] intacta");
        var tB = await SeedTenantAsync("[AGT] editada");
        var tC = await SeedTenantAsync("[AGT] sin agente");
        var (admin, _) = Build(null);
        var plantilla = await admin.SaveAsync(null, Req("Agente prueba {COPROPIEDAD_NOMBRE}", "Hola {COPROPIEDAD_NOMBRE} v1"), Guid.Empty, "test@propia", null);
        Assert.Equal(1, plantilla.Version);

        // A y B nacen de la plantilla (como en el onboarding); B luego edita su prompt.
        await using (var db = Db(tA)) { PlantillaAgenteFabrica_Nuevo(db, plantilla.Id, tA, "[AGT] intacta"); }
        await using (var db = Db(tB)) { PlantillaAgenteFabrica_Nuevo(db, plantilla.Id, tB, "[AGT] editada"); }
        await using (var db = Db(tB))
        {
            var b = await db.AiAgents.SingleAsync(x => x.TemplateId == plantilla.Id);
            b.SystemPrompt = "Prompt propio de la copropiedad B";
            await db.SaveChangesAsync();
        }

        // Cambiar solo el nombre NO sube la version; cambiar el prompt SI.
        var v1 = await admin.SaveAsync(plantilla.Id, Req("Agente prueba {COPROPIEDAD_NOMBRE}", "Hola {COPROPIEDAD_NOMBRE} v1") with { Description = "otra" }, Guid.Empty, "test@propia", null);
        Assert.Equal(1, v1.Version);
        var v2 = await admin.SaveAsync(plantilla.Id, Req("Agente prueba {COPROPIEDAD_NOMBRE}", "Hola {COPROPIEDAD_NOMBRE} v2 mejorado") with { Provider = AiProvider.DeepSeek }, Guid.Empty, "test@propia", null);
        Assert.Equal(2, v2.Version);

        var prev = await admin.PreviewPropagacionAsync(plantilla.Id);
        Assert.NotNull(prev);
        EstadoPropagacion EstadoDe(Guid t) => prev!.Filas.Single(f => f.TenantId == t).Estado;
        Assert.Equal(EstadoPropagacion.Desactualizado, EstadoDe(tA));
        Assert.Equal(EstadoPropagacion.Editado, EstadoDe(tB));
        Assert.Equal(EstadoPropagacion.Falta, EstadoDe(tC));

        // Sin forzar: A se actualiza, B se respeta, C se crea.
        var r = await admin.PropagarAsync(plantilla.Id, new PropagarPlantillaRequest(false, true), Guid.Empty, "test@propia", null);
        Assert.NotNull(r);
        await using (var db = Db(tA))
        {
            var a = await db.AiAgents.SingleAsync(x => x.TemplateId == plantilla.Id);
            Assert.Equal("Hola [AGT] intacta v2 mejorado", a.SystemPrompt);
            Assert.Equal(AiProvider.DeepSeek, a.Provider);   // tambien lleva proveedor y modelo
            Assert.Equal(2, a.TemplateVersion);
            Assert.Contains("v2 mejorado", a.PromptHistoryJson);   // queda en el historial
        }
        await using (var db = Db(tB))
        {
            var b = await db.AiAgents.SingleAsync(x => x.TemplateId == plantilla.Id);
            Assert.Equal("Prompt propio de la copropiedad B", b.SystemPrompt);   // editada: se respeta (prompt y proveedor)
            Assert.Equal(AiProvider.Claude, b.Provider);
        }
        await using (var db = Db(tC))
        {
            var c = await db.AiAgents.SingleAsync(x => x.TemplateId == plantilla.Id);
            Assert.Equal("Agente prueba [AGT] sin agente", c.Name);
            Assert.Equal("Hola [AGT] sin agente v2 mejorado", c.SystemPrompt);
            Assert.True(c.IsActive);
        }

        // Ya al dia: un segundo "actualizar" no toca a A ni a C.
        var prev2 = await admin.PreviewPropagacionAsync(plantilla.Id);
        Assert.Equal(EstadoPropagacion.AlDia, prev2!.Filas.Single(f => f.TenantId == tA).Estado);
        Assert.Equal(EstadoPropagacion.AlDia, prev2.Filas.Single(f => f.TenantId == tC).Estado);

        // Forzando: B tambien queda en la version de A&D.
        await admin.PropagarAsync(plantilla.Id, new PropagarPlantillaRequest(true, false), Guid.Empty, "test@propia", null);
        await using (var db = Db(tB)) Assert.Equal("Hola [AGT] editada v2 mejorado", (await db.AiAgents.SingleAsync(x => x.TemplateId == plantilla.Id)).SystemPrompt);

        // Borrar con un contexto nuevo (como otra peticion): la FK deja los agentes sin vinculo (SET NULL).
        var (adminDel, _) = Build(null);
        await adminDel.DeleteAsync(plantilla.Id, Guid.Empty, "test@propia", null);
        await using (var db = Db(tA)) Assert.Null((await db.AiAgents.SingleAsync(x => x.Name == "Agente prueba [AGT] intacta")).TemplateId);
        foreach (var t in new[] { tA, tB, tC }) await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task Agente_del_sistema_se_crea_al_usarlo_y_se_resuelve_por_clave_aunque_lo_renombren()
    {
        var t = await SeedTenantAsync("[AGT] sistema");
        var (admin, _) = Build(null);
        var plantilla = await EnsurePlantillaDocumentalAsync(admin);

        var (_, sistema) = Build(t);
        var a1 = await sistema.AsegurarAsync(PlantillaAgente.ClaveDocumental);
        Assert.NotNull(a1);
        Assert.True(a1!.Activo);
        await using (var db = Db(t))
        {
            var ag = await db.AiAgents.SingleAsync(x => x.Id == a1.Id);
            Assert.Equal(plantilla.Id, ag.TemplateId);
            ag.Name = "Lector de documentos del conjunto";   // la copropiedad lo renombra
            await db.SaveChangesAsync();
        }
        var (_, sistema2) = Build(t);
        var a2 = await sistema2.AsegurarAsync(PlantillaAgente.ClaveDocumental);
        Assert.Equal(a1.Id, a2!.Id);   // mismo agente: no crea otro

        // Info de plantilla y "Volver a la version de A&D" desde la copropiedad.
        var (adminT, _) = Build(t);
        await using (var db = Db(t)) { var ag = await db.AiAgents.SingleAsync(x => x.Id == a1.Id); ag.SystemPrompt = "editado"; await db.SaveChangesAsync(); }
        var info = await adminT.InfoPlantillaDeAgenteAsync(a1.Id);
        Assert.True(info!.Editado);
        Assert.False(info.AlDia);
        var tras = await adminT.AplicarPlantillaAAgenteAsync(a1.Id);
        Assert.True(tras!.AlDia);

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task Una_copropiedad_no_ve_ni_toca_los_agentes_de_otra()
    {
        var tA = await SeedTenantAsync("[AGT] ISO A");
        var tB = await SeedTenantAsync("[AGT] ISO B");
        var (admin, _) = Build(null);
        await EnsurePlantillaDocumentalAsync(admin);
        var (_, sisA) = Build(tA);
        var aA = await sisA.AsegurarAsync(PlantillaAgente.ClaveDocumental);
        var (adminB, _) = Build(tB);
        Assert.Null(await adminB.InfoPlantillaDeAgenteAsync(aA!.Id));
        Assert.Null(await adminB.AplicarPlantillaAAgenteAsync(aA.Id));
        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ---------- helpers ----------
    private async Task<AiAgentTemplateDto> EnsurePlantillaDocumentalAsync(IAiAgentTemplateService admin)
    {
        await using (var owner = OwnerDb())
        {
            var t = await owner.AiAgentTemplates.FirstOrDefaultAsync(x => x.Clave == PlantillaAgente.ClaveDocumental);
            if (t is not null) return (await admin.GetAsync(t.Id))!;
        }
        var dto = await admin.SaveAsync(null, Req("Agente Documental", "Lees documentos de {COPROPIEDAD_NOMBRE}"), Guid.Empty, "test@propia", null);
        await using (var owner = OwnerDb())
        {
            var t = await owner.AiAgentTemplates.SingleAsync(x => x.Id == dto.Id);
            t.Clave = PlantillaAgente.ClaveDocumental;
            await owner.SaveChangesAsync();
        }
        return (await admin.GetAsync(dto.Id))!;
    }

    // Crea el agente de la copropiedad desde la plantilla (lo mismo que hace el onboarding).
    private static (AiAgent, int) PlantillaAgenteFabrica_Nuevo(PropiaDbContext db, Guid templateId, Guid tenantId, string nombre)
    {
        var t = db.AiAgentTemplates.Include(x => x.McpTools).Single(x => x.Id == templateId);
        var prompt = PlantillaAgente.Render(t.SystemPrompt, nombre, null);
        var a = new AiAgent
        {
            TenantId = tenantId, Name = PlantillaAgente.Render(t.Name, nombre, null), SystemPrompt = prompt, IsActive = true,
            TemplateId = t.Id, TemplateVersion = t.Version, TemplatePromptHash = PlantillaAgente.Hash(prompt)
        };
        db.AiAgents.Add(a);
        db.SaveChanges();
        return (a, 0);
    }

    private (IAiAgentTemplateService Admin, IAgentesSistema Sistema) Build(Guid? tenantId)
    {
        var tenantCtx = new TenantContext();
        if (tenantId is Guid t) tenantCtx.SetTenant(t);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        return (new AiAgentTemplateService(db, NullLogger<AiAgentTemplateService>.Instance, tenantCtx),
                new AgentesSistemaService(db, tenantCtx, NullLogger<AgentesSistemaService>.Instance));
    }
    private PropiaDbContext Db(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new PropiaDbContext(options, tenantCtx);
    }
    private PropiaDbContext OwnerDb()
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        return new PropiaDbContext(options, new TenantContext());
    }
    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }
    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM ai_agent_mcp_tools WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM ai_agents WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
