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

/// <summary>
/// Como se calcula la proxima ejecucion de una programacion.
/// Periodicidad = el enum de arriba (dia completo, sin hora).
/// Cron = expresion cron estandar de 5 campos, con hora y zona horaria.
/// El default es Periodicidad para que las programaciones que ya existen sigan igual.
/// </summary>
public enum TipoProgramacion
{
    Periodicidad = 1,
    Cron = 2
}
