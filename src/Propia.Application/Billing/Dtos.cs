using Propia.Domain.Enums;

namespace Propia.Application.Billing;

// ----- Planes -----
public record PlanDto(
    Guid Id, string Nombre, string? Descripcion,
    decimal FeeBase, decimal FeeVariablePorUnidad,
    bool CicloMensual, bool CicloAnual, decimal DescuentoAnualPct,
    int? LimiteUnidades, int? LimiteUsuarios, int? LimiteStorageGb,
    int? LimiteLineasWhatsapp, int? LimiteLlamadasIaMensual,
    int DiasTrial, EstadoPlan Estado, int SuscripcionesActivas, DateTimeOffset CreatedAt,
    int? LimiteCopropiedades = null, bool EsPromocional = false);

public record CrearPlanRequest(
    string Nombre, string? Descripcion,
    decimal FeeBase, decimal FeeVariablePorUnidad,
    bool CicloMensual, bool CicloAnual, decimal DescuentoAnualPct,
    int? LimiteUnidades, int? LimiteUsuarios, int? LimiteStorageGb,
    int? LimiteLineasWhatsapp, int? LimiteLlamadasIaMensual,
    int DiasTrial, int? LimiteCopropiedades = null, bool EsPromocional = false);

public record ActualizarPlanRequest(
    string Nombre, string? Descripcion,
    decimal FeeBase, decimal FeeVariablePorUnidad,
    bool CicloMensual, bool CicloAnual, decimal DescuentoAnualPct,
    int? LimiteUnidades, int? LimiteUsuarios, int? LimiteStorageGb,
    int? LimiteLineasWhatsapp, int? LimiteLlamadasIaMensual,
    int DiasTrial, EstadoPlan Estado, int? LimiteCopropiedades = null, bool EsPromocional = false);

// ----- Suscripciones -----
public record SuscripcionDto(
    Guid Id,
    Guid? OrganizacionId, string? OrganizacionNombre,
    Guid? CopropiedadId, string? CopropiedadNombre,
    Guid PlanId, string PlanNombre,
    CicloFacturacion Ciclo, EstadoSuscripcion Estado,
    DateOnly FechaInicio, int FechaAniversario,
    DateOnly FechaProximoCobro, DateOnly? FechaFinTrial,
    decimal CreditoAFavor, DateTimeOffset CreatedAt);

public record CrearSuscripcionRequest(
    Guid? OrganizacionId, Guid? CopropiedadId,
    Guid PlanId, CicloFacturacion Ciclo);

public record CambiarPlanRequest(Guid NuevoPlanId, string Justificacion);

public record CambiarEstadoSuscripcionRequest(EstadoSuscripcion NuevoEstado, string Justificacion);

// ----- Facturas -----
public record FacturaDto(
    Guid Id, Guid SuscripcionId,
    string? NumeroFactura, string? Cufe,
    DateOnly PeriodoDesde, DateOnly PeriodoHasta,
    decimal Subtotal, decimal Descuento, decimal Total,
    EstadoFactura Estado, DateTimeOffset FechaEmision,
    DateOnly FechaVencimiento, DateTimeOffset? FechaPago);

public record GenerarFacturaRequest(Guid SuscripcionId, DateOnly PeriodoDesde, DateOnly PeriodoHasta);

public record RegistrarPagoFacturaRequest(string? WompiTransactionId, string? NumeroFactura);

// ----- Historial -----
public record SuscripcionHistorialDto(
    Guid Id, Guid SuscripcionId, TipoEventoSuscripcion Tipo,
    OrigenEventoSuscripcion Origen, Guid? ActorId,
    Guid? PlanAnteriorId, Guid? PlanNuevoId,
    string? EstadoAnterior, string? EstadoNuevo,
    decimal? MontoProrrateo, decimal? CreditoGenerado,
    string? Notas, DateTimeOffset CreatedAt);

// ----- BillingConfig -----
public record BillingConfigDto(
    int DiasGracia, int DiaAlertaMora1, int DiaAlertaMora2,
    int DiaSuspension, int DiaAlertaCancelacion, int DiaCancelacion,
    int ReintentosCobro, string DiasEntreReintentos,
    int DiasPreavisoCobro, int RetencionDatosMeses, int RetencionFacturasAnios,
    decimal ImpuestoPct, string Moneda, ProveedorContable? ProveedorContable);

public record ActualizarBillingConfigRequest(
    int DiasGracia, int DiaAlertaMora1, int DiaAlertaMora2,
    int DiaSuspension, int DiaAlertaCancelacion, int DiaCancelacion,
    int ReintentosCobro, string DiasEntreReintentos,
    int DiasPreavisoCobro, int RetencionDatosMeses, int RetencionFacturasAnios,
    decimal ImpuestoPct, string Moneda, ProveedorContable? ProveedorContable);
