using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Siembra el "Agente Documental": un agente IA que lee documentos (recibos, facturas, polizas,
/// poderes) extraidos por OCR y, via las tools MCP del modulo de Servicios, propone el registro
/// (registrar consumo de un recibo, crear servicio/poliza, etc.) para que el usuario confirme.
///
/// SEMILLA (corre en todos los ambientes, idempotente): solo crea la plantilla global del Super Admin si
/// no existe, con su clave de agente del sistema (PlantillaAgente.ClaveDocumental). NO pisa el prompt de
/// una plantilla existente ni toca los agentes de las copropiedades: el Super Admin manda (edita la
/// plantilla y la propaga con "Actualizar copropiedades"), y a una copropiedad que no tenga el agente se
/// le crea al usarlo (AgentesSistemaService). El provider se toma del proveedor LLM habilitado.
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
vacio y pidelo. Busca en el encabezado, las clausulas, la hoja de firmas y los
anexos. Normaliza (formatos colombianos):
- Moneda en COP sin simbolos: "$ 36.000.000,00" -> 36000000 (punto = miles).
- Fechas como AAAA-MM-DD aunque esten en texto: "1 de enero de 2026" -> 2026-01-01.
- NIT o cedula sin puntos, comas ni espacios, con el digito de verificacion
  separado por guion: "900.456.789-1" -> 900456789-1.
- Mes como numero 1-12.
- El contratista o la aseguradora NUNCA es la copropiedad.

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
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        // Provider del LLM habilitado (global, sin RLS); si no hay, Claude por defecto.
        var provider = await db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .Select(c => (AiProvider?)c.Provider)
            .FirstOrDefaultAsync() ?? AiProvider.Claude;

        await EnsureTemplateAsync(db, provider, logger);
    }

    /// <summary>Plantilla global (BaseEntity, sin RLS) para que las copropiedades nuevas la reciban.</summary>
    private static async Task EnsureTemplateAsync(PropiaDbContext db, AiProvider provider, ILogger logger)
    {
        // Super Admin manda: si la plantilla ya existe NO se pisa su prompt (las mejoras se hacen en el Super
        // Admin y se propagan con "Actualizar copropiedades"); solo se le asegura la clave del agente del sistema.
        var existente = await db.AiAgentTemplates.FirstOrDefaultAsync(t => t.Clave == PlantillaAgente.ClaveDocumental)
            ?? await db.AiAgentTemplates.FirstOrDefaultAsync(t => t.Name == AgenteNombre && t.PlatformKey == null);
        if (existente is not null)
        {
            if (existente.Clave is null)
            {
                existente.Clave = PlantillaAgente.ClaveDocumental;
                existente.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                logger.LogWarning("[SEED] AgenteDocumentalSeeder: clave asignada a la plantilla existente.");
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
            Clave = PlantillaAgente.ClaveDocumental,
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

}
