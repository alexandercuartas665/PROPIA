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
/// <c>Tipo == Seleccion</c>. Ademas de alimentar el gestor de campos, es la fuente UNICA de la hoja
/// VEHICULOS de la plantilla de carga (homogenizacion Fase 1, decision de Alex 2026-09-14): por eso lleva
/// <see cref="Encabezado"/>/<see cref="Ayuda"/>/<see cref="SiempreEnPlantilla"/> como PersonaCampoSistema.
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
/// <param name="Encabezado">Encabezado CANONICO de la columna en la plantilla (contrato del importador).
/// null = se deriva del Label en MAYUSCULAS.</param>
/// <param name="Ayuda">Fila de ayuda de la plantilla.</param>
/// <param name="SiempreEnPlantilla">Columna estructural: la plantilla la emite aunque este oculta (PLACA
/// es la clave del vehiculo). Normalmente coincide con Fija.</param>
public sealed record VehiculoCampoSistema(
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
/// Catalogo UNICO de los campos de sistema de un vehiculo. El ORDEN es el orden por defecto de las columnas
/// (tabla del modulo y hoja de plantilla): placa, tipo, marca, modelo, color. Es la fuente de verdad que
/// consume el gestor de campos compartido (<c>ConfigCamposEntidad</c>) via <c>Catalogo</c> y la generacion
/// de la plantilla. marca/modelo/color son columnas reales de VehiculoAutorizado que hasta ahora salian
/// en la plantilla pero no eran configurables; ahora son campos de sistema (visibles/ocultables/renombrables
/// en el PANEL de campos; la TABLA del modulo sigue fija = Fase 2).
/// </summary>
public static class VehiculoCamposSistema
{
    public static readonly IReadOnlyList<VehiculoCampoSistema> Todos = new[]
    {
        // Placa: identifica al vehiculo, no se puede ocultar y siempre va en la plantilla.
        new VehiculoCampoSistema("placa", "Placa", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true,
            Encabezado: "PLACA", Ayuda: "", SiempreEnPlantilla: true),
        // Tipo de vehiculo: enum TipoVehiculo (Automovil/Moto/Bicicleta/Camioneta/Otro), lista de solo lectura.
        new VehiculoCampoSistema("tipovehiculo", "Tipo de vehiculo", TipoCampoTablero.Seleccion, VisiblePorDefecto: true,
            Encabezado: "TIPO DE VEHICULO", Ayuda: "Elige de la lista"),
        new VehiculoCampoSistema("marca", "Marca", TipoCampoTablero.Texto, VisiblePorDefecto: true,
            Encabezado: "MARCA"),
        new VehiculoCampoSistema("modelo", "Modelo", TipoCampoTablero.Texto, VisiblePorDefecto: true,
            Encabezado: "MODELO"),
        new VehiculoCampoSistema("color", "Color", TipoCampoTablero.Texto, VisiblePorDefecto: true,
            Encabezado: "COLOR"),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene configuracion.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static VehiculoCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    /// <summary>Encabezado canonico de plantilla -> campo (lo usa el importador para resolver la columna).</summary>
    public static VehiculoCampoSistema? PorEncabezado(string encabezado)
        => Todos.FirstOrDefault(c => string.Equals(c.EncabezadoPlantilla, (encabezado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
