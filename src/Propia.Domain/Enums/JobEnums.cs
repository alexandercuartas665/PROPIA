namespace Propia.Domain.Enums;

/// <summary>Estado de la ejecucion de un job nocturno. Modulo de jobs (transversal).</summary>
public enum EstadoEjecucionJob
{
    Ejecutando = 1,
    Exitoso = 2,
    Fallido = 3,
    Saltado = 4   // se intento ejecutar pero ya estaba corriendo o no era hora
}
