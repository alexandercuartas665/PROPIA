using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) de un vehiculo: los que la ficha trae de fabrica. El usuario los puede
/// mostrar, ocultar, renombrar y reordenar por copropiedad desde el gestor de campos compartido; los
/// personalizados viven en <c>vehiculo_campos_definiciones</c> (endpoints <c>vehiculos-campos</c>) y NO
/// entran aqui.
///
/// Usa la forma CANONICA de campo del sistema fijada por VIGIA (record &lt;Entidad&gt;CampoSistema, igual
/// que <see cref="Propia.Application.Tareas.TareaCamposSistema"/> y las demas). "Es lista" se deriva de
/// <c>Tipo == Seleccion</c>.
/// </summary>
/// <param name="Clave">Identificador estable del campo (clave de configuracion, discriminada por 'vehiculos').</param>
/// <param name="Label">Nombre de fabrica (antes del alias del tenant).</param>
/// <param name="Tipo">Tipo de captura/render con el enum existente.</param>
/// <param name="VisiblePorDefecto">Si se muestra cuando la copropiedad no configuro nada.</param>
/// <param name="Fija">Columna estructural: se muestra siempre y no se puede ocultar (identifica la fila).</param>
/// <param name="OpcionesEditables">Solo Tipo=Seleccion: true = opciones/semilla/color configurables por
/// copropiedad; false = lista fija del sistema (enum), de solo lectura en el gestor. En vehiculos el tipo
/// de vehiculo es el enum <see cref="TipoVehiculo"/> (valores estrictos), asi que es false: no se pueden
/// inventar tipos de texto libre sin romper el enum (agregar tipos propios seria Fase 2).</param>
public sealed record VehiculoCampoSistema(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisiblePorDefecto,
    bool Fija = false,
    bool OpcionesEditables = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema de un vehiculo. El ORDEN es el orden por defecto de las columnas
/// de la tabla (ver <c>Vehiculos.razor</c>): placa y tipo de vehiculo. Es la fuente de verdad que consume el
/// gestor de campos compartido (<c>ConfigCamposEntidad</c>) via el parametro <c>Catalogo</c>.
/// </summary>
public static class VehiculoCamposSistema
{
    public static readonly IReadOnlyList<VehiculoCampoSistema> Todos = new[]
    {
        // Placa: identifica al vehiculo, no se puede ocultar.
        new VehiculoCampoSistema("placa", "Placa", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true),
        // Tipo de vehiculo: enum TipoVehiculo (Automovil/Moto/Bicicleta/Camioneta/Otro), lista de solo lectura.
        new VehiculoCampoSistema("tipovehiculo", "Tipo de vehiculo", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene configuracion.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static VehiculoCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
