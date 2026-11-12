using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <inheritdoc cref="IAiAgentTemplateService"/>
public sealed class AiAgentTemplateService : IAiAgentTemplateService
{
    private readonly PropiaDbContext _db;
    private readonly ILogger<AiAgentTemplateService> _logger;
    private readonly ITenantContext _tenant;

    public AiAgentTemplateService(PropiaDbContext db, ILogger<AiAgentTemplateService> logger, ITenantContext tenant)
    {
        _db = db;
        _logger = logger;
        _tenant = tenant;
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

            // Version: sube si cambia lo que "Actualizar copropiedades" propaga: prompt, tools, proveedor o modelo.
            var toolsAntes = await _db.AiAgentTemplateMcpTools.AsNoTracking().Where(x => x.TemplateId == existingId)
                .Select(x => x.ConnectionCode + "|" + x.ToolName).ToListAsync(ct);
            var toolsDespues = req.Tools.Select(x => x.ConnectionCode.Trim() + "|" + x.ToolName.Trim()).Distinct().ToList();
            if (!string.Equals(t.SystemPrompt, req.SystemPrompt ?? "", StringComparison.Ordinal)
                || !toolsAntes.OrderBy(x => x).SequenceEqual(toolsDespues.OrderBy(x => x))
                || t.Provider != req.Provider
                || !string.Equals(t.Model ?? "", req.Model?.Trim() ?? "", StringComparison.Ordinal))
                t.Version++;

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
                // Mismo agente que "Actualizar copropiedades" y la creacion al primer uso (vinculo + version + huella).
                var (agent, tools) = PlantillaAgenteFabrica.Nuevo(template, tenantId, copropiedadNombre, organizacionNombre);
                _db.AiAgents.Add(agent);
                _db.AiAgentMcpTools.AddRange(tools);

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

    // ===================== "Actualizar copropiedades" =====================

    private sealed record Copropiedad(Guid Id, string Nombre, string? Org);

    // Copropiedades vigentes (las canceladas/eliminadas no se tocan). Tenants es global (sin RLS).
    private Task<List<Copropiedad>> CopropiedadesAsync(CancellationToken ct)
        => _db.Tenants.AsNoTracking()
            .Where(t => t.Estado != EstadoCopropiedad.CanceladoArchivado && t.Estado != EstadoCopropiedad.Eliminado)
            .OrderBy(t => t.Nombre)
            .Select(t => new Copropiedad(t.Id, t.Nombre, t.Organizacion != null ? t.Organizacion.Nombre : null))
            .ToListAsync(ct);

    // El agente de la copropiedad activa que salio de la plantilla; si no hay vinculo, el que tenga el nombre
    // de la plantilla (agentes viejos).
    private async Task<AiAgent?> AgenteDeAsync(AiAgentTemplate t, string nombreRender, CancellationToken ct)
        => await _db.AiAgents.Where(a => a.TemplateId == t.Id).OrderByDescending(a => a.IsActive).ThenBy(a => a.CreatedAt).FirstOrDefaultAsync(ct)
           ?? await _db.AiAgents.Where(a => a.TemplateId == null && a.Name == nombreRender).OrderByDescending(a => a.IsActive).FirstOrDefaultAsync(ct);

    private static EstadoPropagacion Estado(AiAgentTemplate t, AiAgent? a, string promptRender)
    {
        if (a is null) return EstadoPropagacion.Falta;
        if (a.TemplateVersion == t.Version && a.SystemPrompt == promptRender && a.Provider == t.Provider && (a.Model ?? "") == (t.Model ?? ""))
            return EstadoPropagacion.AlDia;
        if (PlantillaAgenteFabrica.Editado(a)) return EstadoPropagacion.Editado;
        // Sin huella (agente viejo sin vinculo): si su prompt no es el de la plantilla, se trata como editado.
        if (a.TemplatePromptHash is null && a.SystemPrompt != promptRender) return EstadoPropagacion.Editado;
        return EstadoPropagacion.Desactualizado;
    }

    // Recorre las copropiedades fijando el tenant por cada una (RLS de ai_agents) y lo restaura al final.
    private async Task PorCopropiedadAsync(Func<Copropiedad, Task> accion, CancellationToken ct)
    {
        var previo = _tenant.CurrentTenantId;
        try
        {
            foreach (var c in await CopropiedadesAsync(ct))
            {
                _tenant.SetTenant(c.Id);
                await _db.Database.CloseConnectionAsync();   // el interceptor aplica app.tenant_id al reabrir
                await accion(c);
            }
        }
        finally
        {
            if (previo is Guid p) _tenant.SetTenant(p); else _tenant.Clear();
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task<PropagacionPreviewDto?> PreviewPropagacionAsync(Guid templateId, CancellationToken ct = default)
    {
        var t = await _db.AiAgentTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == templateId, ct);
        if (t is null) return null;
        if (t.PlatformKey is not null) throw new InvalidOperationException("Es un agente de plataforma: no se copia a las copropiedades.");
        var filas = new List<PropagacionFilaDto>();
        await PorCopropiedadAsync(async c =>
        {
            var a = await AgenteDeAsync(t, PlantillaAgente.Render(t.Name, c.Nombre, c.Org), ct);
            filas.Add(new PropagacionFilaDto(c.Id, c.Nombre, Estado(t, a, PlantillaAgente.Render(t.SystemPrompt, c.Nombre, c.Org)), a?.Name, a?.TemplateVersion));
        }, ct);
        return new PropagacionPreviewDto(t.Id, t.Name, t.Version,
            filas.Count(f => f.Estado == EstadoPropagacion.AlDia), filas.Count(f => f.Estado == EstadoPropagacion.Desactualizado),
            filas.Count(f => f.Estado == EstadoPropagacion.Editado), filas.Count(f => f.Estado == EstadoPropagacion.Falta), filas);
    }

    public async Task<PropagacionResultadoDto?> PropagarAsync(Guid templateId, PropagarPlantillaRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var t = await _db.AiAgentTemplates.AsNoTracking().Include(x => x.McpTools).FirstOrDefaultAsync(x => x.Id == templateId, ct);
        if (t is null) return null;
        if (t.PlatformKey is not null) throw new InvalidOperationException("Es un agente de plataforma: no se copia a las copropiedades.");
        int actualizadas = 0, creadas = 0, respetadas = 0, alDia = 0, errores = 0;
        await PorCopropiedadAsync(async c =>
        {
            try
            {
                var prompt = PlantillaAgente.Render(t.SystemPrompt, c.Nombre, c.Org);
                var a = await AgenteDeAsync(t, PlantillaAgente.Render(t.Name, c.Nombre, c.Org), ct);
                var estado = Estado(t, a, prompt);
                if (estado == EstadoPropagacion.AlDia) { alDia++; return; }
                if (estado == EstadoPropagacion.Falta)
                {
                    if (!req.CrearFaltantes || !t.IsActive) { respetadas++; return; }
                    var (nuevo, toolsNuevo) = PlantillaAgenteFabrica.Nuevo(t, c.Id, c.Nombre, c.Org);
                    _db.AiAgents.Add(nuevo);
                    _db.AiAgentMcpTools.AddRange(toolsNuevo);
                    await _db.SaveChangesAsync(ct);
                    creadas++;
                    return;
                }
                if (estado == EstadoPropagacion.Editado && !req.ForzarEditadas) { respetadas++; return; }

                await ActualizarAgenteAsync(t, a!, prompt, ct);
                actualizadas++;
            }
            catch (Exception ex)
            {
                errores++;
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "No se pudo actualizar la plantilla {TemplateId} en la copropiedad {TenantId}", t.Id, c.Id);
            }
        }, ct);

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = "AI_AGENT_TEMPLATE_PROPAGATE",
            EntidadAfectada = $"AiAgentTemplate:{t.Id} v{t.Version} (actualizadas {actualizadas}, creadas {creadas}, respetadas {respetadas}, errores {errores}{(req.ForzarEditadas ? ", forzado" : "")})",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return new PropagacionResultadoDto(actualizadas, creadas, respetadas, alDia, errores);
    }

    // Lleva UN agente a la version actual de su plantilla: prompt, proveedor y modelo de IA, vinculo + version + huella
    // y tools que falten. Queda en el historial del agente para que la copropiedad pueda volver a su version anterior.
    private async Task ActualizarAgenteAsync(AiAgentTemplate t, AiAgent a, string prompt, CancellationToken ct)
    {
        a.SystemPrompt = prompt;
        a.Provider = t.Provider;
        a.Model = t.Model;
        a.TemplateId = t.Id;
        a.TemplateVersion = t.Version;
        a.TemplatePromptHash = PlantillaAgente.Hash(prompt);
        var enrutados = await _db.AiAgentPrompts.AsNoTracking().Where(p => p.AgentId == a.Id).OrderBy(p => p.SortOrder)
            .Select(p => new AgentPromptSnapshotDto(p.Name, p.Rule, p.Body, p.SortOrder)).ToListAsync(ct);
        a.PromptHistoryJson = AiAgentService.PushPromptVersion(a.PromptHistoryJson,
            new AiAgentService.StoredPromptVersion(DateTimeOffset.UtcNow, prompt, enrutados));
        var actuales = await _db.AiAgentMcpTools.AsNoTracking().Where(x => x.AgentId == a.Id).ToListAsync(ct);
        _db.AiAgentMcpTools.AddRange(PlantillaAgenteFabrica.ToolsFaltantes(t, a, actuales));
        await _db.SaveChangesAsync(ct);
    }

    // Plantilla + prompt renderizado para el agente de la copropiedad activa (RLS: solo agentes del tenant).
    private async Task<(AiAgent Agente, AiAgentTemplate Plantilla, string Prompt)?> PlantillaDeAgenteAsync(Guid agentId, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        var a = await _db.AiAgents.FirstOrDefaultAsync(x => x.Id == agentId, ct);
        if (a?.TemplateId is not Guid templateId) return null;
        var t = await _db.AiAgentTemplates.AsNoTracking().Include(x => x.McpTools).FirstOrDefaultAsync(x => x.Id == templateId, ct);
        if (t is null) return null;
        var c = await _db.Tenants.AsNoTracking().Where(x => x.Id == tid)
            .Select(x => new Copropiedad(x.Id, x.Nombre, x.Organizacion != null ? x.Organizacion.Nombre : null)).FirstAsync(ct);
        return (a, t, PlantillaAgente.Render(t.SystemPrompt, c.Nombre, c.Org));
    }

    private static AgentePlantillaInfoDto Info(AiAgentTemplate t, AiAgent a, string prompt)
    {
        var estado = Estado(t, a, prompt);
        return new AgentePlantillaInfoDto(t.Id, t.Name, t.Version, a.TemplateVersion, estado == EstadoPropagacion.Editado, estado == EstadoPropagacion.AlDia);
    }

    public async Task<AgentePlantillaInfoDto?> InfoPlantillaDeAgenteAsync(Guid agentId, CancellationToken ct = default)
        => await PlantillaDeAgenteAsync(agentId, ct) is { } x ? Info(x.Plantilla, x.Agente, x.Prompt) : null;

    public async Task<AgentePlantillaInfoDto?> AplicarPlantillaAAgenteAsync(Guid agentId, CancellationToken ct = default)
    {
        if (await PlantillaDeAgenteAsync(agentId, ct) is not { } x) return null;
        await ActualizarAgenteAsync(x.Plantilla, x.Agente, x.Prompt, ct);
        return Info(x.Plantilla, x.Agente, x.Prompt);
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
        t.PlatformKey, t.Version, t.Clave);
}
