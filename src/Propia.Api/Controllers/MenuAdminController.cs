using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Navegacion;

namespace Propia.Api.Controllers;

/// <summary>
/// Administracion del menu de navegacion GLOBAL (plataforma) para el Super Admin. Lee el menu resuelto
/// (base + overrides) y guarda el arreglo del editor (nombre / orden / ubicacion). Gated por la policy
/// SuperAdmin existente. El menu es global: lo que se guarda aqui lo ven todas las copropiedades.
/// Concepto portado de ECOREX.tareas (ConfiguracionMenu).
/// </summary>
[ApiController]
[Route("admin/menu")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class MenuAdminController : ControllerBase
{
    private readonly IMenuConfigService _svc;

    public MenuAdminController(IMenuConfigService svc) => _svc = svc;

    /// <summary>Menu resuelto (base + overrides) para poblar el editor.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _svc.GetResolvedMenuAsync(ct));

    /// <summary>Guarda el arreglo completo del editor (se persisten solo los deltas vs el base).</summary>
    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveMenuArrangementRequest req, CancellationToken ct)
    {
        await _svc.SaveArrangementAsync(req, ct);
        return NoContent();
    }

    /// <summary>Restablece el menu al base de codigo (borra todos los overrides).</summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await _svc.ResetAsync(ct);
        return NoContent();
    }
}
