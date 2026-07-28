namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Carga masiva de la Distribucion (seccion 2 de Mi Copropiedad) desde una plantilla Excel.
/// Genera la plantilla (.xlsx multi-hoja con instrucciones + tablas de apoyo) y procesa el
/// archivo subido creando torres y unidades en el tenant actual.
/// </summary>
public interface IDistribucionImportService
{
    /// <summary>Genera la plantilla .xlsx (hojas: Instrucciones, Unidades, Torres, Catalogos).</summary>
    byte[] GenerarPlantilla();

    /// <summary>
    /// Procesa un .xlsx con la estructura de la plantilla: crea las torres de la hoja "Torres"
    /// (idempotente por nombre) y luego las unidades de la hoja "Unidades" (resolviendo la torre
    /// por nombre). No aborta ante un error de fila: lo acumula y sigue.
    /// </summary>
    Task<ImportarDistribucionResultado> ImportarAsync(Stream contenidoXlsx, CancellationToken ct);
}

/// <summary>Resumen del resultado de una importacion de distribucion.</summary>
public record ImportarDistribucionResultado(
    int TorresCreadas,
    int UnidadesCreadas,
    int FilasConError,
    IReadOnlyList<ImportarErrorFila> Errores,
    int PersonasVinculadas = 0,
    int VinculosCreados = 0);

/// <summary>Un error puntual en una fila del archivo (para mostrarselo al usuario sin abortar todo).</summary>
public record ImportarErrorFila(string Hoja, int Fila, string Mensaje);
