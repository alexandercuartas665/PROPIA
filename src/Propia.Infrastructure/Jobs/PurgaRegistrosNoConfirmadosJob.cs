using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// S-04b: purga los auto-registros de onboarding que quedaron ABANDONADOS antes de confirmar el
/// correo (EmailConfirmed=false + OnboardingSessionId != null) y superaron la ventana de gracia.
/// Como el JWT del wizard solo se emite tras verificar el OTP, un usuario sin confirmar no pudo
/// avanzar ni crear copropiedad; aun asi se exige que NO tenga membresia de tenant como salvaguarda.
/// Evita que se acumulen cuentas fantasma (y correos "ocupados") indefinidamente.
/// </summary>
public class PurgaRegistrosNoConfirmadosJob : IBackgroundJob
{
    public string Nombre => "PurgaRegistrosNoConfirmados";
    public int FrecuenciaMinutos => 60 * 6; // cada 6 horas

    private readonly PropiaDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly int _horasGracia;

    public PurgaRegistrosNoConfirmadosJob(
        PropiaDbContext db, UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _horasGracia = config.GetValue<int?>("Onboarding:PurgaNoConfirmadosHoras") ?? 48;
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        var corte = DateTimeOffset.UtcNow.AddHours(-_horasGracia);

        // Candidatos: usuarios de onboarding sin confirmar cuya Persona se creo antes del corte
        // (ApplicationUser es IdentityUser sin timestamp; usamos Persona.CreatedAt como edad).
        var candidatos = await (
            from u in _db.Users
            where !u.EmailConfirmed && u.OnboardingSessionId != null && u.PersonaId != null
            join p in _db.Personas.IgnoreQueryFilters() on u.PersonaId equals p.Id
            where p.CreatedAt < corte
            select new { User = u, PersonaId = p.Id }
        ).ToListAsync(ct);

        var purgados = 0;
        foreach (var c in candidatos)
        {
            // Salvaguarda: no tocar si la persona ya tiene alguna membresia de copropiedad.
            var tieneTenant = await _db.UsuariosTenant.IgnoreQueryFilters()
                .AnyAsync(ut => ut.PersonaId == c.PersonaId, ct);
            if (tieneTenant) continue;

            var del = await _userManager.DeleteAsync(c.User);
            if (!del.Succeeded) continue;

            await _db.Personas.IgnoreQueryFilters()
                .Where(p => p.Id == c.PersonaId)
                .ExecuteDeleteAsync(ct);
            purgados++;
        }

        return new { candidatos = candidatos.Count, purgados, horasGracia = _horasGracia };
    }
}
