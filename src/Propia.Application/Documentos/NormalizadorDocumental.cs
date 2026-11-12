using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Propia.Application.Documentos;

/// <summary>
/// Normaliza los datos que salen de un documento (Agente Documental) o que el usuario escribe a mano, con los
/// formatos colombianos habituales. No confia en el modelo: el servidor aplica esto despues de leer su JSON.
///  - NIT / cedula: sin puntos, comas, espacios ni prefijos ("NIT", "C.C."); el digito de verificacion queda
///    separado por guion ("900.456.789-1" -> "900456789-1"; "C.C. 43.100.200" -> "43100200").
///  - Montos: "$ 36.000.000,00" -> 36000000; "36,000,000.50" -> 36000000.50; "1.500" -> 1500.
///  - Fechas: ISO, dd/mm/aaaa y en texto ("1 de enero de 2026", "enero 1 de 2026",
///    "a los quince (15) dias del mes de enero de dos mil veintiseis (2026)") -> 2026-01-01.
///  - Si/No: "si", "x", "true" -> true; "no", "false" -> false.
/// Devuelven null cuando el dato no se puede interpretar (mejor vacio que un valor equivocado en el formulario).
/// </summary>
public static class NormalizadorDocumental
{
    private static readonly Regex DigitoVerif = new(@"^(.*\d)\s*-\s*(\d)\s*$", RegexOptions.Compiled);

    /// <summary>NIT o documento de identificacion: solo digitos y, si lo trae, "-dv" al final.</summary>
    public static string? Nit(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var s = v.Trim();
        string? dv = null;
        var m = DigitoVerif.Match(s);
        if (m.Success) { s = m.Groups[1].Value; dv = m.Groups[2].Value; }
        var digitos = new string(s.Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length == 0) return null;
        return dv is null ? digitos : $"{digitos}-{dv}";
    }

    /// <summary>Monto en pesos. Decide el separador decimal por la forma del numero (formato colombiano por defecto).</summary>
    public static decimal? Monto(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        // Solo digitos, punto, coma y signo (fuera "$", "COP", espacios, "pesos", etc.).
        var s = new string(v.Where(c => char.IsAsciiDigit(c) || c is '.' or ',' or '-').ToArray()).Trim('-', '.', ',');
        if (s.Length == 0 || !s.Any(char.IsAsciiDigit)) return null;
        int pu = s.LastIndexOf('.'), co = s.LastIndexOf(',');
        string entero, dec = "";
        if (pu >= 0 && co >= 0)
        {
            // Ambos: el ultimo es el decimal ("36.000.000,00" o "36,000,000.00").
            var sep = Math.Max(pu, co);
            entero = s[..sep]; dec = s[(sep + 1)..];
        }
        else if (pu >= 0 || co >= 0)
        {
            var c = pu >= 0 ? '.' : ',';
            var partes = s.Split(c);
            var ultima = partes[^1];
            // Un solo separador seguido de 1-2 cifras = decimal ("36000000.5", "150,25"); si no, miles ("1.500", "36,000").
            if (partes.Length == 2 && ultima.Length is 1 or 2) { entero = partes[0]; dec = ultima; }
            else entero = s;
        }
        else entero = s;
        entero = new string(entero.Where(char.IsAsciiDigit).ToArray());
        dec = new string(dec.Where(char.IsAsciiDigit).ToArray());
        if (entero.Length == 0) entero = "0";
        var txt = dec.Length > 0 ? $"{entero}.{dec}" : entero;
        return decimal.TryParse(txt, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static readonly string[] Meses =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
    private static readonly string[] FormatosNum =
        { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yy", "d/M/yy" };

    /// <summary>Fecha en cualquiera de los formatos habituales de un contrato o una poliza colombianos.</summary>
    public static DateOnly? Fecha(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var s = v.Trim();
        if (s.Length >= 10 && DateTime.TryParseExact(s[..Math.Min(s.Length, 19)], FormatosNum, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt0))
            return DateOnly.FromDateTime(dt0);
        if (DateTime.TryParseExact(s, FormatosNum, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt1))
            return DateOnly.FromDateTime(dt1);

        // En texto: buscar el mes por nombre; el anio = primer numero de 4 cifras despues; el dia = numero <= 31
        // mas cercano antes del mes (o justo despues: "enero 1 de 2026"). "(15)" y "15" valen igual.
        var t = SinTildes(s.ToLowerInvariant()).Replace("setiembre", "septiembre");
        var mes = -1; var posMes = -1;
        for (var i = 0; i < Meses.Length; i++)
        {
            var p = Regex.Match(t, $@"\b{Meses[i]}\b");
            if (p.Success && (posMes < 0 || p.Index < posMes)) { mes = i + 1; posMes = p.Index; }
        }
        if (mes < 0) return null;
        var anioM = Regex.Match(t[posMes..], @"\b(19|20)\d{2}\b");
        if (!anioM.Success) return null;
        var anio = int.Parse(anioM.Value, CultureInfo.InvariantCulture);
        int? dia = null;
        var antes = Regex.Matches(t[..posMes], @"\b(\d{1,2})\b");
        if (antes.Count > 0) dia = int.Parse(antes[^1].Value, CultureInfo.InvariantCulture);
        if (dia is null or < 1 or > 31)
        {
            var despues = Regex.Match(t[posMes..], @"^\w+\s+(\d{1,2})\b");
            dia = despues.Success ? int.Parse(despues.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        }
        if (dia is null && Regex.IsMatch(t[..posMes], @"\bprimero\b")) dia = 1;
        if (dia is null or < 1 or > 31) return null;
        try { return new DateOnly(anio, mes, Math.Min(dia.Value, DateTime.DaysInMonth(anio, mes))); }
        catch { return null; }
    }

    /// <summary>Si/No a bool. null si no es reconocible.</summary>
    public static bool? SiNo(string? v)
    {
        var t = SinTildes((v ?? "").Trim().ToLowerInvariant());
        return t switch
        {
            "si" or "s" or "x" or "true" or "verdadero" or "1" or "yes" => true,
            "no" or "n" or "false" or "falso" or "0" => false,
            _ => null
        };
    }

    private static string SinTildes(string s)
    {
        var norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (var c in norm) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
