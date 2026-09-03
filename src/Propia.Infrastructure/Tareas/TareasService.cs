using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Tareas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Tareas;

/// <summary>Modulo 2.10 Tareas y Proyectos - MVP del spec v1.0.</summary>
public partial class TareasService : ITareasService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly Propia.Application.Notificaciones.INotificacionDispatcher _noti;

    public TareasService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        Propia.Application.Notificaciones.INotificacionDispatcher noti)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _noti = noti;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

}
