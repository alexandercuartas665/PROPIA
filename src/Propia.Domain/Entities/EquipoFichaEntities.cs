using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>Foto de un equipo/activo (galeria de la ficha tecnica). Binario en IBlobStorage.</summary>
public class EquipoFoto : TenantEntity
{
    public Guid EquipoActivoId { get; set; }
    public string Url { get; set; } = null!;
}

/// <summary>
/// Mejora capitalizada sobre un activo (ej. cambio de motor). Suma al valor en libros y
/// se refleja en la depreciacion. Opcionalmente referencia una factura/documento.
/// </summary>
public class EquipoMejora : TenantEntity
{
    public Guid EquipoActivoId { get; set; }
    public string Descripcion { get; set; } = null!;
    public decimal Valor { get; set; }
    public DateOnly Fecha { get; set; }
    public string? DocumentoUrl { get; set; }
}

/// <summary>Vinculo entre dos equipos/activos (ej. bomba <-> tanque). Bidireccional logico.</summary>
public class EquipoVinculo : TenantEntity
{
    public Guid EquipoActivoId { get; set; }
    public Guid EquipoVinculadoId { get; set; }
}

/// <summary>Vinculo de un equipo/activo a un contrato de servicio (seccion 5 Servicios).</summary>
public class EquipoContratoVinculo : TenantEntity
{
    public Guid EquipoActivoId { get; set; }
    public Guid ContratoServicioId { get; set; }
}

/// <summary>
/// Campo personalizado (EAV) de la ficha tecnica de un equipo/activo. Extiende la ficha
/// sin migraciones: ej. "N de medidor", "Marca de cerradura", "Capacidad (BTU)".
/// </summary>
public class EquipoCampoPersonalizado : TenantEntity
{
    public Guid EquipoActivoId { get; set; }
    public string Label { get; set; } = null!;
    public string? Valor { get; set; }
}

/// <summary>
/// Definicion de un campo dinamico de equipos a NIVEL de copropiedad (catalogo compartido, tipado).
/// Aplica a TODOS los equipos y se muestra como columna en la tabla. Analogo a UnidadCampoDefinicion.
/// </summary>
public class EquipoCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
}

/// <summary>Valor de un campo dinamico (catalogo) para un equipo concreto (EAV con definicion compartida).</summary>
public class EquipoCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public EquipoCampoDefinicion? Definicion { get; set; }
    public Guid EquipoActivoId { get; set; }
    public string? Valor { get; set; }
}
