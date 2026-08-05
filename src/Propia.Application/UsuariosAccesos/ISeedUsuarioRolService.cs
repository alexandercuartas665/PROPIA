using Propia.Domain.Enums;

namespace Propia.Application.UsuariosAccesos;

/// <summary>
/// Siembra automatica de usuario+directorio segun el "rol semilla" configurado por faceta de la
/// distribucion (RolUnidadPersona). Al asignar una persona a una faceta en una unidad, si un rol
/// Personalizado del tenant declara esa faceta como semilla, se le crea/asegura su UsuarioTenant
/// (con ese rol) y su ApplicationUser (login, si la persona tiene email). Si el rol tiene
/// SoloDirectorio=true, el usuario se oculta de la lista de Usuarios (se gestiona en el Directorio).
/// </summary>
public interface ISeedUsuarioRolService
{
    /// <summary>Siembra una persona segun una faceta puntual (llamado al asignar en la unidad).</summary>
    Task SembrarPorFacetaAsync(Guid personaId, RolUnidadPersona faceta, CancellationToken ct = default);

    /// <summary>
    /// Backfill retroactivo: siembra a todas las personas ya vinculadas a unidades cuyas facetas
    /// esten en el conjunto dado (valores int de RolUnidadPersona). Devuelve cuantas se sembraron.
    /// </summary>
    Task<int> BackfillFacetasAsync(IEnumerable<int> facetasInt, CancellationToken ct = default);
}
