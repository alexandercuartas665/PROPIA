using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

// Campos dinamicos (EAV) a NIVEL de copropiedad para Mantenimiento. Espejo de EquipoCampoDefinicion/Valor:
// la definicion aplica a TODAS las filas (programaciones / intervenciones) y se muestra como columna en la
// tabla. Extiende la vista sin migraciones por tenant. Tipado con TipoCampoTablero (enum compartido).

/// <summary>Definicion de un campo dinamico del CRONOGRAMA (programaciones) a nivel de copropiedad.</summary>
public class ProgramacionCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
    /// <summary>Nota/ayuda del campo, editable desde el menu de columna (opcional).</summary>
    public string? Descripcion { get; set; }
}

/// <summary>Valor de un campo dinamico (catalogo) para una programacion concreta (EAV).</summary>
public class ProgramacionCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public ProgramacionCampoDefinicion? Definicion { get; set; }
    public Guid ProgramacionTareaId { get; set; }
    public string? Valor { get; set; }
}

/// <summary>Definicion de un campo dinamico de INTERVENCIONES a nivel de copropiedad.</summary>
public class IntervencionCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
    /// <summary>Nota/ayuda del campo, editable desde el menu de columna (opcional).</summary>
    public string? Descripcion { get; set; }
}

/// <summary>Valor de un campo dinamico (catalogo) para una intervencion concreta (EAV).</summary>
public class IntervencionCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public IntervencionCampoDefinicion? Definicion { get; set; }
    public Guid MantenimientoIntervencionId { get; set; }
    public string? Valor { get; set; }
}
