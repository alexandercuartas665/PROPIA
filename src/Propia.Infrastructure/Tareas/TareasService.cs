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

    /// <summary>
    /// T-02: valida que la persona pertenezca a la copropiedad activa antes de asignarla,
    /// ponerla de solicitante o agregarla como colaboradora.
    ///
    /// La tabla personas es GLOBAL (sin filtro de tenant), asi que sin esta comprobacion un
    /// usuario del tenant A podia asignar por Guid a una persona del tenant B, que ademas
    /// recibia la notificacion con datos de A. El vinculo se comprueba contra DirectorioVinculos,
    /// que SI tiene HasQueryFilter, asi que ya viene acotado a la copropiedad activa. Es el mismo
    /// criterio que usa UsuariosService al invitar (S-02b).
    ///
    /// Devuelve sin hacer nada si personaId es null (el campo es opcional).
    /// </summary>
    private async Task ValidarPersonaDelTenantAsync(Guid? personaId, string campo, CancellationToken ct)
    {
        if (personaId is not { } pid || pid == Guid.Empty) return;

        var vinculada = await _db.DirectorioVinculos
            .AsNoTracking()
            .AnyAsync(v => v.EntidadTipo == EntidadDirectorio.Persona && v.EntidadId == pid, ct);
        if (!vinculada)
            throw new InvalidOperationException(
                $"La persona indicada en '{campo}' no pertenece a esta copropiedad. " +
                "Vinculala primero en el Directorio.");
    }
}
