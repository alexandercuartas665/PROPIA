using Propia.Domain.Enums;

namespace Propia.Application.ServiciosPublicos;

// ----- Cuentas -----
public record CuentaServicioDto(
    Guid Id, TipoServicioPublico Tipo, string Alias, string Prestador,
    string? NumeroCuenta, string? MetodoPago, string? UnidadMedida,
    int UmbralAlertaPct, bool Activa,
    bool TieneAlerta, decimal? UltimoValor, decimal? VariacionPct);

public record CrearCuentaServicioRequest(
    TipoServicioPublico Tipo, string Alias, string Prestador,
    string? NumeroCuenta, string? MetodoPago, string? UnidadMedida, int UmbralAlertaPct);

// ----- Registros de consumo -----
public record RegistroConsumoDto(
    Guid Id, Guid CuentaServicioId, int Anio, int Mes, string PeriodoLabel,
    decimal Consumo, decimal Valor, EstadoRegistroConsumo Estado,
    decimal? VariacionPct, bool Alerta, string? NotaAdmin);

public record CrearRegistroConsumoRequest(
    int Anio, int Mes, decimal Consumo, decimal Valor, EstadoRegistroConsumo Estado, string? NotaAdmin);

// ----- Reclamaciones -----
public record ReclamacionServicioDto(
    Guid Id, Guid CuentaServicioId, string Motivo, string? Radicado,
    string? Descripcion, EstadoReclamacionServicio Estado, DateOnly Fecha);

public record CrearReclamacionRequest(string Motivo, string? Radicado, string? Descripcion);

// ----- Detalle 360 de una cuenta -----
public record CuentaServicioDetalleDto(
    CuentaServicioDto Cuenta,
    IReadOnlyList<RegistroConsumoDto> Registros,
    IReadOnlyList<ReclamacionServicioDto> Reclamaciones,
    decimal? PromedioConsumo);

// ----- Resumen / KPIs -----
public record ServiciosPublicosResumenDto(
    int CuentasActivas, decimal GastoUltimoMes, int CuentasConAlerta, int ReclamacionesAbiertas);
