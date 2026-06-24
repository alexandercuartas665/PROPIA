using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Ocr;

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
    public OcrController(IDocumentExtractionService ocr) => _ocr = ocr;

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
}
