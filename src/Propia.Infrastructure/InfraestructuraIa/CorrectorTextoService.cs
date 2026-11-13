using System.Text;
using Microsoft.Extensions.Logging;
using Propia.Application.InfraestructuraIa;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Mejora la redaccion de un texto con el agente "Corrector de redaccion" de la copropiedad activa (se crea al usarlo
/// desde la plantilla del Super Admin). Un turno, sin tools MCP; IAiInferenceService resuelve proveedor, modelo y cuota.
/// </summary>
public sealed class CorrectorTextoService : ICorrectorTextoService
{
    private readonly IAgentesSistema _agentes;
    private readonly IAiInferenceService _inference;
    private readonly ILogger<CorrectorTextoService> _logger;

    public CorrectorTextoService(IAgentesSistema agentes, IAiInferenceService inference, ILogger<CorrectorTextoService> logger)
    {
        _agentes = agentes; _inference = inference; _logger = logger;
    }

    public async Task<MejorarTextoResult> MejorarAsync(MejorarTextoRequest req, CancellationToken ct = default)
    {
        var texto = (req.Texto ?? "").Trim();
        if (texto.Length < 3) return new MejorarTextoResult(false, null, "No hay texto para mejorar.");
        if (texto.Length > CorrectorTextoAgente.MaxCaracteres)
            return new MejorarTextoResult(false, null, $"El texto es muy largo para mejorarlo de una vez (máximo {CorrectorTextoAgente.MaxCaracteres} caracteres).");

        var agente = await _agentes.AsegurarAsync(PlantillaAgente.ClaveCorrectorTexto, ct);
        if (agente is null) return new MejorarTextoResult(false, null, "No hay copropiedad activa.");

        var prompt = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(req.Contexto))
            prompt.Append("Campo del formulario: ").Append(req.Contexto.Trim()).Append("\n\n");
        prompt.Append("Texto a corregir:\n<<<\n").Append(texto).Append("\n>>>");

        AiChatResult res;
        try
        {
            res = await _inference.TestChatAsync(agente.Id, new[] { new AiChatTurn("user", prompt.ToString()) }, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IA] Error mejorando texto con el Corrector de redaccion");
            return new MejorarTextoResult(false, null, "No se pudo mejorar el texto. Intenta de nuevo.");
        }
        if (!res.Ok) return new MejorarTextoResult(false, null, res.Error ?? "El corrector de IA no está disponible.");

        var mejorado = Limpiar(res.Text);
        return string.IsNullOrWhiteSpace(mejorado)
            ? new MejorarTextoResult(false, null, "El corrector no devolvió texto.")
            : new MejorarTextoResult(true, mejorado, null);
    }

    /// <summary>Quita lo que algunos modelos agregan aunque se les pida que no: bloques de codigo, comillas y los marcadores.</summary>
    public static string Limpiar(string? s)
    {
        var t = (s ?? "").Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var ini = t.IndexOf('\n');
            var fin = t.LastIndexOf("```", StringComparison.Ordinal);
            t = ini > 0 && fin > ini ? t[(ini + 1)..fin] : t.Trim('`');
        }
        t = t.Replace("<<<", "").Replace(">>>", "").Trim();
        if (t.Length > 1 && ((t[0] == '"' && t[^1] == '"') || (t[0] == '“' && t[^1] == '”'))) t = t[1..^1].Trim();
        return t;
    }
}
