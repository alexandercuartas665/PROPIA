namespace Propia.Domain.Enums;

/// <summary>
/// Entidad a la que cuelga un muro de novedades. El valor 1 (ZonaComun) es el que tenian
/// todas las novedades antes de generalizar el muro, por eso es el default de la migracion.
/// </summary>
public enum TipoEntidadNovedad
{
    ZonaComun = 1,
    EquipoActivo = 2
}
