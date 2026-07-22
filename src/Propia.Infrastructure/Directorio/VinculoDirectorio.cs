using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Directorio;

/// <summary>
/// Alta idempotente del vinculo persona - copropiedad.
///
/// Por que existe: Persona es GLOBAL (una fila por documento en toda la plataforma, sin
/// tenant_id ni RLS). Lo que dice "esta persona pertenece a ESTA copropiedad" es la fila de
/// DirectorioVinculo, y es la que alimenta GET /api/directorio/personas, o sea TODOS los
/// selectores de persona de la app (tareas, reservas, porteria, programaciones...).
///
/// Varios caminos de alta creaban la Persona y su relacion especifica (unidad, consejo,
/// equipo) pero NO el vinculo, dejando gente invisible para el resto del sistema. Este
/// helper centraliza el "asegurate de que quede en el directorio" para que ningun camino
/// se vuelva a olvidar.
/// </summary>
internal static class VinculoDirectorio
{
    /// <summary>
    /// Garantiza que la persona tenga vinculo activo con la copropiedad actual.
    /// Devuelve true si lo creo, false si ya existia o no hay tenant activo.
    /// El query filter de DirectorioVinculos ya acota la consulta al tenant en curso.
    /// </summary>
    public static async Task<bool> AsegurarPersonaAsync(
        PropiaDbContext db, ITenantContext tenant, Guid personaId, CancellationToken ct)
    {
        if (tenant.CurrentTenantId is null) return false;
        if (personaId == Guid.Empty) return false;

        var yaVinculada = await db.DirectorioVinculos.AnyAsync(v =>
            v.EntidadTipo == EntidadDirectorio.Persona &&
            v.EntidadId == personaId &&
            v.Estado == EstadoVinculo.Activo, ct);
        if (yaVinculada) return false;

        db.DirectorioVinculos.Add(new DirectorioVinculo
        {
            EntidadTipo = EntidadDirectorio.Persona,
            EntidadId = personaId,
            FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow),
            Estado = EstadoVinculo.Activo
        });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
