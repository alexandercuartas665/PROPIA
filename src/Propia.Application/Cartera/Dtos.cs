using Propia.Domain.Enums;

namespace Propia.Application.Cartera;

// ================== Tablero ==================

public record CarteraKpisDto(
    decimal TotalMoraCop,
    int UnidadesEnMora,
    int ConAcuerdoActivo,
    int EnJuridico,
    decimal Aging0_30,
    decimal Aging31_60,
    decimal Aging61_90,
    decimal AgingMas90,
    int UnidadesAging0_30,
    int UnidadesAging31_60,
    int UnidadesAging61_90,
    int UnidadesAgingMas90);

public record CarteraUnidadListaDto(
    Guid UnidadPrivadaId,
    string UnidadNumero,
    string? TorreNombre,
    string? PropietarioNombre,
    decimal SaldoCapital,
    decimal SaldoIntereses,
    decimal SaldoTotal,
    int DiasMora,
    Guid? EstadoGestionId,
    string? EstadoGestionNombre,
    string? EstadoGestionColor,
    bool TieneAcuerdoVigente);

public record CarteraTableroDto(
    CarteraKpisDto Kpis,
    IReadOnlyList<CarteraUnidadListaDto> UnidadesEnMora);

// ================== Ficha por unidad ==================

public record DeudaDetalleDto(
    Guid Id,
    DateOnly Periodo,
    string Concepto,
    decimal CapitalOriginal,
    decimal CapitalPendiente,
    decimal InteresAcumulado,
    decimal Total,
    DateOnly FechaVencimiento,
    int DiasMora,
    EstadoDeudaDetalle Estado);

public record CarteraUnidadDetalleDto(
    Guid UnidadPrivadaId,
    string UnidadNumero,
    string? TorreNombre,
    string? PropietarioNombre,
    decimal SaldoCapital,
    decimal SaldoIntereses,
    decimal SaldoTotal,
    int DiasMora,
    Guid? EstadoGestionId,
    string? EstadoGestionNombre,
    string? EstadoGestionColor,
    bool TieneAcuerdoVigente,
    IReadOnlyList<DeudaDetalleDto> Deudas,
    AcuerdoPagoDto? AcuerdoVigente,
    IReadOnlyList<CondonacionDto> Condonaciones,
    IReadOnlyList<PazSalvoDto> PazSalvos,
    IReadOnlyList<CarteraHistorialDto> Historial);

public record CarteraHistorialDto(
    TipoEventoCartera TipoEvento,
    string Descripcion,
    Guid RealizadoPorUsuarioId,
    DateTimeOffset OcurridoAt);

// ================== Acuerdos de pago ==================

public record AcuerdoPagoDto(
    Guid Id,
    Guid UnidadPrivadaId,
    string UnidadNumero,
    EstadoAcuerdoPago Estado,
    decimal MontoTotal,
    decimal CapitalIncluido,
    decimal InteresesIncluidos,
    decimal InteresesCondonados,
    int NumeroCuotas,
    DateOnly FechaPrimeraCuota,
    string? NotasAdmin,
    Guid? AcuerdoPadreId,
    DateTimeOffset? AceptacionAt,
    DateOnly? FechaExpiracion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AcuerdoCuotaDto> Cuotas);

public record AcuerdoCuotaDto(
    Guid Id,
    int NumeroCuota,
    DateOnly FechaVencimiento,
    decimal Monto,
    decimal Capital,
    decimal Intereses,
    EstadoCuotaAcuerdo Estado,
    DateOnly? FechaPago);

public record CrearAcuerdoRequest(
    Guid UnidadPrivadaId,
    int NumeroCuotas,
    DateOnly FechaPrimeraCuota,
    decimal InteresesCondonados,
    string? NotasAdmin);

public record AceptarAcuerdoRequest(string MetodoAutenticacion, string? Ip);

public record CancelarAcuerdoRequest(string Motivo);

public record RegistrarPagoAcuerdoRequest(Guid CuotaAcuerdoId, DateOnly FechaPago);

// ================== Condonaciones ==================

public record CondonacionDto(
    Guid Id,
    TipoCondonacion Tipo,
    decimal MontoCondonado,
    string Motivo,
    Guid AutorizadoPorUsuarioId,
    DateTimeOffset CreatedAt);

public record AplicarCondonacionRequest(
    Guid UnidadPrivadaId,
    TipoCondonacion Tipo,
    decimal MontoCondonado,
    string Motivo,
    string? DocumentoSoporteUrl);

// ================== Paz y salvo ==================

public record PazSalvoDto(
    Guid Id,
    TipoPazSalvo Tipo,
    EstadoPazSalvo Estado,
    string? Condiciones,
    DateTimeOffset FechaEmision,
    DateOnly FechaVencimiento,
    EmisionPazSalvo EmisionTipo,
    string CodigoVerificacion,
    Guid EmitidoPorUsuarioId,
    DateTimeOffset? FechaAnulacion,
    string? MotivoAnulacion);

public record EmitirPazSalvoRequest(
    Guid UnidadPrivadaId,
    EmisionPazSalvo EmisionTipo,
    string? CondicionesManual);

public record AnularPazSalvoRequest(string Motivo);

// ================== Estados de gestion ==================

public record EstadoCarteraDto(Guid Id, string Nombre, int Orden, int DiasAlerta, string? Color, bool EsInicial, bool Activo);
public record CambiarEstadoUnidadRequest(Guid EstadoId, string Motivo);

// ================== Mi cuota (vista residente) ==================

public record MiCuotaDto(
    string UnidadNumero,
    decimal SaldoTotal,
    decimal SaldoCapital,
    decimal SaldoIntereses,
    bool TieneAcuerdoVigente,
    IReadOnlyList<DeudaDetalleDto> Deudas,
    AcuerdoPagoDto? AcuerdoVigente);

// ================== Configuracion ==================

public record CarteraConfigDto(
    ModoCalculoIntereses ModoCalculoIntereses,
    ReglaImputacion ReglaImputacion,
    bool PazSalvoAutomatico,
    bool PazSalvoCondicionado,
    bool SolicitudResidente,
    int ValidezPazSalvoDias,
    string? MensajePazSalvo,
    int PlazoAceptacionDias,
    int GraciaCuotaAcuerdo,
    decimal TasaMoraMensual,
    int PeriodoGraciaDias);
