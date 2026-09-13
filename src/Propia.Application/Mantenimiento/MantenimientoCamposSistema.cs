namespace Propia.Application.Mantenimiento;

/// <summary>
/// Un campo FIJO (del sistema) de la PROGRAMACION de mantenimiento (cronograma). Espejo de
/// <c>UnidadCampoSistema</c>: son los que el usuario puede mostrar, ocultar, renombrar y reordenar por
/// copropiedad desde Mantenimiento &gt; Campos. Los personalizados (EAV) NO existen para la programacion
/// hoy (ver nota en <see cref="MantenimientoCamposSistema"/>).
/// </summary>
/// <param name="Clave">Clave de configuracion (entidad 'programacion' / prefijo 'mantenimiento').</param>
/// <param name="Label">Como se llama de fabrica en la tabla de programacion (antes del alias del tenant).</param>
/// <param name="Encabezado">Encabezado de columna (mayusculas) para cuando exista plantilla/exporte.</param>
/// <param name="Ayuda">Texto de ayuda del campo.</param>
/// <param name="VisiblePorDefecto">Si se muestra cuando la copropiedad no tiene fila de config.</param>
/// <param name="EsLista">El valor sale de una lista de opciones configurable (no aplica a enums fijos).</param>
/// <param name="SiempreEnPlantilla">Columna estructural (la programacion no tiene plantilla de carga hoy).</param>
public sealed record MantenimientoCampoSistema(
    string Clave,
    string Label,
    string Encabezado,
    string Ayuda,
    bool VisiblePorDefecto,
    bool EsLista = false,
    bool SiempreEnPlantilla = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema de la PROGRAMACION de mantenimiento. Espejo exacto de
/// <c>UnidadCamposSistema</c>, para preparar la adopcion del gestor de campos compartido (Fase 1 del
/// Selector de Campos, doc 5.4/5.5). El ORDEN de la lista es el orden por defecto de las columnas y
/// coincide con <c>_progColsAll</c> de <c>Mantenimiento.razor</c>, que es el gestor propio a reemplazar.
///
/// NOTA (preparacion Fase 1): la programacion/intervencion NO tiene campos propios (EAV): no hay tablas
/// <c>programacion_campos*</c> ni endpoints <c>mantenimiento-campos*</c>. Este catalogo cubre solo los
/// campos del SISTEMA. Si Alex decide que la programacion debe soportar campos propios, es una tarea
/// aparte con migracion (tablas EAV + entidades + servicio + endpoints), fuera de esta preparacion.
///
/// Los campos son enums/booleanos fijos, no listas configurables: por eso <c>EsLista</c> es false en
/// todos (tipo/prioridad/periodicidad salen de un enum, no de opciones editables por el usuario).
/// </summary>
public static class MantenimientoCamposSistema
{
    public static readonly IReadOnlyList<MantenimientoCampoSistema> Todos = new[]
    {
        new MantenimientoCampoSistema("tipo", "Tipo", "TIPO", "Equipo o Zona comun (a que activo aplica la programacion)", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("activo", "Activo", "ACTIVO", "Equipo o zona sobre el que se programa el mantenimiento", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("titulo", "Titulo", "TITULO", "Nombre de la programacion", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("tercero", "Tercero", "TERCERO", "Proveedor/contratista del Directorio (opcional)", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("contrato", "Contrato", "CONTRATO", "Contrato de servicios asociado (opcional)", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("costo", "Costo estimado", "COSTO ESTIMADO", "Costo estimado del mantenimiento (numero). Campo del sistema desde ENTREGA_YUNQUE_04", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("tablero", "Tablero", "TABLERO", "Tablero de Tareas donde se crean las tareas generadas", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("prioridad", "Prioridad", "PRIORIDAD", "Prioridad de la tarea generada (enum)", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("periodicidad", "Periodicidad", "PERIODICIDAD", "Cada cuanto se repite (o expresion cron)", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("proxima", "Proxima ejecucion", "PROXIMA EJECUCION", "Proxima fecha en que se materializa la tarea", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("responsables", "Responsables", "RESPONSABLES", "Personas asignadas a la tarea generada", VisiblePorDefecto: true),
        new MantenimientoCampoSistema("activa", "Activa", "ACTIVA", "Si la programacion esta activa o pausada (Si/No)", VisiblePorDefecto: true),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene fila de config.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static MantenimientoCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
