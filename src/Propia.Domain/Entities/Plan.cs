using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Plan comercial del SaaS. Configurable desde la consola sin necesidad de despliegue.
/// Entidad GLOBAL - sin tenant_id. Definido en modulo 0.2 Billing y Suscripciones.
/// </summary>
public class Plan : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Fee fijo mensual o anual por organizacion/copropiedad (COP).</summary>
    public decimal FeeBase { get; set; }

    /// <summary>Fee por unidad privada activa (COP).</summary>
    public decimal FeeVariablePorUnidad { get; set; }

    public bool CicloMensual { get; set; } = true;
    public bool CicloAnual { get; set; } = true;

    /// <summary>Porcentaje de descuento sobre el total cuando se paga anual.</summary>
    public decimal DescuentoAnualPct { get; set; }

    /// <summary>Maximo de unidades privadas. null = ilimitado.</summary>
    public int? LimiteUnidades { get; set; }

    /// <summary>Maximo de usuarios en equipo. null = ilimitado.</summary>
    public int? LimiteUsuarios { get; set; }

    /// <summary>GB de storage incluidos. null = ilimitado.</summary>
    public int? LimiteStorageGb { get; set; }

    /// <summary>Maximo de lineas de WhatsApp (canal Evolution). null = ilimitado.</summary>
    public int? LimiteLineasWhatsapp { get; set; }

    /// <summary>Maximo de llamadas a la IA por mes. null = ilimitado.</summary>
    public int? LimiteLlamadasIaMensual { get; set; }

    /// <summary>Dias de trial antes del primer cobro. 0 = sin trial.</summary>
    public int DiasTrial { get; set; }

    /// <summary>JSON array de modulos incluidos en este plan (ej. ["2.1","2.3","2.10"]).</summary>
    public string ModulosIncluidos { get; set; } = "[]";

    public EstadoPlan Estado { get; set; } = EstadoPlan.Activo;
}
