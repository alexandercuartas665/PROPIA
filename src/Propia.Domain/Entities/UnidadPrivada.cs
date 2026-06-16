using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Unidad privada de la copropiedad (apartamento, local, casa, oficina, parqueadero, etc).
/// El coeficiente de propiedad es porcentual respecto al total de la PH - se usa para
/// calcular cuotas en 2.6. La suma de coeficientes debe ser 100% (validado a nivel de UI).
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadPrivada : TenantEntity
{
    /// <summary>Numero o codigo identificador (101, A-203, L-15, etc).</summary>
    public string Numero { get; set; } = string.Empty;
    public TipoUnidad Tipo { get; set; } = TipoUnidad.Apartamento;

    public Guid? TorreId { get; set; }
    public Torre? Torre { get; set; }

    public int? Piso { get; set; }

    /// <summary>Coeficiente de propiedad en porcentaje (Ley 675 art. 26). Suma total 100.0%.</summary>
    public decimal CoeficientePropiedad { get; set; }

    /// <summary>Area privada en metros cuadrados.</summary>
    public decimal? AreaM2 { get; set; }

    public int? Habitaciones { get; set; }
    public int? Banos { get; set; }
    public int? Parqueaderos { get; set; }

    /// <summary>Estado: habitada / desocupada / arrendada. Texto libre por ahora.</summary>
    public string? Estado { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>Matricula inmobiliaria (folio de registro) - spec 2.3 seccion 2.</summary>
    public string? MatriculaInmobiliaria { get; set; }

    /// <summary>Si la unidad paga cuota de administracion (un deposito/util puede no pagar).</summary>
    public bool PagaAdministracion { get; set; } = true;
}

/// <summary>
/// Vincula una Persona (propietario/residente/familiar/etc) con una Unidad. Una unidad
/// puede tener N personas; una persona puede tener N unidades (multi-propietario).
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadPersona : TenantEntity
{
    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    public Guid PersonaId { get; set; }

    /// <summary>Rol del miembro respecto a la unidad.</summary>
    public RolUnidadPersona Rol { get; set; } = RolUnidadPersona.Propietario;

    /// <summary>Habita la unidad. Util para distinguir propietario-no-residente.</summary>
    public bool Habita { get; set; } = true;

    /// <summary>Parentesco o nota libre (hijo, esposa, sobrina, etc). Opcional, util para Familiar.</summary>
    public string? Parentesco { get; set; }
}

/// <summary>
/// Vinculo entre unidades: una unidad PRINCIPAL (apartamento/casa) con sus unidades
/// ASOCIADAS (parqueadero, deposito). Spec 2.3 tabla unidad_vinculo. RN-09: no circular.
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadVinculo : TenantEntity
{
    public Guid UnidadPrincipalId { get; set; }
    public UnidadPrivada? UnidadPrincipal { get; set; }

    public Guid UnidadAsociadaId { get; set; }
    public UnidadPrivada? UnidadAsociada { get; set; }

    /// <summary>Si el coeficiente de la asociada se factura junto al de la principal (cuenta consolidada).</summary>
    public bool IncluyeEnFacturacion { get; set; } = true;
}
