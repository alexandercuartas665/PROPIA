using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Vista/widget del explorador de datos de "Mi Copropiedad" (modulo 2.3). Guarda SOLO la DEFINICION de la
/// vista (tipo de resultado, fuente/tabla, filtros, pivote, medida, campos, ancho, grafico, nombre), nunca
/// los datos: estos se recalculan en vivo desde los endpoints de cada modulo. Compartida POR COPROPIEDAD
/// (TenantEntity): todos los usuarios de la copropiedad ven el mismo tablero de vistas (pedido de Alex).
/// </summary>
public class VistaSistema : TenantEntity
{
    /// <summary>Nombre visible de la vista (puede ser el titulo automatico o uno escrito por el usuario).</summary>
    public string Nombre { get; set; } = "";

    /// <summary>Posicion en la grilla (0 = arriba). Se persiste al reordenar con subir/bajar.</summary>
    public int Orden { get; set; }

    /// <summary>
    /// JSON (jsonb) con la definicion completa que entiende el explorador del frontend:
    /// { kind, dsKey, title, filters:[{field,value}], rowDim, colDim, measure:{type,f,label,fmt}, fields:[], width, chart }.
    /// El backend lo trata como opaco (store dataset-agnostico); el frontend lo arma y lo lee.
    /// </summary>
    public string Definicion { get; set; } = "{}";
}
