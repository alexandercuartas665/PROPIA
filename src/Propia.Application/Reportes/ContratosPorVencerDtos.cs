namespace Propia.Application.Reportes;

// ===========================================================================
// Reporte "Contratos proximos a vencer" (multi-copropiedad)
// Agrega contratos con semaforo amarillo/rojo (o vencidos) de una o varias
// copropiedades que el usuario administra. Alimenta el modal con graficos +
// tabla exportable a Excel.
// ===========================================================================

/// <summary>Filtro del reporte: copropiedades a incluir (vacio/null = todas las que administra).</summary>
public record ContratosPorVencerFiltro(IReadOnlyList<Guid>? TenantIds);

/// <summary>Una fila del reporte: un contrato proximo a vencer o vencido, con su copropiedad.</summary>
public record ContratoPorVencerFila(
    Guid TenantId,
    string Copropiedad,
    string? CodigoCorto,
    Guid ContratoId,
    string? NumeroContrato,
    string Proveedor,
    string? Categoria,
    string? TipoContrato,
    System.DateOnly? FechaInicio,
    System.DateOnly? FechaFin,
    int? DiasRestantes,
    int PctTranscurrido,
    string Semaforo,          // "amarillo" | "rojo"
    bool Vencido,
    decimal? Valor);

/// <summary>Agregado por copropiedad (para el grafico de barras).</summary>
public record ContratosPorCopropiedad(
    Guid TenantId,
    string Copropiedad,
    string? CodigoCorto,
    int Cantidad,
    int Amarillo,
    int Rojo,
    int Vencidos,
    decimal Valor);

/// <summary>Resumen agregado del reporte (KPIs + distribucion para los graficos).</summary>
public record ContratosPorVencerResumen(
    int Total,
    int Amarillo,
    int Rojo,
    int Vencidos,
    decimal ValorTotal,
    IReadOnlyList<ContratosPorCopropiedad> PorCopropiedad);

// ----- Analitica de costos (sobre TODOS los contratos activos, no solo los por vencer) -----

/// <summary>Costo agregado por categoria de contrato (Servicios, Seguridad, Aseo...).</summary>
public record CategoriaCosto(string Categoria, int Cantidad, decimal ValorMensual, decimal ValorAnual);

/// <summary>Costo comprometido en un mes de la proyeccion (contratos vigentes ese mes).</summary>
public record MesCosto(string Mes, int Anio, decimal Costo);

/// <summary>Analitica financiera de contratos: costo mensual/anual, por categoria y proyeccion.</summary>
public record ContratosAnalitica(
    int ContratosActivos,
    decimal CostoMensual,
    decimal CostoAnualProyectado,
    decimal ValorContratado,
    IReadOnlyList<CategoriaCosto> PorCategoria,
    IReadOnlyList<MesCosto> ProyeccionMensual);

/// <summary>Respuesta completa del reporte.</summary>
public record ContratosPorVencerReporteDto(
    IReadOnlyList<ContratoPorVencerFila> Filas,
    ContratosPorVencerResumen Resumen,
    ContratosAnalitica Analitica);
