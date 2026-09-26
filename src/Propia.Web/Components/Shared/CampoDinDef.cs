using Propia.Domain.Enums;

namespace Propia.Web.Components.Shared;

/// <summary>
/// Forma NEUTRA de una definicion de campo dinamico.
///
/// Los catalogos de Personas / Vehiculos / Mascotas / Terceros tienen cada uno su propio DTO en
/// Propia.Application, pero los cuatro son identicos en forma (Id, Label, Orden, Tipo, Opciones).
/// La UI los consume con este record para no repetir la misma grilla cuatro veces: los nombres de
/// propiedad coinciden con los de los DTOs, asi que la deserializacion JSON funciona igual para
/// los cuatro endpoints ({prefijo}-campos).
/// </summary>
public record CampoDinDef(Guid Id, string Label, int Orden, TipoCampoTablero Tipo, string? Opciones, string? Descripcion = null)
{
    /// <summary>Opciones VISIBLES de una lista de seleccion (texto). Tolera el formato legado
    /// (una por linea) y el nuevo (JSON con color/oculta). Vacio si el tipo no es lista.</summary>
    public string[] OpcionesArray() => OpcionCampo.VisiblesK(Opciones);

    /// <summary>Opciones completas (texto + oculta + color) para el gestor y el render de chips.</summary>
    public List<OpcionCampo> OpcionesDetalle() => OpcionCampo.Parse(Opciones);
}
