namespace Propia.Application.MiCopropiedad;

/// <summary>Error de una fila del import (hoja + fila + motivo).</summary>
public record CargaUnidadesError(string Hoja, int Fila, string Motivo);

/// <summary>Resultado del import multi-hoja / multi-copropiedad.</summary>
public record ResultadoCargaUnidades(
    int Copropiedades,
    int Unidades,
    int Anexos,
    int Personas,
    int Vehiculos,
    int Mascotas,
    int Zonas,
    int Equipos,
    IReadOnlyList<CargaUnidadesError> Errores,
    // Unidades que ya existian y se ACTUALIZARON al recargar (modulo Unidades Privadas). En onboarding
    // siempre es 0: ahi toda unidad es nueva. Opcional para no romper construcciones existentes.
    int UnidadesActualizadas = 0,
    // Terceros (personas/empresas del Directorio) cargados desde la hoja TERCEROS. Con "Todas las
    // copropiedades" el tercero queda vinculado a todas las copropiedades del cliente.
    int Terceros = 0);

/// <summary>
/// Importa la plantilla Excel multi-hoja (Unidades/Personas/Vehiculos/Mascotas) y alimenta el
/// sistema, pudiendo cargar en VARIAS copropiedades del cliente (por la columna COPROPIEDAD).
/// Reusa los servicios de Mi Copropiedad/Porteria (validacion, RLS, bitacora). Tolerante a errores:
/// reporta la fila con problema y sigue con el resto.
/// </summary>
public interface IUnidadesCargaImportService
{
    /// <param name="forzarTenantActual">
    /// Si es true, ignora la columna COPROPIEDAD y carga TODAS las filas en el tenant activo.
    /// Se usa en el onboarding (una sola copropiedad recien creada, cuyo nombre puede no coincidir
    /// con lo que el usuario escribio en la plantilla). Default false = comportamiento multi-copropiedad.
    /// </param>
    /// <param name="reemplazarDependientes">
    /// Si es true (recarga desde el modulo Unidades Privadas, previa confirmacion del usuario), para las
    /// categorias DEPENDIENTES que traiga el archivo (personas, vehiculos, mascotas, zonas comunes,
    /// equipos) se BORRAN primero las existentes de esa copropiedad y se cargan de nuevo (reemplazo). Las
    /// UNIDADES nunca se borran: se actualizan (upsert) para conservar sus vinculos. Default false.
    /// </param>
    Task<ResultadoCargaUnidades> ImportarAsync(Stream contenidoXlsx, CancellationToken ct, bool forzarTenantActual = false, bool reemplazarDependientes = false);
}
