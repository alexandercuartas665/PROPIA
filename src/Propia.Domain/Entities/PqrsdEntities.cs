using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Expediente principal de PQRSD. Spec 2.9 v1.0 tabla pqrsd_expediente.
/// Numero de radicado inmutable PQRS-{ANIO}-{SEQ} unico por tenant (RN-13).
/// El radicador_id NUNCA se borra aunque haya reserva de identidad (RN-03).
/// </summary>
public class PqrsdExpediente : TenantEntity
{
    public string NumeroRadicado { get; set; } = string.Empty;

    /// <summary>Comportamiento legal del expediente (enum fijo). Deriva del tipo configurable elegido.</summary>
    public TipoPqrsd Tipo { get; set; }

    /// <summary>Tipo configurable elegido (catalogo PqrsdTipo por copropiedad). Determina nombre + plazo.</summary>
    public Guid? TipoId { get; set; }
    public PqrsdTipo? TipoConfig { get; set; }

    public Guid CategoriaId { get; set; }
    public PqrsdCategoria? Categoria { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public EstadoPqrsd Estado { get; set; } = EstadoPqrsd.Recibida;

    /// <summary>FK siempre almacenada (RN-03). La reserva es solo capa de presentacion.</summary>
    public Guid RadicadorPersonaId { get; set; }
    public Persona? RadicadorPersona { get; set; }

    /// <summary>Solo valido si Tipo == Denuncia (RN-02).</summary>
    public bool IdentidadReservada { get; set; }

    public bool TutelaActiva { get; set; }
    public DateTimeOffset? TutelaActivadaAt { get; set; }
    public Guid? TutelaActivadaPorUsuarioId { get; set; }

    /// <summary>Fecha de vencimiento del plazo legal, calculada en backend en dias habiles.</summary>
    public DateOnly FechaVencimiento { get; set; }

    /// <summary>FK a la tarea interna en modulo 2.10 (opcional, invisible para el radicador - RN-10).</summary>
    public Guid? TareaId { get; set; }

    /// <summary>
    /// Columna del tablero configurable donde se ubica el expediente (port de Tareas).
    /// Es solo ubicacion visual: la logica legal (plazos/semaforo/tutela) sigue el enum <see cref="Estado"/>.
    /// Cuando la columna tiene semantica legal, mover el expediente sincroniza tambien el enum.
    /// </summary>
    public Guid? EstadoId { get; set; }
    public PqrsdEstado? EstadoColumna { get; set; }

    /// <summary>Unidad privada con la que se relaciona el PQR (opcional). Sin FK dura para evitar cascadas.</summary>
    public Guid? UnidadPrivadaId { get; set; }

    /// <summary>Persona del directorio responsable de gestionar el PQR (opcional). Port del "responsable" de Tareas.
    /// Sin FK dura (se resuelve el nombre por join) para evitar cascadas, igual que UnidadPrivadaId.</summary>
    public Guid? AsignadoPersonaId { get; set; }

    /// <summary>Progreso de gestion 0-100 (port de Tareas). No altera la logica legal (plazos/semaforo).</summary>
    public int Progreso { get; set; }

    /// <summary>
    /// Si es true, el expediente esta ARCHIVADO: sale del tablero/tabla activa y aparece en el tab
    /// "Archivados". Reversible; conserva todos los datos. Es distinto de cerrar (que es estado legal).
    /// </summary>
    public bool Archivado { get; set; }
    public DateTimeOffset? ArchivadoAt { get; set; }
    public Guid? ArchivadoPorUsuarioId { get; set; }

    public string? RespuestaAdmin { get; set; }
    public DateTimeOffset? RespuestaAdminAt { get; set; }
    public Guid? RespuestaAdminPorUsuarioId { get; set; }

    /// <summary>Texto de inconformidad del radicador (solo una vez - RN-06).</summary>
    public string? InconformidadTexto { get; set; }
    public DateTimeOffset? InconformidadAt { get; set; }

    public string? RespuestaDefinitiva { get; set; }
    public DateTimeOffset? RespuestaDefinitivaAt { get; set; }

    public DateTimeOffset? FechaCierre { get; set; }
    public Guid? CerradoPorUsuarioId { get; set; }

    public ICollection<PqrsdAdjunto> Adjuntos { get; set; } = new List<PqrsdAdjunto>();
    public ICollection<PqrsdHistorialEstado> Historial { get; set; } = new List<PqrsdHistorialEstado>();
    public ICollection<PqrsdCampoValor> CamposValores { get; set; } = new List<PqrsdCampoValor>();
    public ICollection<PqrsdComentario> Comentarios { get; set; } = new List<PqrsdComentario>();
}

/// <summary>Reporte de actividad / comentario libre sobre un expediente PQRS (port del feed de Tareas).</summary>
public class PqrsdComentario : TenantEntity
{
    public Guid PqrsdExpedienteId { get; set; }
    public PqrsdExpediente? Expediente { get; set; }
    public string Texto { get; set; } = string.Empty;
    public Guid? AutorUsuarioId { get; set; }
    public string? AutorNombre { get; set; }
}

/// <summary>
/// Columna/estado configurable del tablero PQRS (port de <see cref="TareaEstado"/>).
/// A diferencia de Tareas, PQRS tiene un unico tablero por copropiedad (sin TableroId).
/// Las columnas son totalmente editables (agregar/renombrar/reordenar/quitar). Las 5 columnas
/// legales se siembran con <see cref="SemanticaLegal"/> mapeada al enum <see cref="EstadoPqrsd"/>,
/// que sigue gobernando plazos/semaforo/tutela; las columnas custom (SemanticaLegal null) son
/// carriles de triage que no alteran la logica legal.
/// </summary>
public class PqrsdEstado : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Color hexadecimal para chips en UI. Opcional.</summary>
    public string? Color { get; set; }

    public int Orden { get; set; }

    /// <summary>True = columna terminal (Cerrada / Via interna agotada). No eliminable.</summary>
    public bool EsTerminal { get; set; }

    /// <summary>True = columna base sembrada por plataforma (las 5 legales).</summary>
    public bool EsBase { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Semantica legal de la columna. Si tiene valor, mover un expediente a esta columna
    /// sincroniza el enum Estado del expediente (y por ende los plazos). null = columna custom.
    /// </summary>
    public EstadoPqrsd? SemanticaLegal { get; set; }
}

/// <summary>
/// Tipo de PQRS configurable por copropiedad (catalogo editable). Los 8 tipos legales se siembran
/// como base (EsBase=true) con <see cref="Legal"/> mapeado al enum; el usuario puede crear tipos
/// propios y editar sus tiempos de respuesta. El expediente conserva el enum <see cref="PqrsdExpediente.Tipo"/>
/// (= Legal del tipo elegido) para toda la logica legal; el nombre y el plazo salen de este catalogo.
/// </summary>
public class PqrsdTipo : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Plazo de respuesta en dias habiles.</summary>
    public int DiasHabiles { get; set; } = 15;

    /// <summary>Dias habiles para manifestar inconformidad (RN-06).</summary>
    public int DiasInconformidad { get; set; } = 3;

    public NivelUrgenciaPqrsd NivelUrgencia { get; set; } = NivelUrgenciaPqrsd.Media;

    /// <summary>
    /// Conducta legal a aplicar. Para los 8 tipos base es su enum; para tipos custom es la conducta
    /// que se hereda (por defecto Peticion): asi la reserva de identidad (Denuncia), el comite, etc.
    /// siguen funcionando sobre el enum del expediente.
    /// </summary>
    public TipoPqrsd Legal { get; set; } = TipoPqrsd.Peticion;

    /// <summary>True = uno de los 8 tipos legales sembrados por plataforma.</summary>
    public bool EsBase { get; set; }

    public bool Activo { get; set; } = true;

    public int Orden { get; set; }
}

/// <summary>
/// Campo personalizado del tablero PQRS (port de <see cref="TableroCampo"/>, sin TableroId).
/// Se rellena por expediente en <see cref="PqrsdCampoValor"/>.
/// </summary>
public class PqrsdCampo : TenantEntity
{
    public string Label { get; set; } = string.Empty;
    public int Orden { get; set; }

    /// <summary>Tipo de captura/render del campo (se reusa el enum de Tareas).</summary>
    public TipoCampoTablero Tipo { get; set; } = TipoCampoTablero.Texto;

    /// <summary>Opciones para tipo Seleccion, separadas por salto de linea.</summary>
    public string? Opciones { get; set; }

    /// <summary>Si es true, el campo aparece como filtro (chips por valor) en la vista del tablero.</summary>
    public bool MostrarEnFiltro { get; set; }

    /// <summary>Ancho en el modal: 1 = normal (media columna), 2 = ancho completo.</summary>
    public int Columna { get; set; } = 1;

    /// <summary>Ayuda/contexto del campo para el usuario.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Si es true, el expediente no se puede guardar sin un valor en este campo.</summary>
    public bool Requerido { get; set; }

    /// <summary>Valor inicial al radicar un PQR nuevo.</summary>
    public string? ValorPorDefecto { get; set; }

    /// <summary>Si es true, admite varios valores (separados por salto de linea).</summary>
    public bool PermiteVarios { get; set; }

    /// <summary>Para tipo Total: ids (CSV) de los campos Numero/Moneda cuyos valores se suman.</summary>
    public string? CamposSuma { get; set; }

    /// <summary>Si es false, el campo esta ARCHIVADO: se oculta pero conserva los valores. Restaurable.</summary>
    public bool Activo { get; set; } = true;
}

/// <summary>Valor de un campo personalizado PQRS para un expediente concreto.</summary>
public class PqrsdCampoValor : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public PqrsdExpediente? Expediente { get; set; }
    public Guid PqrsdCampoId { get; set; }
    public string? Valor { get; set; }
}

/// <summary>Catalogo de categorias configurables por copropiedad. Spec 2.9 v1.0 seccion 4.</summary>
public class PqrsdCategoria : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public bool EsPredeterminada { get; set; }
    public bool Activa { get; set; } = true;
    public int Orden { get; set; }
}

/// <summary>Adjunto del expediente. Spec 2.9 v1.0 tabla pqrsd_adjunto.</summary>
public class PqrsdAdjunto : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public PqrsdExpediente? Expediente { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string UrlStorage { get; set; } = string.Empty;
    public Guid SubidoPorUsuarioId { get; set; }
}

/// <summary>Historial append-only de estados. Spec 2.9 v1.0 tabla pqrsd_historial_estado + trigger SQL.</summary>
public class PqrsdHistorialEstado : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public PqrsdExpediente? Expediente { get; set; }

    public EstadoPqrsd? EstadoAnterior { get; set; }
    public EstadoPqrsd EstadoNuevo { get; set; }

    public Guid? ActorUsuarioId { get; set; }
    public OrigenCambioEstado Origen { get; set; } = OrigenCambioEstado.Manual;

    public string? Nota { get; set; }
}

/// <summary>Configuracion de plazos por tipo y copropiedad. Spec 2.9 v1.0 tabla pqrsd_configuracion_plazo.</summary>
public class PqrsdConfiguracionPlazo : TenantEntity
{
    public TipoPqrsd Tipo { get; set; }
    public int DiasHabiles { get; set; }
    public int DiasInconformidad { get; set; } = 3;
    public NivelUrgenciaPqrsd NivelUrgencia { get; set; }
}

/// <summary>Sesion del Comite de Convivencia (Art. 58 Ley 675/2001). Spec 2.9 v1.0 seccion 6.</summary>
public class PqrsdComiteSesion : TenantEntity
{
    public Guid ExpedienteId { get; set; }
    public PqrsdExpediente? Expediente { get; set; }

    public DateTimeOffset? FechaSesion { get; set; }
    public ModalidadComite Modalidad { get; set; }
    public string? EnlaceReunion { get; set; }

    public ResultadoComite? Resultado { get; set; }

    public string? BorradorActa { get; set; }
    public string? ActaFinal { get; set; }

    /// <summary>FK opcional a documento en 2.15 cuando se publica el acta firmada.</summary>
    public Guid? ActaDocumentoId { get; set; }

    public Guid ActivadaPorUsuarioId { get; set; }

    public ICollection<PqrsdComiteMiembroSesion> Miembros { get; set; } = new List<PqrsdComiteMiembroSesion>();
}

/// <summary>Miembros del Comite asignados a una sesion. Spec 2.9 v1.0 tabla pqrsd_comite_miembro_sesion.</summary>
public class PqrsdComiteMiembroSesion : TenantEntity
{
    public Guid SesionId { get; set; }
    public PqrsdComiteSesion? Sesion { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }
}
