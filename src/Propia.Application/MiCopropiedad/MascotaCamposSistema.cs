using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) de una mascota: los que la ficha trae de fabrica. Misma forma canonica
/// que <see cref="VehiculoCamposSistema"/>/UnidadCamposSistema. Los personalizados viven en
/// mascota_campos_definiciones (endpoints mascotas-campos) y NO entran aqui. Ademas de alimentar el gestor
/// de campos, es la fuente UNICA de la hoja MASCOTAS de la plantilla (homogenizacion Fase 1): por eso lleva
/// Encabezado/Ayuda/SiempreEnPlantilla.
/// </summary>
/// <param name="OpcionesEditables">Solo Tipo=Seleccion: 'tipo' es el enum <see cref="TipoMascota"/>
/// (valores estrictos), asi que es false (lista de solo lectura); tipos propios de mascota serian Fase 2.</param>
/// <param name="Encabezado">Encabezado CANONICO de la columna en la plantilla. null = deriva del Label en MAYUSCULAS.</param>
/// <param name="Ayuda">Fila de ayuda de la plantilla.</param>
/// <param name="SiempreEnPlantilla">Columna estructural: la plantilla la emite aunque este oculta (NOMBRE identifica la mascota).</param>
public sealed record MascotaCampoSistema(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisiblePorDefecto,
    bool Fija = false,
    bool OpcionesEditables = false,
    string? Encabezado = null,
    string Ayuda = "",
    bool SiempreEnPlantilla = false)
{
    /// <summary>Encabezado canonico de plantilla; por defecto el Label en MAYUSCULAS.</summary>
    public string EncabezadoPlantilla => string.IsNullOrWhiteSpace(Encabezado) ? Label.ToUpperInvariant() : Encabezado;
}

/// <summary>
/// Catalogo UNICO de los campos de sistema de una mascota. El ORDEN es el de las columnas (tabla del modulo
/// y hoja de plantilla): nombre, tipo, raza. Lo consume el gestor de campos compartido via 'Catalogo' y la
/// generacion de la plantilla.
/// </summary>
public static class MascotaCamposSistema
{
    public static readonly IReadOnlyList<MascotaCampoSistema> Todos = new[]
    {
        // Nombre: identifica a la mascota, no se puede ocultar y siempre va en la plantilla.
        new MascotaCampoSistema("nombre", "Nombre", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true,
            Encabezado: "NOMBRE", Ayuda: "", SiempreEnPlantilla: true),
        // Tipo: enum TipoMascota (Perro/Gato/Ave/Otro), lista de solo lectura.
        new MascotaCampoSistema("tipo", "Tipo", TipoCampoTablero.Seleccion, VisiblePorDefecto: true,
            Encabezado: "TIPO MASCOTA", Ayuda: "Elige de la lista"),
        new MascotaCampoSistema("raza", "Raza", TipoCampoTablero.Texto, VisiblePorDefecto: true,
            Encabezado: "RAZA"),
    };

    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static MascotaCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    /// <summary>Encabezado canonico de plantilla -> campo (lo usa el importador para resolver la columna).</summary>
    public static MascotaCampoSistema? PorEncabezado(string encabezado)
        => Todos.FirstOrDefault(c => string.Equals(c.EncabezadoPlantilla, (encabezado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
