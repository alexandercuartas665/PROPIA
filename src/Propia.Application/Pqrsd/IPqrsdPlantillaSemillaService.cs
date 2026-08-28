namespace Propia.Application.Pqrsd;

/// <summary>Plantilla semilla global (catalogo del operador). Base de la que nacen las de cada copropiedad.</summary>
public record PqrsdPlantillaSemillaDto(Guid Id, string Nombre, string CuerpoHtml, bool Activa, int Orden);

public record GuardarPlantillaSemillaRequest(string Nombre, string CuerpoHtml, bool Activa, int Orden);

/// <summary>
/// Administra el catalogo GLOBAL de plantillas semilla de respuesta PQRSD (Super Admin, Capa 0).
/// Las copropiedades heredan una copia al usar el modulo; editarlas no afecta la semilla.
/// </summary>
public interface IPqrsdPlantillaSemillaService
{
    Task<IReadOnlyList<PqrsdPlantillaSemillaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct);
    Task<PqrsdPlantillaSemillaDto> CrearAsync(GuardarPlantillaSemillaRequest req, CancellationToken ct);
    Task<PqrsdPlantillaSemillaDto?> ActualizarAsync(Guid id, GuardarPlantillaSemillaRequest req, CancellationToken ct);
    Task<bool> EliminarAsync(Guid id, CancellationToken ct);
}
