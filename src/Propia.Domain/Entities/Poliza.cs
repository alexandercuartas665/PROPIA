using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Poliza de seguro de la copropiedad (modulo Seguros, Ola 4). Entidad dedicada (no es un
/// ContratoServicio): tiene aseguradora y corredor del Directorio, cobertura y reclamaciones.
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class Poliza : TenantEntity
{
    /// <summary>Numero de la poliza (idealmente extraido por OCR/IA al cargar el PDF).</summary>
    public string? NumeroPoliza { get; set; }

    // ----- Aseguradora (tercero del Directorio: persona o empresa) -----
    public Guid? AseguradoraPersonaId { get; set; }
    public Guid? AseguradoraEmpresaId { get; set; }
    /// <summary>Snapshot del nombre de la aseguradora (para las que se cargaron a mano/OCR).</summary>
    public string Aseguradora { get; set; } = string.Empty;

    // ----- Corredor de seguros (tercero del Directorio: persona o empresa) -----
    public Guid? CorredorPersonaId { get; set; }
    public Guid? CorredorEmpresaId { get; set; }
    public string? Corredor { get; set; }

    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }

    /// <summary>Valor asegurado / valor de la poliza (COP).</summary>
    public decimal? ValorPoliza { get; set; }

    /// <summary>Forma de pago: cantidad de cuotas.</summary>
    public int? FormaPagoCuotas { get; set; }
    public bool PagoMensual { get; set; }

    /// <summary>Condiciones de cobertura (texto largo).</summary>
    public string? Cobertura { get; set; }

    /// <summary>Si la poliza incluye zonas y unidades privadas.</summary>
    public bool IncluyeZonasUnidades { get; set; }

    /// <summary>Valores agregados de la poliza (texto largo).</summary>
    public string? ValoresAgregados { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>Expediente (modulo 2.15) conectado con los documentos de la poliza.</summary>
    public Guid? ExpedienteId { get; set; }

    /// <summary>Key en el blob storage (R2) del PDF ORIGEN del que se extrajeron los datos con IA/OCR.
    /// Se descarga via endpoint gateado (no URL publica). Null si la poliza no se creo desde un PDF.</summary>
    public string? PdfOrigenKey { get; set; }

    /// <summary>Ultimo umbral de vencimiento ya notificado por el job (20 o 10). Null = ninguno.</summary>
    public int? AlertaVencimientoPctNotificado { get; set; }

    public ICollection<PolizaReclamacion> Reclamaciones { get; set; } = new List<PolizaReclamacion>();
}

/// <summary>Definicion de un campo personalizado (EAV) para las polizas del tenant (como ContratoCampo).</summary>
public class PolizaCampo : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Valor de un campo personalizado para una poliza concreta (EAV).</summary>
public class PolizaCampoValor : TenantEntity
{
    public Guid PolizaId { get; set; }
    public Guid PolizaCampoId { get; set; }
    public string? Valor { get; set; }
}

/// <summary>
/// Reclamacion (siniestro) sobre una poliza (Ola 5). Se lleva el historial de montos reclamados y
/// reconocidos, y el estado (Vigente/Cerrada). Es TenantEntity - aislada por tenant_id.
/// </summary>
public class PolizaReclamacion : TenantEntity
{
    public Guid PolizaId { get; set; }
    public Poliza? Poliza { get; set; }

    /// <summary>Fecha de la reclamacion.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Monto que se va a reclamar.</summary>
    public decimal MontoReclamado { get; set; }

    /// <summary>Que se va a reclamar (descripcion del siniestro).</summary>
    public string Descripcion { get; set; } = string.Empty;

    public EstadoReclamacion Estado { get; set; } = EstadoReclamacion.Vigente;

    /// <summary>Monto reconocido por la aseguradora (se pide al cerrar).</summary>
    public decimal? MontoReconocido { get; set; }
    public DateTimeOffset? FechaCierre { get; set; }

    /// <summary>Expediente con los soportes de la reclamacion (opcional).</summary>
    public Guid? ExpedienteId { get; set; }
}
