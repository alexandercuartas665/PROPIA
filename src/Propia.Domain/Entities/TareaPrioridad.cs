using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Prioridad configurable por TABLERO (Fase 2, modulo 2.10). Reemplaza al enum fijo
/// <see cref="Propia.Domain.Enums.PrioridadTarea"/> como fuente de la LISTA de prioridades: cada tablero
/// arranca con las 4 de fabrica (Urgente/Alta/Normal/Baja) y la copropiedad puede renombrar, recolorear,
/// reordenar y agregar/quitar. Espeja a <see cref="TareaEstado"/>. La tarea referencia una fila via
/// <see cref="Tarea.PrioridadId"/>. El enum se conserva solo como "selector base" en la creacion.
/// </summary>
public class TareaPrioridad : TenantEntity
{
    /// <summary>Tablero al que pertenece la prioridad. Null = prioridades legacy (sin tablero).</summary>
    public Guid? TableroId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Color hexadecimal de acento (texto del pill); el fondo se deriva como tinta clara. Opcional.</summary>
    public string? Color { get; set; }

    public int Orden { get; set; }

    /// <summary>True = una de las 4 de fabrica (Urgente/Alta/Normal/Baja). Se puede renombrar/recolorear
    /// pero no borrar (para que los modulos/jobs que crean por enum siempre encuentren su opcion base).</summary>
    public bool EsBase { get; set; }

    /// <summary>Enum base que representa esta opcion (solo cuando EsBase): resuelve enum -> fila al crear
    /// tareas desde otros modulos. Null en las prioridades propias que agregue la copropiedad.</summary>
    public int? BaseValor { get; set; }

    public bool Activo { get; set; } = true;
}
