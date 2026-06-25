using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Siembra el "Agente Documental": un agente IA que lee documentos (recibos, facturas, polizas,
/// poderes) extraidos por OCR y, via las tools MCP del modulo de Servicios, propone el registro
/// (registrar consumo de un recibo, crear servicio/poliza, etc.) para que el usuario confirme.
///
/// IDEMPOTENTE: (1) crea la plantilla global si no existe (para que las copropiedades NUEVAS la
/// reciban en el onboarding); (2) crea el agente en cada tenant existente que aun no lo tenga
/// (para que las copropiedades YA activas tambien lo tengan). El provider del agente se toma del
/// proveedor LLM habilitado en SuperAdmin; si no hay, queda en Claude por defecto.
/// </summary>
public static class AgenteDocumentalSeeder
{
    public const string AgenteNombre = "Agente Documental";
    private const string ConnectionCode = "copropiedades"; // servidor MCP interno (incluye tools de Servicios)

    /// <summary>Tools MCP que el agente puede usar (consulta + acciones del modulo de Servicios).</summary>
    private static readonly string[] Tools =
    {
        "serviciospublicos_listar_cuentas",
        "serviciospublicos_obtener_cuenta",
        "serviciospublicos_resumen",
        "serviciospublicos_registrar_consumo",
        "serviciospublicos_crear_cuenta",
        "serviciospublicos_agregar_reclamacion",
        "servicios_listar",
        "servicios_obtener",
        "servicios_crear",
        "servicios_agregar_alerta",
    };

    private const string SystemPrompt = """
Eres el "Agente Documental" de PROPIA, una plataforma de administracion de
copropiedades en Colombia. Tu trabajo es leer el TEXTO de un documento (extraido
por OCR) y ayudar al administrador a registrarlo en el sistema usando tus
herramientas MCP. Trabajas sobre la copropiedad activa del usuario.

## Paso 1: Clasifica el documento
Determina el tipo entre:
- RECIBO de servicio publico (energia, agua, gas, internet): tiene prestador,
  numero/NIC de cuenta, periodo, consumo (kWh/m3/etc) y valor a pagar.
- FACTURA de un proveedor/contratista (aseo, vigilancia, mantenimiento, etc.).
- POLIZA de seguro (RCE, todo riesgo, areas comunes).
- PODER / autorizacion (representacion en asamblea u otros).
- OTRO.

## Paso 2: Extrae los datos clave (NUNCA inventes)
Reporta solo lo que el texto dice. Si un dato no aparece o es ambiguo, dejalo
vacio y pidelo. Normaliza: moneda en COP sin simbolos, fechas como AAAA-MM-DD,
mes como numero 1-12.

## Paso 3: Propon la accion con tus herramientas
- RECIBO: llama serviciospublicos_listar_cuentas y empareja la cuenta por
  prestador o numero de cuenta. Luego propon serviciospublicos_registrar_consumo
  (cuentaId, anio, mes, consumo, valor, estado=Confirmado). Si no existe cuenta
  para ese prestador, propon serviciospublicos_crear_cuenta primero.
- FACTURA/POLIZA de un servicio que no existe: propon servicios_crear (para
  poliza usa Tipo=SeguroPH).
- PODER u OTRO: clasifica y resume los datos (otorgante, apoderado, facultades,
  vigencia); por ahora no tienes herramienta para persistirlo, asi que solo
  informa al usuario.

## Reglas de las herramientas
- Para PROPONER, EJECUTA la herramienta de verdad pero SIEMPRE con dryRun=true
  (es una llamada real que valida sin guardar). NUNCA escribas la llamada como
  texto o pseudo-codigo: invocala y usa su resultado real para tu resumen.
- Usa SIEMPRE los valores EXACTOS de los enums segun la descripcion de cada
  herramienta. No inventes variantes. Validos:
  - Tipo de cuenta de servicio publico: Electricidad, Agua, Gas, Internet, Otros
    (energia electrica = Electricidad, NUNCA 'EnergiaElectrica').
  - Estado de consumo: Confirmado, Estimado.
  - Severidad de alerta: Info, Advertencia, Critica.
  - Tipo de servicio (polizas): SeguroPH.

## Regla de oro: dry-run, luego confirmacion
1. En tu PRIMERA respuesta a un documento: ejecuta SOLO con dryRun=true, muestra
   lo que el sistema devolvio que se guardaria y TERMINA pidiendo confirmacion.
   NUNCA uses dryRun=false en esta primera respuesta.
2. SOLO cuando el usuario confirme explicitamente en un mensaje posterior, llama
   de nuevo la(s) misma(s) herramienta(s) con dryRun=false para persistir.
Nunca persistas sin confirmacion. Se conciso y usa solo ASCII.
""";

    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        // Provider del LLM habilitado (global, sin RLS); si no hay, Claude por defecto.
        var provider = await db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .Select(c => (AiProvider?)c.Provider)
            .FirstOrDefaultAsync() ?? AiProvider.Claude;

        await EnsureTemplateAsync(db, provider, logger);
        await EnsureAgentsPerTenantAsync(db, tenant, provider, logger);
    }

    /// <summary>Plantilla global (BaseEntity, sin RLS) para que las copropiedades nuevas la reciban.</summary>
    private static async Task EnsureTemplateAsync(PropiaDbContext db, AiProvider provider, ILogger logger)
    {
        var existente = await db.AiAgentTemplates.FirstOrDefaultAsync(t => t.Name == AgenteNombre);
        if (existente is not null)
        {
            // Upsert del prompt: si cambio la definicion canonica, re-aplicarla.
            if (existente.SystemPrompt != SystemPrompt)
            {
                existente.SystemPrompt = SystemPrompt;
                existente.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                logger.LogWarning("[SEED] AgenteDocumentalSeeder: prompt de la plantilla '{Name}' actualizado.", AgenteNombre);
            }
            return;
        }

        var template = new AiAgentTemplate
        {
            Id = Guid.NewGuid(),
            Name = AgenteNombre,
            Role = "Documental",
            Description = "Lee documentos (recibos, facturas, polizas, poderes) via OCR y propone su registro en el modulo de Servicios.",
            Provider = provider,
            Model = null,
            SystemPrompt = SystemPrompt,
            IsActive = true,
            IncludeInOnboarding = true,
            SortOrder = 50,
            CreatedAt = DateTimeOffset.UtcNow,
            McpTools = Tools.Select(t => new AiAgentTemplateMcpTool
            {
                Id = Guid.NewGuid(),
                ConnectionCode = ConnectionCode,
                ToolName = t,
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList()
        };
        db.AiAgentTemplates.Add(template);
        await db.SaveChangesAsync();
        logger.LogWarning("[SEED] AgenteDocumentalSeeder: plantilla global '{Name}' creada (provider {Provider}).", AgenteNombre, provider);
    }

    /// <summary>
    /// Crea el agente en cada tenant que aun no lo tenga. Itera Tenants (global) y por cada uno fija
    /// el contexto + reabre la conexion para que el TenantConnectionInterceptor aplique app.tenant_id
    /// (RLS deja insertar en ai_agents / ai_agent_mcp_tools).
    /// </summary>
    private static async Task EnsureAgentsPerTenantAsync(PropiaDbContext db, ITenantContext tenant, AiProvider provider, ILogger logger)
    {
        var tenantIds = await db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync();
        var creados = 0;
        foreach (var tid in tenantIds)
        {
            try
            {
                tenant.SetTenant(tid);
                await db.Database.CloseConnectionAsync(); // el interceptor aplica app.tenant_id al reabrir

                var existente = await db.AiAgents.FirstOrDefaultAsync(a => a.Name == AgenteNombre);
                if (existente is not null)
                {
                    // Upsert del prompt: re-aplicar la definicion canonica si cambio.
                    if (existente.SystemPrompt != SystemPrompt)
                    {
                        existente.SystemPrompt = SystemPrompt;
                        existente.UpdatedAt = DateTimeOffset.UtcNow;
                        await db.SaveChangesAsync();
                    }
                    continue;
                }

                var agent = new AiAgent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tid,
                    Name = AgenteNombre,
                    Role = "Documental",
                    Provider = provider,
                    Model = null,
                    SystemPrompt = SystemPrompt,
                    IsActive = true,
                    SortOrder = 50,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AiAgents.Add(agent);
                foreach (var t in Tools)
                {
                    db.AiAgentMcpTools.Add(new AiAgentMcpTool
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tid,
                        AgentId = agent.Id,
                        ConnectionCode = ConnectionCode,
                        ToolName = t,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
                await db.SaveChangesAsync();
                creados++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[SEED] AgenteDocumentalSeeder: no se pudo crear el agente en tenant {TenantId}", tid);
                db.ChangeTracker.Clear();
            }
        }
        tenant.Clear();
        if (creados > 0)
            logger.LogWarning("[SEED] AgenteDocumentalSeeder: agente '{Name}' creado en {N} copropiedad(es).", AgenteNombre, creados);
    }
}
