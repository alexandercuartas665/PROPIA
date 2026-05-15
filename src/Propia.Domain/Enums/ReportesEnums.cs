namespace Propia.Domain.Enums;

/// <summary>Origen del reporte generado. Spec 2.16 RN-09 - trazabilidad de IA.</summary>
public enum OrigenReporte
{
    Manual = 1,
    Programado = 2,
    /// <summary>Agente IA T.1 - reservado para Fase 2.</summary>
    Ia = 3
}

/// <summary>Estado del reporte en el historial. Spec 2.16 seccion 14 + RN-02 asincrono.</summary>
public enum EstadoReporteGenerado
{
    Generando = 1,
    Listo = 2,
    Error = 3
}

/// <summary>Frecuencia de una programacion. Spec 2.16 seccion 6.1.</summary>
public enum FrecuenciaProgramacion
{
    Mensual = 1,
    Trimestral = 2,
    Semestral = 3,
    Anual = 4
}

/// <summary>Periodo de datos que cubre el envio programado.</summary>
public enum PeriodoQueCubre
{
    MesAnterior = 1,
    TrimestreAnterior = 2,
    SemestreAnterior = 3,
    AnioAnterior = 4,
    PeriodoPresupuestal = 5
}

/// <summary>Formato del archivo entregable. MVP solo escribe la config, T.2 lo despacha.</summary>
public enum FormatoReporte
{
    Pdf = 1,
    Excel = 2,
    Ambos = 3
}

/// <summary>Estado de una programacion. Pausada NO elimina historial (spec seccion 6.2).</summary>
public enum EstadoProgramacion
{
    Activa = 1,
    Pausada = 2
}

/// <summary>Audiencia objetivo de un reporte del catalogo (spec seccion 4.2).</summary>
public enum AudienciaReporte
{
    Administrador = 1,
    Consejo = 2,
    Propietario = 3
}
