using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Plantilla de documento del catalogo central (Configuracion > Plantillas de documentos). El cuerpo HTML
/// lleva datos {grupo.campo} que cada modulo reemplaza por sus valores reales al usarla. La Categoria
/// (clave del catalogo PlantillasCatalogo: pqrsd, cartera, asambleas...) define que datos puede insertar y
/// en que modulo se ofrece. Reemplaza a la antigua PqrsdPlantillaRespuesta (migrada a categoria "pqrsd").
/// </summary>
public class PlantillaDocumento : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = "pqrsd";
    public string CuerpoHtml { get; set; } = string.Empty;
    public int Orden { get; set; }

    /// <summary>Encabezado que usa: el predeterminado de la copropiedad, ninguno o uno fijo (EncabezadoId).</summary>
    public ModoMembrete EncabezadoModo { get; set; } = ModoMembrete.Predeterminado;
    public Guid? EncabezadoId { get; set; }
    /// <summary>Pie de pagina que usa: el predeterminado, ninguno o uno fijo (PieId).</summary>
    public ModoMembrete PieModo { get; set; } = ModoMembrete.Predeterminado;
    public Guid? PieId { get; set; }
}

/// <summary>
/// Encabezado o pie de pagina reutilizable de los documentos de la copropiedad (institucional, Navidad...).
/// Html con datos {copropiedad.*} que se resuelven al generar el documento. Uno por tipo es el Predeterminado
/// (indice unico filtrado por tenant + tipo). La copropiedad nace con "Institucional" (encabezado y pie).
/// </summary>
public class MembreteDocumento : TenantEntity
{
    public TipoMembrete Tipo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    public bool Predeterminado { get; set; }
}

/// <summary>
/// Definicion de un campo propio de las plantillas a NIVEL de copropiedad (columna "+" de la vista tabla).
/// Aplica a TODAS las plantillas del tenant. Espejo de ZonaCampoDefinicion.
/// </summary>
public class PlantillaCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
    /// <summary>Nota/ayuda del campo, editable desde el menu de columna (opcional).</summary>
    public string? Descripcion { get; set; }
}

/// <summary>Valor de un campo propio para una plantilla concreta (EAV). Se borra con la plantilla o la definicion.</summary>
public class PlantillaCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public PlantillaCampoDefinicion? Definicion { get; set; }
    public Guid PlantillaDocumentoId { get; set; }
    public PlantillaDocumento? Plantilla { get; set; }
    public string? Valor { get; set; }
}
