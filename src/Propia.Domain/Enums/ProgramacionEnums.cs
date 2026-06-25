namespace Propia.Domain.Enums;

/// <summary>
/// Periodicidad con la que un programador de tareas dispara la creacion de tareas.
/// "Unica" crea una sola tarea y se desactiva.
/// </summary>
public enum PeriodicidadProgramacion
{
    Unica = 1,
    Diaria = 2,
    Semanal = 3,
    Quincenal = 4,
    Mensual = 5,
    Bimestral = 6,
    Trimestral = 7,
    Semestral = 8,
    Anual = 9
}
