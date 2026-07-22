using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>Factura de compra asociada a una zona comun (ficha de la zona, prototipo).</summary>
public class ZonaFactura : TenantEntity
{
    public Guid ZonaComunId { get; set; }
    public string Concepto { get; set; } = null!;
    public decimal? Valor { get; set; }
    public DateOnly? Fecha { get; set; }
}

/// <summary>Documento adjunto de una zona comun (ficha de la zona). Binario en IBlobStorage.</summary>
public class ZonaDocumento : TenantEntity
{
    public Guid ZonaComunId { get; set; }
    public string Nombre { get; set; } = null!;
    public string Url { get; set; } = null!;
}

/// <summary>Campo personalizado (EAV) de la ficha de una zona comun.</summary>
public class ZonaCampoPersonalizado : TenantEntity
{
    public Guid ZonaComunId { get; set; }
    public string Label { get; set; } = null!;
    public string? Valor { get; set; }
}

/// <summary>
/// Novedad publicada en el muro de una entidad. Nacio atada a las zonas comunes; ahora el
/// muro es generico (EntidadTipo + EntidadId) para colgarlo tambien de equipos y de lo que
/// venga despues sin duplicar tablas.
/// </summary>
public class Novedad : TenantEntity
{
    public TipoEntidadNovedad EntidadTipo { get; set; }
    public Guid EntidadId { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Texto { get; set; }
    public string? ImagenUrl { get; set; }
    public string AutorNombre { get; set; } = "Administracion";
    public Guid? AutorPersonaId { get; set; }
    public int LikesCount { get; set; }
}

/// <summary>Comentario sobre una novedad del muro.</summary>
public class NovedadComentario : TenantEntity
{
    public Guid NovedadId { get; set; }
    public string AutorNombre { get; set; } = "Residente";
    public Guid? AutorPersonaId { get; set; }
    public string Texto { get; set; } = null!;
}

/// <summary>Like de una persona sobre una novedad (evita doble like).</summary>
public class NovedadLike : TenantEntity
{
    public Guid NovedadId { get; set; }
    public Guid PersonaId { get; set; }
}
