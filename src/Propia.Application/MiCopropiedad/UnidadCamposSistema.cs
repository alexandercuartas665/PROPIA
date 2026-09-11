namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) de la unidad privada. Son los que el usuario puede mostrar, ocultar,
/// renombrar y reordenar por copropiedad desde Unidades > Configurar; los personalizados viven en
/// unidad_campos_definiciones y NO entran aqui.
/// </summary>
/// <param name="Clave">Clave de configuracion en unidad_campos_config (entidad 'unidad').</param>
/// <param name="Label">Como se llama de fabrica en la tabla de unidades (antes del alias del tenant).</param>
/// <param name="Encabezado">Encabezado de su columna en la plantilla de carga masiva.</param>
/// <param name="Ayuda">Fila de ayuda de la plantilla.</param>
/// <param name="VisiblePorDefecto">Si se muestra cuando la copropiedad no tiene fila de config.</param>
/// <param name="EsLista">El valor sale de una lista de opciones configurable.</param>
/// <param name="SiempreEnPlantilla">Columna estructural: la plantilla la emite aunque este oculta
/// (sin ella el importador no puede identificar la unidad).</param>
public sealed record UnidadCampoSistema(
    string Clave,
    string Label,
    string Encabezado,
    string Ayuda,
    bool VisiblePorDefecto,
    bool EsLista = false,
    bool SiempreEnPlantilla = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema de la unidad privada. Es la fuente de verdad de
/// tres consumidores que antes tenian cada uno su propia lista y se desalineaban:
///   1. la tabla de unidades (Distribucion.razor): columnas y panel de Configurar,
///   2. la plantilla de carga masiva (UnidadesPlantillaService): que columnas se emiten,
///   3. el importador (UnidadesCargaImportService): que columnas se leen.
/// Un campo que este aqui y que la copropiedad tenga VISIBLE tiene que salir en la plantilla y
/// poder importarse. Agregar un campo nuevo = agregar una entrada aqui, mapearlo en el importador
/// y cubrirlo en UnidadesPlantillaRoundTripTests (ese test recorre esta lista completa).
/// El ORDEN de la lista es el orden por defecto de las columnas.
/// </summary>
public static class UnidadCamposSistema
{
    /// <summary>Opciones de fabrica del campo ESTADO (la copropiedad puede ocultarlas o agregar otras).</summary>
    public static readonly string[] EstadosSemilla = { "Habitada", "Desocupada", "Arrendada" };

    public static readonly IReadOnlyList<UnidadCampoSistema> Todos = new[]
    {
        new UnidadCampoSistema("num", "Unidad", "UNIDAD PRIVADA",
            "Codigo de la unidad con guion TORRE-NUMERO. Ej: Apartamento A1-101 (A1=Torre, 101=Apto); Parqueadero P1-15; Deposito D1-02",
            VisiblePorDefecto: true, SiempreEnPlantilla: true),
        new UnidadCampoSistema("tipo", "Tipo", "TIPO", "Elige de la lista",
            VisiblePorDefecto: true, EsLista: true),
        new UnidadCampoSistema("estado", "Estado", "ESTADO", "Elige de la lista",
            VisiblePorDefecto: false, EsLista: true),
        new UnidadCampoSistema("agrupacion", "Agrupacion", "AGRUPACION", "1=Individual, 2=Principal, 3=Anexo",
            VisiblePorDefecto: true),
        new UnidadCampoSistema("coef", "Coeficiente", "COEFICIENTE", "Porcentaje. Max 5 decimales (1,25)",
            VisiblePorDefecto: true),
        // Modulos contributivos: coeficientes adicionales para repartir gastos especificos.
        new UnidadCampoSistema("modcontrib1", "Modulo Contributivo 1", "MODULO CONTRIBUTIVO 1", "Porcentaje. Max 5 decimales", false),
        new UnidadCampoSistema("modcontrib2", "Modulo Contributivo 2", "MODULO CONTRIBUTIVO 2", "Porcentaje. Max 5 decimales", false),
        new UnidadCampoSistema("modcontrib3", "Modulo Contributivo 3", "MODULO CONTRIBUTIVO 3", "Porcentaje. Max 5 decimales", false),
        new UnidadCampoSistema("modcontrib4", "Modulo Contributivo 4", "MODULO CONTRIBUTIVO 4", "Porcentaje. Max 5 decimales", false),
        new UnidadCampoSistema("modcontrib5", "Modulo Contributivo 5", "MODULO CONTRIBUTIVO 5", "Porcentaje. Max 5 decimales", false),
        new UnidadCampoSistema("area", "Area", "AREA", "Area privada en metros cuadrados (numero)", false),
        new UnidadCampoSistema("piso", "Piso", "PISO", "Numero de piso (entero)", false),
        new UnidadCampoSistema("habitaciones", "Habitaciones", "HABITACIONES", "Cantidad (entero)", false),
        new UnidadCampoSistema("banos", "Banos", "BANOS", "Cantidad (entero)", false),
        new UnidadCampoSistema("parqueaderos", "Parqueaderos", "PARQUEADEROS", "Cantidad (entero)", false),
        new UnidadCampoSistema("matricula", "Matricula", "MATRICULA", "Matricula inmobiliaria", true),
        new UnidadCampoSistema("refpago", "Ref. pago", "REF PAGO", "Referencia de pago (alfanumerica)", true),
        new UnidadCampoSistema("pagaadmin", "Paga administracion", "PAGA ADMIN", "Si / No", false),
        new UnidadCampoSistema("cuota", "Cuota mensual", "CUOTA MENSUAL", "Cuota mensual de administracion (numero)", false),
        new UnidadCampoSistema("observaciones", "Observaciones", "OBSERVACIONES", "Texto libre", false),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene fila en unidad_campos_config.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static UnidadCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    /// <summary>Encabezado de plantilla -> campo. Lo usa el importador para leer las columnas.</summary>
    public static UnidadCampoSistema? PorEncabezado(string encabezado)
        => Todos.FirstOrDefault(c => string.Equals(c.Encabezado, (encabezado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
