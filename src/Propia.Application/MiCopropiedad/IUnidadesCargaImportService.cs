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
    IReadOnlyList<CargaUnidadesError> Errores);

/// <summary>
/// Importa la plantilla Excel multi-hoja (Unidades/Personas/Vehiculos/Mascotas) y alimenta el
/// sistema, pudiendo cargar en VARIAS copropiedades del cliente (por la columna COPROPIEDAD).
/// Reusa los servicios de Mi Copropiedad/Porteria (validacion, RLS, bitacora). Tolerante a errores:
/// reporta la fila con problema y sigue con el resto.
/// </summary>
public interface IUnidadesCargaImportService
{
    Task<ResultadoCargaUnidades> ImportarAsync(Stream contenidoXlsx, CancellationToken ct);
}
