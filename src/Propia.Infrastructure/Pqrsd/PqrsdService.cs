using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Pqrsd;

/// <summary>
/// Modulo 2.9 PQRSD y Convivencia (spec v1.0 - MVP).
/// Implementa radicacion, ciclo completo de estados, semaforo en dias habiles,
/// reserva de identidad, marca de Tutela y flujo del Comite de Convivencia.
/// </summary>
public partial class PqrsdService : IPqrsdService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly Propia.Application.Notificaciones.INotificacionDispatcher _noti;
    private readonly Propia.Application.Tareas.ITareasService _tareas;
    private readonly Propia.Application.Documents.IMembreteDocumentBuilder _membrete;

    public PqrsdService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        Propia.Application.Notificaciones.INotificacionDispatcher noti,
        Propia.Application.Tareas.ITareasService tareas,
        Propia.Application.Documents.IMembreteDocumentBuilder membrete)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _noti = noti;
        _tareas = tareas;
        _membrete = membrete;
    }

    private (Guid? UsuarioId, string? Nombre) ActorActual()
    {
        var u = _http.HttpContext?.User;
        Guid? uid = Guid.TryParse(u?.FindFirst("user_id")?.Value, out var g) ? g : null;
        var nombre = u?.FindFirst("name")?.Value
            ?? u?.FindFirst(ClaimTypes.Name)?.Value
            ?? u?.FindFirst("email")?.Value
            ?? u?.FindFirst(ClaimTypes.Email)?.Value
            ?? u?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
        return (uid, nombre);
    }

    // Nombre legible del actor: resuelve por persona_id (Nombres + Apellidos); cae al email del token.
    private async Task<string?> ResolverNombreActorAsync(CancellationToken ct)
    {
        var u = _http.HttpContext?.User;
        if (Guid.TryParse(u?.FindFirst("persona_id")?.Value, out var pid))
        {
            var p = await _db.Personas.AsNoTracking()
                .Where(x => x.Id == pid)
                .Select(x => new { x.Nombres, x.Apellidos })
                .FirstOrDefaultAsync(ct);
            if (p is not null)
            {
                var nombre = $"{p.Nombres} {p.Apellidos}".Trim();
                if (!string.IsNullOrWhiteSpace(nombre)) return nombre;
            }
        }
        return ActorActual().Nombre;
    }

    private async Task NotificarAdminsTenantAsync(
        string codigoModulo, Guid? entidadOrigen, string asunto, string cuerpo,
        Domain.Enums.PrioridadNotificacion prioridad, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return;
        var personaIds = await _db.UsuariosTenant.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Estado == Domain.Enums.EstadoUsuarioTenant.Activo)
            .Select(u => u.PersonaId).Distinct().Take(20).ToListAsync(ct);
        if (personaIds.Count == 0) return;
        var lote = personaIds.Select(pid =>
            new Propia.Application.Notificaciones.EnviarNotificacionRequest(
                Canal: Domain.Enums.CanalNotificacion.InApp,
                Cuerpo: cuerpo,
                TenantId: tenantId,
                PersonaDestinatariaId: pid,
                Asunto: asunto,
                Prioridad: prioridad,
                ModuloOrigenCodigo: codigoModulo,
                EntidadOrigenId: entidadOrigen));
        await _noti.EnviarLoteAsync(lote, ct);
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private async Task<Guid?> GetPersonaActualIdAsync(CancellationToken ct)
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("persona_id");
        if (Guid.TryParse(sub, out var id)) return id;
        var uid = GetUsuarioActualId();
        if (uid == Guid.Empty) return null;
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        return u?.PersonaId;
    }

}
