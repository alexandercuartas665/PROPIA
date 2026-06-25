using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
[Authorize]
public class OcrController : ControllerBase
{
    private readonly IDocumentExtractionService _ocr;
    private readonly IAiInferenceService _ai;
    private readonly PropiaDbContext _db;

    public OcrController(IDocumentExtractionService ocr, IAiInferenceService ai, PropiaDbContext db)
    {
        _ocr = ocr;
        _ai = ai;
        _db = db;
    }

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
        var agentId = await _db.AiAgents.AsNoTracking()
            .Where(a => a.Name == AgenteDocumentalSeeder.AgenteNombre && a.IsActive)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
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
    public async Task<IActionResult> Continuar([FromBody] ContinuarAnalisisRequest req, CancellationToken ct)
    {
        if (req?.Conversacion is null || req.Conversacion.Count == 0)
            return BadRequest(new { error = "conversacion_requerida" });

        var agentId = await _db.AiAgents.AsNoTracking()
            .Where(a => a.Name == AgenteDocumentalSeeder.AgenteNombre && a.IsActive)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
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

    public sealed record TurnoDto(string Role, string Text);
    public sealed record ContinuarAnalisisRequest(List<TurnoDto> Conversacion, string? Mensaje);
}
