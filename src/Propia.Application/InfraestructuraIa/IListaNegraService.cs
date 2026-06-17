namespace Propia.Application.InfraestructuraIa;

/// <summary>Numero en la lista negra global del tenant.</summary>
public sealed record NumeroEnListaNegraDto(Guid Id, string Telefono, string? Nota, DateTimeOffset CreatedAt);

public sealed record AgregarNumeroBloqueadoRequest(string Telefono, string? Nota);

/// <summary>
/// Lista negra global del tenant: numeros que ningun agente de IA debe atender. Compartida por todos
/// los agentes del tenant. Portado desde CUBOT.travels.
/// </summary>
public interface IListaNegraService
{
    Task<IReadOnlyList<NumeroEnListaNegraDto>> ListarAsync(CancellationToken ct = default);
    /// <summary>Agrega un numero (normalizado a digitos). Devuelve null si el telefono es invalido o no hay tenant; si ya existia, devuelve el existente.</summary>
    Task<NumeroEnListaNegraDto?> AgregarAsync(AgregarNumeroBloqueadoRequest req, CancellationToken ct = default);
    Task<bool> EliminarAsync(Guid id, CancellationToken ct = default);
    /// <summary>True si el telefono dado esta bloqueado en este tenant. Usado por el dispatcher.</summary>
    Task<bool> EstaBloqueadoAsync(string telefono, CancellationToken ct = default);
}
