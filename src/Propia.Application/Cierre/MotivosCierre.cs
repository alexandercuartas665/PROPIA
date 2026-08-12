using Propia.Domain.Enums;

namespace Propia.Application.Cierre;

/// <summary>Modulos que usan motivos de cierre (catalogos separados).</summary>
public static class ModuloCierre
{
    public const string Tareas = "tareas";
    public const string Pqrsd = "pqrsd";
}

public sealed record MotivoCierreDto(
    Guid Id, string Modulo, string Nombre, ClasificacionCierre Clasificacion,
    bool EsBase, bool Activo, int Orden);

public sealed record GuardarMotivoCierreRequest(
    string Nombre, ClasificacionCierre Clasificacion, int? Orden = null, bool? Activo = null);

/// <summary>
/// Catalogo configurable de motivos de cierre por copropiedad, SEPARADO por modulo (tareas/pqrsd).
/// Siembra defaults de forma perezosa la primera vez que se listan para un modulo.
/// </summary>
public interface IMotivosCierreService
{
    Task<IReadOnlyList<MotivoCierreDto>> ListarAsync(string modulo, bool incluirInactivos, CancellationToken ct = default);
    Task<MotivoCierreDto> CrearAsync(string modulo, GuardarMotivoCierreRequest req, CancellationToken ct = default);
    Task<MotivoCierreDto?> ActualizarAsync(Guid id, GuardarMotivoCierreRequest req, CancellationToken ct = default);
    Task<bool> EliminarAsync(Guid id, CancellationToken ct = default);
}
