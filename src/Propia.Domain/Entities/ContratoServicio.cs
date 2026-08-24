using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Contrato vigente con un proveedor externo (aseo, seguridad, ascensores, seguro PH, etc).
/// Genera alertas automaticas cuando se acerca FechaFin (modulo 2.10 Tareas).
/// </summary>
public class ContratoServicio : TenantEntity
{
    public TipoServicio Tipo { get; set; }
    /// <summary>
    /// Directriz: "en servicios el proveedor es un tercero de directorio". Puede ser una
    /// Persona o una Empresa; los campos de texto quedan como snapshot para no perder los
    /// contratos viejos que se cargaron a mano.
    /// </summary>
    public Guid? ProveedorPersonaId { get; set; }
    public Guid? ProveedorEmpresaId { get; set; }

    public string Proveedor { get; set; } = string.Empty;
    public string? NitProveedor { get; set; }

    /// <summary>Persona de contacto del contrato, tambien del Directorio.</summary>
    public Guid? ContactoPersonaId { get; set; }
    public string? Contacto { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public decimal? ValorMensual { get; set; }

    // ---- Campos del pedido de Contratos (Ola 1). Aditivos: /servicios sigue usando lo de arriba. ----
    /// <summary>Numero/consecutivo del contrato (no obligatorio).</summary>
    public string? NumeroContrato { get; set; }

    /// <summary>Tipo de contrato (clasificacion legal del pedido: Prestacion de servicios, Obra, ...).
    /// Distinto de Tipo (TipoServicio), que sigue vivo para el modulo Servicios.</summary>
    public TipoContrato? TipoContrato { get; set; }

    /// <summary>Categoria del contrato (Administracion, Aseo, Seguridad, ...).</summary>
    public CategoriaContrato? Categoria { get; set; }

    /// <summary>Valor total del contrato (COP).</summary>
    public decimal? ValorTotal { get; set; }

    /// <summary>Forma de pago: cantidad de cuotas.</summary>
    public int? FormaPagoCuotas { get; set; }

    /// <summary>Si el contrato se paga mensualmente (Si/No).</summary>
    public bool PagoMensual { get; set; }

    /// <summary>"Asociado a": amarra el contrato a un Equipo o una Zona comun. Reusa el discriminador
    /// polimorfico de Mantenimiento (Equipo/ZonaComun). Null = sin asociacion.</summary>
    public TipoActivoMantenimiento? AsociadoTipo { get; set; }
    public Guid? AsociadoId { get; set; }

    /// <summary>Texto libre: "Renovacion automatica con 30 dias de aviso", etc.</summary>
    public string? Observaciones { get; set; }

    /// <summary>Estado declarado por el admin. "Vencido" se deriva por fecha (no se setea a mano).</summary>
    public EstadoContrato Estado { get; set; } = EstadoContrato.Vigente;

    /// <summary>Dias de anticipacion para alertar el vencimiento (RN-15, default 30).</summary>
    public int DiasAnticipacionAlerta { get; set; } = 30;

    /// <summary>Clausula de renovacion automatica del contrato.</summary>
    public bool RenovacionAutomatica { get; set; }

    /// <summary>Servicio al que pertenece (un servicio agrupa uno o mas contratos). Opcional.</summary>
    public Guid? ServicioId { get; set; }
    public Servicio? Servicio { get; set; }

    /// <summary>FK logico a Expediente (modulo 2.15): un contrato puede pertenecer a un expediente.</summary>
    public Guid? ExpedienteId { get; set; }

    /// <summary>FK logico a Tarea con EsProyecto=true (modulo 2.10): un contrato puede atarse a un proyecto.</summary>
    public Guid? ProyectoTareaId { get; set; }

    /// <summary>Etapa de flujo/pipeline configurable (En tramite, Pendiente asamblea, Activo, Terminado...).
    /// Distinto de Estado (vigencia derivada por fecha); esto es el ciclo de vida operativo.</summary>
    public Guid? EtapaId { get; set; }

    /// <summary>Archivos del contrato (PDF firmado, anexos, otrosi).</summary>
    public ICollection<ContratoAdjunto> Adjuntos { get; set; } = new List<ContratoAdjunto>();
}

/// <summary>
/// Definicion de un campo personalizado (EAV) para los contratos de la copropiedad.
/// A diferencia de los campos de un tablero, estos son compartidos por TODOS los contratos
/// del tenant (aparecen como columna en la tabla de /contratos). Mismo patron que TableroCampo.
/// </summary>
public class ContratoCampo : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }

    /// <summary>Tipo de captura/render del campo (reusa el enum de tableros).</summary>
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;

    /// <summary>Opciones para tipo Seleccion, separadas por salto de linea.</summary>
    public string? Opciones { get; set; }

    /// <summary>Ayuda/contexto del campo.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Soft-delete: si es false el campo se oculta sin borrar sus valores.</summary>
    public bool Activo { get; set; } = true;
}

/// <summary>Valor de un campo personalizado para un contrato concreto (EAV).</summary>
public class ContratoCampoValor : TenantEntity
{
    public Guid ContratoId { get; set; }
    public Guid ContratoCampoId { get; set; }
    public string? Valor { get; set; }
}

/// <summary>Vinculo N:M entre un contrato y expedientes del modulo 2.15 (Ola 2). Un contrato puede
/// conectar uno o varios expedientes existentes para su documentacion.</summary>
public class ContratoExpediente : TenantEntity
{
    public Guid ContratoId { get; set; }
    public Guid ExpedienteId { get; set; }
}

/// <summary>
/// Etapa de flujo (columna del Kanban) de los contratos de la copropiedad. Configurable por tenant,
/// sembrada con 4 etapas base. Mismo espiritu que los estados de un tablero de Tareas.
/// </summary>
public class ContratoEtapa : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    /// <summary>Color hex para la columna/pill (ej. "#22C55E").</summary>
    public string? Color { get; set; }
}
