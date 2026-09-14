using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo de SISTEMA (fijo) del contrato de servicio. Forma CANONICA de los catalogos de campos del
/// sistema del Selector de Campos (fijada por VIGIA 2026-09-13; la Fase 0 alinea el de Unidades a esta
/// misma). El gestor de campos compartido consume esta unica forma para todas las entidades.
/// </summary>
/// <param name="Clave">Clave de configuracion (entidad 'contrato').</param>
/// <param name="Label">Nombre de fabrica en la tabla de contratos (antes del alias del tenant).</param>
/// <param name="Tipo">Tipo del campo. EsLista se deriva de Tipo == Seleccion.</param>
/// <param name="VisiblePorDefecto">Si se muestra cuando la copropiedad no tiene configuracion propia.</param>
/// <param name="Fija">Estructural: no se puede ocultar (ej. el contratista).</param>
/// <param name="SiempreEnPlantilla">Solo aplica a modulos con carga por Excel; contratos no tiene.</param>
/// <param name="Encabezado">Encabezado de plantilla Excel (null: contratos no tiene plantilla).</param>
/// <param name="Ayuda">Fila de ayuda de la plantilla (null: contratos no tiene plantilla).</param>
/// <param name="OpcionesEditables">Solo para Tipo == Seleccion: true = opciones/semilla/color
/// configurables por copropiedad; false = lista fija del sistema (enum), solo lectura. En contratos
/// las Seleccion salen de enums del dominio, por eso todas van en false.</param>
public sealed record ContratoCampoSistema(
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
/// Catalogo de los campos de SISTEMA del contrato de servicio, para el Selector de Campos (Fase 1). El
/// ORDEN es el orden por defecto de las columnas de la tabla de /contratos; VisiblePorDefecto = las
/// columnas que hoy se ven sin configurar.
///
/// Nota para Fase 0/Alex: los campos de Tipo Seleccion aqui (tipo de contrato, categoria, estado, tipo de
/// servicio) salen de ENUMS del dominio, no de listas de opciones editables como en Unidades. Las semillas
/// de abajo son solo las etiquetas de esos enums; decidir en Fase 0 si el gestor las trata como lista fija
/// o editable.
/// </summary>
public static class ContratoCamposSistema
{
    /// <summary>Etiquetas del enum EstadoContrato (campo "estado").</summary>
    public static readonly string[] EstadosSemilla = { "Vigente", "En renovacion", "Vencido" };

    /// <summary>Etiquetas del enum TipoContrato (campo "tipocontrato").</summary>
    public static readonly string[] TiposContratoSemilla =
        { "Prestacion de servicios", "Obra", "Mantenimiento", "Compra y venta", "Arrendamiento", "Seguro", "Licenciamiento" };

    /// <summary>Etiquetas del enum CategoriaContrato (campo "categoria").</summary>
    public static readonly string[] CategoriasSemilla =
        { "Administracion", "Contabilidad", "Asesoria", "Aseo", "Seguridad", "Mantenimiento", "Jardineria", "Servicios publicos", "Seguros" };

    // Las CLAVES son las que usa la tabla de /contratos (Servicios.razor, switch de columnas), en MINUSCULA:
    // el endpoint compartido unidades-config normaliza la clave con ToLowerInvariant() al guardar, asi que una
    // clave camelCase no round-tripearia y la config (orden/visibilidad/alias) no se encontraria. Config
    // compartida (unidades-config, entidad 'contrato') + render por clave del switch = un unico vocabulario
    // (adopcion Fase 1, decision VIGIA 2026-09-14). El ORDEN y VisiblePorDefecto espejan las columnas actuales.
    // No hay 'vencimiento' ni 'nitProveedor' aparte: el semaforo va en 'estado' (label "Vencimiento") y no hay
    // columna NIT. 'etapa' es la etapa del kanban.
    public static readonly IReadOnlyList<ContratoCampoSistema> Todos = new[]
    {
        new ContratoCampoSistema("tercero", "Contratista", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true),
        new ContratoCampoSistema("tipocontrato", "Tipo de contrato", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new ContratoCampoSistema("numero", "N contrato", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new ContratoCampoSistema("categoria", "Categoria", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new ContratoCampoSistema("asociado", "Asociado a", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new ContratoCampoSistema("inicio", "Inicio", TipoCampoTablero.Fecha, VisiblePorDefecto: true),
        new ContratoCampoSistema("fin", "Finalizacion", TipoCampoTablero.Fecha, VisiblePorDefecto: true),
        // 'estado' muestra el semaforo/estado del contrato (label historico "Vencimiento"); lista de sistema
        // (EstadosSemilla) de solo lectura, derivada por fecha para el semaforo.
        new ContratoCampoSistema("estado", "Vencimiento", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new ContratoCampoSistema("valortotal", "Valor", TipoCampoTablero.Moneda, VisiblePorDefecto: true),
        new ContratoCampoSistema("formapago", "Forma de pago", TipoCampoTablero.Numero, VisiblePorDefecto: true),
        new ContratoCampoSistema("pagomensual", "Pago mensual", TipoCampoTablero.Booleano, VisiblePorDefecto: true),
        new ContratoCampoSistema("obs", "Observaciones", TipoCampoTablero.AreaTexto, VisiblePorDefecto: true),
        // No visibles por defecto (legacy / avanzadas):
        new ContratoCampoSistema("tipo", "Tipo de servicio", TipoCampoTablero.Seleccion, VisiblePorDefecto: false),
        new ContratoCampoSistema("valor", "$/mes", TipoCampoTablero.Moneda, VisiblePorDefecto: false),
        new ContratoCampoSistema("etapa", "Etapa", TipoCampoTablero.Texto, VisiblePorDefecto: false),
        new ContratoCampoSistema("contacto", "Contacto", TipoCampoTablero.Texto, VisiblePorDefecto: false),
        new ContratoCampoSistema("renov", "Renov.", TipoCampoTablero.Booleano, VisiblePorDefecto: false),
        new ContratoCampoSistema("dias", "Dias alerta", TipoCampoTablero.Numero, VisiblePorDefecto: false),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene configuracion propia.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static ContratoCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
