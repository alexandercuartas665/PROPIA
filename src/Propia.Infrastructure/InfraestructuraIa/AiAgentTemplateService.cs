using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <inheritdoc cref="IAiAgentTemplateService"/>
public sealed class AiAgentTemplateService : IAiAgentTemplateService
{
    private readonly PropiaDbContext _db;
    private readonly ILogger<AiAgentTemplateService> _logger;

    public AiAgentTemplateService(PropiaDbContext db, ILogger<AiAgentTemplateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<AiAgentTemplateDto>> GetListAsync(CancellationToken ct = default)
    {
        var list = await _db.AiAgentTemplates
            .Include(t => t.McpTools)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<AiAgentTemplateDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.AiAgentTemplates
            .Include(x => x.McpTools)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? null : Map(t);
    }

    public async Task<AiAgentTemplateDto> SaveAsync(Guid? id, SaveAiAgentTemplateRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new InvalidOperationException("El nombre de la plantilla es obligatorio.");

        AiAgentTemplate t;
        var isNew = false;
        if (id is Guid existingId)
        {
            // No incluimos McpTools en el load: las borramos con ExecuteDeleteAsync (sin tracking)
            // para evitar AggregateUpdateConcurrencyException que EF emite cuando un batch mezcla
            // muchos DELETEs con un UPDATE y el conteo de rows-affected no le cuadra (problema
            // conocido con npgsql al batchear). ExecuteDeleteAsync emite un solo DELETE WHERE.
            var loaded = await _db.AiAgentTemplates
                .FirstOrDefaultAsync(x => x.Id == existingId, ct)
                ?? throw new InvalidOperationException("Plantilla no encontrada.");
            t = loaded;
            t.UpdatedAt = DateTimeOffset.UtcNow;
            t.UpdatedBy = actorId;

            await _db.AiAgentTemplateMcpTools
                .Where(x => x.TemplateId == existingId)
                .ExecuteDeleteAsync(ct);
            // McpTools queda vacia en la entidad (ya no la trackeamos aqui)
            t.McpTools.Clear();
        }
        else
        {
            isNew = true;
            // Id explicito para que los Add de tools puedan referenciar el TemplateId antes de SaveChanges.
            t = new AiAgentTemplate { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorId };
            _db.AiAgentTemplates.Add(t);
        }

        t.Name = req.Name.Trim();
        t.Role = req.Role?.Trim();
        t.Description = req.Description?.Trim();
        t.Provider = req.Provider;
        t.Model = req.Model?.Trim();
        t.SystemPrompt = req.SystemPrompt ?? "";
        t.IsActive = req.IsActive;
        t.IncludeInOnboarding = req.IncludeInOnboarding;
        t.SortOrder = req.SortOrder;

        // De-duplicar por (connection, tool) para no romper el unique index
        var unique = req.Tools
            .GroupBy(x => (x.ConnectionCode.Trim(), x.ToolName.Trim()))
            .Select(g => g.First())
            .ToList();
        // Importante: agregar al DbSet directamente con TemplateId + Id explicitos. Hacerlo via
        // t.McpTools.Add (navigation) en update path confundia a EF y emitia 32 UPDATEs (no INSERTs)
        // sobre los IDs nuevos -> AggregateUpdateConcurrencyException.
        foreach (var tool in unique)
        {
            _db.AiAgentTemplateMcpTools.Add(new AiAgentTemplateMcpTool
            {
                Id = Guid.NewGuid(),
                TemplateId = t.Id,
                ConnectionCode = tool.ConnectionCode.Trim(),
                ToolName = tool.ToolName.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = actorId
            });
        }

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = isNew ? "AI_AGENT_TEMPLATE_CREATE" : "AI_AGENT_TEMPLATE_UPDATE",
            EntidadAfectada = $"AiAgentTemplate:{t.Id}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Map(t);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var t = await _db.AiAgentTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        if (t.PlatformKey is not null)
            throw new InvalidOperationException(
                "Esta plantilla es un agente de PLATAFORMA (la usa el sistema directamente); no se puede eliminar. Puedes desactivarla o editar su prompt.");
        _db.AiAgentTemplates.Remove(t);

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = "AI_AGENT_TEMPLATE_DELETE",
            EntidadAfectada = $"AiAgentTemplate:{id}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> DeployToTenantAsync(Guid tenantId, string copropiedadNombre, string? organizacionNombre, CancellationToken ct = default)
    {
        // Las plantillas con PlatformKey son agentes de PLATAFORMA (ej. bienvenida): jamas se
        // despliegan a tenants, aunque alguien marque IncludeInOnboarding.
        var templates = await _db.AiAgentTemplates
            .Include(t => t.McpTools)
            .Where(t => t.IsActive && t.IncludeInOnboarding && t.PlatformKey == null)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .ToListAsync(ct);

        if (templates.Count == 0) return 0;

        var copied = 0;
        foreach (var template in templates)
        {
            try
            {
                var agent = new AiAgent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = template.Name,
                    Role = template.Role,
                    Provider = template.Provider,
                    Model = template.Model,
                    SystemPrompt = ReplacePlaceholders(template.SystemPrompt, copropiedadNombre, organizacionNombre),
                    IsActive = true,
                    SortOrder = template.SortOrder,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _db.AiAgents.Add(agent);

                foreach (var tool in template.McpTools.Where(x =>
                    McpConnectionCatalog.Find(x.ConnectionCode)?.PlatformOnly != true))
                {
                    _db.AiAgentMcpTools.Add(new AiAgentMcpTool
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        AgentId = agent.Id,
                        ConnectionCode = tool.ConnectionCode,
                        ToolName = tool.ToolName,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }

                await _db.SaveChangesAsync(ct);
                copied++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo desplegar plantilla {TemplateId} ({Name}) al tenant {TenantId}",
                    template.Id, template.Name, tenantId);
                // continuar con las siguientes
            }
        }

        return copied;
    }

    public async Task<List<ImportableAgentDto>> ListImportSourcesAsync(CancellationToken ct = default)
    {
        // Usamos la funcion SECURITY DEFINER admin_list_active_agents() para bypass RLS
        // sin tener que cambiar el rol propia_app a BYPASSRLS. Devuelve agentes + tenant + count.
        var conn = _db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);
        var result = new List<ImportableAgentDto>();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT agent_id, agent_name, role, provider, model, tenant_id, tenant_nombre, tools_count, is_active FROM admin_list_active_agents()";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var providerStr = reader.GetString(3);
                var provider = Enum.TryParse<Domain.Enums.AiProvider>(providerStr, true, out var p)
                    ? p
                    : Domain.Enums.AiProvider.Claude;
                result.Add(new ImportableAgentDto(
                    AgentId: reader.GetGuid(0),
                    AgentName: reader.GetString(1),
                    Role: reader.IsDBNull(2) ? null : reader.GetString(2),
                    Provider: provider,
                    Model: reader.IsDBNull(4) ? null : reader.GetString(4),
                    TenantId: reader.GetGuid(5),
                    TenantNombre: reader.GetString(6),
                    ToolsCount: (int)reader.GetInt64(7),
                    IsActive: reader.GetBoolean(8)));
            }
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
        return result;
    }

    public async Task<AiAgentTemplateDto> ImportFromAgentAsync(Guid agentId, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        // Usamos la funcion SECURITY DEFINER admin_get_agent_for_template para bypass RLS.
        var conn = _db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);

        string agentName, providerStr, systemPrompt, tenantNombre;
        string? role, model, organizacionNombre;
        int sortOrder;
        string toolsJson;
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name, role, provider, model, system_prompt, sort_order, tenant_nombre, organizacion_nombre, tools FROM admin_get_agent_for_template(@p)";
            var p = cmd.CreateParameter();
            p.ParameterName = "@p";
            p.Value = agentId;
            cmd.Parameters.Add(p);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Agente no encontrado.");
            agentName = reader.GetString(0);
            role = reader.IsDBNull(1) ? null : reader.GetString(1);
            providerStr = reader.GetString(2);
            model = reader.IsDBNull(3) ? null : reader.GetString(3);
            systemPrompt = reader.GetString(4);
            sortOrder = reader.GetInt32(5);
            tenantNombre = reader.GetString(6);
            organizacionNombre = reader.IsDBNull(7) ? null : reader.GetString(7);
            toolsJson = reader.GetString(8);
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }

        var provider = Enum.TryParse<Domain.Enums.AiProvider>(providerStr, true, out var prov)
            ? prov
            : Domain.Enums.AiProvider.Claude;

        // Reverse placeholder substitution
        var promptParametrizado = ReversePlaceholders(systemPrompt, tenantNombre, organizacionNombre);

        var template = new AiAgentTemplate
        {
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actorId,
            Name = agentName,
            Role = role,
            Description = $"Importado del agente '{agentName}' del tenant '{tenantNombre}'.",
            Provider = provider,
            Model = model,
            SystemPrompt = promptParametrizado,
            IsActive = true,
            IncludeInOnboarding = false,
            SortOrder = sortOrder
        };
        _db.AiAgentTemplates.Add(template);

        // Parse tools JSON [{"connection_code":"x","tool_name":"y"}, ...]
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(toolsJson);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var cc = el.GetProperty("connection_code").GetString();
                var tn = el.GetProperty("tool_name").GetString();
                if (cc is null || tn is null) continue;
                template.McpTools.Add(new AiAgentTemplateMcpTool
                {
                    Template = template,
                    ConnectionCode = cc,
                    ToolName = tn,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = actorId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron parsear las tools del agente {AgentId}; la plantilla quedara sin tools.", agentId);
        }

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = "AI_AGENT_TEMPLATE_IMPORT",
            EntidadAfectada = $"AiAgent:{agentId}->AiAgentTemplate:{template.Id}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Map(template);
    }

    /// <summary>
    /// Reemplaza ocurrencias literales del nombre del tenant/organizacion por sus placeholders.
    /// Hace match case-insensitive. Si el nombre es muy corto (&lt; 3 chars) lo omite para evitar
    /// falsos positivos.
    /// </summary>
    private static string ReversePlaceholders(string prompt, string copropiedadNombre, string? organizacionNombre)
    {
        if (string.IsNullOrEmpty(prompt)) return prompt;
        var result = prompt;
        if (!string.IsNullOrEmpty(organizacionNombre) && organizacionNombre.Length >= 3)
        {
            result = System.Text.RegularExpressions.Regex.Replace(
                result, System.Text.RegularExpressions.Regex.Escape(organizacionNombre),
                "{ORGANIZACION_NOMBRE}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        if (!string.IsNullOrEmpty(copropiedadNombre) && copropiedadNombre.Length >= 3)
        {
            result = System.Text.RegularExpressions.Regex.Replace(
                result, System.Text.RegularExpressions.Regex.Escape(copropiedadNombre),
                "{COPROPIEDAD_NOMBRE}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return result;
    }

    // ---------- helpers ----------

    private static string ReplacePlaceholders(string prompt, string copropiedadNombre, string? organizacionNombre)
    {
        var result = prompt
            .Replace("{COPROPIEDAD_NOMBRE}", copropiedadNombre, StringComparison.Ordinal)
            .Replace("{ORGANIZACION_NOMBRE}", organizacionNombre ?? copropiedadNombre, StringComparison.Ordinal);
        return result;
    }

    private static AiAgentTemplateDto Map(AiAgentTemplate t) => new(
        t.Id, t.Name, t.Role, t.Description, t.Provider, t.Model, t.SystemPrompt,
        t.IsActive, t.IncludeInOnboarding, t.SortOrder,
        t.McpTools.OrderBy(x => x.ConnectionCode).ThenBy(x => x.ToolName)
            .Select(x => new AiAgentTemplateToolDto(x.ConnectionCode, x.ToolName)).ToList(),
        t.PlatformKey);
}
