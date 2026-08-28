namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Genera la plantilla Excel de carga masiva de unidades privadas y sus datos relacionados
/// (personas, vehiculos, mascotas, terceros). Trae datos de referencia (IDs de las copropiedades
/// del cliente, catalogos) y listas desplegables para forzar valores validos del sistema.
/// </summary>
public interface IUnidadesPlantillaService
{
    /// <summary>Devuelve el .xlsx de la plantilla de carga (con hojas, columnas dinamicas y dropdowns).</summary>
    Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaCargaAsync(CancellationToken ct);
}
