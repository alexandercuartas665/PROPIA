using System.Text.Json;
using System.Text.Json.Serialization;
using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Config de un campo <see cref="TipoCampoTablero.Formula"/> (Fase 2 del Selector de Campos): la
/// operacion y las claves de los campos FUENTE. Se serializa como JSON en la columna POLIMORFICA
/// <c>Opciones</c> de la definicion (esa columna guarda "opciones, una por linea" para Seleccion y este
/// JSON para Formula; cada tipo parsea SOLO su formato). Helper COMPARTIDO: lo reusan Unidades (referencia)
/// y, al replicar, las demas superficies via el componente compartido, para no duplicar el shape/computo.
/// </summary>
/// <param name="Operacion">Que agrega (Suma/Promedio/Conteo/Minimo/Maximo).</param>
/// <param name="Campos">Claves de los campos fuente (p.ej. "coef", "area", "cd:{guid}"). Solo se admiten
/// campos Numero/Moneda; NUNCA otra Formula ni Usuario/Directorio (sin anidamiento en esta version).</param>
public sealed record CampoFormulaConfig(
    [property: JsonPropertyName("op")] OperacionFormula Operacion,
    [property: JsonPropertyName("campos")] IReadOnlyList<string> Campos)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Serializa a JSON para guardar en la columna Opciones.</summary>
    public string Serializar() => JsonSerializer.Serialize(this, JsonOpts);

    /// <summary>
    /// Parsea el JSON de una definicion Formula. Devuelve null si el texto no es un JSON de formula valido
    /// (asi, si por error una definicion NO-Formula trae otra cosa en Opciones, no revienta). El parseo de
    /// las opciones de Seleccion (una por linea) es aparte: cada tipo lee SOLO su formato.
    /// </summary>
    public static CampoFormulaConfig? Parse(string? opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones)) return null;
        var t = opciones.TrimStart();
        if (t.Length == 0 || t[0] != '{') return null;   // el formato de Seleccion (lineas) no es JSON objeto
        try
        {
            var cfg = JsonSerializer.Deserialize<CampoFormulaConfig>(opciones, JsonOpts);
            if (cfg is null) return null;
            return cfg with { Campos = (cfg.Campos ?? Array.Empty<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList() };
        }
        catch { return null; }
    }

    /// <summary>
    /// Computa el agregado en LECTURA (no se persiste). <paramref name="valorDe"/> resuelve el valor
    /// decimal de cada campo fuente para la fila concreta; null = campo vacio/no numerico. Reglas:
    /// Conteo = numero de fuentes CON valor; Suma/Promedio/Minimo/Maximo sobre esos valores. Sin ninguna
    /// fuente con valor: Conteo=0; los demas devuelven null (celda vacia).
    /// </summary>
    public decimal? Computar(Func<string, decimal?> valorDe)
    {
        var valores = Campos.Select(valorDe).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return Operacion switch
        {
            OperacionFormula.Conteo => valores.Count,
            _ when valores.Count == 0 => null,
            OperacionFormula.Suma => valores.Sum(),
            OperacionFormula.Promedio => Math.Round(valores.Average(), 4, MidpointRounding.AwayFromZero),
            OperacionFormula.Minimo => valores.Min(),
            OperacionFormula.Maximo => valores.Max(),
            _ => null
        };
    }

    /// <summary>Tipos de campo fuente admitidos en una Formula (sin anidamiento).</summary>
    public static bool EsFuenteValida(TipoCampoTablero tipo)
        => tipo is TipoCampoTablero.Numero or TipoCampoTablero.Moneda;

    /// <summary>
    /// Computa el TEXTO de un campo Formula en lectura (helper COMPARTIDO por todas las superficies): parsea
    /// la config de <paramref name="opciones"/>, aplica la operacion con <paramref name="valorDe"/> (que
    /// cada superficie construye desde sus valores propios + su resolver de campos de sistema), y formatea
    /// recortando ceros de cola (los campos de sistema son numeric con escala). Devuelve null si la config
    /// no es valida o el agregado es vacio (celda en blanco).
    /// </summary>
    public static string? ComputarTexto(string? opciones, Func<string, decimal?> valorDe)
    {
        var cfg = Parse(opciones);
        if (cfg is null) return null;
        var r = cfg.Computar(valorDe);
        if (r is null) return null;
        var s = r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (s.Contains('.')) s = s.TrimEnd('0').TrimEnd('.');
        return s;
    }

    /// <summary>
    /// Valida las fuentes de una formula. <paramref name="tipoDe"/> devuelve el Tipo de un campo fuente por
    /// su clave (null si la clave no existe). Devuelve el motivo del rechazo, o null si es valida. Prohibe:
    /// lista vacia, clave inexistente, y fuentes que no sean Numero/Moneda (esto excluye por diseno otra
    /// Formula/Usuario/Directorio -> sin formulas anidadas ni circulares en esta version).
    /// </summary>
    public static string? Validar(CampoFormulaConfig cfg, Func<string, TipoCampoTablero?> tipoDe)
    {
        if (cfg.Campos.Count == 0) return "Una formula debe referenciar al menos un campo Numero o Moneda.";
        foreach (var clave in cfg.Campos)
        {
            var tipo = tipoDe(clave);
            if (tipo is null) return $"El campo fuente '{clave}' no existe.";
            if (!EsFuenteValida(tipo.Value))
                return $"El campo fuente '{clave}' es {tipo.Value}; una formula solo puede sumar/promediar campos Numero o Moneda (no otra formula ni usuario/directorio).";
        }
        return null;
    }
}
