using Propia.Domain.Enums;

namespace Propia.Application.Pqrsd;

/// <summary>
/// Un campo FIJO (del sistema) del expediente PQRSD: las columnas que hoy trae de fabrica el tablero /
/// bandeja (radicado, asunto, tipo, etapa, semaforo, solicitante, unidad, vence...). Son los que el
/// usuario puede mostrar, ocultar, renombrar y reordenar por copropiedad desde el gestor de campos; los
/// personalizados viven en <c>pqrsd_campos</c> (<see cref="Propia.Domain.Entities.PqrsdCampo"/>) y NO
/// entran aqui.
///
/// Espejo de <c>UnidadCamposSistema</c> (modulo Unidades), adaptado a PQRSD: PQRSD no tiene plantilla
/// Excel, asi que no lleva Encabezado/Ayuda; en su lugar cada campo declara su
/// <see cref="TipoCampoTablero"/> (como pide el contrato 5.4 seccion 3). <see cref="SiempreEnPlantilla"/>
/// se conserva por paridad de contrato pero hoy es NOMINAL: ningun consumidor de plantilla lo lee.
/// </summary>
/// Forma CANONICA del catalogo de campos del sistema (acordada por VIGIA para todos los modulos): la lista
/// EsLista no se guarda, se deriva de <c>Tipo == Seleccion</c>.
/// <param name="Clave">Clave de configuracion del campo (entidad 'pqrsd'); coincide con la key de columna
/// que hoy usa PqrsKanban.razor (_colsFijas).</param>
/// <param name="Label">Como se llama de fabrica la columna (antes del alias del tenant).</param>
/// <param name="Tipo">Tipo de captura/render del campo (se reusa el enum de Tareas).</param>
/// <param name="VisiblePorDefecto">Si se muestra cuando la copropiedad no tiene configuracion propia.</param>
/// <param name="Fija">Columna estructural que NO se puede ocultar (el identificador: radicado).</param>
/// <param name="SiempreEnPlantilla">Flag de plantilla Excel. Nominal en PQRSD: no hay plantilla que la lea;
/// se mantiene por paridad con el contrato del gestor.</param>
/// <param name="Encabezado">Encabezado de plantilla Excel. Null en PQRSD (no tiene plantilla).</param>
/// <param name="Ayuda">Fila de ayuda de la plantilla. Null en PQRSD.</param>
/// <param name="OpcionesEditables">Solo aplica a Tipo=Seleccion. true = opciones/semilla/color configurables
/// por copropiedad en el gestor; false = lista fija del sistema o respaldada por sus propias tablas (read-only
/// en el gestor). En PQRSD todos los de Seleccion (tipo, categoria, etapa/estado) son false: sus opciones
/// viven en pqrsd_tipos/categorias/estados y NO se tocan (opcion b).</param>
public sealed record PqrsdCampoSistema(
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
/// Catalogo UNICO de los campos de sistema del expediente PQRSD. Fuente de verdad de las columnas fijas de
/// la bandeja/tablero (hoy hardcodeadas en <c>PqrsKanban.razor</c> como <c>_colsFijas</c>). Cuando el modulo
/// adopte el gestor de campos compartido (Fase 1 del Selector de Campos), estas claves seran las que el
/// gestor configure por copropiedad via <c>campos-config?entidad=pqrsd</c>, reemplazando la persistencia
/// actual en localStorage. El ORDEN de la lista es el orden por defecto de las columnas.
///
/// Los catalogos de tipos / categorias / estados / motivos de PQRSD NO son campos de sistema: son entidades
/// configurables aparte y quedan fuera de este catalogo (decision de Alex, opcion (b) de 5.3 seccion C).
/// </summary>
public static class PqrsdCamposSistema
{
    public const string Entidad = "pqrsd";

    public static readonly IReadOnlyList<PqrsdCampoSistema> Todos = new[]
    {
        new PqrsdCampoSistema("radicado", "Radicado", TipoCampoTablero.Texto,
            VisiblePorDefecto: true, Fija: true),
        new PqrsdCampoSistema("asunto", "Asunto", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new PqrsdCampoSistema("tipo", "Tipo", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new PqrsdCampoSistema("categoria", "Categoria", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new PqrsdCampoSistema("estado", "Etapa", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new PqrsdCampoSistema("semaforo", "Estado", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new PqrsdCampoSistema("radicador", "Solicitante", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new PqrsdCampoSistema("unidad", "Unidad", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new PqrsdCampoSistema("vence", "Vence", TipoCampoTablero.Fecha, VisiblePorDefecto: true),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene configuracion propia.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static PqrsdCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
