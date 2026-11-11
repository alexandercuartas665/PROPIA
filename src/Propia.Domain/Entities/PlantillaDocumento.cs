using Propia.Domain.Common;

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
}
