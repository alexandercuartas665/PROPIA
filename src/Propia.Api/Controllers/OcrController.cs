using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Ocr;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.SuperAdmin;

namespace Propia.Api.Controllers;

/// <summary>
/// Extraccion de datos de documentos (OCR) para los modulos cliente: recibos de servicios
/// publicos (2.17), RUT/Camara de Comercio (Directorio) y contratos. Usa el proveedor OCR
/// configurado en Super Admin (config maestra). Si no hay proveedor habilitado, responde 400.
/// </summary>
[ApiController]
[Route("api/ocr")]

[Authorize]  // S-06: extraccion (lectura) abierta al tenant; el agente documental que persiste va gateado (ver metodos)
public class OcrController : ControllerBase
{
    private readonly IDocumentExtractionService _ocr;
    private readonly IAiInferenceService _ai;
    private readonly IAiDocumentExtractor _aiExtractor;
    private readonly Propia.Infrastructure.Storage.IBlobStorage _blob;
    private readonly PropiaDbContext _db;
    private readonly Application.InfraestructuraIa.IAgentesSistema _agentesSistema;

    public OcrController(IDocumentExtractionService ocr, IAiInferenceService ai, IAiDocumentExtractor aiExtractor,
        Propia.Infrastructure.Storage.IBlobStorage blob, PropiaDbContext db, Application.InfraestructuraIa.IAgentesSistema agentesSistema)
    {
        _agentesSistema = agentesSistema;
        _ocr = ocr;
        _ai = ai;
        _aiExtractor = aiExtractor;
        _blob = blob;
        _db = db;
    }

    /// <summary>
    /// Extraccion con IA (arnes IAiDocumentExtractor) por PERFIL: manda el PDF/imagen nativo al motor
    /// de IA configurado (p.ej. Gemini) y devuelve exactamente los campos del perfil, con confianza.
    /// Si el motor configurado no es de IA, responde 400 con guia. Reutilizable por modulo via 'perfil'.
    /// </summary>
    [HttpPost("extraer-ia")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> ExtraerIa([FromForm] IFormFile? file, [FromForm] string? perfil, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "archivo_requerido" });
        if (!await _aiExtractor.DisponibleAsync(ct))
            return BadRequest(new { error = "El motor configurado no es de IA. Configura 'IA - Google Gemini' en Super Admin > OCR / Documentos." });

        using var ms = new MemoryStream();
        await using (var s = file.OpenReadStream()) await s.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var campos = PerfilCampos(perfil);
        var r = await _aiExtractor.ExtraerAsync(bytes, file.ContentType ?? "application/pdf", file.FileName, campos, perfil ?? "generico", ct);
        if (!r.Ok) return BadRequest(new { error = r.Error });

        // Guardamos el PDF ORIGEN en R2 (tenant-scoped) y devolvemos la key para que el modulo la
        // persista al guardar (descarga gateada, no URL publica). Best-effort: si falla, seguimos.
        string? pdfOrigenKey = null;
        var tenantId = User.FindFirstValue("tenant_id");
        if (!string.IsNullOrEmpty(tenantId))
        {
            try
            {
                var ext = (file.ContentType ?? "").Contains("pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "bin";
                var key = $"tenants/{tenantId}/extracciones/{Guid.NewGuid():N}.{ext}";
                using var up = new MemoryStream(bytes);
                await _blob.UploadAsync(key, up, file.ContentType ?? "application/octet-stream", ct);
                pdfOrigenKey = key;
            }
            catch { /* el guardado del origen no debe romper la extraccion */ }
        }

        return Ok(new
        {
            ok = true,
            proveedor = r.Proveedor,
            modelo = r.Modelo,
            pdfOrigenKey,
            campos = r.Campos.Select(c => new { c.Nombre, c.Valor, c.Confianza })
        });
    }

    /// <summary>
    /// B1 (SOLO DeepSeek, sin Gemini/Azure): recibe el TEXTO ya extraido del documento en el cliente
    /// (pdf.js lee la capa de texto del PDF; no es OCR de IA) y lo pasa por el AGENTE DOCUMENTAL del
    /// tenant (su proveedor = DeepSeek), que devuelve SOLO los campos del perfil como JSON para prellenar
    /// el formulario. NO persiste nada (lo guarda el modulo con su flujo normal). Devuelve el MISMO shape
    /// que extraer-ia para que el frontend reuse su mapeador.
    /// </summary>
    [HttpPost("extraer-agente")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> ExtraerAgente([FromBody] ExtraerAgenteRequest req, CancellationToken ct)
    {
        var hayImagenes = req?.Imagenes is { Count: > 0 };
        if (req is null || (string.IsNullOrWhiteSpace(req.Texto) && !hayImagenes)) return BadRequest(new { error = "texto_o_imagenes_requerido" });
        var campos = PerfilCampos(req.Perfil);
        if (campos.Count == 0) return BadRequest(new { error = "perfil_desconocido" });

        // El Agente Documental de la copropiedad (por la clave de su plantilla; si no lo tiene, se crea al usarlo).
        var agentId = await AgenteDocumentalAsync(ct);
        if (agentId is null) return BadRequest(new { error = "agente_documental_no_disponible" });

        // Nombre de la copropiedad: el contratista / la aseguradora NUNCA es ella (la IA a veces la toma como tal).
        string? copropiedad = null;
        if (Guid.TryParse(User.FindFirstValue("tenant_id"), out var tid))
            copropiedad = await _db.Tenants.AsNoTracking().Where(t => t.Id == tid).Select(t => t.Nombre).FirstOrDefaultAsync(ct);

        // Ruta de lectura. Texto (pdf.js) si es suficiente; VISION si no hay texto o si es POBRE (pocas palabras por
        // pagina: PDF escaneado con una capa de texto residual). Con vision se adjunta el texto que haya como apoyo.
        var texto = string.IsNullOrWhiteSpace(req.Texto) ? null : (req.Texto.Length > 60000 ? req.Texto[..60000] : req.Texto).Trim();
        var imgs = (req.Imagenes ?? new()).Where(b => !string.IsNullOrWhiteSpace(b)).Take(12)
            .Select(b => new Application.InfraestructuraIa.AiInboundImage("image/jpeg", b)).ToList();
        var paginas = Math.Max(1, req.Paginas ?? (imgs.Count > 0 ? imgs.Count : 1));
        var usarVision = imgs.Count > 0 && (texto is null || EsTextoPobre(texto, paginas));
        List<Application.InfraestructuraIa.AiChatTurn> Contenido(string instruccion) => usarVision
            ? new() { new("user", $"Te entrego {imgs.Count} pagina(s) del documento como imagenes, en orden. {instruccion}"
                + (texto is null ? "" : "\nTEXTO EXTRAIDO DEL PDF (incompleto; las imagenes mandan):\n" + texto), imgs) }
            : new() { new("user", "TEXTO DEL DOCUMENTO:\n" + texto) };

        // 1a pasada: todos los campos del perfil. bearerToken null => sin tools MCP (completion limpia).
        var perfilKey = (req.Perfil ?? "").Trim().ToLowerInvariant();
        var res = await _ai.TestChatAsync(agentId.Value,
            Contenido("Lee el texto de las imagenes y devuelve UNICAMENTE el JSON con las claves indicadas."),
            systemPromptOverride: PromptExtraccion(perfilKey, campos, copropiedad, null), ct: ct);
        if (!res.Ok) return BadRequest(new { error = res.Error ?? "fallo_agente" });
        var valores = NormalizarCampos(ParseCamposJson(res.Text, campos), campos);
        int inTok = res.InputTokens, outTok = res.OutputTokens, pasadas = 1;

        // 2a pasada DIRIGIDA solo a los datos clave que faltaron (contratista/aseguradora, NIT, valores, fechas):
        // el modelo suele saltarlos cuando estan en una clausula o en la hoja de firmas.
        var faltan = ClavesFaltantes(perfilKey, valores);
        if (faltan.Count > 0)
        {
            var objetivo = campos.Where(c => faltan.Contains(c.Nombre)).ToList();
            var res2 = await _ai.TestChatAsync(agentId.Value,
                Contenido("Busca SOLO los datos indicados y devuelve UNICAMENTE el JSON con esas claves."),
                systemPromptOverride: PromptExtraccion(perfilKey, objetivo, copropiedad, faltan), ct: ct);
            if (res2.Ok)
            {
                pasadas = 2; inTok += res2.InputTokens; outTok += res2.OutputTokens;
                foreach (var v in NormalizarCampos(ParseCamposJson(res2.Text, objetivo), objetivo))
                    if (!valores.Any(x => x.Nombre == v.Nombre)) valores.Add(v);
            }
        }

        // Recibo: si la suma de los conceptos no cuadra con el total (+-1%), "conceptos" queda dudoso (conf baja).
        if (perfilKey == "recibo_servicio") MarcarCuadreConceptos(valores);

        // Origen PDF (opcional): si el modulo manda el PDF en base64, lo guardamos tenant-scoped y
        // devolvemos su key para persistirla al guardar (igual que extraer-ia). No pasa por IA. Best-effort.
        string? pdfOrigenKey = null;
        if (!string.IsNullOrWhiteSpace(req.PdfBase64))
        {
            var tenantId = User.FindFirstValue("tenant_id");
            if (!string.IsNullOrEmpty(tenantId))
            {
                try
                {
                    var bytes = Convert.FromBase64String(req.PdfBase64);
                    var mime = string.IsNullOrWhiteSpace(req.Mime) ? "application/pdf" : req.Mime!;
                    var ext = mime.ToLowerInvariant() switch
                    {
                        var m when m.Contains("pdf") => "pdf",
                        var m when m.Contains("jpeg") || m.Contains("jpg") => "jpg",
                        var m when m.Contains("png") => "png",
                        var m when m.Contains("webp") => "webp",
                        _ => "bin"
                    };
                    var key = $"tenants/{tenantId}/extracciones/{Guid.NewGuid():N}.{ext}";
                    using var up = new MemoryStream(bytes);
                    await _blob.UploadAsync(key, up, mime, ct);
                    pdfOrigenKey = key;
                }
                catch { /* el guardado del origen no debe romper la extraccion */ }
            }
        }

        return Ok(new
        {
            ok = true,
            modelo = "agente",
            pdfOrigenKey,
            inputTokens = inTok,
            outputTokens = outTok,
            ruta = usarVision ? "vision" : "texto",
            pasadas,
            campos = valores.Select(v => new { Nombre = v.Nombre, Valor = v.Valor, Confianza = v.Conf })
        });
    }

    // Agente Documental ACTIVO de la copropiedad (null si esta apagado o no hay plantilla).
    private async Task<Guid?> AgenteDocumentalAsync(CancellationToken ct)
        => await _agentesSistema.AsegurarAsync(Application.InfraestructuraIa.PlantillaAgente.ClaveDocumental, ct) is { Activo: true } a ? a.Id : null;

    // Texto "pobre" = pocas palabras por pagina: casi seguro un escaneado con una capa de texto residual
    // (sellos, numeros de pagina). Por debajo de esto se lee con vision.
    private const int PalabrasMinPorPagina = 40;
    private static bool EsTextoPobre(string texto, int paginas)
        => texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length / (double)Math.Max(1, paginas) < PalabrasMinPorPagina;

    /// <summary>Prompt de EXTRACCION (override del prompt del agente SOLO para esta llamada). Formatos colombianos,
    /// donde buscar cada dato y quien NO es el contratista. soloFaltantes != null = 2a pasada dirigida.</summary>
    private static string PromptExtraccion(string perfil, IReadOnlyList<CampoObjetivo> campos, string? copropiedad, IReadOnlyCollection<string>? soloFaltantes)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Eres un extractor de datos de documentos colombianos. Te doy un documento (texto o imagenes) y debes responder");
        sb.AppendLine("UNICAMENTE un objeto JSON. Por cada clave devuelve un objeto {\"valor\": <dato o null>, \"conf\": <0.0 a 1.0>}.");
        sb.AppendLine("conf = que tan seguro estas de haber leido bien ese dato (1 = claro y legible; 0.5 = dudoso o borroso;");
        sb.AppendLine("bajo = conjetura). Usa null en valor si el dato no aparece; no inventes.");
        sb.AppendLine("No agregues explicaciones ni bloques de codigo: responde solo el JSON.");
        if (soloFaltantes is not null)
        {
            sb.AppendLine();
            sb.AppendLine("En una primera lectura NO se encontraron estos datos. Revisa el documento COMPLETO con cuidado:");
            sb.AppendLine("encabezado, cada clausula (OBJETO, VALOR, PLAZO, VIGENCIA, FORMA DE PAGO, PARTES), la hoja de firmas,");
            sb.AppendLine("los sellos y los anexos (RUT, camara de comercio, caratula de la poliza). Devuelve SOLO estas claves.");
        }
        sb.AppendLine();
        sb.AppendLine("DONDE BUSCAR: los datos estan en el encabezado o caratula, en las clausulas, en la hoja de firmas y en los anexos.");
        sb.AppendLine("FORMATOS (Colombia):");
        sb.AppendLine("- Fechas: devuelvelas como YYYY-MM-DD aunque esten en texto (\"1 de enero de 2026\" -> 2026-01-01;");
        sb.AppendLine("  \"a los quince (15) dias del mes de marzo de 2025\" -> 2025-03-15).");
        sb.AppendLine("- Montos: solo el numero, sin simbolos. En Colombia el punto separa miles y la coma los decimales:");
        sb.AppendLine("  \"$ 36.000.000,00\" -> 36000000. Si el valor esta en letras y en numeros, usa el de numeros.");
        sb.AppendLine("- NIT o cedula: tal como aparece, con el digito de verificacion (\"900.456.789-1\").");
        sb.AppendLine("- Si/No: devuelve \"si\" o \"no\". Numeros enteros: solo la cifra.");
        if (perfil == "contrato")
        {
            sb.AppendLine("PARTES: el CONTRATANTE es la copropiedad (conjunto, edificio, propiedad horizontal) que recibe el servicio.");
            sb.AppendLine("El CONTRATISTA es quien presta el servicio, ejecuta la obra o vende: NUNCA es la copropiedad.");
            if (!string.IsNullOrWhiteSpace(copropiedad)) sb.AppendLine($"La copropiedad de este documento es \"{copropiedad}\": NO es el contratista.");
        }
        else if (perfil == "recibo_servicio")
        {
            sb.AppendLine("ES UN RECIBO DE SERVICIO PUBLICO colombiano (EPM, Enel/Codensa, Vanti, Gases de Occidente, acueductos y");
            sb.AppendLine("empresas de aseo municipales, operadores de internet y telefonia como Claro, Movistar, Tigo, ETB).");
            sb.AppendLine("- referencia_contrato: el numero FIJO que identifica el servicio ante el prestador y se repite cada mes:");
            sb.AppendLine("  numero de contrato, cuenta, NIC, NIU, codigo de suscriptor, matricula o referencia de pago. NUNCA es el NIT");
            sb.AppendLine("  del prestador ni el numero de la factura (ese cambia cada mes). Devuelvelo tal como aparece.");
            sb.AppendLine("- titular: el suscriptor o usuario a cuyo nombre esta el servicio; suele ser la copropiedad.");
            if (!string.IsNullOrWhiteSpace(copropiedad)) sb.AppendLine($"  La copropiedad que sube el recibo es \"{copropiedad}\": NO es el prestador.");
            sb.AppendLine("- prestador: la empresa que presta el servicio y expide el recibo (no el banco ni el recaudador).");
            sb.AppendLine("- Separa el valor del PERIODO (los cobros del mes) de los saldos anteriores, la mora o intereses y el TOTAL A PAGAR:");
            sb.AppendLine("  valor_periodo = solo lo facturado en este periodo; saldo_anterior = deuda o saldo de periodos previos; valor_total = total a pagar.");
            sb.AppendLine("- periodo_desde / periodo_hasta: el periodo facturado o de consumo. Si cruza dos meses, devuelve ambas fechas completas.");
            sb.AppendLine("- consumo: la cantidad consumida (kWh, m3, minutos, GB) como numero, puede tener decimales. Lecturas del medidor si aparecen.");
            sb.AppendLine("- conceptos: el desglose de cobros del periodo como lista [{\"concepto\": texto, \"valor\": numero}], en el orden del recibo,");
            sb.AppendLine("  sin el total ni los subtotales. Devuelvelo como {\"valor\": [ ... ], \"conf\": 0.0-1.0}.");
        }
        else if (perfil == "poliza")
        {
            sb.AppendLine("PARTES: la ASEGURADORA es la compania de seguros que expide la poliza. El TOMADOR y el ASEGURADO suelen ser");
            sb.AppendLine("la copropiedad. El CORREDOR o intermediario es una agencia distinta de la aseguradora.");
            if (!string.IsNullOrWhiteSpace(copropiedad)) sb.AppendLine($"La copropiedad es \"{copropiedad}\": NO es la aseguradora.");
        }
        sb.AppendLine();
        sb.AppendLine("Claves esperadas:");
        foreach (var c in campos) sb.AppendLine($"- \"{c.Nombre}\": {c.Descripcion} (tipo {c.Tipo})");
        return sb.ToString();
    }

    /// <summary>Normaliza en el SERVIDOR (no se confia en el prompt): NIT sin puntos ni espacios con el DV separado
    /// por guion, montos como numero invariante, fechas ISO, si/no como true/false. Lo que no se puede
    /// interpretar se descarta (mejor vacio que un dato equivocado en el formulario).</summary>
    private static List<(string Nombre, string? Valor, double? Conf)> NormalizarCampos(
        List<(string Nombre, string? Valor, double? Conf)> valores, IReadOnlyList<CampoObjetivo> campos)
    {
        var outp = new List<(string, string?, double?)>();
        foreach (var (nombre, valor, conf) in valores)
        {
            var tipo = campos.FirstOrDefault(c => c.Nombre == nombre)?.Tipo ?? "texto";
            var raw = valor?.Trim();
            if (string.IsNullOrWhiteSpace(raw) || raw.Equals("null", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("n/a", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("no aparece", StringComparison.OrdinalIgnoreCase)) continue;
            string? norm = tipo switch
            {
                "nit" => Application.Documentos.NormalizadorDocumental.Nit(raw),
                "moneda" => Application.Documentos.NormalizadorDocumental.Monto(raw)?.ToString("0.##", CultureInfo.InvariantCulture),
                "numero" => Application.Documentos.NormalizadorDocumental.Monto(raw) is { } n ? decimal.Truncate(n).ToString(CultureInfo.InvariantCulture) : null,
                "fecha" => Application.Documentos.NormalizadorDocumental.Fecha(raw)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "sino" => Application.Documentos.NormalizadorDocumental.SiNo(raw) is { } b ? (b ? "true" : "false") : null,
                "decimal" => Application.Documentos.NormalizadorDocumental.Monto(raw)?.ToString("0.####", CultureInfo.InvariantCulture),
                "lista" => NormalizarLista(raw),
                _ => raw
            };
            if (!string.IsNullOrWhiteSpace(norm)) outp.Add((nombre, norm, conf));
        }
        return outp;
    }

    /// <summary>Datos clave del perfil que faltaron en la 1a pasada (disparan la 2a pasada dirigida). Los valores
    /// cuentan como uno: basta con que llegue el total, el mensual, la prima o el asegurado.</summary>
    private static List<string> ClavesFaltantes(string perfil, List<(string Nombre, string? Valor, double? Conf)> valores)
    {
        bool Hay(string k) => valores.Any(v => v.Nombre == k);
        var faltan = new List<string>();
        if (perfil == "contrato")
        {
            foreach (var k in new[] { "contratista", "nit", "fecha_inicio", "fecha_fin" }) if (!Hay(k)) faltan.Add(k);
            if (!Hay("valor_total") && !Hay("valor_mensual")) { faltan.Add("valor_total"); faltan.Add("valor_mensual"); }
        }
        else if (perfil == "poliza")
        {
            foreach (var k in new[] { "aseguradora", "nit_aseguradora", "numero_poliza", "fecha_inicio", "fecha_fin" }) if (!Hay(k)) faltan.Add(k);
            if (!Hay("prima") && !Hay("valor_asegurado")) { faltan.Add("prima"); faltan.Add("valor_asegurado"); }
        }
        else if (perfil == "recibo_servicio")
        {
            foreach (var k in new[] { "referencia_contrato", "valor_total", "fecha_limite_pago" }) if (!Hay(k)) faltan.Add(k);
            if (!Hay("periodo_desde") && !Hay("periodo_hasta")) { faltan.Add("periodo_desde"); faltan.Add("periodo_hasta"); }
        }
        return faltan;
    }

    /// <summary>Parse tolerante del JSON del agente: por clave {valor, conf} (o un valor plano). Ignora lo ajeno al perfil.</summary>
    private static List<(string Nombre, string? Valor, double? Conf)> ParseCamposJson(string? text, IReadOnlyList<CampoObjetivo> campos)
    {
        var outp = new List<(string, string?, double?)>();
        if (string.IsNullOrWhiteSpace(text)) return outp;
        var t = text.Trim();
        var i = t.IndexOf('{'); var j = t.LastIndexOf('}');
        if (i < 0 || j <= i) return outp;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(t.Substring(i, j - i + 1));
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return outp;
            foreach (var c in campos)
            {
                if (!root.TryGetProperty(c.Nombre, out var el)) continue;
                string? val; double? conf = null;
                if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    // Formato {valor, conf}: el modelo tambien puede usar "confianza".
                    val = el.TryGetProperty("valor", out var ve) ? LeerValor(ve) : null;
                    if (el.TryGetProperty("conf", out var ce) && ce.ValueKind == System.Text.Json.JsonValueKind.Number) { conf = ce.GetDouble(); }
                    else if (el.TryGetProperty("confianza", out var ce2) && ce2.ValueKind == System.Text.Json.JsonValueKind.Number) { conf = ce2.GetDouble(); }
                }
                else { val = LeerValor(el); }   // tolerante: el modelo devolvio el valor plano
                if (!string.IsNullOrWhiteSpace(val)) { outp.Add((c.Nombre, val, conf)); }
            }
        }
        catch { /* json invalido: devolvemos lo que haya */ }
        return outp;
    }

    private static string? LeerValor(System.Text.Json.JsonElement el) => el.ValueKind switch
    {
        System.Text.Json.JsonValueKind.String => el.GetString(),
        System.Text.Json.JsonValueKind.Number => el.GetRawText(),
        System.Text.Json.JsonValueKind.True => "true",
        System.Text.Json.JsonValueKind.False => "false",
        System.Text.Json.JsonValueKind.Array => el.GetRawText(),   // tipo "lista" (se normaliza en NormalizarLista)
        _ => null
    };

    /// <summary>Tipo "lista" (desglose del recibo): arreglo [{concepto, valor}] -> JSON con el valor como numero
    /// invariante. Descarta filas sin concepto o sin valor legible. Vacio -> null.</summary>
    private static string? NormalizarLista(string raw)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return null;
            var outp = new List<object>();
            foreach (var it in doc.RootElement.EnumerateArray())
            {
                if (it.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                var concepto = it.TryGetProperty("concepto", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.String ? c.GetString()?.Trim() : null;
                var v = it.TryGetProperty("valor", out var ve) ? LeerValor(ve) : null;
                var monto = Application.Documentos.NormalizadorDocumental.Monto(v);
                if (string.IsNullOrWhiteSpace(concepto) || monto is null) continue;
                outp.Add(new { concepto, valor = monto.Value });
            }
            return outp.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(outp);
        }
        catch { return null; }
    }

    /// <summary>Si la suma de los conceptos no cuadra con valor_total ni con valor_periodo (+-1%), baja la confianza
    /// de "conceptos" a 0.4: la tarjeta lo marca dudoso (ambar) para revisarlo.</summary>
    private static void MarcarCuadreConceptos(List<(string Nombre, string? Valor, double? Conf)> valores)
    {
        var i = valores.FindIndex(v => v.Nombre == "conceptos");
        if (i < 0) return;
        decimal? Total(string k) => valores.FirstOrDefault(v => v.Nombre == k).Valor is { } s
            && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        decimal[] montos;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(valores[i].Valor!);
            montos = doc.RootElement.EnumerateArray().Select(x => x.GetProperty("valor").GetDecimal()).ToArray();
        }
        catch { return; }
        // El desglose puede ser solo del periodo (sin saldos): cuadra contra el total o contra el valor del periodo.
        var cuadra = Application.ServiciosPublicos.ReglasServicioPublico.CuadraDesglose(montos, Total("valor_total"))
                  || Application.ServiciosPublicos.ReglasServicioPublico.CuadraDesglose(montos, Total("valor_periodo"));
        if (!cuadra) valores[i] = (valores[i].Nombre, valores[i].Valor, Math.Min(valores[i].Conf ?? 1, 0.4));
    }

    /// <summary>Paginas = total de paginas del PDF (para medir si el texto es pobre); Imagenes pueden venir JUNTO
    /// con Texto: el servidor decide la ruta (texto si es suficiente, vision si no).</summary>
    public sealed record ExtraerAgenteRequest(string? Perfil, string? Texto, string? PdfBase64 = null, string? Mime = null, string? FileName = null, List<string>? Imagenes = null, int? Paginas = null);

    /// <summary>Campos objetivo por perfil (server-side; el cliente no define el schema).</summary>
    private static IReadOnlyList<CampoObjetivo> PerfilCampos(string? perfil) => (perfil ?? "").Trim().ToLowerInvariant() switch
    {
        // Tipos: texto | referencia (tal cual) | nit (sin puntos, DV con guion) | moneda | numero (entero) | decimal |
        // fecha (ISO) | sino (true/false) | lista (JSON [{concepto, valor}]).
        // El servidor normaliza cada tipo despues de leer el JSON (NormalizarCampos).
        "poliza" => new[]
        {
            new CampoObjetivo("aseguradora", "Nombre de la compania aseguradora que expide la poliza (NO el corredor ni la copropiedad)", "texto"),
            new CampoObjetivo("nit_aseguradora", "NIT de la compania aseguradora, con digito de verificacion si aparece", "nit"),
            new CampoObjetivo("corredor", "Nombre del corredor, agencia o intermediario de seguros, si aparece", "texto"),
            new CampoObjetivo("numero_poliza", "Numero de la poliza tal cual figura (no el numero de certificado ni de anexo, salvo que sea el unico)", "texto"),
            new CampoObjetivo("ramo", "Ramo o tipo de seguro (ej. RCE, Todo Riesgo Danos Materiales, Incendio, Manejo, Copropiedades)", "texto"),
            new CampoObjetivo("tomador", "Nombre del tomador de la poliza (quien la contrata)", "texto"),
            new CampoObjetivo("asegurado", "Nombre del asegurado o beneficiario de la cobertura", "texto"),
            new CampoObjetivo("valor_asegurado", "Valor asegurado total o suma asegurada, solo el numero", "moneda"),
            new CampoObjetivo("prima", "Prima total a pagar por la poliza (con IVA si se indica el total), solo el numero", "moneda"),
            new CampoObjetivo("deducible", "Deducible tal como lo expresa la poliza (porcentaje y minimo), en texto breve", "texto"),
            new CampoObjetivo("forma_pago_cuotas", "Numero de cuotas en que se paga la prima, si se indica", "numero"),
            new CampoObjetivo("pago_mensual", "Si la prima se paga mes a mes (si/no)", "sino"),
            new CampoObjetivo("incluye_zonas_unidades", "Si la cobertura incluye las zonas comunes Y las unidades privadas (si/no)", "sino"),
            new CampoObjetivo("fecha_inicio", "Fecha de inicio de vigencia", "fecha"),
            new CampoObjetivo("fecha_fin", "Fecha de fin de vigencia", "fecha"),
        },
        "contrato" => new[]
        {
            new CampoObjetivo("contratista", "Nombre o razon social del CONTRATISTA o proveedor: quien presta el servicio, ejecuta la obra o vende. NUNCA la copropiedad contratante", "texto"),
            new CampoObjetivo("nit", "NIT o cedula del contratista, con digito de verificacion si aparece", "nit"),
            new CampoObjetivo("representante_legal", "Nombre del representante legal del contratista (si es una empresa)", "texto"),
            new CampoObjetivo("direccion_contratista", "Direccion del contratista", "texto"),
            new CampoObjetivo("telefono_contratista", "Telefono o celular del contratista (solo uno, el principal)", "texto"),
            new CampoObjetivo("correo_contratista", "Correo electronico del contratista", "texto"),
            new CampoObjetivo("ciudad", "Ciudad donde se firma o se ejecuta el contrato", "texto"),
            new CampoObjetivo("numero_contrato", "Numero o consecutivo del contrato, si aparece", "texto"),
            new CampoObjetivo("tipo_contrato", "Tipo de contrato (prestacion de servicios, obra, suministro, mantenimiento, arrendamiento, vigilancia, consultoria...)", "texto"),
            new CampoObjetivo("objeto", "Objeto del contrato: resumen breve de que se contrata (obra, servicio, suministro)", "texto"),
            new CampoObjetivo("valor_total", "Valor total del contrato, solo el numero", "moneda"),
            new CampoObjetivo("valor_mensual", "Valor mensual o periodico, solo el numero, si el contrato lo indica", "moneda"),
            new CampoObjetivo("forma_pago_cuotas", "Numero de cuotas o pagos pactados, si se indica", "numero"),
            new CampoObjetivo("pago_mensual", "Si el contrato se paga mes a mes (si/no)", "sino"),
            new CampoObjetivo("renovacion_automatica", "Si el contrato se prorroga o renueva automaticamente (si/no)", "sino"),
            new CampoObjetivo("fecha_inicio", "Fecha de inicio de vigencia del contrato", "fecha"),
            new CampoObjetivo("fecha_fin", "Fecha de fin o terminacion del contrato", "fecha"),
        },
        "recibo_servicio" => new[]
        {
            new CampoObjetivo("referencia_contrato", "Referencia FIJA del servicio: numero de contrato, cuenta, NIC, NIU, codigo de suscriptor, matricula o referencia de pago. NUNCA el NIT ni el numero de factura", "referencia"),
            new CampoObjetivo("prestador", "Empresa que presta el servicio y expide el recibo", "texto"),
            new CampoObjetivo("nit_prestador", "NIT del prestador, con digito de verificacion si aparece", "nit"),
            new CampoObjetivo("titular", "Suscriptor o titular del servicio (suele ser la copropiedad)", "texto"),
            new CampoObjetivo("direccion_suministro", "Direccion donde se presta el servicio", "texto"),
            new CampoObjetivo("tipo_servicio", "Tipo de servicio: energia, acueducto, gas, aseo, internet o telefonia", "texto"),
            new CampoObjetivo("periodo_desde", "Fecha inicial del periodo facturado o de consumo", "fecha"),
            new CampoObjetivo("periodo_hasta", "Fecha final del periodo facturado o de consumo", "fecha"),
            new CampoObjetivo("fecha_expedicion", "Fecha de expedicion o emision del recibo", "fecha"),
            new CampoObjetivo("fecha_limite_pago", "Fecha limite de pago u oportuna (pague hasta)", "fecha"),
            new CampoObjetivo("lectura_anterior", "Lectura anterior del medidor, solo el numero", "numero"),
            new CampoObjetivo("lectura_actual", "Lectura actual del medidor, solo el numero", "numero"),
            new CampoObjetivo("consumo", "Consumo del periodo, solo el numero (puede tener decimales)", "decimal"),
            new CampoObjetivo("unidad_medida", "Unidad del consumo: kWh, m3, minutos, GB", "texto"),
            new CampoObjetivo("valor_periodo", "Valor facturado SOLO por el periodo (sin saldos anteriores ni mora), solo el numero", "moneda"),
            new CampoObjetivo("saldo_anterior", "Saldo anterior, deuda o mora de periodos previos, solo el numero", "moneda"),
            new CampoObjetivo("valor_total", "TOTAL A PAGAR del recibo, solo el numero", "moneda"),
            new CampoObjetivo("conceptos", "Desglose de cobros del periodo: lista de objetos {concepto, valor}", "lista"),
        },
        _ => Array.Empty<CampoObjetivo>()
    };

    [HttpPost("extraer")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> Extraer([FromForm] IFormFile? file, [FromForm] string? modelo, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "archivo_requerido" });

        await using var stream = file.OpenReadStream();
        var r = await _ocr.ExtraerAsync(stream, file.ContentType, modelo, ct);
        if (!r.Ok) return BadRequest(new { error = r.Error });

        // Sugerencias para prefilling de un recibo: valor y fecha si el modelo los detecto.
        var valorRaw = r.Campo("InvoiceTotal", "AmountDue", "Total", "TotalPrice", "SubTotal");
        decimal? valor = decimal.TryParse(valorRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
        var fecha = r.Campo("InvoiceDate", "TransactionDate", "DueDate", "ServiceStartDate");

        // Fallback: proveedores que solo devuelven texto crudo (ej. Azure Computer Vision) no traen
        // campos estructurados; parseamos el texto para inferir el total y la fecha del recibo.
        if (valor is null) valor = InferirValorDesdeTexto(r.TextoCompleto);
        if (string.IsNullOrWhiteSpace(fecha)) fecha = InferirFechaDesdeTexto(r.TextoCompleto);

        return Ok(new
        {
            ok = true,
            proveedor = r.Proveedor,
            modelo = r.Modelo,
            valorSugerido = valor,
            fechaSugerida = fecha,
            campos = r.Campos.Select(c => new { c.Nombre, c.Valor, c.Confianza }),
            textoCompleto = r.TextoCompleto
        });
    }

    /// <summary>
    /// Analiza un documento con el Agente Documental: extrae el texto con OCR y se lo pasa al agente
    /// de IA de la copropiedad activa, que lo clasifica (recibo/factura/poliza/poder) y PROPONE el
    /// registro via sus tools MCP del modulo de Servicios (en modo dry-run: el usuario confirma).
    /// Requiere un proveedor OCR habilitado, un proveedor LLM habilitado y el agente "Agente
    /// Documental" creado en el tenant (lo siembra AgenteDocumentalSeeder).
    /// </summary>
    [HttpPost("analizar")]
    [RequiereRol("Administrador")]  // S-06: el agente documental propone/persiste en Servicios/Cartera
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> Analizar([FromForm] IFormFile? file, [FromForm] string? instruccion, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "archivo_requerido" });

        // 1. OCR -> texto crudo del documento.
        string texto;
        string proveedorOcr;
        await using (var stream = file.OpenReadStream())
        {
            var ocr = await _ocr.ExtraerAsync(stream, file.ContentType, null, ct);
            if (!ocr.Ok) return BadRequest(new { error = ocr.Error });
            texto = ocr.TextoCompleto ?? "";
            proveedorOcr = ocr.Proveedor;
        }
        if (string.IsNullOrWhiteSpace(texto))
            return Ok(new { ok = true, proveedorOcr, ocrTexto = "", analisis = "El OCR no extrajo texto del documento. Sube una imagen mas nitida o un PDF con texto." });

        // 2. Ubicar el Agente Documental de la copropiedad activa (RLS: solo el del tenant del JWT).
        var agentId = await AgenteDocumentalAsync(ct);
        if (agentId is null) return BadRequest(new { error = "agente_documental_no_disponible" });

        // 3. Token del usuario: el agente lo presenta a las tools MCP y hereda tenant (RLS) + permisos.
        var bearer = Request.Headers.Authorization.ToString();
        if (bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) bearer = bearer["Bearer ".Length..].Trim();

        // 4. Construir el turno y correr el agente (function-calling MCP).
        var msg = "Analiza este documento extraido por OCR y clasificalo. Luego PROPON su registro en el modulo de " +
                  "Servicios ejecutando las herramientas SOLO con dryRun=true. IMPORTANTE: en esta respuesta NO llames " +
                  "ninguna herramienta con dryRun=false y NO persistas nada todavia; unicamente propon y termina " +
                  "pidiendo mi confirmacion explicita.\n\n" +
                  "--- DOCUMENTO (OCR) ---\n" + texto;
        if (!string.IsNullOrWhiteSpace(instruccion))
            msg += "\n\n--- INSTRUCCION ADICIONAL ---\n" + instruccion.Trim();

        var turns = new List<Application.InfraestructuraIa.AiChatTurn> { new("user", msg) };
        var res = await _ai.TestChatAsync(agentId.Value, turns, bearerToken: bearer, ct: ct);
        if (!res.Ok) return BadRequest(new { error = res.Error ?? "fallo_agente" });

        return Ok(new
        {
            ok = true,
            proveedorOcr,
            ocrTexto = texto,
            analisis = res.Text,
            inputTokens = res.InputTokens,
            outputTokens = res.OutputTokens,
            // Conversacion para poder CONTINUAR (confirmar) sin re-subir el documento.
            conversacion = new[]
            {
                new TurnoDto("user", msg),
                new TurnoDto("model", res.Text ?? "")
            }
        });
    }

    /// <summary>
    /// Continua la conversacion con el Agente Documental (paso de CONFIRMACION). Recibe el historial
    /// devuelto por /analizar y un mensaje (por defecto "confirmar"); el agente ejecuta entonces las
    /// tools MCP con dryRun=false para persistir lo propuesto. El token del usuario se reusa (RLS).
    /// </summary>
    [HttpPost("analizar/continuar")]
    [RequiereRol("Administrador")]  // S-06: paso de confirmacion que persiste (dryRun=false) via MCP
    public async Task<IActionResult> Continuar([FromBody] ContinuarAnalisisRequest req, CancellationToken ct)
    {
        if (req?.Conversacion is null || req.Conversacion.Count == 0)
            return BadRequest(new { error = "conversacion_requerida" });

        var agentId = await AgenteDocumentalAsync(ct);
        if (agentId is null) return BadRequest(new { error = "agente_documental_no_disponible" });

        var bearer = Request.Headers.Authorization.ToString();
        if (bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) bearer = bearer["Bearer ".Length..].Trim();

        var turns = req.Conversacion.Select(t => new Application.InfraestructuraIa.AiChatTurn(t.Role, t.Text)).ToList();
        var mensaje = string.IsNullOrWhiteSpace(req.Mensaje)
            ? "Confirmado. Ejecuta AHORA, de verdad, la(s) misma(s) herramienta(s) que propusiste pero con " +
              "dryRun=false, usando exactamente los mismos datos, para guardar definitivamente. Tienes esas " +
              "herramientas disponibles y habilitadas: USALAS, no respondas que no estan disponibles. Tras " +
              "guardar, confirma brevemente el resultado real que devolvio la herramienta."
            : req.Mensaje.Trim();
        turns.Add(new Application.InfraestructuraIa.AiChatTurn("user", mensaje));

        var res = await _ai.TestChatAsync(agentId.Value, turns, bearerToken: bearer, ct: ct);
        if (!res.Ok) return BadRequest(new { error = res.Error ?? "fallo_agente" });

        turns.Add(new Application.InfraestructuraIa.AiChatTurn("model", res.Text ?? ""));
        return Ok(new
        {
            ok = true,
            analisis = res.Text,
            conversacion = turns.Select(t => new TurnoDto(t.Role, t.Text)).ToList()
        });
    }

    // ---- Inferencia desde texto crudo (para proveedores OCR que no devuelven campos estructurados) ----

    /// <summary>Busca el total a pagar en el texto del recibo. Prioriza lineas con "total a pagar"/etc;
    /// si no, toma el monto mas grande. Maneja formato colombiano ($ 528.000 = 528000).</summary>
    internal static decimal? InferirValorDesdeTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var lineas = texto.Split('\n');
        var prioridad = new[] { "total a pagar", "valor a pagar", "neto a pagar", "total factura", "saldo a pagar", "total" };
        foreach (var key in prioridad)
            foreach (var ln in lineas)
                if (ln.ToLowerInvariant().Contains(key))
                {
                    var m = MontoMaximoEnLinea(ln);
                    if (m is not null) return m;
                }
        // Fallback: el monto mas grande de todo el documento.
        decimal? max = null;
        foreach (var ln in lineas)
        {
            var m = MontoMaximoEnLinea(ln);
            if (m is not null && (max is null || m > max)) max = m;
        }
        return max;
    }

    private static decimal? MontoMaximoEnLinea(string linea)
    {
        // Tokens monetarios: con simbolo $ o con separadores de miles (evita capturar "315 kWh").
        var rx = new Regex(@"\$\s*\d[\d.,]*|\d{1,3}(?:[.,]\d{3})+(?:[.,]\d{1,2})?");
        decimal? max = null;
        foreach (Match mt in rx.Matches(linea))
        {
            var val = ParseMontoColombiano(mt.Value);
            if (val is not null && (max is null || val > max)) max = val;
        }
        return max;
    }

    private static decimal? ParseMontoColombiano(string raw)
    {
        raw = raw.Replace("$", "").Replace(" ", "").Trim();
        if (raw.Length == 0) return null;
        bool hasDot = raw.Contains('.'), hasComma = raw.Contains(',');
        string norm;
        if (hasDot && hasComma)
        {
            // El ultimo separador es el decimal; el otro son miles.
            char dec = raw.LastIndexOf('.') > raw.LastIndexOf(',') ? '.' : ',';
            char miles = dec == '.' ? ',' : '.';
            norm = raw.Replace(miles.ToString(), "").Replace(dec, '.');
        }
        else if (hasComma)
        {
            var p = raw.Split(',');
            norm = (p.Length == 2 && p[1].Length <= 2) ? raw.Replace(',', '.') : raw.Replace(",", "");
        }
        else if (hasDot)
        {
            var p = raw.Split('.');
            norm = (p.Length == 2 && p[1].Length <= 2) ? raw : raw.Replace(".", "");
        }
        else norm = raw;
        return decimal.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>Infiere la fecha del recibo desde el texto. Prefiere la fecha de emision/factura
    /// (mas cercana al periodo de consumo) sobre la fecha limite de pago. Devuelve ISO yyyy-MM-dd.</summary>
    internal static string? InferirFechaDesdeTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var rx = new Regex(@"\b(\d{4}-\d{1,2}-\d{1,2}|\d{1,2}/\d{1,2}/\d{4}|\d{1,2}-\d{1,2}-\d{4})\b");
        var lineas = texto.Split('\n');
        var prefer = new[] { "emision", "expedicion", "factura", "generaci", "periodo", "fecha" };
        foreach (var key in prefer)
            foreach (var ln in lineas)
                if (ln.ToLowerInvariant().Contains(key))
                {
                    var m = rx.Match(ln);
                    if (m.Success) return NormalizarFechaIso(m.Value);
                }
        var any = rx.Match(texto);
        return any.Success ? NormalizarFechaIso(any.Value) : null;
    }

    private static string? NormalizarFechaIso(string raw)
    {
        foreach (var fmt in new[] { "yyyy-M-d", "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy" })
            if (DateTime.TryParseExact(raw, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.ToString("yyyy-MM-dd");
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dd) ? dd.ToString("yyyy-MM-dd") : raw;
    }

    public sealed record TurnoDto(string Role, string Text);
    public sealed record ContinuarAnalisisRequest(List<TurnoDto> Conversacion, string? Mensaje);
}
