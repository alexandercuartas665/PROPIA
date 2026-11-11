using Propia.Domain.Enums;

namespace Propia.Application.Asambleas;

// ===================== Bandeja =====================

public record SesionListaDto(
    Guid Id,
    string Titulo,
    TipoSesion Tipo,
    ModalidadSesion Modalidad,
    EstadoSesion Estado,
    DateTimeOffset FechaSesion,
    bool SegundaConvocatoria,
    int CantidadPuntos,
    int CantidadParticipantes,
    bool? QuorumAlcanzado,
    bool ActaPublicada);

public record SesionKpisDto(
    int Total, int Borradores, int Citadas, int EnCurso, int Cerradas,
    int QuorumFallido, int Proximas);

public record SesionBandejaDto(SesionKpisDto Kpis, IReadOnlyList<SesionListaDto> Items);

// ===================== Ficha =====================

public record SesionPuntoDto(
    Guid Id,
    int Numero,
    string Titulo,
    string? Descripcion,
    bool RequiereVotacion,
    TipoMayoria TipoMayoria,
    decimal MayoriaPct,
    ModalidadVoto ModalidadVoto,
    IReadOnlyList<string> OpcionesVoto,
    Guid? PresupuestoId,
    string? NarrativaSecretario,
    EstadoPunto Estado,
    VotacionDto? Votacion);

public record SesionParticipanteDto(
    Guid Id,
    Guid PersonaId,
    string Nombre,
    Guid UnidadPrivadaId,
    string UnidadNumero,
    decimal Coeficiente,
    CalidadParticipante Calidad,
    bool Presente,
    DateTimeOffset? HoraIngreso,
    DateTimeOffset? HoraSalida);

public record SesionPoderDto(
    Guid Id,
    Guid OtorganteUnidadId,
    string OtorganteUnidadNumero,
    Guid ApoderadoPersonaId,
    string ApoderadoNombre,
    TipoPoder TipoPoder,
    EstadoPoder Estado,
    string? DocumentoUrl,
    string? HashPoder,
    DateTimeOffset? TimestampFirma,
    string? NotaRechazo);

public record SesionDocumentoDto(
    Guid Id, Guid? PuntoId, string Nombre, string? Descripcion,
    string UrlStorage, string? TipoArchivo, long TamanioBytes,
    VisibilidadDocumento Visibilidad, DateTimeOffset CreatedAt);

public record QuorumDto(
    decimal CoeficienteRepresentado,
    decimal CoeficienteTotal,
    decimal PctRepresentado,
    decimal? PctRequerido,
    bool Alcanzado,
    int ParticipantesPresentes,
    int ParticipantesTotales);

public record VotacionDto(
    Guid Id,
    Guid PuntoId,
    EstadoVotacion Estado,
    DateTimeOffset HoraApertura,
    DateTimeOffset? HoraCierre,
    decimal QuorumAlAbrirPct,
    decimal CoeficienteTotalSala,
    string? ResultadoOpcion,
    decimal? ResultadoPct,
    ResultadoVotacion? ResultadoFinal,
    IReadOnlyList<VotoDto> Votos);

public record VotoDto(
    Guid Id, Guid PersonaId, Guid UnidadPrivadaId,
    decimal CoeficienteAportado, string Opcion, bool EsSecreto, DateTimeOffset CreatedAt);

public record SesionDetalleDto(
    Guid Id,
    TipoSesion Tipo,
    ModalidadSesion Modalidad,
    EstadoSesion Estado,
    string Titulo,
    DateTimeOffset FechaSesion,
    string? LugarFisico,
    string? EnlaceVideo,
    int PlazoCitacionDias,
    DateTimeOffset? FechaCitacionEnviada,
    bool SegundaConvocatoria,
    Guid? SesionPadreId,
    decimal? QuorumRequeridoPct,
    DateTimeOffset? HoraApertura,
    DateTimeOffset? HoraCierre,
    bool? QuorumAlcanzado,
    QuorumDto QuorumActual,
    IReadOnlyList<SesionPuntoDto> Puntos,
    IReadOnlyList<SesionParticipanteDto> Participantes,
    IReadOnlyList<SesionPoderDto> Poderes,
    IReadOnlyList<SesionDocumentoDto> Documentos,
    ActaDto? Acta);

public record ActaDto(
    Guid Id,
    EstadoActa Estado,
    string ContenidoGenerado,
    string? NarrativaSecretario,
    string? DocumentoUrl,
    string? HashDocumento,
    Guid? FirmadoPorUsuarioId,
    TipoFirmaActa? TipoFirma,
    DateTimeOffset? TimestampFirma,
    DateTimeOffset? PublicadaEn);

// ===================== Requests =====================

public record CrearSesionRequest(
    TipoSesion Tipo,
    ModalidadSesion Modalidad,
    string Titulo,
    DateTimeOffset FechaSesion,
    string? LugarFisico,
    string? EnlaceVideo,
    IReadOnlyList<CrearPuntoRequest> Puntos);

public record CrearPuntoRequest(
    int Numero,
    string Titulo,
    string? Descripcion,
    bool RequiereVotacion,
    TipoMayoria TipoMayoria,
    decimal MayoriaPct,
    ModalidadVoto ModalidadVoto,
    Guid? PresupuestoId);

public record ActualizarPuntoRequest(
    string Titulo,
    string? Descripcion,
    bool RequiereVotacion,
    TipoMayoria TipoMayoria,
    decimal MayoriaPct,
    ModalidadVoto ModalidadVoto,
    string? NarrativaSecretario);

public record EnviarCitacionRequest(string? MensajeAdicional);

public record AgregarDocumentoRequest(
    Guid? PuntoId, string Nombre, string? Descripcion,
    string UrlStorage, string? TipoArchivo, long TamanioBytes,
    VisibilidadDocumento Visibilidad);

public record OtorgarPoderRequest(
    Guid OtorganteUnidadId,
    Guid ApoderadoPersonaId,
    TipoPoder TipoPoder,
    string? DocumentoUrl);

public record DecidirPoderRequest(bool Aprobar, string? Motivo);

public record CheckInParticipanteRequest(Guid UnidadPrivadaId, bool Presente);

public record AbrirVotacionRequest(Guid PuntoId);

public record EmitirVotoRequest(Guid UnidadPrivadaId, string Opcion);

public record CerrarVotacionRequest;

public record CerrarSesionRequest(bool QuorumAlcanzado);

public record FirmarActaRequest(string? NarrativaSecretario, TipoFirmaActa TipoFirma);

public record PublicarActaRequest;

// ===================== Configuracion =====================

public record AsambleaConfigDto(
    int PlazoCitacionDias,
    int? LimitePoderesPorPersona,
    int GraciaReconexionSeg,
    int NotifRecordatorioDias,
    CriterioMayoria CriterioMayoria);

/// <summary>
/// PUT de la configuracion de Asambleas con semantica MERGE: solo se actualiza lo que viene (null = no
/// tocar). LimitePoderesPorPersona: 0 = sin limite.
/// </summary>
public record ActualizarAsambleaConfigRequest(
    int? PlazoCitacionDias,
    int? LimitePoderesPorPersona,
    int? GraciaReconexionSeg,
    int? NotifRecordatorioDias,
    CriterioMayoria? CriterioMayoria);

// ===================== Self-service del residente (Ola 1) =====================
// Vista ACOTADA para el copropietario/apoderado: datos de la sesion, puntos y SOLO sus unidades.
// No expone la lista de participantes, poderes ajenos ni votos individuales de otras unidades.

public record SesionResidenteDto(
    Guid Id,
    string Titulo,
    TipoSesion Tipo,
    ModalidadSesion Modalidad,
    EstadoSesion Estado,
    DateTimeOffset FechaSesion,
    string? LugarFisico,
    string? EnlaceVideo,
    string PersonaNombre,
    QuorumDto QuorumActual,
    IReadOnlyList<SesionPuntoResidenteDto> Puntos,
    IReadOnlyList<MiUnidadSesionDto> MisUnidades,
    string CopropiedadNombre,
    string? LogoUrl);

/// <summary>Punto del orden del dia con su votacion mas reciente (resultado solo si ya cerro).</summary>
public record SesionPuntoResidenteDto(
    Guid Id,
    int Numero,
    string Titulo,
    string? Descripcion,
    bool RequiereVotacion,
    EstadoPunto Estado,
    ModalidadVoto ModalidadVoto,
    IReadOnlyList<string> OpcionesVoto,
    Guid? VotacionId,
    EstadoVotacion? VotacionEstado,
    ResultadoVotacion? ResultadoFinal,
    string? ResultadoOpcion,
    decimal? ResultadoPct);

/// <summary>Unidad sobre la que la persona puede actuar: como propietaria o por poder aprobado.</summary>
public record MiUnidadSesionDto(
    Guid UnidadPrivadaId,
    string UnidadNumero,
    decimal Coeficiente,
    bool Presente,
    bool PorPoder,
    IReadOnlyList<MiVotoDto> Votos);

/// <summary>Voto ya emitido por la unidad. Opcion null si la votacion es secreta.</summary>
public record MiVotoDto(Guid VotacionId, Guid PuntoId, string? Opcion);

public record MiAsistenciaRequest(Guid UnidadPrivadaId, bool Presente);

public record MiVotoRequest(Guid VotacionId, Guid UnidadPrivadaId, string Opcion);

/// <summary>Enlace self-service emitido por el admin: token opaco + ruta publica relativa + vencimiento.</summary>
public record EnlaceResidenteDto(string Token, string Ruta, DateTimeOffset Expira);

/// <summary>
/// Envio de enlaces self-service por correo. PersonaIds null = todos los habilitados de la sesion.
/// BaseUrl = origen publico del Web que llama; solo se usa si App:PublicBaseUrl no esta configurado.
/// </summary>
public record EnviarEnlacesRequest(IReadOnlyList<Guid>? PersonaIds, string? BaseUrl);

/// <summary>Persona que recibe enlace: propietario activo o apoderado con poder aprobado.</summary>
public record DestinatarioEnlaceDto(Guid PersonaId, string Nombre, string? Email, IReadOnlyList<string> Unidades);

/// <summary>Respuesta inmediata del envio (los correos salen en segundo plano).</summary>
public record EnvioEnlacesDto(int Destinatarios, int ConCorreo, IReadOnlyList<string> SinCorreo);

/// <summary>Resultado del lote de correos (se registra en el log del API).</summary>
public record EnvioEnlacesResultado(int Enviados, int Fallidos, IReadOnlyList<string> Errores);

/// <summary>Estado del ultimo envio de enlaces de una sesion (lo muestra la ficha del admin).</summary>
public record EstadoEnvioEnlacesDto(DateTimeOffset Inicio, bool EnCurso, int Total, int Enviados, int Fallidos,
    IReadOnlyList<string> Errores);
