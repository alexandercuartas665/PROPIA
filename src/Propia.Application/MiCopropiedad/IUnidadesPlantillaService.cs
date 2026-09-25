namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Alcance de las hojas que lleva la plantilla de carga. Permite un archivo por HUB en lugar de un
/// unico libro con todas las entidades:
/// - Todas: unidades + personas + vehiculos + mascotas + terceros + zonas + equipos (compat/onboarding).
/// - Config: hub "Configuracion copropiedad" -> unidades + zonas comunes + equipos.
/// - Residentes: hub "Residentes" -> personas (residentes) + mascotas + vehiculos.
/// </summary>
public enum PlantillaCargaScope
{
    Todas = 0,
    Config = 1,
    Residentes = 2,
}

/// <summary>
/// Genera la plantilla Excel de carga masiva de unidades privadas y sus datos relacionados
/// (personas, vehiculos, mascotas, terceros). Trae datos de referencia (IDs de las copropiedades
/// del cliente, catalogos) y listas desplegables para forzar valores validos del sistema.
/// </summary>
public interface IUnidadesPlantillaService
{
    /// <summary>Devuelve el .xlsx de la plantilla de carga (con hojas, columnas dinamicas y dropdowns)
    /// segun el alcance pedido (todas las hojas, o solo las de un hub).</summary>
    Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaCargaAsync(PlantillaCargaScope scope, CancellationToken ct);
}
