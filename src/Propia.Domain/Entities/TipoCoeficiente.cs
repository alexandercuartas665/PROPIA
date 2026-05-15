using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Tipo de coeficiente de propiedad horizontal. Spec 2.3 v1.0 - RN-02:
/// "La suma de valores de cada tipo de coeficiente debe ser exactamente 1.0.
///  La validacion aplica por tipo de forma independiente".
///
/// Tipos tipicos en PH colombiana:
///  - Propiedad (coeficiente principal segun Ley 675)
///  - Administracion (puede diferir si hay locales que no pagan)
///  - Asambleas (peso de voto)
///  - Reserva (fondo de imprevistos)
///
/// Cada tenant tiene su propia lista. Se crea uno "Propiedad" por defecto
/// cuando se completa el wizard de onboarding (paso 2.1).
/// </summary>
public class TipoCoeficiente : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public bool EsPrincipal { get; set; }  // El default "Propiedad" lo marca true
}
