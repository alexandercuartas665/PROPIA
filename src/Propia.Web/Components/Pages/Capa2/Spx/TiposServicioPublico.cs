using Propia.Domain.Enums;
using Propia.Web.Components.Shared;

namespace Propia.Web.Components.Pages.Capa2.Spx;

/// <summary>
/// Catalogo visual de los tipos de servicio publico (replica: TIPOS de js/mod-servicios-publicos.js): nombre,
/// icono, color del chip y unidad por defecto. Los colores viven aqui (no en .razor) como en PlantillasCatalogo.
/// </summary>
public static class TiposServicioPublico
{
    public sealed record Tipo(TipoServicioPublico Valor, string Clave, string Nombre, string Icono, string Color, string? Unidad);

    public static readonly IReadOnlyList<Tipo> Todos = new[]
    {
        new Tipo(TipoServicioPublico.EnergiaElectrica, "EnergiaElectrica", "Energía eléctrica", "fi-rr-bolt", "#FFE39B", "kWh"),
        new Tipo(TipoServicioPublico.Acueducto, "Acueducto", "Acueducto y alcantarillado", "fi-rr-raindrops", "#AAD4FF", "m3"),
        new Tipo(TipoServicioPublico.Gas, "Gas", "Gas natural", "fi-rr-flame", "#FFD8A8", "m3"),
        new Tipo(TipoServicioPublico.Aseo, "Aseo", "Aseo / recolección", "fi-rr-trash", "#AEE6C4", null),
        new Tipo(TipoServicioPublico.Internet, "Internet", "Internet", "fi-rr-wifi", "#C9B8FF", "GB"),
        new Tipo(TipoServicioPublico.TelefoniaFija, "TelefoniaFija", "Telefonía fija", "fi-rr-phone-call", "#CFE0EF", "min"),
    };

    public static Tipo De(TipoServicioPublico t) => Todos.FirstOrDefault(x => x.Valor == t) ?? Todos[0];

    /// <summary>Select tipo chip con el color del tipo (mismo formato que la replica y Plantillas).</summary>
    public static string EstiloChip(TipoServicioPublico t)
    {
        var c = De(t).Color;
        return $"background:{c}; border-color:{c}; color:{PaletaChips.TextoSobre(c)};";
    }

    public static readonly IReadOnlyList<(PeriodicidadServicio Valor, string Nombre)> Periodicidades = new[]
    {
        (PeriodicidadServicio.Mensual, "Mensual"), (PeriodicidadServicio.Bimestral, "Bimestral"),
        (PeriodicidadServicio.Trimestral, "Trimestral"), (PeriodicidadServicio.Semestral, "Semestral"),
        (PeriodicidadServicio.Anual, "Anual"),
    };
}
