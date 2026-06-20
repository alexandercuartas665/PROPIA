using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Tablero de trabajo (Kanban/Lista) del modulo 2.10 Tareas. Cada tablero tiene sus propios
/// estados (columnas), campos personalizados y usuarios habilitados. Fiel al prototipo.
/// </summary>
public class Tablero : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Color { get; set; } = "#6D4FE3";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<TableroUsuario> Usuarios { get; set; } = new List<TableroUsuario>();
    public ICollection<TableroCampo> Campos { get; set; } = new List<TableroCampo>();
}

/// <summary>Persona habilitada para trabajar en un tablero.</summary>
public class TableroUsuario : TenantEntity
{
    public Guid TableroId { get; set; }
    public Guid PersonaId { get; set; }
}

/// <summary>Campo personalizado de un tablero (se rellena por tarjeta en TareaCampoValor).</summary>
public class TableroCampo : TenantEntity
{
    public Guid TableroId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
}

/// <summary>Valor de un campo personalizado del tablero para una tarea concreta.</summary>
public class TareaCampoValor : TenantEntity
{
    public Guid TareaId { get; set; }
    public Guid TableroCampoId { get; set; }
    public string? Valor { get; set; }
}

/// <summary>Archivo adjunto de una tarea. Binario en IBlobStorage.</summary>
public class TareaAdjunto : TenantEntity
{
    public Guid TareaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Subtarea ligera tipo checklist de una tarjeta (campo "Tareas relacionadas" del modal).
/// Es distinta de las tareas hijas reales (Tarea.PadreId): no tiene estado ni ficha propia.
/// </summary>
public class TareaSubtarea : TenantEntity
{
    public Guid TareaId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public bool Hecho { get; set; }
    public int Orden { get; set; }
}
