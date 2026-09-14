using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) de una mascota: los que la ficha trae de fabrica. Misma forma canonica
/// que <see cref="VehiculoCamposSistema"/>/UnidadCamposSistema. Los personalizados viven en
/// mascota_campos_definiciones (endpoints mascotas-campos) y NO entran aqui.
/// </summary>
/// <param name="OpcionesEditables">Solo Tipo=Seleccion: 'tipo' es el enum <see cref="TipoMascota"/>
/// (valores estrictos), asi que es false (lista de solo lectura); tipos propios de mascota serian Fase 2.</param>
public sealed record MascotaCampoSistema(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisiblePorDefecto,
    bool Fija = false,
    bool OpcionesEditables = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema de una mascota. El ORDEN es el de las columnas de la tabla
/// (ver Mascotas.razor): nombre, tipo, raza. Lo consume el gestor de campos compartido via 'Catalogo'.
/// </summary>
public static class MascotaCamposSistema
{
    public static readonly IReadOnlyList<MascotaCampoSistema> Todos = new[]
    {
        // Nombre: identifica a la mascota, no se puede ocultar.
        new MascotaCampoSistema("nombre", "Nombre", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true),
        // Tipo: enum TipoMascota (Perro/Gato/Ave/Otro), lista de solo lectura.
        new MascotaCampoSistema("tipo", "Tipo", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new MascotaCampoSistema("raza", "Raza", TipoCampoTablero.Texto, VisiblePorDefecto: true),
    };

    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static MascotaCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
