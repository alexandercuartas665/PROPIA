using Propia.Domain.Enums;

namespace Propia.Application.Presupuesto;

// ============================ Presupuesto + Rubros ============================

public record PresupuestoDto(
    Guid Id, string Nombre,
    DateOnly VigenciaInicio, DateOnly VigenciaFin,
    EstadoPresupuesto Estado, decimal MontoTotal,
    TipoAprobacion? AprobacionTipo, DateOnly? AprobacionFecha,
    int CantidadRubrosActivos, int CantidadLiquidaciones);

public record PresupuestoDetalleDto(
    Guid Id, string Nombre,
    DateOnly VigenciaInicio, DateOnly VigenciaFin,
    EstadoPresupuesto Estado, decimal MontoTotal,
    TipoAprobacion? AprobacionTipo, string? AprobacionActaUrl, DateOnly? AprobacionFecha,
    string? Notas,
    IReadOnlyList<RubroDto> Rubros,
    decimal CuotaProyectadaPromedioMes,
    decimal MontoFondoImprevistos,
    decimal MontoFondoImprevistosMinimoLegal,  // 1% segun Art. 39 Ley 675
    bool CumpleMinimoFondoImprevistos);

public record RubroDto(
    Guid Id, string Codigo, string Nombre,
    decimal MontoAnual, BaseLiquidacion BaseLiquidacion,
    bool EsFondoImprevistos, bool EsObligatorio, bool Activo, int Orden,
    string? NotasInternas);

public record CrearPresupuestoRequest(
    string Nombre, DateOnly VigenciaInicio, DateOnly VigenciaFin,
    bool IncluirCatalogoBase);

public record ActualizarRubroRequest(
    string Nombre, decimal MontoAnual, BaseLiquidacion BaseLiquidacion,
    bool Activo, int Orden, string? NotasInternas);

public record AgregarRubroPersonalizadoRequest(string Nombre, decimal MontoAnual, BaseLiquidacion BaseLiquidacion);

public record AprobarPresupuestoRequest(TipoAprobacion Tipo, DateOnly Fecha, string? ActaUrl, Guid? AsambleaId);

// ============================ Liquidacion ============================

public record LiquidacionDto(
    Guid Id, Guid PresupuestoId, string PresupuestoNombre,
    DateOnly Periodo, EstadoLiquidacion Estado, decimal MontoTotal,
    DateTimeOffset EmitidaAt,
    int CantidadUnidades, int CantidadPagadas);

public record LiquidacionUnidadDto(
    Guid Id, Guid LiquidacionId, DateOnly Periodo,
    Guid UnidadId, string UnidadNumero, string? TorreNombre,
    decimal Monto, EstadoPagoLiquidacion EstadoPago,
    IReadOnlyList<RenglonDesgloseDto> Desglose);

public record RenglonDesgloseDto(string RubroCodigo, string RubroNombre, decimal Monto, decimal Coeficiente);

public record EmitirLiquidacionRequest(Guid PresupuestoId, DateOnly Periodo);

// ============================ Pagos ============================

public record PagoDto(
    Guid Id, Guid? LiquidacionUnidadId, DateOnly? Periodo,
    Guid UnidadId, string UnidadNumero,
    TipoPago Tipo, CanalPago Canal, decimal Monto,
    DateTimeOffset? FechaPago, EstadoPago Estado,
    bool EsManual, string? ReferenciaExterna, string? Notas);

public record RegistrarPagoManualRequest(
    Guid LiquidacionUnidadId,
    CanalPago Canal, decimal Monto, DateOnly FechaPago,
    string? ReferenciaExterna, string? Notas);

// ============================ Cuotas extraordinarias ============================

public record CuotaExtraordinariaDto(
    Guid Id, string Nombre, string Proposito,
    decimal MontoTotal, FormaRecaudo FormaRecaudo, int? NumeroCuotas,
    BaseLiquidacion BaseLiquidacion,
    EstadoCuotaExtraordinaria Estado,
    DateOnly? FechaInicioRecaudo, DateOnly? FechaFinRecaudo,
    decimal MontoRecaudado);

public record CrearCuotaExtraordinariaRequest(
    string Nombre, string Proposito, decimal MontoTotal,
    FormaRecaudo FormaRecaudo, int? NumeroCuotas,
    BaseLiquidacion BaseLiquidacion);

public record AprobarCuotaExtraordinariaRequest(
    TipoAprobacion Tipo, string? ActaUrl, Guid? AsambleaId,
    DateOnly FechaInicioRecaudo, DateOnly? FechaFinRecaudo);

// ============================ Panel de recaudo ============================

public record RecaudoResumenDto(
    DateOnly Periodo,
    decimal Esperado, decimal Recibido, decimal Pendiente,
    decimal PorcentajeRecaudo,
    int UnidadesTotales, int UnidadesPagadas, int UnidadesPendientes, int UnidadesVencidas);

public record UnidadRecaudoDto(
    Guid UnidadId, string UnidadNumero, string? TorreNombre,
    string? PropietarioNombre,
    decimal MontoCuota, EstadoPagoLiquidacion EstadoPago,
    Guid? LiquidacionUnidadId);

// ============================ Vista residente ============================

public record MiCuotaDto(
    Guid? LiquidacionUnidadId, DateOnly? Periodo,
    Guid UnidadId, string UnidadNumero, string? TorreNombre,
    decimal MontoTotal, EstadoPagoLiquidacion? EstadoPago,
    DateOnly? FechaVencimiento,
    IReadOnlyList<RenglonDesgloseDto> Desglose,
    IReadOnlyList<CuotaExtraordinariaPendienteDto> CuotasExtraordinarias);

public record CuotaExtraordinariaPendienteDto(
    Guid Id, string Nombre, decimal MontoMiUnidad, int? CuotaActual, int? NumeroCuotas);

// ============================ Auditoria ============================

public record AuditPresupuestoDto(
    Guid Id, DateTimeOffset CreatedAt,
    string Entidad, Guid EntidadId, string Accion,
    Guid UsuarioId, string? ValorAnterior, string? ValorNuevo);
