using System.Text.Json;

namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Ola H - enrutamiento de las tareas que el agente crea con crear_tarea:
///  - <see cref="TablerosPermitidos"/>: WHITELIST de ids de Tablero. Vacio = sin restriccion (todos).
///  - <see cref="Asignados"/>: lista ORDENADA de personas para el reparto round-robin. Vacio = sin reparto.
/// Se persiste en AiAgent.AllowedBoardIdsJson / AssigneeUserIdsJson (+ AssigneeCursor). Portado de ECOREX.
/// Los helpers de (de)serializacion los comparten la API (guardar config) y la tool crear_tarea.
/// </summary>
public static class AgentEnrutamientoTareas
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    /// <summary>Parsea un arreglo JSON de GUID. Null/vacio/invalido =&gt; lista vacia. Descarta Guid.Empty y duplicados preservando el orden.</summary>
    public static IReadOnlyList<Guid> ParseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return Array.Empty<Guid>(); }
        try
        {
            var arr = JsonSerializer.Deserialize<List<Guid>>(json, JsonOpts);
            if (arr is null) { return Array.Empty<Guid>(); }
            var seen = new HashSet<Guid>();
            var outp = new List<Guid>(arr.Count);
            foreach (var g in arr) { if (g != Guid.Empty && seen.Add(g)) { outp.Add(g); } }
            return outp;
        }
        catch { return Array.Empty<Guid>(); }
    }

    /// <summary>Serializa a arreglo JSON. Lista vacia =&gt; null (deja el campo limpio).</summary>
    public static string? SerializeIds(IReadOnlyList<Guid>? ids)
    {
        if (ids is null) { return null; }
        var clean = new List<Guid>(ids.Count);
        var seen = new HashSet<Guid>();
        foreach (var g in ids) { if (g != Guid.Empty && seen.Add(g)) { clean.Add(g); } }
        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean, JsonOpts);
    }

    /// <summary>true si las dos listas de ids son iguales EN ORDEN (para decidir si reiniciar el cursor).</summary>
    public static bool SameOrder(IReadOnlyList<Guid> a, IReadOnlyList<Guid> b)
    {
        if (a.Count != b.Count) { return false; }
        for (var i = 0; i < a.Count; i++) { if (a[i] != b[i]) { return false; } }
        return true;
    }
}

/// <summary>Un asignado del reparto round-robin, con nombre resuelto para la UI.</summary>
public sealed record AgentAsignadoDto(Guid PersonaId, string Nombre);

/// <summary>Config de enrutamiento que lee la UI/GET: ids de tableros permitidos + asignados (con nombre).</summary>
public sealed record AgentEnrutamientoDto(
    IReadOnlyList<Guid> TablerosPermitidos,
    IReadOnlyList<AgentAsignadoDto> Asignados);

/// <summary>Cuerpo del PUT: ids de tableros permitidos + ids de personas (en orden de rotacion).</summary>
public sealed record AgentEnrutamientoSaveRequest(
    IReadOnlyList<Guid> TablerosPermitidos,
    IReadOnlyList<Guid> AsignadoPersonaIds);
