namespace Propia.Domain.Enums;

/// <summary>Categoria de reporte consolidado. Spec 1.4 seccion 3.</summary>
public enum CategoriaReporteConsolidado
{
    SaludPortafolio = 1,
    FinancieroConsolidado = 2,
    OperativoConsolidado = 3,
    ConvivenciaPqrsd = 4,
    DesempenoEquipo = 5,
    Personalizado = 6
}

/// <summary>Origen de la generacion. Spec 1.4 seccion 10 + RN-09.</summary>
public enum OrigenGeneracionConsolidada
{
    Manual = 1,
    Programado = 2,
    Ia = 3
}

/// <summary>Estado de la generacion. Spec 1.4 seccion 15.</summary>
public enum EstadoGeneracionConsolidada
{
    Generando = 1,
    Listo = 2,
    Error = 3
}
