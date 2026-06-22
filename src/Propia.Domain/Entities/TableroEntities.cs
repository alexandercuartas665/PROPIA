using Propia.Domain.Common;
using Propia.Domain.Enums;

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

/// <summary>
/// Campo personalizado de un tablero (se rellena por tarjeta en TareaCampoValor).
/// Tipado al estilo de los campos dinamicos de CUBOT.travels: cada campo define como se captura.
/// </summary>
public class TableroCampo : TenantEntity
{
    public Guid TableroId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }

    /// <summary>Tipo de captura/render del campo.</summary>
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;

    /// <summary>Opciones para tipo Seleccion, separadas por salto de linea.</summary>
    public string? Opciones { get; set; }

    /// <summary>Si es true, el campo aparece como filtro (chips por valor) en la vista del tablero.</summary>
    public bool MostrarEnFiltro { get; set; }

    /// <summary>Ancho en el modal: 1 = normal (media columna), 2 = ancho completo.</summary>
    public int Columna { get; set; } = 1;

    /// <summary>Ayuda/contexto del campo para el usuario (y para agentes IA a futuro).</summary>
    public string? Descripcion { get; set; }
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
