using Propia.Domain.Enums;

namespace Propia.Application.Mantenimiento;

/// <summary>
/// Un campo FIJO (del sistema) de la PROGRAMACION de mantenimiento (cronograma). Forma canonica del
/// catalogo de campos del sistema (definida por VIGIA el 2026-09-13 para todos los agentes; la Fase 0
/// alineara <c>UnidadCamposSistema</c> a esta misma). <c>EsLista</c> se deriva de <c>Tipo == Seleccion</c>.
/// </summary>
/// <param name="Clave">Clave de configuracion (prefijo 'mantenimiento', entidad 'programacion').</param>
/// <param name="Label">Como se llama de fabrica en la tabla (antes del alias del tenant).</param>
/// <param name="Tipo">Tipo de campo (render/captura) segun <see cref="TipoCampoTablero"/>.</param>
/// <param name="VisiblePorDefecto">Si se muestra cuando la copropiedad no tiene fila de config.</param>
/// <param name="Fija">Estructural: no se puede ocultar (ej. el titulo de la programacion).</param>
/// <param name="SiempreEnPlantilla">Columna estructural de plantilla (la programacion no tiene plantilla hoy).</param>
/// <param name="Encabezado">Encabezado de plantilla. Null en Mantenimiento (no tiene plantilla Excel).</param>
/// <param name="Ayuda">Ayuda de plantilla. Null en Mantenimiento.</param>
/// <param name="OpcionesEditables">Solo para Tipo=Seleccion: true = opciones/semilla/color configurables por
/// copropiedad; false = lista fija del sistema (enum), read-only. En la programacion todas las listas salen
/// de enums (tipo, prioridad, periodicidad, tablero), asi que es false en todas.</param>
public sealed record MantenimientoCampoSistema(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisiblePorDefecto,
    bool Fija = false,
    bool SiempreEnPlantilla = false,
    string? Encabezado = null,
    string? Ayuda = null,
    bool OpcionesEditables = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema de la PROGRAMACION de mantenimiento. Preparacion de la Fase 1
/// del Selector de Campos (doc 5.4/5.5). El ORDEN coincide con <c>_progColsAll</c> de
/// <c>Mantenimiento.razor</c> (el gestor propio a reemplazar en la adopcion).
///
/// NOTA: la programacion/intervencion NO tiene campos propios (EAV): no hay tablas
/// <c>programacion_campos*</c> ni endpoints <c>mantenimiento-campos*</c>. Este catalogo cubre solo los
/// campos del SISTEMA. Si Alex decide que la programacion debe soportar campos propios, es tarea aparte
/// con migracion. Los campos son enums/referencias/booleanos fijos; ninguno es lista configurable.
/// </summary>
public static class MantenimientoCamposSistema
{
    public static readonly IReadOnlyList<MantenimientoCampoSistema> Todos = new[]
    {
        new MantenimientoCampoSistema("tipo", "Tipo", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("activo", "Activo", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("titulo", "Titulo", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true),
        new MantenimientoCampoSistema("tercero", "Tercero", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("contrato", "Contrato", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("costo", "Costo estimado", TipoCampoTablero.Moneda, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("tablero", "Tablero", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("prioridad", "Prioridad", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("periodicidad", "Periodicidad", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("proxima", "Proxima ejecucion", TipoCampoTablero.Fecha, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("responsables", "Responsables", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("activa", "Activa", TipoCampoTablero.Booleano, VisiblePorDefecto: true),
    };

    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static MantenimientoCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
