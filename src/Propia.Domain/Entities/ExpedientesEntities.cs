using Propia.Domain.Common;

namespace Propia.Domain.Entities;

// ===========================================================================
// Modulo 2.15 Documentos - Vista "Expedientes" (Tabla de Retencion Documental).
// Fiel al prototipo PROPIA.dc.html: series -> subseries -> tipologias + metadatos,
// y expedientes (instancias) con su checklist de documentos esperados.
// Independiente del repositorio "Archivo central" (DocumentoCategoria/Carpeta/Documento).
// ===========================================================================

/// <summary>Serie documental de la TRD (ej. "Actas y gobierno"). Configurable por copropiedad.</summary>
public class SerieDocumental : TenantEntity
{
    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }
    public ICollection<SubserieDocumental> Subseries { get; set; } = new List<SubserieDocumental>();
}

/// <summary>Subserie de una serie (ej. "Actas de Asamblea"). Define tipologias esperadas + metadatos.</summary>
public class SubserieDocumental : TenantEntity
{
    public Guid SerieId { get; set; }
    public SerieDocumental? Serie { get; set; }
    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }
    public ICollection<SubserieTipologia> Tipologias { get; set; } = new List<SubserieTipologia>();
    public ICollection<SubserieCampo> Campos { get; set; } = new List<SubserieCampo>();
}

/// <summary>Tipologia esperada en la subserie (un "documento esperado", ej. "Convocatoria").</summary>
public class SubserieTipologia : TenantEntity
{
    public Guid SubserieId { get; set; }
    public SubserieDocumental? Subserie { get; set; }
    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }
}

/// <summary>Metadato (campo) de la subserie que se pedira al cargar cada documento.</summary>
public class SubserieCampo : TenantEntity
{
    public Guid SubserieId { get; set; }
    public SubserieDocumental? Subserie { get; set; }
    public string Clave { get; set; } = null!;
    public string Label { get; set; } = null!;
    public int Orden { get; set; }
}

/// <summary>
/// Expediente (instancia) creado a partir de una subserie. Copia sus tipologias y campos.
/// El nombre de serie/subserie se guarda como snapshot para que el expediente no cambie si se edita la TRD.
/// </summary>
public class Expediente : TenantEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Serie { get; set; } = null!;
    public string Subserie { get; set; } = null!;
    public ICollection<ExpedienteTipologia> Tipologias { get; set; } = new List<ExpedienteTipologia>();
    public ICollection<ExpedienteCampo> Campos { get; set; } = new List<ExpedienteCampo>();
}

/// <summary>Tipologia dentro de un expediente: una casilla del checklist. Puede tener archivo cargado + metadatos.</summary>
public class ExpedienteTipologia : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Obligatoria { get; set; }
    public int Orden { get; set; }

    /// <summary>Archivo cargado (blob via IBlobStorage). Null = sin cargar.</summary>
    public string? ArchivoUrl { get; set; }
    public string? ArchivoNombre { get; set; }
    public string? ArchivoMime { get; set; }
    public long ArchivoTamano { get; set; }

    /// <summary>Metadatos capturados al cargar: JSON dict claveCampo -> valor.</summary>
    public string? MetaJson { get; set; }
}

/// <summary>Campo de metadato del expediente (copiado de la subserie, editable por expediente).</summary>
public class ExpedienteCampo : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }
    public string Clave { get; set; } = null!;
    public string Label { get; set; } = null!;
    public int Orden { get; set; }
}
