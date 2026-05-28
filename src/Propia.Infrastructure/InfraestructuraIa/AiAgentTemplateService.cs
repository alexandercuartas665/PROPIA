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
            var loaded = await _db.AiAgentTemplates
                .Include(x => x.McpTools)
                .FirstOrDefaultAsync(x => x.Id == existingId, ct)
                ?? throw new InvalidOperationException("Plantilla no encontrada.");
            t = loaded;
            t.UpdatedAt = DateTimeOffset.UtcNow;
            t.UpdatedBy = actorId;
            // Limpiamos tools para re-crearlas (mas simple que diff)
            _db.AiAgentTemplateMcpTools.RemoveRange(t.McpTools);
            t.McpTools.Clear();
        }
        else
        {
            isNew = true;
            t = new AiAgentTemplate { CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorId };
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

        // De-duplicar tools que vengan repetidas
        foreach (var tool in req.Tools.Distinct())
        {
            t.McpTools.Add(new AiAgentTemplateMcpTool
            {
                Template = t,
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
        var templates = await _db.AiAgentTemplates
            .Include(t => t.McpTools)
            .Where(t => t.IsActive && t.IncludeInOnboarding)
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

                foreach (var tool in template.McpTools)
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
            .Select(x => new AiAgentTemplateToolDto(x.ConnectionCode, x.ToolName)).ToList());
}
