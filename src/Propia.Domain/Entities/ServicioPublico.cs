using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Servicio publico domiciliario de la copropiedad (medidor o contrato ante un prestador). Modulo 2.17
/// Servicios publicos. NumeroCuenta se muestra como "Referencia del contrato": es el numero que imprime el
/// recibo y con el que la IA reconoce que un recibo es de la copropiedad (se compara normalizado).
/// </summary>
public class CuentaServicioPublico : TenantEntity
{
    public TipoServicioPublico Tipo { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Prestador { get; set; } = string.Empty;
    /// <summary>Referencia del contrato tal como se escribio o se leyo del recibo.</summary>
    public string? NumeroCuenta { get; set; }
    /// <summary>NumeroCuenta sin espacios, puntos ni guiones (mayusculas). Unica por copropiedad.</summary>
    public string? ReferenciaNormalizada { get; set; }
    public string? MetodoPago { get; set; }
    /// <summary>Unidad de medida del consumo: kWh, m3, min, GB, etc.</summary>
    public string? UnidadMedida { get; set; }
    /// <summary>Umbral de variacion (%) contra el periodo anterior. Solo se usa si el servicio no tiene topes.</summary>
    public int UmbralAlertaPct { get; set; } = 25;
    public bool Activa { get; set; } = true;

    public PeriodicidadServicio Periodicidad { get; set; } = PeriodicidadServicio.Mensual;
    /// <summary>Dia del mes en que se paga (1-31). Si el mes no lo tiene, cuenta el ultimo dia.</summary>
    public int? DiaPago { get; set; }
    /// <summary>Topes para alertas (opcionales): un recibo fuera de [min, max] se marca y alerta.</summary>
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public decimal? ConsumoMin { get; set; }
    public decimal? ConsumoMax { get; set; }

    public List<RegistroConsumoServicio> Registros { get; set; } = new();
    public List<ReclamacionServicio> Reclamaciones { get; set; } = new();
}

/// <summary>Registro de consumo/factura de un periodo para una cuenta de servicio.</summary>
public class RegistroConsumoServicio : TenantEntity
{
    public Guid CuentaServicioId { get; set; }
    public CuentaServicioPublico? Cuenta { get; set; }

    public int Anio { get; set; }
    public int Mes { get; set; }                 // 1-12
    public decimal Consumo { get; set; }
    public decimal Valor { get; set; }
    public EstadoRegistroConsumo Estado { get; set; } = EstadoRegistroConsumo.Confirmado;
    public string? NotaAdmin { get; set; }

    public DateOnly? FechaLimitePago { get; set; }
    public decimal? LecturaAnterior { get; set; }
    public decimal? LecturaActual { get; set; }
    public OrigenRegistroConsumo Origen { get; set; } = OrigenRegistroConsumo.Manual;
    /// <summary>Recibo original (PDF o foto) en el blob storage del tenant.</summary>
    public string? ArchivoKey { get; set; }
    public string? ArchivoNombre { get; set; }

    /// <summary>Desglose de cobros del recibo (lo que la IA separa del total).</summary>
    public List<RegistroConsumoConcepto> Conceptos { get; set; } = new();
}

/// <summary>Un cobro del desglose de un recibo ("Consumo de energia", "Alumbrado publico"...).</summary>
public class RegistroConsumoConcepto : TenantEntity
{
    public Guid RegistroConsumoId { get; set; }
    public RegistroConsumoServicio? Registro { get; set; }
    public int Orden { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}

/// <summary>Reclamacion radicada ante el prestador por un cobro/consumo de una cuenta.</summary>
public class ReclamacionServicio : TenantEntity
{
    public Guid CuentaServicioId { get; set; }
    public CuentaServicioPublico? Cuenta { get; set; }

    public string Motivo { get; set; } = string.Empty;
    public string? Radicado { get; set; }
    public string? Descripcion { get; set; }
    public EstadoReclamacionServicio Estado { get; set; } = EstadoReclamacionServicio.Radicada;
    public DateOnly Fecha { get; set; }
}

/// <summary>Campo propio de los servicios publicos (columna "+" de Configuracion). Patron de Plantillas.</summary>
public class ServicioPublicoCampoDefinicion : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
    public string? Descripcion { get; set; }
}

/// <summary>Valor de un campo propio para un servicio publico (EAV). Se borra con el servicio o la definicion.</summary>
public class ServicioPublicoCampoValor : TenantEntity
{
    public Guid DefinicionId { get; set; }
    public ServicioPublicoCampoDefinicion? Definicion { get; set; }
    public Guid CuentaServicioId { get; set; }
    public CuentaServicioPublico? Cuenta { get; set; }
    public string? Valor { get; set; }
}
