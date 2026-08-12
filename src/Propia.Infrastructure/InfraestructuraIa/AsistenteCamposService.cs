using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Genera el contenido de campos de texto con el agente "Auxiliar Administrativo" de la copropiedad
/// activa. Resuelve (o crea al vuelo) el agente y delega en IAiInferenceService, que ya resuelve
/// credenciales/modelo del proveedor global y controla cuota. Sin tools MCP: completado de un turno.
/// </summary>
public class AsistenteCamposService : IAsistenteCamposService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAiInferenceService _inference;
    private readonly ILogger<AsistenteCamposService> _logger;

    public AsistenteCamposService(PropiaDbContext db, ITenantContext tenant, IAiInferenceService inference, ILogger<AsistenteCamposService> logger)
    {
        _db = db;
        _tenant = tenant;
        _inference = inference;
        _logger = logger;
    }

    public async Task<Guid?> EnsureAgenteAsync(CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;

        var existente = await _db.AiAgents
            .Where(a => a.Name == AuxiliarAdministrativoAgente.Nombre || a.Role == AuxiliarAdministrativoAgente.RoleTag)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync(ct);
        if (existente is not null) return existente.Id;

        // Provider del LLM habilitado (global, sin RLS); si no hay, Claude por defecto.
        var provider = await _db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .Select(c => (AiProvider?)c.Provider)
            .FirstOrDefaultAsync(ct) ?? AiProvider.Claude;

        var agent = new AiAgent
        {
            Id = Guid.NewGuid(),
            TenantId = tid,
            Name = AuxiliarAdministrativoAgente.Nombre,
            Role = AuxiliarAdministrativoAgente.RoleTag,
            Provider = provider,
            Model = null,
            SystemPrompt = AuxiliarAdministrativoAgente.SystemPrompt,
            IsActive = true,
            SortOrder = 60,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.AiAgents.Add(agent);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[IA] Auxiliar Administrativo creado al vuelo para tenant {TenantId}", tid);
        return agent.Id;
    }

    public async Task<AsistenteCampoResult> CompletarAsync(AsistenteCampoRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Proposito))
            return new AsistenteCampoResult(false, null, "Falta el proposito del campo a generar.");

        var agentId = await EnsureAgenteAsync(ct);
        if (agentId is null)
            return new AsistenteCampoResult(false, null, "No hay copropiedad activa.");

        var prompt = new StringBuilder();
        prompt.Append("Proposito: ").Append(req.Proposito.Trim()).Append('\n');
        if (!string.IsNullOrWhiteSpace(req.Contexto))
            prompt.Append("Contexto: ").Append(req.Contexto.Trim()).Append('\n');
        if (!string.IsNullOrWhiteSpace(req.PuntosClave))
            prompt.Append("Puntos clave a incluir: ").Append(req.PuntosClave.Trim()).Append('\n');
        if (req.MaxPalabras is int max && max > 0)
            prompt.Append("Extension maxima aproximada: ").Append(max).Append(" palabras.\n");
        prompt.Append("Redacta unicamente el texto final del campo.");

        AiChatResult res;
        try
        {
            res = await _inference.TestChatAsync(agentId.Value,
                new[] { new AiChatTurn("user", prompt.ToString()) }, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IA] Error generando campo con Auxiliar Administrativo");
            return new AsistenteCampoResult(false, null, "No se pudo generar el texto. Intenta de nuevo.");
        }

        if (!res.Ok)
            return new AsistenteCampoResult(false, null, res.Error ?? "El asistente de IA no esta disponible.");

        var texto = (res.Text ?? string.Empty).Trim().Trim('"');
        return new AsistenteCampoResult(true, texto, null);
    }
}
