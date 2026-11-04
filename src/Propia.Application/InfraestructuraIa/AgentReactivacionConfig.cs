using System.Text.Json;
using System.Text.Json.Serialization;

namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Un PASO de la secuencia de reactivacion/seguimiento. Tras <see cref="OffsetHoras"/> horas de
/// inactividad (contadas desde el ULTIMO mensaje ENTRANTE del contacto), se envia <see cref="MensajeTexto"/>.
/// v1: solo texto libre (sin plantillas HSM). Portado de ECOREX (AgentReactivacionPaso) simplificado.
/// </summary>
public sealed record AgentReactivacionPaso(
    int OffsetHoras = 24,
    string? MensajeTexto = null,
    bool Habilitado = true);

/// <summary>
/// Configuracion de REACTIVACION del agente. Se serializa a <see cref="Domain.Entities.AiAgent.ReactivacionJson"/>.
/// Mismo patron JSON-en-el-agente que usan las reacciones. Portado de ECOREX.
/// </summary>
public sealed record AgentReactivacionConfig(
    bool Habilitada = false,
    IReadOnlyList<AgentReactivacionPaso>? Pasos = null)
{
    public static readonly AgentReactivacionConfig Empty = new(false, Array.Empty<AgentReactivacionPaso>());

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Parsea el JSON almacenado. Null/vacio/invalido =&gt; configuracion vacia (sin reactivacion).</summary>
    public static AgentReactivacionConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return Empty; }
        try
        {
            var cfg = JsonSerializer.Deserialize<AgentReactivacionConfig>(json, JsonOpts);
            return cfg is null ? Empty : cfg with { Pasos = cfg.Pasos ?? Array.Empty<AgentReactivacionPaso>() };
        }
        catch { return Empty; }
    }

    public string Serialize() => JsonSerializer.Serialize(this, JsonOpts);

    /// <summary>Pasos HABILITADOS con texto, ordenados por horas de inactividad (orden de disparo).</summary>
    [JsonIgnore]
    public IReadOnlyList<AgentReactivacionPaso> PasosActivos =>
        (Pasos ?? Array.Empty<AgentReactivacionPaso>())
            .Where(p => p.Habilitado && p.OffsetHoras >= 0 && !string.IsNullOrWhiteSpace(p.MensajeTexto))
            .OrderBy(p => p.OffsetHoras)
            .ToList();

    /// <summary>true si no hay nada que hacer (deshabilitada o sin pasos activos).</summary>
    [JsonIgnore]
    public bool IsNoop => !Habilitada || PasosActivos.Count == 0;
}
