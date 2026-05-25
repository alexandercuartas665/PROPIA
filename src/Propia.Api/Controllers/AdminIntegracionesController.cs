using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Common;
using Propia.Application.Integraciones;

namespace Propia.Api.Controllers;

/// <summary>
/// Integraciones de plataforma del Super Admin (correo, marca, y proximamente Wompi/IA/Google/Evolution).
/// Ruta /admin/* protegida por la policy SuperAdmin. Portado y adaptado de CUBOT.travels.
/// </summary>
[ApiController]
[Route("admin")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class AdminIntegracionesController : ControllerBase
{
    private readonly IEmailConfigService _email;
    private readonly IPlatformBrandingService _branding;
    private readonly IEmailSender _emailSender;
    private readonly IAiServerConfigService _ai;
    private readonly IWompiConfigService _wompi;
    private readonly IEvolutionMasterConfigService _evolution;

    public AdminIntegracionesController(
        IEmailConfigService email,
        IPlatformBrandingService branding,
        IEmailSender emailSender,
        IAiServerConfigService ai,
        IWompiConfigService wompi,
        IEvolutionMasterConfigService evolution)
    {
        _email = email;
        _branding = branding;
        _emailSender = emailSender;
        _ai = ai;
        _wompi = wompi;
        _evolution = evolution;
    }

    // -------------------- Servidor de Correo --------------------

    [HttpGet("email-config")]
    public async Task<IActionResult> GetEmailConfig(CancellationToken ct)
        => Ok(await _email.GetAsync(ct) ?? new EmailConfigDto(null, 587, null, false, true, null, null, false, null));

    [HttpPut("email-config")]
    public async Task<IActionResult> SaveEmailConfig([FromBody] SaveEmailConfigRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        var dto = await _email.SaveAsync(req, id, email, Ip(), ct);
        return Ok(dto);
    }

    [HttpPost("email-config/test")]
    public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ToEmail))
            return BadRequest(new { error = "destinatario_requerido" });

        var result = await _emailSender.SendAsync(
            req.ToEmail,
            "PROPIA - Correo de prueba",
            "<p>Este es un correo de prueba enviado desde la consola de administracion de PROPIA. " +
            "Si lo recibes, la configuracion SMTP es correcta.</p>",
            ct);

        return result.Success
            ? Ok(new { enviado = true })
            : BadRequest(new { enviado = false, error = result.Error });
    }

    // -------------------- Marca de plataforma --------------------

    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding(CancellationToken ct)
        => Ok(await _branding.GetAsync(ct));

    [HttpPut("branding")]
    public async Task<IActionResult> SaveBranding([FromBody] SaveBrandingRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        await _branding.SaveAsync(req, id, email, Ip(), ct);
        return Ok(await _branding.GetAsync(ct));
    }

    // -------------------- Servidores de IA --------------------

    [HttpGet("ai-config")]
    public async Task<IActionResult> ListAiConfig(CancellationToken ct)
        => Ok(await _ai.ListAsync(ct));

    [HttpPut("ai-config")]
    public async Task<IActionResult> SaveAiConfig([FromBody] SaveAiProviderRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        var dto = await _ai.SaveAsync(req, id, email, Ip(), ct);
        return Ok(dto);
    }

    // -------------------- Wompi (config maestra) --------------------

    [HttpGet("wompi-config")]
    public async Task<IActionResult> GetWompiConfig(CancellationToken ct)
        => Ok(await _wompi.GetAsync(ct) ?? new WompiConfigDto(
            Propia.Domain.Enums.WompiEnvironment.Sandbox, null, null, null, null, null, "COP", 3,
            Propia.Domain.Enums.WompiIntegrationStatus.NotConfigured, null, false, false, false));

    [HttpPut("wompi-config")]
    public async Task<IActionResult> SaveWompiConfig([FromBody] SaveWompiConfigRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        return Ok(await _wompi.SaveAsync(req, id, email, Ip(), ct));
    }

    [HttpPost("wompi-config/validar")]
    public async Task<IActionResult> ValidarWompiConfig(CancellationToken ct)
    {
        var (id, email) = Actor();
        var result = await _wompi.ValidateAsync(id, email, Ip(), ct);
        if (result is null) return BadRequest(new { error = "wompi_no_configurado" });
        return Ok(result);
    }

    // -------------------- Evolution API (WhatsApp) --------------------

    [HttpGet("evolution-config")]
    public async Task<IActionResult> GetEvolutionConfig(CancellationToken ct)
        => Ok(await _evolution.GetAsync(ct) ?? new EvolutionMasterDto(
            null, null, false, Propia.Domain.Enums.EvolutionIntegrationStatus.NotConfigured, null, "Production", null, false));

    [HttpPut("evolution-config")]
    public async Task<IActionResult> SaveEvolutionConfig([FromBody] SaveEvolutionMasterRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        return Ok(await _evolution.SaveAsync(req, id, email, Ip(), ct));
    }

    [HttpPost("evolution-config/validar")]
    public async Task<IActionResult> ValidarEvolutionConfig(CancellationToken ct)
    {
        var (id, email) = Actor();
        var result = await _evolution.ValidateAsync(id, email, Ip(), ct);
        if (result is null) return BadRequest(new { error = "evolution_no_configurado" });
        return Ok(result);
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
