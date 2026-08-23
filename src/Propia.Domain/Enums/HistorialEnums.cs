namespace Propia.Domain.Enums;

/// <summary>Tipo de entidad de la copropiedad sobre la que se arma el "Historial relacionado"
/// (tareas + PQRSD + mantenimientos vinculados). Ver IHistorialRelacionadoService.</summary>
public enum TipoEntidadHistorial
{
    Unidad = 1,
    ZonaComun = 2,
    Equipo = 3
}

/// <summary>Modulo del que proviene una entrada del historial relacionado.</summary>
public enum OrigenHistorial
{
    Tarea = 1,
    Pqrsd = 2,
    Mantenimiento = 3
}
