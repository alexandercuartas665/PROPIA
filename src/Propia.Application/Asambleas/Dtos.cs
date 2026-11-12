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
    VisibilidadDocumento Visibilidad, DateTimeOffset CreatedAt,
    TipoDocumentoSesion Tipo = TipoDocumentoSesion.Otro);

public record QuorumDto(
    decimal CoeficienteRepresentado,
    decimal CoeficienteTotal,
    decimal PctRepresentado,
    decimal? PctRequerido,
    bool Alcanzado,
    int ParticipantesPresentes,
    int ParticipantesTotales,
    /// <summary>Segunda convocatoria (Ley 675 art. 41): sesiona con un numero plural (2+ unidades presentes).</summary>
    bool SegundaConvocatoria = false,
    /// <summary>MasDe = se requiere MAS del PctRequerido; AlMenos = basta alcanzarlo.</summary>
    CriterioMayoria Criterio = CriterioMayoria.MasDe);

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
    IReadOnlyList<VotoDto> Votos,
    /// <summary>Al cerrar: votos y % por opcion sobre la base de la votacion (tambien en voto secreto, agregado).</summary>
    IReadOnlyList<DesgloseOpcionDto>? Desglose = null);

public record DesgloseOpcionDto(string Opcion, int Votos, decimal Coeficiente, decimal Pct);

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
    ActaDto? Acta,
    bool SalaPropia = false,
    InstalacionDto? Instalacion = null,
    string? JustificacionQuorum = null,
    string? MotivoCancelacion = null,
    Guid? PuntoActualId = null,
    /// <summary>Sesion de segunda convocatoria creada desde esta (si existe).</summary>
    Guid? SesionHijaId = null,
    /// <summary>Limite de poderes por persona de Configuracion (null = sin limite).</summary>
    int? LimitePoderesPorPersona = null,
    bool TranscripcionActiva = false,
    /// <summary>Grabacion de la sala en curso (desde cuando).</summary>
    DateTimeOffset? GrabandoDesde = null);

// ----- Sala virtual (ola 5) -----
/// <summary>Acceso a la sala de video: URL del servidor, token firmado para esta persona y su rol.</summary>
public record SalaAccesoDto(string Url, string Token, string Sala, string Identidad, string Nombre, bool Moderador);
/// <summary>Estado de la sala para la ficha: si el proveedor esta configurado.</summary>
public record SalaConfigDto(bool Configurada, bool GrabacionConfigurada, string Proveedor);

// ----- Transcripcion (ola 4) -----
public record TranscripcionLineaDto(Guid Id, DateTimeOffset Hora, TipoLineaTranscripcion Tipo, Guid? PuntoId, int? PuntoNumero,
    Guid? PersonaId, string? AutorNombre, string? UnidadNumero, string Texto, decimal? Confianza);
/// <summary>Linea de voz desde el navegador ("Mi voz a texto") o el proveedor de voz a texto. Sin persona = el usuario actual.</summary>
public record AgregarTranscripcionRequest(string Texto, decimal? Confianza = null, Guid? PersonaId = null, Guid? UnidadPrivadaId = null,
    string? AutorNombre = null, string? Origen = null);
public record ActivarTranscripcionRequest(bool Activa);

/// <summary>Instalacion de la asamblea (Ley 675 art. 47): presidente y secretario + quorum al instalar.</summary>
public record InstalacionDto(DateTimeOffset Hora, decimal QuorumPct, Guid? PresidentePersonaId, string PresidenteNombre,
    Guid? SecretarioPersonaId, string SecretarioNombre);

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
    DateTimeOffset? PublicadaEn,
    string? Numero = null,
    string? BorradorHtml = null,
    EstadoRedaccionActa EstadoRedaccion = EstadoRedaccionActa.Ninguna,
    string? ErrorRedaccion = null,
    bool RedactadaPorIa = false,
    bool Editada = false,
    FirmaActaDto? FirmaPresidente = null,
    FirmaActaDto? FirmaSecretario = null,
    /// <summary>Fecha limite para poner el acta a disposicion: 20 dias habiles desde el cierre (Ley 675 art. 47).</summary>
    DateOnly? PlazoPublicacion = null,
    Guid? ComunicadoId = null);

public record FirmaActaDto(DateTimeOffset En, MetodoFirmaActa Metodo);

// ----- Acta (ola 3) -----
public record GuardarBorradorActaRequest(string Html);
public record SolicitarCodigoFirmaRequest(RolFirmaActa Rol);
/// <summary>CodigoDev solo en Development (alli no se envia correo); en produccion siempre null.</summary>
public record CodigoFirmaDto(string CorreoEnmascarado, DateTimeOffset Expira, string? CodigoDev);
/// <summary>Codigo null = firma desde la cuenta (la persona del usuario es la designada para el rol).</summary>
public record FirmarActaRolRequest(RolFirmaActa Rol, string? Codigo);
public record PublicacionActaDto(string DocumentoUrl, Guid? ComunicadoId, string? Aviso);

// ===================== Requests =====================

public record CrearSesionRequest(
    TipoSesion Tipo,
    ModalidadSesion Modalidad,
    string Titulo,
    DateTimeOffset FechaSesion,
    string? LugarFisico,
    string? EnlaceVideo,
    IReadOnlyList<CrearPuntoRequest> Puntos,
    bool SalaPropia = false);

public record CrearPuntoRequest(
    int Numero,
    string Titulo,
    string? Descripcion,
    bool RequiereVotacion,
    TipoMayoria TipoMayoria,
    decimal MayoriaPct,
    ModalidadVoto ModalidadVoto,
    Guid? PresupuestoId,
    /// <summary>Opciones propias (planchas o candidatos). Null/vacio = Si / No / Abstencion.</summary>
    IReadOnlyList<string>? OpcionesVoto = null);

public record ActualizarPuntoRequest(
    string Titulo,
    string? Descripcion,
    bool RequiereVotacion,
    TipoMayoria TipoMayoria,
    decimal MayoriaPct,
    ModalidadVoto ModalidadVoto,
    string? NarrativaSecretario,
    IReadOnlyList<string>? OpcionesVoto = null);

/// <summary>PUT MERGE de la sesion (null = no tocar). Titulo/fecha/lugar/modalidad solo en Borrador; la sala
/// (propia o enlace) tambien en Citada.</summary>
public record ActualizarSesionRequest(
    string? Titulo = null,
    DateTimeOffset? FechaSesion = null,
    string? LugarFisico = null,
    string? EnlaceVideo = null,
    bool? SalaPropia = null,
    ModalidadSesion? Modalidad = null);

/// <summary>Instalar la asamblea: presidente = unidad presente; secretario = unidad presente o null (la administracion).</summary>
public record InstalarSesionRequest(Guid PresidenteUnidadId, Guid? SecretarioUnidadId);

public record CancelarSesionRequest(string Motivo);

public record VotoBloqueItem(Guid UnidadPrivadaId, string Opcion);
public record VotarBloqueRequest(IReadOnlyList<VotoBloqueItem> Votos);
public record VotarBloqueResultado(int Registrados, IReadOnlyList<string> Errores);

/// <summary>ConfirmarFueraDePlazo: en extraordinaria/consejo/comite se puede citar dentro del plazo con confirmacion
/// explicita (la ordinaria nunca: Ley 675 art. 39).</summary>
public record EnviarCitacionRequest(string? MensajeAdicional, bool ConfirmarFueraDePlazo = false);

public record AgregarDocumentoRequest(
    Guid? PuntoId, string Nombre, string? Descripcion,
    string UrlStorage, string? TipoArchivo, long TamanioBytes,
    VisibilidadDocumento Visibilidad,
    TipoDocumentoSesion Tipo = TipoDocumentoSesion.Otro);

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

/// <summary>El quorum al cerrar lo calcula el sistema (instalada = hubo quorum). QuorumAlcanzado solo para CORREGIRLO,
/// y entonces la Justificacion es obligatoria (queda en el acta).</summary>
public record CerrarSesionRequest(bool? QuorumAlcanzado = null, string? Justificacion = null);



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
    string? LogoUrl,
    bool TranscripcionActiva = false,
    /// <summary>Ultimas lineas de voz (subtitulos) mientras la transcripcion esta activa.</summary>
    IReadOnlyList<TranscripcionLineaDto>? Subtitulos = null,
    /// <summary>La sesion usa la sala virtual de PROPIA.</summary>
    bool SalaPropia = false);

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
