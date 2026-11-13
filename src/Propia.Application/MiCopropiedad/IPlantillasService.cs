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

    /// <summary>Usuarios (2.5): una fila por usuario (persona + roles + cargos). Crea la persona en el Directorio si
    /// no existe y da el alta en estado Pendiente, SIN enviar invitacion (se invita despues desde Usuarios).</summary>
    Task<byte[]> GenerarPlantillaUsuariosAsync(CancellationToken ct);
    Task<ImportarUsuariosResultado> ImportarUsuariosAsync(Stream xlsx, CancellationToken ct);
}

/// <summary>Resultado de la carga de usuarios: creados, pendientes actualizados (roles/cargos), los que ya tenian
/// acceso activo (no se tocan) y las filas con error (con su motivo).</summary>
public record ImportarUsuariosResultado(
    int Creados,
    int Actualizados,
    int SinCambios,
    int FilasConError,
    IReadOnlyList<ImportarErrorFila> Errores);

/// <summary>Resultado de una carga por plantilla (upsert). Reusa ImportarErrorFila.</summary>
public record ImportarPlantillaResultado(
    int Creados,
    int Actualizados,
    int CamposCargados,
    int FilasConError,
    IReadOnlyList<ImportarErrorFila> Errores);
