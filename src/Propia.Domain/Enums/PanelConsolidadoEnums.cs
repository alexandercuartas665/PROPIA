namespace Propia.Domain.Enums;

/// <summary>Estado de salud de una copropiedad. Spec 1.1 v1.0 seccion 8.1 (semaforo).</summary>
public enum EstadoSaludCopropiedad
{
    Verde = 1,
    Amarillo = 2,
    Rojo = 3
}

/// <summary>Vista por defecto del panel. Spec 1.1 v1.0 RN-07.</summary>
public enum VistaPanelDefault
{
    Tarjetas = 1,
    Tabla = 2
}

/// <summary>Tipo de evento en el feed de actividad consolidado. Spec 1.1 v1.0 tabla panel_feed_evento.</summary>
public enum TipoEventoPanel
{
    PqrsdNueva = 1,
    TareaCompletada = 2,
    TareaVencida = 3,
    PagoRecibido = 4,
    AlertaNueva = 5,
    AsambleaConvocada = 6,
    ContratoPorVencer = 7,
    Otro = 99
}

/// <summary>Catalogo de KPIs configurables para la banda superior del panel. Spec 1.1 v1.0 seccion 6.</summary>
public static class KpiPanelCatalogo
{
    public const string TotalCopropiedadesActivas = "TOTAL_COPROPIEDADES_ACTIVAS";
    public const string CopropiedadesEstadoCritico = "COPROPIEDADES_ESTADO_CRITICO";
    public const string TotalTareasVencidas = "TOTAL_TAREAS_VENCIDAS";
    public const string TotalPqrsdSinResponder = "TOTAL_PQRSD_SIN_RESPONDER";
    public const string RecaudoPromedioMes = "RECAUDO_PROMEDIO_MES";
    public const string CarteraVencidaTotal = "CARTERA_VENCIDA_TOTAL";
    public const string ContratosPorVencer = "CONTRATOS_POR_VENCER";

    public static readonly string[] DefaultPorPlataforma = new[]
    {
        TotalCopropiedadesActivas,
        CopropiedadesEstadoCritico,
        TotalTareasVencidas,
        TotalPqrsdSinResponder
    };
}
