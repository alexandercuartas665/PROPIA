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
    int NotifRecordatorioDias);
