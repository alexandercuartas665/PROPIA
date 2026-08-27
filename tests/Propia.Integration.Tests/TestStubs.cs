using Propia.Application.UsuariosAccesos;
using Propia.Domain.Enums;

namespace Propia.Integration.Tests;

/// <summary>
/// Stub no-op de <see cref="ISeedUsuarioRolService"/> para construir servicios en tests que no
/// ejercen la siembra automatica de usuarios/roles. Evita tener que levantar UserManager en el test.
/// </summary>
internal sealed class StubSeedUsuarioRolService : ISeedUsuarioRolService
{
    public Task SembrarPorFacetaAsync(Guid personaId, RolUnidadPersona faceta, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<int> BackfillFacetasAsync(IEnumerable<int> facetasInt, CancellationToken ct = default)
        => Task.FromResult(0);
}
