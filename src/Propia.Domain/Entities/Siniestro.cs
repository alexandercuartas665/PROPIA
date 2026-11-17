using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Siniestro de la copropiedad (modulo Siniestros). Un evento de dano o perdida que se gestiona,
/// con o sin poliza: elemento afectado (equipo, zona comun, unidad u otro), estado del tramite con la
/// aseguradora y montos. Coexiste con PolizaReclamacion (decision de Alex 2026-11-16).
/// Es TenantEntity - aislado por tenant_id (HasQueryFilter + RLS FORCE).
/// </summary>
public class Siniestro : TenantEntity
{
    /// <summary>Consecutivo por copropiedad y anio: SIN-AAAA-NNNN.</summary>
    public string Numero { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Clave de la opcion de la lista por copropiedad "siniestro.tipo" (null = sin tipo).</summary>
    public string? TipoClave { get; set; }

    public DateOnly FechaOcurrencia { get; set; }
    public DateOnly? FechaReporte { get; set; }

    // ----- Elemento afectado -----
    public ElementoSiniestro ElementoTipo { get; set; } = ElementoSiniestro.Ninguno;
    /// <summary>Id del EquipoActivo / ZonaComun / UnidadPrivada segun ElementoTipo (vinculo logico, sin FK).</summary>
    public Guid? ElementoId { get; set; }
    /// <summary>Descripcion libre cuando ElementoTipo = Otro.</summary>
    public string? ElementoOtro { get; set; }

    // ----- Poliza y aseguradora (opcional) -----
    public Guid? PolizaId { get; set; }
    public Poliza? Poliza { get; set; }
    /// <summary>Numero o radicado de la reclamacion en la aseguradora.</summary>
    public string? NumeroReclamacion { get; set; }

    /// <summary>Responsable (persona del Directorio vinculada a la copropiedad). Opcional.</summary>
    public Guid? ResponsablePersonaId { get; set; }

    // ----- Montos (COP) -----
    public decimal? MontoEstimado { get; set; }
    public decimal? MontoReclamado { get; set; }
    public decimal? Deducible { get; set; }
    public decimal? MontoReconocido { get; set; }
    public DateOnly? FechaPago { get; set; }

    public Guid EstadoId { get; set; }
    public SiniestroEstado? Estado { get; set; }
    /// <summary>Se fija al pasar a un estado con clave "cerrado"; se limpia al salir de el.</summary>
    public DateTimeOffset? FechaCierre { get; set; }

    /// <summary>Tarea de reparacion vinculada (S3). Opcional.</summary>
    public Guid? TareaId { get; set; }
}

/// <summary>
/// Estado (columna) del tramite de los siniestros de la copropiedad. Configurable por tenant, sembrado con
/// 6 estados base que llevan Clave (ver EstadoSiniestroClave); los propios no la tienen. Molde ContratoEtapa.
/// </summary>
public class SiniestroEstado : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    /// <summary>Color hex de la columna/pill (ej. "#22C55E").</summary>
    public string? Color { get; set; }
    /// <summary>Inactivo = no elegible en altas ni cambios nuevos; los siniestros que ya lo tienen lo conservan.</summary>
    public bool Activo { get; set; } = true;
    /// <summary>Clave estable de un estado base (null = estado propio de la copropiedad).</summary>
    public string? Clave { get; set; }
}

/// <summary>Documento de un siniestro (fotos, informe tecnico, cotizaciones, carta de la aseguradora).</summary>
public class SiniestroDocumento : TenantEntity
{
    public Guid SiniestroId { get; set; }
    public Siniestro? Siniestro { get; set; }
    public string Nombre { get; set; } = null!;
    public string Url { get; set; } = null!;
}

/// <summary>
/// Hilo de actividad de un siniestro: comentarios de las personas y eventos del sistema (cambio de estado).
/// </summary>
public class SiniestroComentario : TenantEntity
{
    public Guid SiniestroId { get; set; }
    public Siniestro? Siniestro { get; set; }
    public string Texto { get; set; } = string.Empty;
    public Guid? AutorUsuarioId { get; set; }
    public string? AutorNombre { get; set; }
    /// <summary>true = evento generado por el sistema (cambio de estado), no un comentario escrito.</summary>
    public bool EsSistema { get; set; }
}

/// <summary>Definicion de un campo personalizado (EAV) de los siniestros del tenant (molde PolizaCampo).</summary>
public class SiniestroCampo : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;
    public string? Opciones { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Valor de un campo personalizado para un siniestro concreto (EAV).</summary>
public class SiniestroCampoValor : TenantEntity
{
    public Guid SiniestroId { get; set; }
    public Guid SiniestroCampoId { get; set; }
    public string? Valor { get; set; }
}
