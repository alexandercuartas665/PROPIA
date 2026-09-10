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
public record CampoDinDef(Guid Id, string Label, int Orden, TipoCampoTablero Tipo, string? Opciones)
{
    /// <summary>Opciones de una lista de seleccion (una por linea). Vacio si el tipo no es lista.</summary>
    public string[] OpcionesArray()
        => string.IsNullOrWhiteSpace(Opciones)
            ? Array.Empty<string>()
            : Opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
