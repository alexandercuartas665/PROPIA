using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Plantilla inteligente de informe de gestion: define el esqueleto (titulo + secciones), y cada
/// seccion trae su propio prompt para que el sistema genere el contenido con los agentes de IA
/// existentes. Configurable por copropiedad (modulo Informes de gestion, Capa 2).
/// </summary>
public class InformePlantilla : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Secciones que componen el informe, en orden.</summary>
    public ICollection<InformePlantillaSeccion> Secciones { get; set; } = new List<InformePlantillaSeccion>();
}

/// <summary>Una seccion configurable de una plantilla: titulo, orden y el prompt de generacion IA.</summary>
public class InformePlantillaSeccion : TenantEntity
{
    public Guid PlantillaId { get; set; }
    public InformePlantilla? Plantilla { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public int Orden { get; set; }

    /// <summary>Instruccion que se le entrega al agente de IA para redactar esta seccion.</summary>
    public string? Prompt { get; set; }
}

/// <summary>
/// Un informe de gestion concreto (instancia), creado a partir de una plantilla para un periodo.
/// Sus secciones guardan el contenido generado por IA y editado en pantalla.
/// </summary>
public class Informe : TenantEntity
{
    /// <summary>Plantilla de la que se instancio (snapshot: la plantilla puede cambiar despues).</summary>
    public Guid? PlantillaId { get; set; }

    public string Titulo { get; set; } = string.Empty;
    /// <summary>Periodo cubierto, texto libre: "Agosto 2026", "III Trimestre 2026", etc.</summary>
    public string? Periodo { get; set; }

    public EstadoInforme Estado { get; set; } = EstadoInforme.Borrador;

    /// <summary>Momento de la ultima generacion completa con IA.</summary>
    public DateTimeOffset? GeneradoEn { get; set; }

    public ICollection<InformeSeccion> Secciones { get; set; } = new List<InformeSeccion>();
}

/// <summary>Seccion de un informe concreto: hereda el prompt de la plantilla y guarda el contenido.</summary>
public class InformeSeccion : TenantEntity
{
    public Guid InformeId { get; set; }
    public Informe? Informe { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public int Orden { get; set; }

    /// <summary>Prompt usado para generar esta seccion (copiado de la plantilla, editable).</summary>
    public string? Prompt { get; set; }

    /// <summary>Contenido generado por IA y/o editado por el usuario en pantalla.</summary>
    public string? Contenido { get; set; }
}
