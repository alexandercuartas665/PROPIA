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

    /// <summary>Cuota de administracion mensual fijada manualmente para la unidad (override).
    /// Si es null, la cuota se deriva del coeficiente en 2.6. Prototipo v3, ficha de inmueble.</summary>
    public decimal? CuotaMensual { get; set; }
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

/// <summary>
/// Definicion de un campo personalizado de unidades a NIVEL de copropiedad (catalogo compartido).
/// Aplica a TODAS las unidades: al crear "Mascotas" cada ficha de unidad pide ese dato.
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadCampoDefinicion : TenantEntity
{
    /// <summary>Nombre del campo (ej. Mascotas, N de medidor, Color de fachada).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Orden de presentacion en la ficha.</summary>
    public int Orden { get; set; }
}

/// <summary>
/// Valor de un campo personalizado para una unidad concreta (EAV con definicion compartida).
/// La definicion vive a nivel de copropiedad; el valor es por unidad.
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public UnidadCampoDefinicion? Definicion { get; set; }

    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    public string? Valor { get; set; }
}

/// <summary>
/// Documento/anexo adjunto a una unidad (escritura, plano, contrato, etc). El archivo se sube
/// a blob storage y aqui se guarda el nombre + URL. Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadDocumento : TenantEntity
{
    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>Tamano del archivo en bytes (para mostrar "X KB" en la ficha).</summary>
    public long Tamano { get; set; }
}

/// <summary>
/// Placa de vehiculo habilitada para el ingreso de una unidad (control de acceso de Porteria 2.12).
/// Prototipo v3, ficha de inmueble. Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadPlaca : TenantEntity
{
    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    /// <summary>Placa del vehiculo (ej. ABC123). Se almacena en mayusculas.</summary>
    public string Placa { get; set; } = string.Empty;

    /// <summary>Reusa el enum de Porteria 2.12 (Automovil/Moto/...). En la ficha v3 se muestra Carro/Moto.</summary>
    public TipoVehiculo TipoVehiculo { get; set; } = TipoVehiculo.Automovil;
}

/// <summary>
/// Arriendo o cobro mensual adicional de una unidad (ej. arriendo de un parqueadero, cuota extra).
/// Opcionalmente referencia a un inmueble de la unidad. Prototipo v3, ficha de inmueble.
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadArriendo : TenantEntity
{
    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    /// <summary>Concepto del cobro (ej. Arriendo mensual, Cuota parqueadero).</summary>
    public string Concepto { get; set; } = string.Empty;

    /// <summary>Valor mensual del arriendo/cobro.</summary>
    public decimal ValorMensual { get; set; }

    /// <summary>Referencia opcional a que se arrienda (ej. "Parqueadero P-1011"). Texto libre.</summary>
    public string? Referencia { get; set; }
}

/// <summary>
/// Mascota registrada en una unidad. Prototipo v3, ficha de inmueble.
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadMascota : TenantEntity
{
    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public TipoMascota Tipo { get; set; } = TipoMascota.Perro;
    public string? Raza { get; set; }
}

/// <summary>
/// Empleada(s) de servicio de una unidad (datos de contacto + horario). Prototipo v3, ficha de inmueble.
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadEmpleada : TenantEntity
{
    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public string? Celular { get; set; }

    /// <summary>Dias / horario de trabajo (texto libre, ej. "Lun-Vie 8am-5pm").</summary>
    public string? Horario { get; set; }
}

/// <summary>
/// Registro del historico de titularidad (propietarios) de una unidad: linea de tiempo
/// de quien fue propietario y en que periodo. Prototipo v3, tab "Historico de propietarios".
/// El registro con Hasta == null es el ACTUAL. Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadTitularidad : TenantEntity
{
    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    /// <summary>Nombre del titular (texto libre: titulares historicos pueden no estar en el sistema).</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Rol del titular en el periodo (ej. Propietario, Arrendatario). Texto libre.</summary>
    public string? Rol { get; set; }

    /// <summary>Inicio del periodo de titularidad.</summary>
    public DateOnly Desde { get; set; }

    /// <summary>Fin del periodo. null = titular actual.</summary>
    public DateOnly? Hasta { get; set; }
}

/// <summary>
/// Campo dinamico (EAV) de una persona vinculada a una unidad: cada contacto puede tener sus
/// propios pares label/valor. Prototipo v3, "+ Agregar campo dinamico" por contacto.
/// Distinto del EAV por-unidad (UnidadCampoDefinicion/Valor). Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadPersonaCampo : TenantEntity
{
    public Guid UnidadPersonaId { get; set; }
    public UnidadPersona? UnidadPersona { get; set; }

    public string Label { get; set; } = string.Empty;
    public string? Valor { get; set; }
}
