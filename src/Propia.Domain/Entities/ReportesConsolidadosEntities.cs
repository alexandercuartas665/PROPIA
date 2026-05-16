using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Reporte guardado de la organizacion. Spec 1.4 seccion 5 + RN-07.
/// Pertenece a la organizacion (no al usuario). Sobrevive a desactivacion de creador.
/// </summary>
public class OrgReporte : BaseEntity
{
    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public CategoriaReporteConsolidado Categoria { get; set; }

    /// <summary>True = plantilla del sistema (no editable por usuarios).</summary>
    public bool EsPlantillaBase { get; set; }

    /// <summary>RN-04 + RN-06: calculado automaticamente al guardar segun indicadores usados.</summary>
    public bool TieneDatosNominativos { get; set; }

    /// <summary>JSON con bloques, filtros y layout. En MVP simplificado a estructura basica.</summary>
    public string ConfiguracionJson { get; set; } = "{}";

    public Guid CreadoPorUsuarioId { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Historial de generaciones de reportes. Spec 1.4 seccion 10 + RN-11.
/// Se conserva indefinidamente. Cada generacion guarda su resultado.
/// </summary>
public class OrgReporteGeneracion : BaseEntity
{
    public Guid ReporteId { get; set; }
    public OrgReporte? Reporte { get; set; }

    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public OrigenGeneracionConsolidada Origen { get; set; } = OrigenGeneracionConsolidada.Manual;

    /// <summary>NULL si origen=Programado.</summary>
    public Guid? GeneradoPorUsuarioId { get; set; }

    public DateOnly PeriodoDesde { get; set; }
    public DateOnly PeriodoHasta { get; set; }

    public EstadoGeneracionConsolidada Estado { get; set; } = EstadoGeneracionConsolidada.Generando;

    /// <summary>Resultado serializado en JSON (datos del reporte + metadata).</summary>
    public string? ResultadoJson { get; set; }

    public string? UrlPdf { get; set; }
    public string? UrlExcel { get; set; }
    public DateTimeOffset? UrlExpiracion { get; set; }

    /// <summary>RN-10 reintento automatico una vez. Si falla 2da, notifica al Director.</summary>
    public int Intentos { get; set; } = 1;
    public string? ErrorDetalle { get; set; }

    public DateTimeOffset? GeneradoAt { get; set; }
}
