using Propia.Domain.Enums;

namespace Propia.Application.Tareas;

/// <summary>
/// Un campo FIJO (del sistema) de una tarea: las columnas que trae el tablero de fabrica. Son los que
/// el usuario podra mostrar, ocultar, renombrar y reordenar por copropiedad cuando Tareas adopte el gestor
/// de campos compartido (Fase 1 del Selector de Campos). Los personalizados viven en <c>tablero_campos</c>
/// (por TABLERO, no por copropiedad) y NO entran aqui.
///
/// Usa la forma CANONICA de campo del sistema fijada por VIGIA para los cuatro modulos y la Fase 0
/// (record &lt;Entidad&gt;CampoSistema). En Tareas: <see cref="Fija"/> = true para las columnas estructurales
/// (titulo, etapa, progreso); <see cref="SiempreEnPlantilla"/>, <see cref="Encabezado"/> y <see cref="Ayuda"/>
/// son de entidades con plantilla Excel (Unidades) y aqui quedan en false/null. El tipo se declara con
/// <see cref="TipoCampoTablero"/> (el enum ya existe; aqui solo se REFERENCIA, no se modifica); "es lista" se
/// deriva de <c>Tipo == Seleccion</c>.
/// </summary>
/// <param name="Clave">Identificador estable del campo del sistema (clave de configuracion futura).</param>
/// <param name="Label">Como se llama de fabrica la columna (antes del alias del tenant).</param>
/// <param name="Tipo">Tipo de captura/render, con el enum existente. Ver notas de tipos aun no soportados.</param>
/// <param name="VisiblePorDefecto">Si la columna se muestra cuando la copropiedad no ha configurado nada.</param>
/// <param name="Fija">Columna estructural: se muestra siempre y no se puede ocultar.</param>
/// <param name="SiempreEnPlantilla">Solo entidades con plantilla Excel: la plantilla la emite aunque este oculta. En Tareas siempre false.</param>
/// <param name="Encabezado">Solo plantilla Excel: encabezado de la columna. En Tareas null.</param>
/// <param name="Ayuda">Solo plantilla Excel: fila de ayuda. En Tareas null.</param>
public sealed record TareaCampoSistema(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisiblePorDefecto,
    bool Fija = false,
    bool SiempreEnPlantilla = false,
    string? Encabezado = null,
    string? Ayuda = null);

/// <summary>
/// Catalogo UNICO de los campos de sistema de una tarea. El ORDEN es el orden por defecto de las columnas
/// de la tabla del tablero (ver <c>TableroTareas.razor</c>, cabecera <c>tb-list-hdr</c>): titulo, descripcion,
/// codigo, etapa, asignado, progreso, etiquetas, prioridad, vence, valor, relacionado con. Hoy esas columnas
/// se controlan con toggles sueltos (<c>_colDesc</c>, <c>_colResp</c>, ...); al adoptar el gestor compartido
/// esa configuracion pasara a persistir por copropiedad y este catalogo sera su fuente de verdad.
///
/// PREPARACION Fase 1: esta clase NO cambia comportamiento; es el dato que consumira el componente compartido.
/// </summary>
public static class TareaCamposSistema
{
    public static readonly IReadOnlyList<TareaCampoSistema> Todos = new[]
    {
        // Estructurales: siempre visibles, sin toggle (Fija).
        new TareaCampoSistema("titulo", "Titulo", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true),
        // Toggle _colDesc (default true).
        new TareaCampoSistema("descripcion", "Descripcion", TipoCampoTablero.AreaTexto, VisiblePorDefecto: true),
        // Toggle _colCodigo (default true). Consecutivo autogenerado "T-AAAA-NNNN", solo lectura.
        new TareaCampoSistema("codigo", "Codigo", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        // Estructural (columna "ETAPA"). Respaldado por tarea_estados (columnas del tablero), no por una
        // lista de opciones simple; el tipo mas cercano hoy es Seleccion.
        new TareaCampoSistema("estado", "Etapa", TipoCampoTablero.Seleccion, VisiblePorDefecto: true, Fija: true),
        // Toggle _colResp (default true). NOTA Fase 2 (gap J): necesitaria tipo Usuario (persona del
        // directorio); hoy no existe en el enum, se declara como Texto de forma provisional.
        new TareaCampoSistema("asignado", "Asignado", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        // Estructural. Avance 0-100. NOTA Fase 0 (gap C): idealmente Porcentaje; hoy Numero.
        new TareaCampoSistema("progreso", "Progreso", TipoCampoTablero.Numero, VisiblePorDefecto: true, Fija: true),
        // Toggle _colEtiq (default true). Multi-seleccion respaldada por tarea_etiquetas; el tipo mas cercano
        // hoy es Seleccion (el enum no distingue multiple).
        new TareaCampoSistema("etiquetas", "Etiquetas", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        // Toggle _colPrio (default true). Enum PrioridadTarea (Urgente/Alta/Normal/Baja).
        new TareaCampoSistema("prioridad", "Prioridad", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        // Toggle _colDue (default true).
        new TareaCampoSistema("vence", "Vence", TipoCampoTablero.Fecha, VisiblePorDefecto: true),
        // Toggle _colValor (default true).
        new TareaCampoSistema("valor", "Valor", TipoCampoTablero.Moneda, VisiblePorDefecto: true),
        // Toggle _colOrigen (default true). Vinculo a otro modulo (ej. PQRSD); el tipo mas cercano hoy es Texto.
        new TareaCampoSistema("origen", "Relacionado con", TipoCampoTablero.Texto, VisiblePorDefecto: true),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene configuracion de columnas.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static TareaCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
