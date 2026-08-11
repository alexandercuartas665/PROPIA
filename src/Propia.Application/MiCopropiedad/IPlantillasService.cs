namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Carga masiva por plantilla Excel (.xlsx) para Zonas Comunes, Equipos/Activos y Directorio.
/// Cada plantilla trae encabezados fijos + una columna por cada campo dinamico del modulo + una
/// hoja "Instrucciones" con los valores validos. La carga hace UPSERT (crea o actualiza) sin abortar
/// ante un error de fila (lo acumula y sigue). Reusa los servicios de dominio para conservar RLS,
/// validacion y bitacora. Espejo de IDistribucionImportService.
/// </summary>
public interface IPlantillasService
{
    Task<byte[]> GenerarPlantillaZonasAsync(CancellationToken ct);
    Task<ImportarPlantillaResultado> ImportarZonasAsync(Stream xlsx, CancellationToken ct);

    Task<byte[]> GenerarPlantillaEquiposAsync(CancellationToken ct);
    Task<ImportarPlantillaResultado> ImportarEquiposAsync(Stream xlsx, CancellationToken ct);

    Task<byte[]> GenerarPlantillaDirectorioAsync(CancellationToken ct);
    Task<ImportarPlantillaResultado> ImportarDirectorioAsync(Stream xlsx, CancellationToken ct);
}

/// <summary>Resultado de una carga por plantilla (upsert). Reusa ImportarErrorFila.</summary>
public record ImportarPlantillaResultado(
    int Creados,
    int Actualizados,
    int CamposCargados,
    int FilasConError,
    IReadOnlyList<ImportarErrorFila> Errores);
