using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Sesion de asamblea o organo de gobierno. Spec 2.8 v1.0 tabla sesiones.
/// Ciclo: Borrador -> Citada -> EnCurso -> (Cerrada | QuorumFallido | Cancelada).
/// </summary>
public class Sesion : TenantEntity
{
    public TipoSesion Tipo { get; set; }
    public ModalidadSesion Modalidad { get; set; }
    public EstadoSesion Estado { get; set; } = EstadoSesion.Borrador;

    public string Titulo { get; set; } = string.Empty;
    public DateTimeOffset FechaSesion { get; set; }

    public string? LugarFisico { get; set; }
    public string? EnlaceVideo { get; set; }

    public int PlazoCitacionDias { get; set; } = 5;
    public DateTimeOffset? FechaCitacionEnviada { get; set; }

    public bool SegundaConvocatoria { get; set; }
    public Guid? SesionPadreId { get; set; }
    public Sesion? SesionPadre { get; set; }

    /// <summary>Porcentaje de quorum requerido (NULL = cualquier numero en 2da convocatoria).</summary>
    public decimal? QuorumRequeridoPct { get; set; }

    public DateTimeOffset? HoraApertura { get; set; }
    public DateTimeOffset? HoraCierre { get; set; }
    public bool? QuorumAlcanzado { get; set; }

    public Guid CreadoPorUsuarioId { get; set; }

    public ICollection<SesionPunto> Puntos { get; set; } = new List<SesionPunto>();
    public ICollection<SesionDocumento> Documentos { get; set; } = new List<SesionDocumento>();
    public ICollection<SesionParticipante> Participantes { get; set; } = new List<SesionParticipante>();
    public ICollection<SesionPoder> Poderes { get; set; } = new List<SesionPoder>();
    public Acta? Acta { get; set; }
}

/// <summary>Punto del orden del dia. Spec 2.8 v1.0 tabla sesion_puntos.</summary>
public class SesionPunto : TenantEntity
{
    public Guid SesionId { get; set; }
    public Sesion? Sesion { get; set; }

    public int Numero { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public bool RequiereVotacion { get; set; } = true;
    public TipoMayoria TipoMayoria { get; set; } = TipoMayoria.Simple;
    /// <summary>Porcentaje requerido para aprobacion. 50 = mayoria simple, 70 = calificada Art. 45.</summary>
    public decimal MayoriaPct { get; set; } = 50m;
    public ModalidadVoto ModalidadVoto { get; set; } = ModalidadVoto.Publico;

    /// <summary>JSON con opciones de voto (default ["Si","No","Abstencion"]).</summary>
    public string OpcionesVoto { get; set; } = "[\"Si\",\"No\",\"Abstencion\"]";

    /// <summary>FK al presupuesto si este punto es la aprobacion de un presupuesto del modulo 2.6.</summary>
    public Guid? PresupuestoId { get; set; }

    /// <summary>Texto editable por el secretario para el acta.</summary>
    public string? NarrativaSecretario { get; set; }

    public EstadoPunto Estado { get; set; } = EstadoPunto.Pendiente;
}

/// <summary>Documento adjunto al expediente. Visibilidad configurable. Spec 2.8 v1.0 RN-13.</summary>
public class SesionDocumento : TenantEntity
{
    public Guid SesionId { get; set; }
    public Sesion? Sesion { get; set; }

    /// <summary>Si es null, es documento general de la sesion; si tiene valor, esta vinculado a un punto.</summary>
    public Guid? PuntoId { get; set; }
    public SesionPunto? Punto { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string UrlStorage { get; set; } = string.Empty;
    public string? TipoArchivo { get; set; }
    public long TamanioBytes { get; set; }

    public VisibilidadDocumento Visibilidad { get; set; } = VisibilidadDocumento.PostAsamblea;
    public Guid SubidoPorUsuarioId { get; set; }
}

/// <summary>Participante citado a la sesion. Spec 2.8 v1.0 tabla sesion_participantes.</summary>
public class SesionParticipante : TenantEntity
{
    public Guid SesionId { get; set; }
    public Sesion? Sesion { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public decimal Coeficiente { get; set; }
    public CalidadParticipante Calidad { get; set; } = CalidadParticipante.Propietario;

    /// <summary>True cuando el participante ha sido marcado como presente.</summary>
    public bool Presente { get; set; }
    public DateTimeOffset? HoraIngreso { get; set; }
    public DateTimeOffset? HoraSalida { get; set; }
}

/// <summary>Poder de representacion. Spec 2.8 v1.0 tabla sesion_poderes + Art. 44 Ley 675.</summary>
public class SesionPoder : TenantEntity
{
    public Guid SesionId { get; set; }
    public Sesion? Sesion { get; set; }

    public Guid OtorganteUsuarioPersonaId { get; set; }
    public Guid OtorganteUnidadId { get; set; }

    public Guid ApoderadoPersonaId { get; set; }
    public Persona? ApoderadoPersona { get; set; }

    public TipoPoder TipoPoder { get; set; } = TipoPoder.Digital;
    public EstadoPoder Estado { get; set; } = EstadoPoder.Pendiente;

    public string? DocumentoUrl { get; set; }

    /// <summary>SHA-256 del documento + metadatos (trazabilidad inmutable).</summary>
    public string? HashPoder { get; set; }
    public DateTimeOffset? TimestampFirma { get; set; }
    public string? FirmanteIp { get; set; }

    public Guid? AprobadoPorUsuarioId { get; set; }
    public string? NotaRechazo { get; set; }
}

/// <summary>Log de quorum continuo (append-only via trigger SQL). Decreto 1074/2015.</summary>
public class SesionQuorumLog : TenantEntity
{
    public Guid SesionId { get; set; }

    public EventoQuorum Evento { get; set; }
    public Guid PersonaId { get; set; }
    public Guid UnidadPrivadaId { get; set; }
    public decimal Coeficiente { get; set; }

    /// <summary>% de quorum acumulado al momento del evento.</summary>
    public decimal QuorumAcumuladoPct { get; set; }
}

/// <summary>Votacion por punto del orden del dia.</summary>
public class Votacion : TenantEntity
{
    public Guid SesionId { get; set; }
    public Sesion? Sesion { get; set; }

    public Guid PuntoId { get; set; }
    public SesionPunto? Punto { get; set; }

    public EstadoVotacion Estado { get; set; } = EstadoVotacion.Abierta;
    public DateTimeOffset HoraApertura { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? HoraCierre { get; set; }

    /// <summary>% de quorum al momento de abrir la votacion (informativo para acta).</summary>
    public decimal QuorumAlAbrirPct { get; set; }

    /// <summary>Suma de coeficientes de participantes presentes al abrir.</summary>
    public decimal CoeficienteTotalSala { get; set; }

    /// <summary>Opcion ganadora.</summary>
    public string? ResultadoOpcion { get; set; }
    public decimal? ResultadoPct { get; set; }
    public ResultadoVotacion? ResultadoFinal { get; set; }

    public ICollection<Voto> Votos { get; set; } = new List<Voto>();
}

/// <summary>Voto individual. Constraint unique por (votacion_id, unidad_privada_id) - RN-09.</summary>
public class Voto : TenantEntity
{
    public Guid VotacionId { get; set; }
    public Votacion? Votacion { get; set; }

    public Guid PersonaId { get; set; }
    public Guid UnidadPrivadaId { get; set; }
    public decimal CoeficienteAportado { get; set; }

    public string Opcion { get; set; } = string.Empty;
    public bool EsSecreto { get; set; }
}

/// <summary>Acta de la sesion. Spec 2.8 v1.0 tabla actas + RN-10/RN-20.</summary>
public class Acta : TenantEntity
{
    public Guid SesionId { get; set; }
    public Sesion? Sesion { get; set; }

    public EstadoActa Estado { get; set; } = EstadoActa.Borrador;

    /// <summary>JSON estructurado con datos duros generados al cerrar la sesion (asistentes, votaciones, quorum).</summary>
    public string ContenidoGenerado { get; set; } = "{}";

    /// <summary>JSON con narrativas por punto editadas por el secretario.</summary>
    public string? NarrativaSecretario { get; set; }

    public string? DocumentoUrl { get; set; }
    public string? HashDocumento { get; set; }

    public Guid? FirmadoPorUsuarioId { get; set; }
    public TipoFirmaActa? TipoFirma { get; set; }
    public DateTimeOffset? TimestampFirma { get; set; }
    public string? FirmanteIp { get; set; }

    public DateTimeOffset? PublicadaEn { get; set; }
}

/// <summary>Eleccion de Consejo registrada en una asamblea (dispara actualizacion en 2.3 y 2.4).</summary>
public class EleccionConsejo : TenantEntity
{
    public Guid SesionId { get; set; }
    public Guid PuntoId { get; set; }

    public DateOnly PeriodoInicio { get; set; }
    public DateOnly? PeriodoFin { get; set; }

    public string Estado { get; set; } = "Confirmada";

    public ICollection<EleccionCandidato> Candidatos { get; set; } = new List<EleccionCandidato>();
}

public class EleccionCandidato : TenantEntity
{
    public Guid EleccionId { get; set; }
    public EleccionConsejo? Eleccion { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public string? Cargo { get; set; }
    public decimal VotosCoeficiente { get; set; }
    public bool Elegido { get; set; }
}

/// <summary>Configuracion de asambleas por copropiedad (singleton). Spec 2.8 v1.0 tabla asamblea_config.</summary>
public class AsambleaConfig : TenantEntity
{
    public int PlazoCitacionDias { get; set; } = 5;
    public int? LimitePoderesPorPersona { get; set; }
    public int GraciaReconexionSeg { get; set; } = 60;

    public int NotifRecordatorioDias { get; set; } = 1;
}
