using Propia.Domain.Enums;

namespace Propia.Application.Seguros;

/// <summary>
/// Un campo de SISTEMA (fijo) de la poliza. Misma forma CANONICA que ContratoCampoSistema
/// (fijada por VIGIA 2026-09-13): el gestor de campos compartido consume una sola forma por entidad.
/// </summary>
/// <param name="Clave">Clave de configuracion (entidad 'poliza').</param>
/// <param name="Label">Nombre de fabrica en la tabla de polizas (antes del alias del tenant).</param>
/// <param name="Tipo">Tipo del campo. EsLista se deriva de Tipo == Seleccion.</param>
/// <param name="VisiblePorDefecto">Si se muestra cuando la copropiedad no tiene configuracion propia.</param>
/// <param name="Fija">Estructural: no se puede ocultar (ej. el numero de poliza).</param>
/// <param name="SiempreEnPlantilla">Solo aplica a modulos con carga por Excel; seguros no tiene.</param>
/// <param name="Encabezado">Encabezado de plantilla Excel (null: seguros no tiene plantilla).</param>
/// <param name="Ayuda">Fila de ayuda de la plantilla (null: seguros no tiene plantilla).</param>
/// <param name="OpcionesEditables">Solo para Tipo == Seleccion: true = opciones/semilla/color
/// configurables; false = lista fija del sistema (enum), solo lectura. La poliza no tiene campos de
/// sistema de tipo Seleccion, asi que queda en el default.</param>
public sealed record PolizaCampoSistema(
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
/// Catalogo de los campos de SISTEMA de la poliza, para el Selector de Campos (Fase 1). El ORDEN es el
/// orden por defecto de las columnas de la tabla de /seguros; VisiblePorDefecto = las columnas que hoy se
/// ven sin configurar. La poliza no tiene campos de sistema tipo lista editable (aseguradora y corredor
/// son terceros del Directorio), por eso ninguno es Seleccion.
/// </summary>
public static class PolizaCamposSistema
{
    public static readonly IReadOnlyList<PolizaCampoSistema> Todos = new[]
    {
        new PolizaCampoSistema("numeroPoliza", "N poliza", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true),
        new PolizaCampoSistema("aseguradora", "Aseguradora", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new PolizaCampoSistema("corredor", "Corredor", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new PolizaCampoSistema("fechaInicio", "Inicio", TipoCampoTablero.Fecha, VisiblePorDefecto: true),
        new PolizaCampoSistema("fechaFin", "Finalizacion", TipoCampoTablero.Fecha, VisiblePorDefecto: true),
        // Semaforo de vencimiento: derivado por fecha, solo lectura.
        new PolizaCampoSistema("vencimiento", "Vencimiento", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new PolizaCampoSistema("valorPoliza", "Valor", TipoCampoTablero.Moneda, VisiblePorDefecto: true),
        // Numero de reclamaciones: derivado (conteo), solo lectura.
        new PolizaCampoSistema("reclamaciones", "Reclamaciones", TipoCampoTablero.Numero, VisiblePorDefecto: true),
        // No visibles por defecto:
        new PolizaCampoSistema("formaPagoCuotas", "Forma de pago", TipoCampoTablero.Numero, VisiblePorDefecto: false),
        new PolizaCampoSistema("pagoMensual", "Pago mensual", TipoCampoTablero.Booleano, VisiblePorDefecto: false),
        new PolizaCampoSistema("cobertura", "Cobertura", TipoCampoTablero.AreaTexto, VisiblePorDefecto: false),
        new PolizaCampoSistema("incluyeZonasUnidades", "Incluye zonas y unidades", TipoCampoTablero.Booleano, VisiblePorDefecto: false),
        new PolizaCampoSistema("valoresAgregados", "Valores agregados", TipoCampoTablero.AreaTexto, VisiblePorDefecto: false),
        new PolizaCampoSistema("observaciones", "Observaciones", TipoCampoTablero.AreaTexto, VisiblePorDefecto: false),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene configuracion propia.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static PolizaCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
