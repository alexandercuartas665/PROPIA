using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Bienvenida;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Bienvenida;

/// <summary>
/// Asistente del onboarding /bienvenida. Agente de PLATAFORMA: su definicion (prompt, proveedor,
/// modelo, tools) vive en la AiAgentTemplate con PlatformKey="bienvenida", editable por el Super
/// Admin en Plantillas de agentes IA. Si la plantilla no existe se crea sola (seed idempotente)
/// con el prompt por defecto. Las credenciales salen de AiProviderConfigs (global, sin tenant).
/// Tools: las de la plantilla, via la conexion MCP "plataforma" (PlatformOnly) con el bearer del
/// usuario; se ejecutan en un loop de function-calling igual al de AiInferenceService pero sin
/// cuota ni recursos de tenant (aqui no hay tenant).
/// </summary>
public class AsistenteBienvenidaService : IAsistenteBienvenidaService
{
    private const int MaxToolRounds = 4;

    private static readonly string[] NombresPasos =
        { "Bienvenida", "Tu perfil", "Co-propiedad", "Estructura", "Personas" };

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IAiProviderClient _client;
    private readonly IMcpGateway _mcp;
    private readonly ILogger<AsistenteBienvenidaService> _logger;

    public AsistenteBienvenidaService(
        PropiaDbContext db,
        ISecretProtector secret,
        IAiProviderClient client,
        IMcpGateway mcp,
        ILogger<AsistenteBienvenidaService> logger)
    {
        _db = db;
        _secret = secret;
        _client = client;
        _mcp = mcp;
        _logger = logger;
    }

    public async Task<BienvenidaChatDto> ResponderAsync(BienvenidaChatRequest req, string? bearerToken, CancellationToken ct)
    {
        var template = await ObtenerPlantillaAsync(ct);

        var credenciales = await ResolverProveedorAsync(template, ct);
        if (credenciales is null)
            return new BienvenidaChatDto(false, null, "ia_no_configurada");

        var (provider, apiKey, baseUrl, model) = credenciales.Value;

        var paso = Math.Clamp(req.Paso, 0, NombresPasos.Length - 1);
        var basePrompt = template is not null && !string.IsNullOrWhiteSpace(template.SystemPrompt)
            ? template.SystemPrompt
            : BienvenidaPrompts.Sistema;
        var system = basePrompt +
            $"\n\nContexto actual: el usuario esta en el paso {paso + 1} de {NombresPasos.Length} ({NombresPasos[paso]})." +
            (string.IsNullOrWhiteSpace(req.NombreUsuario) ? "" : $" Se llama {req.NombreUsuario.Trim()}.") +
            (string.IsNullOrWhiteSpace(req.ContextoPaso) ? "" : $"\nDatos que lleva diligenciados: {req.ContextoPaso.Trim()}");

        // Ultimos 16 turnos: suficiente memoria para el recorrido sin inflar el prompt.
        var turns = (req.Conversacion ?? new List<BienvenidaTurno>())
            .Where(t => !string.IsNullOrWhiteSpace(t.Texto))
            .TakeLast(16)
            .Select(t => new AiChatTurn(t.Rol == "model" ? "model" : "user", t.Texto.Trim()))
            .ToList();
        if (turns.Count == 0)
            return new BienvenidaChatDto(false, null, "conversacion_vacia");

        return await CompletarAsync(template, provider, apiKey, baseUrl, model, system, turns, bearerToken, ct);
    }

    public async Task<BienvenidaChatDto> ProbarAsync(Guid templateId, List<BienvenidaTurno> conversacion, string? bearerToken, CancellationToken ct)
    {
        var template = await _db.AiAgentTemplates.AsNoTracking()
            .Include(x => x.McpTools)
            .FirstOrDefaultAsync(x => x.Id == templateId, ct);
        if (template is null)
            return new BienvenidaChatDto(false, null, "plantilla_no_encontrada");
        if (template.PlatformKey is null)
            return new BienvenidaChatDto(false, null, "solo_agentes_de_plataforma");

        var turns = (conversacion ?? new List<BienvenidaTurno>())
            .Where(t => !string.IsNullOrWhiteSpace(t.Texto))
            .TakeLast(16)
            .Select(t => new AiChatTurn(t.Rol == "model" ? "model" : "user", t.Texto.Trim()))
            .ToList();
        if (turns.Count == 0)
            return new BienvenidaChatDto(false, null, "conversacion_vacia");

        var credenciales = await ResolverProveedorAsync(template, ct);
        if (credenciales is null)
            return new BienvenidaChatDto(false, null, "ia_no_configurada");
        var (provider, apiKey, baseUrl, model) = credenciales.Value;

        var system = (string.IsNullOrWhiteSpace(template.SystemPrompt) ? BienvenidaPrompts.Sistema : template.SystemPrompt)
            + "\n\nContexto: modo PRUEBA desde la consola del Super Admin (no es un usuario real del onboarding).";
        return await CompletarAsync(template, provider, apiKey, baseUrl, model, system, turns, bearerToken, ct);
    }

    /// <summary>Nucleo comun: tools de la plantilla (si hay bearer) o completado directo.</summary>
    private async Task<BienvenidaChatDto> CompletarAsync(
        AiAgentTemplate? template, Domain.Enums.AiProvider provider, string apiKey, string? baseUrl, string model,
        string system, List<AiChatTurn> turns, string? bearerToken, CancellationToken ct)
    {
        try
        {
            var toolSpecs = await BuildToolSpecsAsync(template, bearerToken, ct);
            if (toolSpecs.Count > 0 && !string.IsNullOrEmpty(bearerToken))
            {
                return await RunToolLoopAsync(provider, apiKey, baseUrl, model, system, turns, toolSpecs, bearerToken!, ct);
            }

            var res = await _client.CompleteAsync(provider, apiKey, baseUrl, model, system, turns, ct);
            return new BienvenidaChatDto(res.Ok, res.Text, res.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo el asistente de bienvenida (proveedor {Provider})", provider);
            return new BienvenidaChatDto(false, null, "error_inferencia");
        }
    }

    public async Task<BienvenidaChatDto> GenerarDescripcionAsync(BienvenidaDescripcionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            return new BienvenidaChatDto(false, null, "nombre_requerido");

        var template = await ObtenerPlantillaAsync(ct);
        var credenciales = await ResolverProveedorAsync(template, ct);
        if (credenciales is null)
            return new BienvenidaChatDto(false, null, "ia_no_configurada");

        var (provider, apiKey, baseUrl, model) = credenciales.Value;

        var datos = $"Nombre: {req.Nombre.Trim()}"
            + (string.IsNullOrWhiteSpace(req.Tipo) ? "" : $". Tipo: {req.Tipo}")
            + (string.IsNullOrWhiteSpace(req.Ciudad) ? "" : $". Ciudad: {req.Ciudad}")
            + (string.IsNullOrWhiteSpace(req.Departamento) ? "" : $". Departamento: {req.Departamento}")
            + (string.IsNullOrWhiteSpace(req.Estrato) ? "" : $". Estrato: {req.Estrato}")
            + (string.IsNullOrWhiteSpace(req.TextoActual) ? "" : $". Borrador del usuario a mejorar: {req.TextoActual.Trim()}");

        try
        {
            var res = await _client.CompleteAsync(provider, apiKey, baseUrl, model,
                BienvenidaPrompts.Descripcion,
                new List<AiChatTurn> { new("user", datos) }, ct);
            return new BienvenidaChatDto(res.Ok, res.Text?.Trim(), res.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo generando la descripcion de bienvenida (proveedor {Provider})", provider);
            return new BienvenidaChatDto(false, null, "error_inferencia");
        }
    }

    // ---------- plantilla de plataforma ----------

    /// <summary>
    /// Plantilla del agente de bienvenida (PlatformKey="bienvenida"). Si no existe, se crea con
    /// el prompt por defecto (seed idempotente): asi el Super Admin la encuentra lista para editar
    /// en Plantillas de agentes IA. Si esta desactivada (IsActive=false) se ignora y el asistente
    /// cae al prompt por defecto sin tools.
    /// </summary>
    private async Task<AiAgentTemplate?> ObtenerPlantillaAsync(CancellationToken ct)
    {
        var t = await _db.AiAgentTemplates.AsNoTracking()
            .Include(x => x.McpTools)
            .FirstOrDefaultAsync(x => x.PlatformKey == AiAgentTemplate.BienvenidaKey, ct);
        if (t is not null) return t.IsActive ? t : null;

        try
        {
            var nueva = new AiAgentTemplate
            {
                Id = Guid.NewGuid(),
                Name = "Auxiliar de Bienvenida",
                Role = "Agente de plataforma",
                Description = "Acompana el onboarding /bienvenida (usuarios sin copropiedad). Lo ejecuta la plataforma directamente: NUNCA se despliega a tenants.",
                Provider = Domain.Enums.AiProvider.Gemini,
                SystemPrompt = BienvenidaPrompts.Sistema,
                IsActive = true,
                IncludeInOnboarding = false,
                PlatformKey = AiAgentTemplate.BienvenidaKey,
                SortOrder = 999,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.AiAgentTemplates.Add(nueva);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Plantilla de plataforma 'Auxiliar de Bienvenida' creada (seed perezoso).");
            return nueva;
        }
        catch (Exception ex)
        {
            // Carrera con otra request u otro problema: seguimos con el prompt por defecto.
            _logger.LogWarning(ex, "No se pudo sembrar la plantilla de bienvenida; se usa el prompt por defecto.");
            return await _db.AiAgentTemplates.AsNoTracking()
                .Include(x => x.McpTools)
                .FirstOrDefaultAsync(x => x.PlatformKey == AiAgentTemplate.BienvenidaKey && x.IsActive, ct);
        }
    }

    /// <summary>
    /// Credenciales del proveedor: el de la plantilla si esta habilitado en AiProviderConfigs;
    /// si no, el primer proveedor global habilitado cuya key descifre. Modelo: plantilla >
    /// config global > default del catalogo (mismo criterio de AiInferenceService).
    /// </summary>
    private async Task<(Domain.Enums.AiProvider Provider, string ApiKey, string? BaseUrl, string Model)?> ResolverProveedorAsync(
        AiAgentTemplate? template, CancellationToken ct)
    {
        var configs = await _db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled && c.ApiKeyEncrypted != null)
            .OrderBy(c => c.Provider)
            .ToListAsync(ct);
        if (template is not null)
        {
            configs = configs.OrderBy(c => c.Provider == template.Provider ? 0 : 1).ThenBy(c => c.Provider).ToList();
        }

        foreach (var cfg in configs)
        {
            string apiKey;
            try { apiKey = _secret.Unprotect(cfg.ApiKeyEncrypted!); }
            catch { continue; }

            var meta = AiProviderCatalog.For(cfg.Provider);
            var model = template is not null && cfg.Provider == template.Provider && !string.IsNullOrWhiteSpace(template.Model)
                ? template.Model!
                : !string.IsNullOrWhiteSpace(cfg.Model) ? cfg.Model! : meta.DefaultModel;
            return (cfg.Provider, apiKey, cfg.BaseUrl, model);
        }
        return null;
    }

    // ---------- tools de plataforma ----------

    private readonly Dictionary<string, string> _toolToConnection = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Specs de las tools habilitadas en la plantilla, descubiertas en vivo del servidor MCP.
    /// Solo se aceptan conexiones PlatformOnly (defensa: aunque alguien agregue una tool de
    /// "copropiedades" a la plantilla, aqui no se ejecuta - no hay tenant).
    /// </summary>
    private async Task<IReadOnlyList<AiToolSpec>> BuildToolSpecsAsync(AiAgentTemplate? template, string? bearerToken, CancellationToken ct)
    {
        _toolToConnection.Clear();
        if (template is null || template.McpTools.Count == 0 || string.IsNullOrEmpty(bearerToken))
            return Array.Empty<AiToolSpec>();

        var specs = new List<AiToolSpec>();
        foreach (var grupo in template.McpTools.GroupBy(t => t.ConnectionCode))
        {
            if (McpConnectionCatalog.Find(grupo.Key)?.PlatformOnly != true) continue;
            var habilitadas = grupo.Select(g => g.ToolName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            try
            {
                var tools = await _mcp.ListToolsAsync(grupo.Key, bearerToken!, ct);
                foreach (var t in tools.Where(t => habilitadas.Contains(t.Name)))
                {
                    _toolToConnection[t.Name] = grupo.Key;
                    specs.Add(new AiToolSpec(t.Name, t.Description, t.InputSchemaJson ?? "{}"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron listar las tools de la conexion {Conexion} para bienvenida", grupo.Key);
            }
        }
        return specs;
    }

    /// <summary>Loop de function-calling (mismo patron de AiInferenceService, sin cuota de tenant).</summary>
    private async Task<BienvenidaChatDto> RunToolLoopAsync(
        Domain.Enums.AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt,
        IReadOnlyList<AiChatTurn> turns, IReadOnlyList<AiToolSpec> toolSpecs, string bearerToken, CancellationToken ct)
    {
        var msgs = new List<AiToolMessage>(turns.Select(t => new AiToolMessage(t.Role, t.Text)));

        for (var ronda = 0; ronda < MaxToolRounds; ronda++)
        {
            var comp = await _client.CompleteWithToolsAsync(provider, apiKey, baseUrl, model, systemPrompt, msgs, toolSpecs, ct);
            if (!comp.Ok)
                return new BienvenidaChatDto(false, null, comp.Error);
            if (comp.ToolCalls.Count == 0)
                return new BienvenidaChatDto(true, comp.Text, null);

            msgs.Add(new AiToolMessage("assistant", comp.Text, comp.ToolCalls));
            foreach (var call in comp.ToolCalls)
            {
                string resultado;
                try
                {
                    if (!_toolToConnection.TryGetValue(call.Name, out var conexion))
                    {
                        resultado = $"La tool '{call.Name}' no esta habilitada para este agente.";
                    }
                    else
                    {
                        resultado = await _mcp.CallToolAsync(conexion, call.Name, ParseArgs(call.ArgumentsJson), bearerToken, null, null, ct);
                    }
                }
                catch (Exception ex) { resultado = $"Error ejecutando '{call.Name}': {ex.Message}"; }
                msgs.Add(new AiToolMessage("tool", resultado, null, call.Id, call.Name));
            }
        }
        return new BienvenidaChatDto(false, null, $"El asistente supero el maximo de pasos de herramientas ({MaxToolRounds}).");
    }

    private static IReadOnlyDictionary<string, object?> ParseArgs(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) { return new Dictionary<string, object?>(); }
        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
            return dict ?? new Dictionary<string, object?>();
        }
        catch { return new Dictionary<string, object?>(); }
    }
}
