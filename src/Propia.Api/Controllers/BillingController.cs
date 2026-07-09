using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Billing;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints del modulo 0.2 Billing y Suscripciones - alcance MVP.
/// Rutas: /admin/billing/* - solo SuperAdmin (claim is_super_admin=true).
/// </summary>
[ApiController]
[Route("api/admin/billing")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;

    public BillingController(IBillingService billing) => _billing = billing;

    // -------------------- Planes --------------------

    [HttpGet("planes")]
    public async Task<IActionResult> ListPlanes(CancellationToken ct)
        => Ok(await _billing.ListPlanesAsync(ct));

    [HttpPost("planes")]
    public async Task<IActionResult> CrearPlan([FromBody] CrearPlanRequest req, CancellationToken ct)
    {
        try
        {
            var (id, email) = Actor();
            var plan = await _billing.CrearPlanAsync(req, id, email, Ip(), ct);
            return CreatedAtAction(nameof(ListPlanes), new { }, plan);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("planes/{planId:guid}")]
    public async Task<IActionResult> ActualizarPlan(Guid planId, [FromBody] ActualizarPlanRequest req, CancellationToken ct)
    {
        try
        {
            var (id, email) = Actor();
            var plan = await _billing.ActualizarPlanAsync(planId, req, id, email, Ip(), ct);
            return plan is null ? NotFound() : Ok(plan);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("planes/{planId:guid}")]
    public async Task<IActionResult> EliminarPlan(Guid planId, CancellationToken ct)
    {
        try
        {
            var (id, email) = Actor();
            var ok = await _billing.EliminarPlanAsync(planId, id, email, Ip(), ct);
            return ok is null ? NotFound() : NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // -------------------- Suscripciones --------------------

    [HttpGet("suscripciones")]
    public async Task<IActionResult> ListSuscripciones(CancellationToken ct)
        => Ok(await _billing.ListSuscripcionesAsync(ct));

    [HttpPost("suscripciones")]
    public async Task<IActionResult> CrearSuscripcion([FromBody] CrearSuscripcionRequest req, CancellationToken ct)
    {
        try
        {
            var (id, email) = Actor();
            var s = await _billing.CrearSuscripcionAsync(req, id, email, Ip(), ct);
            return CreatedAtAction(nameof(ListSuscripciones), new { }, s);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("suscripciones/{suscripcionId:guid}/plan")]
    public async Task<IActionResult> CambiarPlan(Guid suscripcionId, [FromBody] CambiarPlanRequest req, CancellationToken ct)
    {
        try
        {
            var (id, email) = Actor();
            var s = await _billing.CambiarPlanSuscripcionAsync(suscripcionId, req, id, email, Ip(), ct);
            return s is null ? NotFound() : Ok(s);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("suscripciones/{suscripcionId:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid suscripcionId, [FromBody] CambiarEstadoSuscripcionRequest req, CancellationToken ct)
    {
        try
        {
            var (id, email) = Actor();
            var s = await _billing.CambiarEstadoSuscripcionAsync(suscripcionId, req, id, email, Ip(), ct);
            return s is null ? NotFound() : Ok(s);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("suscripciones/{suscripcionId:guid}/historial")]
    public async Task<IActionResult> GetHistorial(Guid suscripcionId, CancellationToken ct)
        => Ok(await _billing.GetHistorialAsync(suscripcionId, ct));

    // -------------------- Facturas --------------------

    [HttpGet("facturas")]
    public async Task<IActionResult> ListFacturas([FromQuery] Guid? suscripcionId, CancellationToken ct)
        => Ok(await _billing.ListFacturasAsync(suscripcionId, ct));

    [HttpPost("facturas/generar")]
    public async Task<IActionResult> Generar([FromBody] GenerarFacturaRequest req, CancellationToken ct)
    {
        try
        {
            var (id, email) = Actor();
            var f = await _billing.GenerarFacturaAsync(req, id, email, Ip(), ct);
            return CreatedAtAction(nameof(ListFacturas), new { suscripcionId = req.SuscripcionId }, f);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("facturas/{facturaId:guid}/registrar-pago")]
    public async Task<IActionResult> RegistrarPago(Guid facturaId, [FromBody] RegistrarPagoFacturaRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        var f = await _billing.RegistrarPagoAsync(facturaId, req, id, email, Ip(), ct);
        return f is null ? NotFound() : Ok(f);
    }

    // -------------------- Config --------------------

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
        => Ok(await _billing.GetConfigAsync(ct));

    [HttpPut("config")]
    public async Task<IActionResult> ActualizarConfig([FromBody] ActualizarBillingConfigRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        return Ok(await _billing.ActualizarConfigAsync(req, id, email, Ip(), ct));
    }

    // -------------------- Helpers --------------------

    private (Guid Id, string Email) Actor()
    {
        var id = Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : Guid.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "unknown";
        return (id, email);
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
