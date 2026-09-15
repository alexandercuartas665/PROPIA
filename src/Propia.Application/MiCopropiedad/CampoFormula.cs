using System.Text.Json;
using System.Text.Json.Serialization;
using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un operando de un paso de formula. Puede ser: un CAMPO fuente (Numero/Moneda; clave "coef", "area",
/// "cd:{guid}"), una CONSTANTE numerica, o el RESULTADO de un paso ANTERIOR (por indice 0-based). Se
/// serializa compacto: <c>{"t":"campo","k":"coef"}</c> | <c>{"t":"const","v":10}</c> | <c>{"t":"paso","p":0}</c>.
/// </summary>
public sealed record OperandoFormula(
    [property: JsonPropertyName("t")] string Tipo,
    [property: JsonPropertyName("k")] string? Campo = null,
    [property: JsonPropertyName("v")] decimal? Valor = null,
    [property: JsonPropertyName("p")] int? Paso = null)
{
    public const string TipoCampo = "campo";
    public const string TipoConstante = "const";
    public const string TipoPaso = "paso";

    public static OperandoFormula DeCampo(string clave) => new(TipoCampo, Campo: clave);
    public static OperandoFormula DeConstante(decimal v) => new(TipoConstante, Valor: v);
    public static OperandoFormula DePaso(int idx) => new(TipoPaso, Paso: idx);
}

/// <summary>
/// Un paso de una formula multi-paso: una operacion sobre sus operandos. Los AGREGADOS
/// (Suma/Promedio/Conteo/Minimo/Maximo) aplican sobre N operandos; las BINARIAS (Resta/Division/
/// Multiplicacion) sobre EXACTAMENTE 2 (A op B, en orden).
/// </summary>
public sealed record PasoFormula(
    [property: JsonPropertyName("op")] OperacionFormula Operacion,
    [property: JsonPropertyName("ops")] IReadOnlyList<OperandoFormula> Operandos);

/// <summary>
/// Config de un campo <see cref="TipoCampoTablero.Formula"/> (Fase 2 del Selector de Campos): una lista
/// ORDENADA de pasos que arman una expresion; el resultado del campo = el ULTIMO paso. Cada paso puede
/// referenciar campos fuente Numero/Moneda, constantes, o el resultado de un paso anterior (sin ciclos:
/// un paso solo ve pasos previos). Se serializa como JSON en la columna POLIMORFICA <c>Opciones</c> de la
/// definicion (esa columna guarda "opciones, una por linea" para Seleccion y este JSON para Formula; cada
/// tipo parsea SOLO su formato). Helper COMPARTIDO por las 9 superficies via el componente de campos.
///
/// RETROCOMPATIBLE: el formato viejo de 1 operacion <c>{"op":"Suma","campos":[...]}</c> se lee como un
/// UNICO paso agregado sobre esos campos, dando EXACTAMENTE el mismo resultado que antes.
/// </summary>
public sealed record CampoFormulaConfig(
    [property: JsonPropertyName("pasos")] IReadOnlyList<PasoFormula> Pasos)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>DTO del formato LEGADO (1 operacion sobre N campos) para leerlo y convertirlo a un paso.</summary>
    private sealed record LegacyFormula(
        [property: JsonPropertyName("op")] OperacionFormula Operacion,
        [property: JsonPropertyName("campos")] IReadOnlyList<string>? Campos);

    /// <summary>Serializa a JSON (formato nuevo, multi-paso) para guardar en la columna Opciones.</summary>
    public string Serializar() => JsonSerializer.Serialize(this, JsonOpts);

    /// <summary>
    /// Parsea el JSON de una definicion Formula. Acepta el formato NUEVO (<c>{"pasos":[...]}</c>) y el
    /// LEGADO (<c>{"op":...,"campos":[...]}</c>, convertido a un unico paso agregado). Devuelve null si el
    /// texto no es un JSON de formula valido (asi, si por error una definicion NO-Formula trae otra cosa en
    /// Opciones, no revienta). El parseo de las opciones de Seleccion (una por linea) es aparte.
    /// </summary>
    public static CampoFormulaConfig? Parse(string? opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones)) return null;
        var t = opciones.TrimStart();
        if (t.Length == 0 || t[0] != '{') return null;   // el formato de Seleccion (lineas) no es JSON objeto
        try
        {
            using var doc = JsonDocument.Parse(opciones);
            var root = doc.RootElement;

            // Formato NUEVO: tiene "pasos".
            if (root.TryGetProperty("pasos", out _))
            {
                var cfg = JsonSerializer.Deserialize<CampoFormulaConfig>(opciones, JsonOpts);
                return cfg is null ? null : cfg.Normalizar();
            }

            // Formato LEGADO: {op, campos} -> un unico paso agregado sobre esos campos (mismo computo).
            if (root.TryGetProperty("op", out _) && root.TryGetProperty("campos", out _))
            {
                var legacy = JsonSerializer.Deserialize<LegacyFormula>(opciones, JsonOpts);
                if (legacy is null) return null;
                var operandos = (legacy.Campos ?? Array.Empty<string>())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => OperandoFormula.DeCampo(c.Trim()))
                    .ToList();
                return new CampoFormulaConfig(new[] { new PasoFormula(legacy.Operacion, operandos) });
            }

            return null;
        }
        catch { return null; }
    }

    /// <summary>Limpia operandos vacios/invalidos que puedan venir del editor o de un JSON manipulado.</summary>
    private CampoFormulaConfig Normalizar()
    {
        var pasos = (Pasos ?? Array.Empty<PasoFormula>())
            .Select(p => new PasoFormula(p.Operacion, (p.Operandos ?? Array.Empty<OperandoFormula>())
                .Select(o => o with { Campo = o.Campo?.Trim() })
                .Where(o => o.Tipo switch
                {
                    OperandoFormula.TipoCampo => !string.IsNullOrWhiteSpace(o.Campo),
                    OperandoFormula.TipoConstante => o.Valor.HasValue,
                    OperandoFormula.TipoPaso => o.Paso.HasValue,
                    _ => false
                })
                .ToList()))
            .ToList();
        return new CampoFormulaConfig(pasos);
    }

    /// <summary>
    /// Computa el valor de la formula en LECTURA (no se persiste). <paramref name="valorDe"/> resuelve el
    /// valor decimal de cada campo fuente para la fila concreta; null = campo vacio/no numerico. Evalua los
    /// pasos EN ORDEN (cada paso puede usar el resultado de pasos anteriores) y devuelve el resultado del
    /// ULTIMO paso. Sin pasos -> null.
    /// </summary>
    public decimal? Computar(Func<string, decimal?> valorDe)
    {
        if (Pasos is null || Pasos.Count == 0) return null;
        var resultados = new decimal?[Pasos.Count];
        for (var i = 0; i < Pasos.Count; i++)
            resultados[i] = ComputarPaso(Pasos[i], valorDe, resultados, i);
        return resultados[^1];
    }

    private static decimal? ComputarPaso(PasoFormula paso, Func<string, decimal?> valorDe, decimal?[] previos, int idx)
    {
        decimal? Resolver(OperandoFormula o) => o.Tipo switch
        {
            OperandoFormula.TipoConstante => o.Valor,
            OperandoFormula.TipoPaso => (o.Paso is int p && p >= 0 && p < idx) ? previos[p] : null,
            OperandoFormula.TipoCampo => string.IsNullOrWhiteSpace(o.Campo) ? null : valorDe(o.Campo!),
            _ => null
        };

        var operandos = paso.Operandos ?? Array.Empty<OperandoFormula>();
        // Agregados: sobre los operandos CON valor. Binarias: exigen 2 operandos, ambos con valor.
        switch (paso.Operacion)
        {
            case OperacionFormula.Resta:
            case OperacionFormula.Multiplicacion:
            case OperacionFormula.Division:
            {
                if (operandos.Count != 2) return null;
                var a = Resolver(operandos[0]);
                var b = Resolver(operandos[1]);
                if (a is null || b is null) return null;
                return paso.Operacion switch
                {
                    OperacionFormula.Resta => a - b,
                    OperacionFormula.Multiplicacion => a * b,
                    OperacionFormula.Division => b.Value == 0m ? null : Math.Round(a.Value / b.Value, 4, MidpointRounding.AwayFromZero),
                    _ => null
                };
            }
            default:
            {
                var valores = operandos.Select(Resolver).Where(v => v.HasValue).Select(v => v!.Value).ToList();
                return paso.Operacion switch
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
        }
    }

    /// <summary>Tipos de campo fuente admitidos en una Formula (sin anidamiento de otras formulas).</summary>
    public static bool EsFuenteValida(TipoCampoTablero tipo)
        => tipo is TipoCampoTablero.Numero or TipoCampoTablero.Moneda;

    /// <summary>
    /// Computa el TEXTO de un campo Formula en lectura (helper COMPARTIDO por todas las superficies): parsea
    /// la config de <paramref name="opciones"/>, evalua los pasos con <paramref name="valorDe"/> (que cada
    /// superficie construye desde sus valores propios + su resolver de campos de sistema), y formatea
    /// recortando ceros de cola. Devuelve null si la config no es valida o el resultado es vacio.
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
    /// Valida una formula multi-paso. <paramref name="tipoDe"/> devuelve el Tipo de un campo fuente por su
    /// clave (null si no existe). Devuelve el motivo del rechazo, o null si es valida. Reglas: al menos un
    /// paso; cada paso con operandos; las binarias con exactamente 2; los campos fuente deben existir y ser
    /// Numero/Moneda (excluye otra Formula/Usuario/Directorio -> sin formulas anidadas); un ref a paso solo
    /// puede apuntar a un paso ANTERIOR (sin ciclos); una constante debe traer valor.
    /// </summary>
    public static string? Validar(CampoFormulaConfig cfg, Func<string, TipoCampoTablero?> tipoDe)
    {
        var pasos = cfg.Pasos ?? Array.Empty<PasoFormula>();
        if (pasos.Count == 0) return "Una formula debe tener al menos un paso.";
        for (var i = 0; i < pasos.Count; i++)
        {
            var paso = pasos[i];
            var operandos = paso.Operandos ?? Array.Empty<OperandoFormula>();
            var esBinaria = paso.Operacion is OperacionFormula.Resta or OperacionFormula.Multiplicacion or OperacionFormula.Division;
            if (operandos.Count == 0) return $"El paso {i + 1} no tiene operandos.";
            if (esBinaria && operandos.Count != 2)
                return $"El paso {i + 1} ({paso.Operacion}) necesita exactamente 2 operandos.";
            foreach (var o in operandos)
            {
                switch (o.Tipo)
                {
                    case OperandoFormula.TipoCampo:
                        if (string.IsNullOrWhiteSpace(o.Campo)) return $"El paso {i + 1} tiene un campo vacio.";
                        var tipo = tipoDe(o.Campo!);
                        if (tipo is null) return $"El campo fuente '{o.Campo}' no existe.";
                        if (!EsFuenteValida(tipo.Value))
                            return $"El campo fuente '{o.Campo}' es {tipo.Value}; una formula solo usa campos Numero o Moneda (no otra formula ni usuario/directorio).";
                        break;
                    case OperandoFormula.TipoPaso:
                        if (o.Paso is not int p || p < 0 || p >= i)
                            return $"El paso {i + 1} referencia un paso invalido (solo puede usar un paso anterior).";
                        break;
                    case OperandoFormula.TipoConstante:
                        if (o.Valor is null) return $"El paso {i + 1} tiene una constante vacia.";
                        break;
                    default:
                        return $"El paso {i + 1} tiene un operando de tipo desconocido '{o.Tipo}'.";
                }
            }
        }
        return null;
    }
}
