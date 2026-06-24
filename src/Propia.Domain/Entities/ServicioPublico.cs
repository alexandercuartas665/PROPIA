using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Cuenta de servicio publico domiciliario de la copropiedad (medidor o numero de cuenta
/// ante un prestador). Modulo 2.17 Servicios publicos.
/// </summary>
public class CuentaServicioPublico : TenantEntity
{
    public TipoServicioPublico Tipo { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Prestador { get; set; } = string.Empty;
    public string? NumeroCuenta { get; set; }
    public string? MetodoPago { get; set; }
    /// <summary>Unidad de medida del consumo: kWh, m3, min, GB, etc.</summary>
    public string? UnidadMedida { get; set; }
    /// <summary>Umbral de desviacion (%) que dispara alerta sobre el promedio historico.</summary>
    public int UmbralAlertaPct { get; set; } = 25;
    public bool Activa { get; set; } = true;

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
