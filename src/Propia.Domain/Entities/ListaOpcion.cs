using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Copia POR TENANT de una opcion de una lista de seleccion del sistema (Ola 4, modelo PQRSD generalizado).
/// Cada copropiedad materializa su propia copia de cada lista (perezosa, al primer uso) a partir del catalogo
/// GLOBAL de A&D (<see cref="CatalogoOpcion"/>). A partir de ahi es INDEPENDIENTE: editar el catalogo global no
/// la cambia hasta un "re-sembrar listas base" por-tenant que reaplica las bases respetando lo que la
/// copropiedad creo (custom) o edito (<see cref="OverrideVisual"/>).
///
/// El valor guardado en los registros (ej. Vehiculo.TipoVehiculo) sigue siendo el enum; esta tabla solo
/// gobierna que opciones se OFRECEN y como se ven. Mapeo enum &lt;-&gt; opcion por <see cref="Clave"/> ("e:&lt;Enum&gt;").
/// </summary>
public class ListaOpcion : TenantEntity
{
    /// <summary>Discriminador de la lista, igual que en el catalogo global. Ej. "vehiculo.tipovehiculo".</summary>
    public string Lista { get; set; } = string.Empty;

    /// <summary>Clave ESTABLE heredada del catalogo global: "e:&lt;Enum&gt;" para bases enum, el texto para listas
    /// libres, o "c:&lt;guid&gt;"/slug para opciones propias de la copropiedad. A ella apuntan los datos; no se edita.</summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Texto visible (editable por la copropiedad).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Color del chip (hex o token). null = neutro.</summary>
    public string? Color { get; set; }

    /// <summary>Orden en el selector.</summary>
    public int Orden { get; set; }

    /// <summary>Si la opcion se ofrece. Ocultar (false) en vez de borrar: las filas que ya la usan la conservan.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>true = vino de una base del catalogo global; false = opcion propia creada por la copropiedad.</summary>
    public bool EsBase { get; set; }

    /// <summary>Solo bases: true = la copropiedad edito label/color/orden/activo y el re-sembrar ya no la pisa.
    /// false = el re-sembrar la realinea con el catalogo global.</summary>
    public bool OverrideVisual { get; set; }
}
