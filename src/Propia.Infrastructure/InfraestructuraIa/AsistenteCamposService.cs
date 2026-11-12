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
    private readonly IAgentesSistema _agentes;

    public AsistenteCamposService(PropiaDbContext db, ITenantContext tenant, IAiInferenceService inference, ILogger<AsistenteCamposService> logger, IAgentesSistema agentes)
    {
        _agentes = agentes;
        _db = db;
        _tenant = tenant;
        _inference = inference;
        _logger = logger;
    }

    // El Auxiliar Administrativo de la copropiedad por la clave de su plantilla; si no lo tiene, se crea al
    // usarlo desde la plantilla del Super Admin. Como antes, se usa aunque la copropiedad lo haya apagado.
    public async Task<Guid?> EnsureAgenteAsync(CancellationToken ct = default)
        => (await _agentes.AsegurarAsync(PlantillaAgente.ClaveAuxiliar, ct))?.Id;

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
