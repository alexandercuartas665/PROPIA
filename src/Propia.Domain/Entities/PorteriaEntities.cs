using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Turno de un guarda en un punto de acceso. Spec 2.12 seccion 6.
/// RN-01: solo puede existir un turno Activo por combinacion (GuardaPersonaId, PuntoAcceso).
/// La FechaApertura es automatica al crear; FechaCierre solo se setea al cerrar.
/// </summary>
public class TurnoPorteria : TenantEntity
{
    /// <summary>FK a Persona (Directorio 2.4) que es el guarda en turno.</summary>
    public Guid GuardaPersonaId { get; set; }
    public Persona? GuardaPersona { get; set; }

    /// <summary>Ej. "Acceso principal", "Parqueadero" - libre por copropiedad.</summary>
    public string PuntoAcceso { get; set; } = string.Empty;

    public EstadoTurnoPorteria Estado { get; set; } = EstadoTurnoPorteria.Activo;

    public DateTimeOffset FechaApertura { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FechaCierre { get; set; }

    /// <summary>Nota libre del guarda al cerrar el turno.</summary>
    public string? NotaCierre { get; set; }
}

/// <summary>
/// Visitante frecuente - catalogo acumulativo de personas externas. Spec 2.12 seccion 7.
/// NO son del Directorio 2.4. RN-08: purga automatica configurable (Fase 2).
/// </summary>
public class VisitanteFrecuente : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public TipoDocumentoVisitante? TipoDocumento { get; set; }
    public string? FotoUrl { get; set; }

    /// <summary>RN-09 - el portero confirmo que el visitante fue informado del aviso Ley 1581.</summary>
    public bool AvisoTratamientoAceptado { get; set; }
    public DateTimeOffset? AvisoAceptadoAt { get; set; }

    public DateTimeOffset? UltimoIngresoAt { get; set; }
    public int TotalVisitas { get; set; }

    /// <summary>Fecha de purga automatica calculada segun configuracion del tenant.</summary>
    public DateOnly? PurgaProgramadaAt { get; set; }
}

/// <summary>
/// Autorizacion previa creada por el residente o admin. Spec 2.12 seccion 10.
/// RN-04 vigencia_fin > vigencia_inicio + RN-05 revocacion permanente.
/// </summary>
public class AutorizacionPrevia : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public Guid CreadoPorPersonaId { get; set; }
    public Persona? CreadoPorPersona { get; set; }

    public OrigenAutorizacion Origen { get; set; }

    public string NombreVisitante { get; set; } = string.Empty;
    public string? DocumentoVisitante { get; set; }
    public TipoVisitante TipoVisitante { get; set; }

    public DateTimeOffset VigenciaInicio { get; set; }
    public DateTimeOffset VigenciaFin { get; set; }

    /// <summary>Instruccion visible para el guarda en el momento de validar.</summary>
    public string? NotaPortero { get; set; }

    public EstadoAutorizacion Estado { get; set; } = EstadoAutorizacion.Activo;

    /// <summary>Default 1. RN-03: contador respeta UsosMaximos.</summary>
    public int UsosMaximos { get; set; } = 1;
    public int UsosRealizados { get; set; } = 0;

    public ICollection<CodigoIngreso> Codigos { get; set; } = new List<CodigoIngreso>();
}

/// <summary>
/// Codigo numerico + QR payload asociado a una autorizacion. Spec 2.12 seccion 10.
/// RN-03: un codigo usado o expirado no puede ser utilizado nuevamente.
/// </summary>
public class CodigoIngreso : TenantEntity
{
    public Guid AutorizacionId { get; set; }
    public AutorizacionPrevia? Autorizacion { get; set; }

    /// <summary>Codigo de 8 digitos unico por copropiedad (RN: random con verificacion).</summary>
    public string CodigoNumerico { get; set; } = string.Empty;

    /// <summary>JSON con autorizacion_id + nonce + tenant para QR firmado.</summary>
    public string QrPayload { get; set; } = string.Empty;

    public EstadoAutorizacion Estado { get; set; } = EstadoAutorizacion.Activo;
    public DateTimeOffset? UsadoAt { get; set; }
}

/// <summary>
/// Registro inmutable de ingreso/egreso de una persona. Spec 2.12 seccion 7.
/// RN-10: append-only, sin UPDATE/DELETE (trigger SQL bloquea).
/// </summary>
public class RegistroVisita : TenantEntity
{
    public Guid TurnoId { get; set; }
    public TurnoPorteria? Turno { get; set; }

    public TipoEventoAccesoPorteria TipoEvento { get; set; }
    public TipoVisitante TipoVisitante { get; set; }

    /// <summary>FK opcional al catalogo de visitantes frecuentes (reconocimiento).</summary>
    public Guid? VisitanteFrecuenteId { get; set; }
    public VisitanteFrecuente? VisitanteFrecuente { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public TipoDocumentoVisitante? TipoDocumento { get; set; }

    /// <summary>Destino del visitante - configurable obligatorio u opcional por tenant (RN-07).</summary>
    public DestinoVisita? DestinoTipo { get; set; }
    public Guid? DestinoId { get; set; }

    /// <summary>FK si ingreso con autorizacion previa.</summary>
    public Guid? AutorizacionId { get; set; }
    public AutorizacionPrevia? Autorizacion { get; set; }

    /// <summary>FK si ingreso con QR validado.</summary>
    public Guid? CodigoIngresoId { get; set; }
    public CodigoIngreso? CodigoIngreso { get; set; }

    public string? Observacion { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>FK al guarda (Persona) que registro el evento.</summary>
    public Guid GuardaPersonaId { get; set; }

    /// <summary>RN-09 - obligatorio para visitantes externos.</summary>
    public bool AvisoTratamientoConfirmado { get; set; }

    /// <summary>Valores de campos personalizados del modulo (JSON: { campoId: valor }). Patron CUBOT.travels.</summary>
    public string? CamposValoresJson { get; set; }
}

/// <summary>
/// Catalogo de vehiculos autorizados por unidad. Spec 2.12 seccion 8.
/// Soft delete via Activo. Placa unique por (tenant, placa) cuando Activo.
/// </summary>
public class VehiculoAutorizado : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    /// <summary>Normalizada uppercase sin espacios/guiones (validado en service).</summary>
    public string Placa { get; set; } = string.Empty;

    public TipoVehiculo Tipo { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Color { get; set; }
    public string? ParqueaderoAsignado { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>
/// Registro inmutable de ingreso/egreso vehicular. Spec 2.12 seccion 8.
/// RN-10: append-only.
/// </summary>
public class RegistroVehiculo : TenantEntity
{
    public Guid TurnoId { get; set; }
    public TurnoPorteria? Turno { get; set; }

    public TipoEventoAccesoPorteria TipoEvento { get; set; }

    public string Placa { get; set; } = string.Empty;

    public Guid? VehiculoAutorizadoId { get; set; }
    public VehiculoAutorizado? VehiculoAutorizado { get; set; }

    public Guid? UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public bool EsVisita { get; set; }
    public string? Conductor { get; set; }
    public string? Observacion { get; set; }

    public OrigenRegistroVehiculo OrigenRegistro { get; set; } = OrigenRegistroVehiculo.Manual;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>NULL si OrigenRegistro=LprWebhook sin turno asociado (Fase 2).</summary>
    public Guid? GuardaPersonaId { get; set; }
}

/// <summary>
/// Correspondencia recibida en porteria. Spec 2.12 seccion 9.
/// RN-06: NUNCA almacena contenido - solo metadatos (Constitucion Art. 15).
/// Ciclo Recibido -> Notificado -> Entregado | Devuelto.
/// </summary>
public class Correspondencia : TenantEntity
{
    public Guid TurnoId { get; set; }
    public TurnoPorteria? Turno { get; set; }

    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public TipoCorrespondencia Tipo { get; set; }
    public string? Remitente { get; set; }
    public string? Descripcion { get; set; }

    public EstadoCorrespondencia Estado { get; set; } = EstadoCorrespondencia.Recibido;

    public DateTimeOffset RecibidoAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NotificadoAt { get; set; }
    public DateTimeOffset? EntregadoAt { get; set; }
    public string? EntregadoA { get; set; }
    public DateTimeOffset? DevueltoAt { get; set; }
    public string? MotivoDevolucion { get; set; }

    /// <summary>FK al guarda que recibio (Persona).</summary>
    public Guid GuardaRecibePersonaId { get; set; }
    public Guid? GuardaEntregaPersonaId { get; set; }
}

/// <summary>
/// Novedad reportada por el guarda en su turno. Spec 2.12 seccion 12.
/// RN-14: GeneraTarea es configurable por copropiedad (defer integracion con 2.10).
/// </summary>
public class NovedadTurno : TenantEntity
{
    public Guid TurnoId { get; set; }
    public TurnoPorteria? Turno { get; set; }

    public Guid GuardaPersonaId { get; set; }

    /// <summary>Clasificacion: novedad general, dano, reclamo, seguridad, mantenimiento.</summary>
    public TipoNovedadPorteria Tipo { get; set; } = TipoNovedadPorteria.General;

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Destino opcional al que se asigna la novedad (inmueble, zona o equipo).</summary>
    public DestinoNovedad? DestinoTipo { get; set; }
    public Guid? DestinoId { get; set; }

    public bool GeneraTarea { get; set; }

    /// <summary>FK opcional a Tarea de 2.10 si GeneraTarea=true y se creo.</summary>
    public Guid? TareaId { get; set; }
    public Tarea? Tarea { get; set; }
}

/// <summary>
/// Configuracion singleton del modulo por tenant. Spec 2.12 seccion 23.
/// Una sola fila por copropiedad - UNIQUE TenantId.
/// </summary>
public class PorteriaConfiguracion : TenantEntity
{
    /// <summary>RN-07: si true, el portero debe seleccionar destino antes de guardar.</summary>
    public bool DestinoVisitanteObligatorio { get; set; } = true;

    public CanalNotificacionPaquete CanalNotificacionPaquetes { get; set; } = CanalNotificacionPaquete.Whatsapp;

    /// <summary>Minutos para que un paquete pendiente vire a amarillo.</summary>
    public int UmbralPaqueteAmarilloMin { get; set; } = 60;

    /// <summary>Minutos para que un paquete pendiente vire a rojo.</summary>
    public int UmbralPaqueteRojoMin { get; set; } = 180;

    /// <summary>RN-11: autoregistro QR fijo desactivado por defecto (Fase 2).</summary>
    public bool AutoregistroQrActivo { get; set; }

    /// <summary>RN-14: si true, las novedades crean tarea automatica en 2.10.</summary>
    public bool GeneraTareaDesdeNovedad { get; set; }

    /// <summary>Dias de retencion de datos de visitantes. RN-08 minimo 365.</summary>
    public int RetencionDatosVisitantesDias { get; set; } = 365;
}

/// <summary>
/// Campo personalizado del modulo de Porteria (a nivel copropiedad). Se captura en el registro
/// de ingreso y se guarda por visita en RegistroVisita.CamposValoresJson. Tipado al estilo de los
/// campos dinamicos de CUBOT.travels.
/// </summary>
public class PorteriaCampo : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;

    /// <summary>Opciones para tipo Seleccion, separadas por salto de linea.</summary>
    public string? Opciones { get; set; }

    /// <summary>Si es true, aparece como filtro en la minuta.</summary>
    public bool MostrarEnFiltro { get; set; }

    /// <summary>Ancho en el modal: 1 = normal, 2 = ancho completo.</summary>
    public int Columna { get; set; } = 1;

    public string? Descripcion { get; set; }
}
