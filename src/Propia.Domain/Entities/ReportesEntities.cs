using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Categoria del catalogo de reportes. Spec 2.16 seccion 4.1 + RN-11.
///
/// - TenantId NULL -> categoria global PropIA (8 base seedadas: Financiero, Cartera,
///   PQRSD, Gestion Operativa, Mantenimiento, Comunicaciones, Reservas, Porteria).
/// - TenantId con valor -> categoria custom de la copropiedad (Fase 2).
/// </summary>
public class ReporteCategoria : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Icono { get; set; }
    public string? Color { get; set; }

    /// <summary>Codigo del modulo productor: '2.6', '2.7', '2.10', etc.</summary>
    public string ModuloOrigen { get; set; } = string.Empty;

    public int Orden { get; set; }
    public bool EsActiva { get; set; } = true;
}

/// <summary>
/// Reporte prediseñado dentro del catalogo. RN-11: extensible por modulo.
/// Cada modulo productor siembra sus reportes via migracion.
/// </summary>
public class ReporteCatalogo : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid CategoriaId { get; set; }
    public ReporteCategoria? Categoria { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Modulo productor del cual se consulta la capa de indicadores.</summary>
    public string ModuloOrigen { get; set; } = string.Empty;

    /// <summary>
    /// Clave logica del reporte (ej. "financiero.ejecucion_presupuestal",
    /// "cartera.aging"). Permite al engine resolverlo sin depender del Id.
    /// </summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>JSON con array de audiencias: ["ADMINISTRADOR","CONSEJO","PROPIETARIO"].</summary>
    public string AudienciasJson { get; set; } = "[\"ADMINISTRADOR\"]";

    /// <summary>JSON con definicion de filtros disponibles. Reservado para Fase 2.</summary>
    public string? FiltrosConfigJson { get; set; }

    public bool EsActivo { get; set; } = true;
    public bool EsSistema { get; set; } = true;
    public int Orden { get; set; }
}

/// <summary>
/// Historial de cada reporte generado (manual, programado o IA). Spec 2.16 seccion 9.
/// RN-03: guardado automatico, archivos expiran a los 30 dias.
/// </summary>
public class ReporteGenerado : TenantEntity
{
    /// <summary>FK al catalogo. NULL si origen=IA libre (Fase 2).</summary>
    public Guid? ReporteCatalogoId { get; set; }
    public ReporteCatalogo? ReporteCatalogo { get; set; }

    public string NombreReporte { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;

    public DateOnly PeriodoInicio { get; set; }
    public DateOnly PeriodoFin { get; set; }

    /// <summary>JSON con los filtros aplicados al generar - permite regeneracion identica.</summary>
    public string? FiltrosAplicadosJson { get; set; }

    public OrigenReporte Origen { get; set; } = OrigenReporte.Manual;

    /// <summary>Prompt en lenguaje natural si origen=IA. RN-09 trazabilidad.</summary>
    public string? PromptIa { get; set; }

    public bool CompartidoConsejo { get; set; }
    public DateTimeOffset? CompartidoAt { get; set; }
    public Guid? CompartidoPorUsuarioId { get; set; }

    public EstadoReporteGenerado Estado { get; set; } = EstadoReporteGenerado.Generando;
    public string? ErrorMensaje { get; set; }

    /// <summary>Resultado serializado (JSON con datos del reporte + metadata).</summary>
    public string? ResultadoJson { get; set; }

    public string? UrlPdf { get; set; }
    public string? UrlExcel { get; set; }
    public DateTimeOffset? UrlExpiracion { get; set; }

    /// <summary>NULL si origen=Programado.</summary>
    public Guid? GeneradoPorUsuarioId { get; set; }
}

/// <summary>
/// Programacion de envio automatico. Spec 2.16 seccion 6.
/// RN-06: 2.16 solo guarda la config; T.2 (no construido) hace el despacho real.
/// </summary>
public class ReporteProgramacion : TenantEntity
{
    public Guid ReporteCatalogoId { get; set; }
    public ReporteCatalogo? ReporteCatalogo { get; set; }

    /// <summary>Nombre descriptivo opcional ("Informe financiero mensual").</summary>
    public string? Nombre { get; set; }

    public FrecuenciaProgramacion Frecuencia { get; set; }

    /// <summary>Dia del mes (1-28). Limite a 28 para evitar problemas con febrero.</summary>
    public int DiaEnvio { get; set; }

    public PeriodoQueCubre PeriodoQueCubre { get; set; } = PeriodoQueCubre.MesAnterior;

    public string? FiltrosAplicadosJson { get; set; }

    public FormatoReporte Formato { get; set; } = FormatoReporte.Pdf;

    /// <summary>JSON array con ["EMAIL","WHATSAPP"].</summary>
    public string CanalesJson { get; set; } = "[\"EMAIL\"]";

    public EstadoProgramacion Estado { get; set; } = EstadoProgramacion.Activa;

    public DateOnly? ProximoEnvio { get; set; }
    public DateTimeOffset? UltimoEnvio { get; set; }
    public bool? UltimoEnvioExitoso { get; set; }

    public Guid CreadoPorUsuarioId { get; set; }

    public ICollection<ReporteProgramacionDestinatario> Destinatarios { get; set; } = new List<ReporteProgramacionDestinatario>();
}

/// <summary>Destinatario de una programacion. Spec 2.16 seccion 14.</summary>
public class ReporteProgramacionDestinatario : TenantEntity
{
    public Guid ProgramacionId { get; set; }
    public ReporteProgramacion? Programacion { get; set; }

    /// <summary>FK a persona registrada en PropIA.</summary>
    public Guid? PersonaId { get; set; }
    public Persona? Persona { get; set; }

    /// <summary>Email externo (si no es persona registrada).</summary>
    public string? EmailExterno { get; set; }

    /// <summary>WhatsApp externo (si no es persona registrada).</summary>
    public string? WhatsappExterno { get; set; }
}

/// <summary>
/// Configuracion de semaforos para la vista del consejo. Spec 2.16 RN-13.
/// Una fila por (tenant, indicador). Si no existe -> usar defaults PropIA.
/// </summary>
public class ReporteSemaforoConfig : TenantEntity
{
    /// <summary>Clave del indicador: "recaudo_pct", "mora_total", "pqrsd_vencidas", etc.</summary>
    public string IndicadorKey { get; set; } = string.Empty;

    public decimal UmbralAmarillo { get; set; }
    public decimal UmbralRojo { get; set; }

    /// <summary>true: mas es mejor (recaudo); false: menos es mejor (mora).</summary>
    public bool EsAscendente { get; set; } = true;

    public Guid ActualizadoPorUsuarioId { get; set; }
}
