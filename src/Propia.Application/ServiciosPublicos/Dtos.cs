using Propia.Domain.Enums;

namespace Propia.Application.ServiciosPublicos;

// ----- Cuentas (servicios publicos) -----
// Los campos nuevos van al final con valor por defecto: la tool MCP y los consumidores viejos siguen igual.
public record CuentaServicioDto(
    Guid Id, TipoServicioPublico Tipo, string Alias, string Prestador,
    string? NumeroCuenta, string? MetodoPago, string? UnidadMedida,
    int UmbralAlertaPct, bool Activa,
    bool TieneAlerta, decimal? UltimoValor, decimal? VariacionPct,
    PeriodicidadServicio Periodicidad = PeriodicidadServicio.Mensual, int? DiaPago = null,
    decimal? ValorMin = null, decimal? ValorMax = null, decimal? ConsumoMin = null, decimal? ConsumoMax = null,
    int? UltimoAnio = null, int? UltimoMes = null, decimal? UltimoConsumo = null,
    DateOnly? ProximoPago = null, RangoRecibo Rango = RangoRecibo.Nada, IReadOnlyList<string>? Motivos = null);

public record CrearCuentaServicioRequest(
    TipoServicioPublico Tipo, string Alias, string Prestador,
    string? NumeroCuenta, string? MetodoPago, string? UnidadMedida, int UmbralAlertaPct,
    PeriodicidadServicio? Periodicidad = null, int? DiaPago = null,
    decimal? ValorMin = null, decimal? ValorMax = null, decimal? ConsumoMin = null, decimal? ConsumoMax = null);

/// <summary>PUT MERGE: solo se aplica lo que viene con valor. Para BORRAR un dato opcional (referencia, topes,
/// dia de pago, metodo, unidad) se nombra su clave en Limpiar ("numeroCuenta", "valorMin", ...).</summary>
public record ActualizarCuentaServicioRequest(
    TipoServicioPublico? Tipo = null, string? Alias = null, string? Prestador = null,
    string? NumeroCuenta = null, string? MetodoPago = null, string? UnidadMedida = null,
    PeriodicidadServicio? Periodicidad = null, int? DiaPago = null,
    decimal? ValorMin = null, decimal? ValorMax = null, decimal? ConsumoMin = null, decimal? ConsumoMax = null,
    bool? Activa = null, IReadOnlyList<string>? Limpiar = null);

// ----- Registros de consumo -----
public record ConceptoReciboDto(string Concepto, decimal Valor);

public record RegistroConsumoDto(
    Guid Id, Guid CuentaServicioId, int Anio, int Mes, string PeriodoLabel,
    decimal Consumo, decimal Valor, EstadoRegistroConsumo Estado,
    decimal? VariacionPct, bool Alerta, string? NotaAdmin,
    DateOnly? FechaLimitePago = null, decimal? LecturaAnterior = null, decimal? LecturaActual = null,
    OrigenRegistroConsumo Origen = OrigenRegistroConsumo.Manual, string? ArchivoKey = null, string? ArchivoNombre = null,
    IReadOnlyList<ConceptoReciboDto>? Conceptos = null, RangoRecibo Rango = RangoRecibo.Nada, IReadOnlyList<string>? Motivos = null,
    /// <summary>URL para abrir el recibo original (resuelta por el storage: /uploads en dev, R2 en prod).</summary>
    string? ArchivoUrl = null);

public record CrearRegistroConsumoRequest(
    int Anio, int Mes, decimal Consumo, decimal Valor, EstadoRegistroConsumo Estado, string? NotaAdmin,
    DateOnly? FechaLimitePago = null, decimal? LecturaAnterior = null, decimal? LecturaActual = null);

// ----- Recibos leidos con IA (evaluar NO guarda; confirmar guarda) -----
/// <summary>Campos leidos del recibo por el Agente Documental (perfil recibo_servicio), ya normalizados.</summary>
public record ReciboLeidoRequest(
    string? Referencia, string? Prestador = null, string? NitPrestador = null, string? Titular = null,
    string? DireccionSuministro = null, string? TipoServicio = null,
    DateOnly? PeriodoDesde = null, DateOnly? PeriodoHasta = null, DateOnly? FechaExpedicion = null, DateOnly? FechaLimitePago = null,
    decimal? LecturaAnterior = null, decimal? LecturaActual = null, decimal? Consumo = null, string? UnidadMedida = null,
    decimal? ValorPeriodo = null, decimal? SaldoAnterior = null, decimal? ValorTotal = null,
    IReadOnlyList<ConceptoReciboDto>? Conceptos = null,
    string? ArchivoKey = null, string? ArchivoNombre = null,
    /// <summary>Solo confirmar: la cuenta que devolvio evaluar.</summary>
    Guid? CuentaId = null);

/// <summary>Resultado de validar un recibo contra los servicios configurados (paso "Validando contra tus servicios").</summary>
public record EvaluacionReciboDto(
    Guid? CuentaId, string? CuentaAlias, string? Referencia, string? ReferenciaNormalizada,
    int? Anio, int? Mes, string? PeriodoLabel,
    bool Duplicado, RangoRecibo Rango, IReadOnlyList<string> Motivos,
    decimal? Valor, decimal? ValorMin, decimal? ValorMax, string? Unidad,
    bool CuadraDesglose, bool CuentaInactiva);

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
    int CuentasActivas, decimal GastoUltimoMes, int CuentasConAlerta, int ReclamacionesAbiertas,
    int RecibosLeidosIa = 0);

// ----- Campos propios (columna "+" de Configuracion). Misma forma que CampoDinDef de la Web. -----
public sealed record ServicioPublicoCampoDefinicionDto(Guid Id, string Label, int Orden, TipoCampoTablero Tipo, string? Opciones, string? Descripcion);
public sealed record ServicioPublicoCampoValorFlatDto(Guid CuentaId, Guid DefinicionId, string? Valor);
