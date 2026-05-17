using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Festivo colombiano nacional. Tabla global (no tenant - aplica a todo el pais).
/// Se siembra via migracion con festivos de la Ley 51/1983 (Ley Emiliani) +
/// festivos religiosos calculados desde Pascua para los anios 2024-2032.
///
/// Consumido por:
///  - 2.9 PQRSD: calculo de fecha de vencimiento en dias habiles (RN-Ley 1755/2015).
///  - 2.6 Presupuesto: vencimientos de cuotas (futuro).
///  - 2.7 Cartera: dias de mora habiles (futuro).
/// </summary>
public class FestivoColombiano : BaseEntity
{
    public DateOnly Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}
