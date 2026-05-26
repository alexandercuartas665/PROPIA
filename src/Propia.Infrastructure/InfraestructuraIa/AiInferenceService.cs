using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Inferencia de IA para probar un agente. Resuelve las credenciales del proveedor desde la config
/// GLOBAL de SuperAdmin (AiProviderConfig, cifrada con ISecretProtector); el tenant solo elige el
/// proveedor/modelo. Aplica control de cuota del plan y entrega de recursos. Portado de CUBOT.travels.
/// </summary>
public sealed class AiInferenceService : IAiInferenceService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IAiProviderClient _client;
    private readonly IAiUsageService _usage;

    public AiInferenceService(PropiaDbContext db, ISecretProtector secret, IAiProviderClient client, IAiUsageService usage)
    {
        _db = db;
        _secret = secret;
        _client = client;
        _usage = usage;
    }

    public async Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) { return new AiChatResult(false, null, "El agente no existe."); }

        // La cuenta del proveedor (API key, modelo, base url) la define el SuperAdmin (config global).
        var providerCfg = await _db.AiProviderConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Provider == agent.Provider, ct);
        if (providerCfg is null || !providerCfg.IsEnabled || string.IsNullOrWhiteSpace(providerCfg.ApiKeyEncrypted))
        {
            return new AiChatResult(false, null, $"El proveedor {agent.Provider} no esta habilitado en la plataforma.");
        }

        string apiKey;
        try { apiKey = _secret.Unprotect(providerCfg.ApiKeyEncrypted); }
        catch { return new AiChatResult(false, null, "La API key del proveedor esta cifrada con una version anterior. Vuelve a guardarla en Servidores de IA."); }

        var meta = AiProviderCatalog.For(agent.Provider);
        var model = !string.IsNullOrWhiteSpace(agent.Model) ? agent.Model!
            : !string.IsNullOrWhiteSpace(providerCfg.Model) ? providerCfg.Model!
            : meta.DefaultModel;

        if (turns.Count == 0) { return new AiChatResult(false, null, "Escribe un mensaje para probar el agente."); }

        // Control de cuota: si el plan tiene limite y ya se agoto el mes, no se ejecuta.
        var quota = await _usage.GetQuotaAsync(ct);
        if (quota.Exceeded)
        {
            return new AiChatResult(false, null, $"Alcanzaste el limite de llamadas de IA de tu plan este mes ({quota.MonthlyLimitCalls:N0}). Actualiza tu plan para seguir usando los agentes.");
        }

        var resources = await _db.AiAgentResources.AsNoTracking()
            .Where(r => r.AgentId == agentId).OrderBy(r => r.SortOrder)
            .Select(r => new AiChatAttachment(r.Name, r.ResourceType, r.FileUrl, r.FileName, r.Detail))
            .ToListAsync(ct);

        var systemPrompt = await BuildSystemPrompt(agentId, systemPromptOverride ?? agent.SystemPrompt, resources, ct);

        var result = await _client.CompleteAsync(agent.Provider, apiKey, providerCfg.BaseUrl, model, systemPrompt, turns, ct);

        if (result.Ok)
        {
            await _usage.RecordAsync(agent.Id, agent.Provider, model, result.InputTokens, result.OutputTokens, "test", true, ct);
        }

        if (result.Ok && !string.IsNullOrEmpty(result.Text))
        {
            var (cleanText, attachments) = ExtractAttachments(result.Text!, resources);
            return result with { Text = cleanText, Attachments = attachments };
        }

        return result;
    }

    private async Task<string> BuildSystemPrompt(Guid agentId, string basePrompt, IReadOnlyList<AiChatAttachment> resources, CancellationToken ct)
    {
        var sb = new StringBuilder(ExpandResourceRefs(basePrompt, resources));

        var prompts = await _db.AiAgentPrompts.AsNoTracking()
            .Where(p => p.AgentId == agentId).OrderBy(p => p.SortOrder)
            .Select(p => new { p.Name, p.Rule, p.Body })
            .ToListAsync(ct);
        if (prompts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Enrutador de prompts: evalua el mensaje del cliente y, si coincide alguna de estas reglas, sigue PRIMERO las instrucciones del prompt correspondiente (ademas del comportamiento base). Si ninguna aplica, responde con el comportamiento base.");
            foreach (var p in prompts)
            {
                sb.AppendLine();
                sb.AppendLine($"### Prompt \"{p.Name}\"");
                sb.AppendLine($"Regla (cuando usarlo): {(string.IsNullOrWhiteSpace(p.Rule) ? "(sin regla; usar a criterio)" : p.Rule)}");
                sb.AppendLine($"Instrucciones: {ExpandResourceRefs(p.Body, resources)}");
            }
        }

        if (resources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Recursos disponibles. REGLA IMPORTANTE: cuando vayas a comunicar el contenido de un recurso (precios, politicas, textos, imagenes, videos, PDF, ubicacion), NO lo reescribas ni lo resumas: entregalo EXACTO incluyendo en tu respuesta el marcador [[enviar: Nombre exacto del recurso]]. El sistema agregara el contenido o el archivo tal cual. Puedes acompanarlo con una frase breve, pero el contenido del recurso lo entrega el marcador.");
            foreach (var r in resources)
            {
                var kind = r.ResourceType == AgentResourceType.Text ? "Texto" : r.ResourceType.ToString();
                var desc = string.IsNullOrWhiteSpace(r.Detail) ? "archivo" : r.Detail;
                sb.AppendLine($"- ({kind}) {r.Name}: {desc}  -> entregar con [[enviar: {r.Name}]]");
            }
        }

        return sb.ToString();
    }

    private static string ExpandResourceRefs(string text, IReadOnlyList<AiChatAttachment> resources)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{")) { return text; }
        return Regex.Replace(text, @"\{\{\s*([^}]+?)\s*\}\}", m =>
        {
            var res = FindResource(resources, m.Groups[1].Value);
            if (res is null) { return m.Value; }
            return $"el recurso \"{res.Name}\" (entregalo EXACTO incluyendo el marcador [[enviar: {res.Name}]]; el sistema agrega su contenido, no lo reescribas)";
        });
    }

    private static (string, IReadOnlyList<AiChatAttachment>) ExtractAttachments(string text, IReadOnlyList<AiChatAttachment> resources)
    {
        var attachments = new List<AiChatAttachment>();
        var clean = Regex.Replace(text, @"\[\[\s*enviar\s*:\s*([^\]]+?)\s*\]\]", m =>
        {
            var res = FindResource(resources, m.Groups[1].Value);
            if (res is not null && attachments.All(a => a.Name != res.Name)) { attachments.Add(res); }
            return string.Empty;
        }, RegexOptions.IgnoreCase);

        clean = Regex.Replace(clean, @"[ \t]+\n", "\n").Trim();
        return (clean, attachments);
    }

    private static AiChatAttachment? FindResource(IReadOnlyList<AiChatAttachment> resources, string name)
    {
        var key = Normalize(name);
        return resources.FirstOrDefault(r => Normalize(r.Name) == key);
    }

    private static string Normalize(string s)
    {
        var n = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) { sb.Append(c); }
        }
        return sb.ToString();
    }
}
