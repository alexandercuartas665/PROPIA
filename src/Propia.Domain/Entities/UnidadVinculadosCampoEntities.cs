using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

// Campos dinamicos tipados (catalogo por copropiedad + valor por registro) de las entidades
// vinculadas a una unidad: Personas, Vehiculos (placas), Mascotas y Terceros (empleadas).
// Mismo patron que EquipoCampoDefinicion/EquipoCampoValor y ZonaCampoDefinicion/ZonaCampoValor.
// Es aditivo: la entidad vieja UnidadPersonaCampo (Label/Valor suelto por fila) se conserva.

/// <summary>
/// Definicion de un campo dinamico de las personas de una unidad, a NIVEL de copropiedad
/// (catalogo compartido y tipado). Aplica a TODAS las personas y se muestra como columna.
/// </summary>
public class PersonaCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
}

/// <summary>Valor de un campo dinamico (catalogo) para una persona de unidad concreta.</summary>
public class PersonaCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public PersonaCampoDefinicion? Definicion { get; set; }
    public Guid UnidadPersonaId { get; set; }
    public string? Valor { get; set; }
}

/// <summary>
/// Definicion de un campo dinamico de los vehiculos (placas) de una unidad, a NIVEL de
/// copropiedad (catalogo compartido y tipado).
/// </summary>
public class VehiculoCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
}

/// <summary>Valor de un campo dinamico (catalogo) para una placa de unidad concreta.</summary>
public class VehiculoCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public VehiculoCampoDefinicion? Definicion { get; set; }
    public Guid UnidadPlacaId { get; set; }
    public string? Valor { get; set; }
}

/// <summary>
/// Definicion de un campo dinamico de las mascotas de una unidad, a NIVEL de copropiedad
/// (catalogo compartido y tipado).
/// </summary>
public class MascotaCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
}

/// <summary>Valor de un campo dinamico (catalogo) para una mascota de unidad concreta.</summary>
public class MascotaCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public MascotaCampoDefinicion? Definicion { get; set; }
    public Guid UnidadMascotaId { get; set; }
    public string? Valor { get; set; }
}

/// <summary>
/// Definicion de un campo dinamico de los terceros (empleadas) de una unidad, a NIVEL de
/// copropiedad (catalogo compartido y tipado).
/// </summary>
public class TerceroCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
}

/// <summary>Valor de un campo dinamico (catalogo) para un tercero (empleada) de unidad concreto.</summary>
public class TerceroCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public TerceroCampoDefinicion? Definicion { get; set; }
    public Guid UnidadEmpleadaId { get; set; }
    public string? Valor { get; set; }
}
