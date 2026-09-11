using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Etiquetas legibles de TipoUnidad y su resolucion desde texto. Es la fuente de verdad compartida
/// por la tabla de unidades, la plantilla de carga masiva y el importador.
///
/// Existe porque la etiqueta que ve el usuario NO siempre es el nombre del enum (UtilCuarto ->
/// "Cuarto util", ZonaComun -> "Zona comun", ParqueaderoDeposito -> "Parqueadero + deposito"). La
/// plantilla ofrecia los nombres crudos del enum mientras la app mostraba las etiquetas, y el
/// importador solo parseaba nombres del enum: escribir "Cuarto util" en la plantilla terminaba
/// guardando Apartamento sin decir nada.
/// </summary>
public static class TiposUnidadCatalogo
{
    /// <summary>Etiqueta legible de un tipo del sistema.</summary>
    public static string Etiqueta(TipoUnidad t) => t switch
    {
        TipoUnidad.UtilCuarto => "Cuarto util",
        TipoUnidad.ZonaComun => "Zona comun",
        TipoUnidad.ParqueaderoDeposito => "Parqueadero + deposito",
        // Valor guardado que no corresponde a ningun tipo del enum (dato viejo o importado mal).
        // Se muestra asi, y NO como el primer tipo de la lista: si se disfrazara, cualquier edicion
        // de la fila guardaria un tipo que nadie eligio.
        _ when !Enum.IsDefined(t) => $"(sin definir: {(int)t})",
        _ => t.ToString()
    };

    /// <summary>Todos los tipos del sistema con su etiqueta, en el orden del enum.</summary>
    public static IEnumerable<(TipoUnidad Tipo, string Etiqueta)> Todos
        => Enum.GetValues<TipoUnidad>().Select(t => (t, Etiqueta(t)));

    /// <summary>
    /// Resuelve un texto escrito por el usuario a un tipo del sistema. Acepta tanto el nombre del
    /// enum ("UtilCuarto") como la etiqueta que muestra la app ("Cuarto util"), sin distinguir
    /// mayusculas. Devuelve false si el texto no corresponde a ningun tipo del sistema: quien llama
    /// decide (puede ser un tipo PROPIO de la copropiedad, o un error del archivo).
    /// </summary>
    public static bool TryResolver(string? texto, out TipoUnidad tipo)
    {
        tipo = default;
        var t = (texto ?? "").Trim();
        if (t.Length == 0) return false;
        if (Enum.TryParse(t, ignoreCase: true, out TipoUnidad porNombre) && Enum.IsDefined(porNombre))
        {
            tipo = porNombre;
            return true;
        }
        foreach (var v in Enum.GetValues<TipoUnidad>())
        {
            if (string.Equals(Etiqueta(v), t, StringComparison.OrdinalIgnoreCase))
            {
                tipo = v;
                return true;
            }
        }
        return false;
    }
}
