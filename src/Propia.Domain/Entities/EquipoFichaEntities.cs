using Propia.Domain.Common;

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
