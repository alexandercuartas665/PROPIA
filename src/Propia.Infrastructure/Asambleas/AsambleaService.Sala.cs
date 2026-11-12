using Microsoft.EntityFrameworkCore;
using Propia.Application.Asambleas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Ola 5 del hand-off de Asambleas: sala virtual de PROPIA (videollamada con LiveKit detras de IVideoSalaProvider).
/// - Acceso: token firmado por persona. La administracion entra como moderadora; el copropietario o apoderado entra con
///   su enlace de la asamblea (solo si tiene unidades habilitadas).
/// - Asistencia automatica por webhook: al entrar se registra el ingreso de TODAS sus unidades habilitadas; al salir queda
///   una "salida pendiente" que se confirma pasada la gracia de reconexion (Configuracion de asambleas); si vuelve antes,
///   se cancela y el quorum no se mueve.
/// - Grabacion compuesta (egress) a S3/R2; al terminar queda como documento "Grabacion" de la sesion.
/// </summary>
public partial class AsambleaService
{
    private IVideoSalaProvider? _video;

    private IVideoSalaProvider Video => _video is { Configurada: true } v ? v
        : throw new InvalidOperationException("La sala virtual no está configurada (LiveKit). Pídele a A&D que la active.");

    public SalaConfigDto ConfigSala()
        => new(_video?.Configurada == true, _video?.GrabacionConfigurada == true, _video?.Nombre ?? "");

    private static void ExigirSalaAbierta(Sesion s)
    {
        if (!s.SalaPropia || s.Modalidad == ModalidadSesion.Presencial)
            throw new InvalidOperationException("Esta sesión no usa la sala virtual de PROPIA.");
        if (s.Estado != EstadoSesion.EnCurso) throw new InvalidOperationException("La sala no está abierta.");
    }

    /// <summary>Acceso del equipo de la administracion (moderador).</summary>
    public async Task<SalaAccesoDto?> AccesoSalaAdminAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return null;
        ExigirSalaAbierta(s);
        var v = Video;
        var userId = GetUsuarioActualId();
        var u = await _db.Users.AsNoTracking().Where(x => x.Id == userId)
            .Select(x => new { x.Email, Nombre = x.Persona == null ? null : (x.Persona.Nombres + " " + x.Persona.Apellidos).Trim() })
            .FirstOrDefaultAsync(ct);
        var nombre = string.IsNullOrWhiteSpace(u?.Nombre) ? (u?.Email ?? "Administración") : u.Nombre;
        var sala = v.NombreSala(s.TenantId, s.Id);
        var identidad = IdentidadSala.Usuario(userId);
        return new SalaAccesoDto(v.Url, v.CrearToken(sala, identidad, nombre, true, TimeSpan.FromHours(6)),
            sala, identidad, nombre, true);
    }

    /// <summary>Acceso del copropietario o apoderado (enlace firmado de la asamblea).</summary>
    public async Task<SalaAccesoDto?> AccesoSalaResidenteAsync(Guid sesionId, Guid personaId, CancellationToken ct)
    {
        var s = await _db.Sesiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null || s.Estado == EstadoSesion.Borrador) return null;
        ExigirSalaAbierta(s);
        var v = Video;
        var habilitadas = await UnidadesHabilitadasAsync(sesionId, personaId, ct);
        var ids = habilitadas.Keys.ToList();
        var numeros = await _db.SesionParticipantes.AsNoTracking()
            .Where(p => p.SesionId == sesionId && ids.Contains(p.UnidadPrivadaId))
            .Join(_db.UnidadesPrivadas, p => p.UnidadPrivadaId, u => u.Id, (p, u) => u.Numero).ToListAsync(ct);
        if (numeros.Count == 0)
            throw new UnauthorizedAccessException("No tienes unidades habilitadas para esta asamblea.");
        var nombre = await _db.Personas.AsNoTracking().Where(x => x.Id == personaId)
            .Select(x => (x.Nombres + " " + x.Apellidos).Trim()).FirstOrDefaultAsync(ct) ?? "Copropietario";
        nombre = string.Join(" ", nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var etiqueta = numeros.Count <= 2 ? string.Join(", ", numeros.OrderBy(n => n)) : $"{numeros.Count} unidades";
        var sala = v.NombreSala(s.TenantId, s.Id);
        var identidad = IdentidadSala.Persona(personaId);
        return new SalaAccesoDto(v.Url, v.CrearToken(sala, identidad, $"{nombre} · {etiqueta}", false, TimeSpan.FromHours(6)),
            sala, identidad, nombre, false);
    }

    /// <summary>
    /// Evento ya validado del proveedor de video (el tenant ya esta fijado por quien llama, desde el nombre de la sala).
    /// Devuelve el momento en que vence una salida pendiente (para programar su confirmacion), o null.
    /// </summary>
    public async Task<DateTimeOffset?> ProcesarEventoSalaAsync(Guid sesionId, VideoSalaEvento ev, CancellationToken ct)
    {
        switch (ev.Evento)
        {
            case "participant_joined":
                await IngresoSalaAsync(sesionId, ev.Identidad, ct);
                return null;
            case "participant_left":
                return await SalidaSalaAsync(sesionId, ev.Identidad, ct);
            case "egress_ended":
                await GrabacionTerminadaAsync(sesionId, ev, ct);
                return null;
            default:
                return null;
        }
    }

    private async Task IngresoSalaAsync(Guid sesionId, string? identidad, CancellationToken ct)
    {
        if (IdentidadSala.PersonaDe(identidad) is not Guid personaId) return;   // el equipo de la administracion no suma quorum
        var s = await _db.Sesiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null || s.Estado != EstadoSesion.EnCurso) return;
        var ids = (await UnidadesHabilitadasAsync(sesionId, personaId, ct)).Keys.ToList();
        var parts = await _db.SesionParticipantes.Where(p => p.SesionId == sesionId && ids.Contains(p.UnidadPrivadaId)).ToListAsync(ct);
        // Reconexion dentro de la gracia: se cancela la salida y el quorum no se mueve.
        foreach (var p in parts.Where(p => p.SalidaPendienteEn != null)) { p.SalidaPendienteEn = null; p.UpdatedAt = DateTimeOffset.UtcNow; }
        await _db.SaveChangesAsync(ct);
        foreach (var p in parts.Where(p => !p.Presente))
            await CheckInParticipanteCoreAsync(sesionId, new CheckInParticipanteRequest(p.UnidadPrivadaId, true), personaId, ct);
    }

    private async Task<DateTimeOffset?> SalidaSalaAsync(Guid sesionId, string? identidad, CancellationToken ct)
    {
        if (IdentidadSala.PersonaDe(identidad) is not Guid personaId) return null;
        var s = await _db.Sesiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null || s.Estado != EstadoSesion.EnCurso) return null;
        var gracia = await _db.AsambleaConfigs.AsNoTracking().Select(c => (int?)c.GraciaReconexionSeg).FirstOrDefaultAsync(ct) ?? 60;
        var vence = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(gracia, 0, 600));
        var ids = (await UnidadesHabilitadasAsync(sesionId, personaId, ct)).Keys.ToList();
        var parts = await _db.SesionParticipantes
            .Where(p => p.SesionId == sesionId && ids.Contains(p.UnidadPrivadaId) && p.Presente).ToListAsync(ct);
        if (parts.Count == 0) return null;
        foreach (var p in parts) { p.SalidaPendienteEn = vence; p.UpdatedAt = DateTimeOffset.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return vence;
    }

    /// <summary>Confirma las salidas cuya gracia ya vencio (registra la salida en el quorum). Idempotente.</summary>
    public async Task<int> ConfirmarSalidasPendientesAsync(Guid sesionId, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var vencidas = await _db.SesionParticipantes
            .Where(p => p.SesionId == sesionId && p.SalidaPendienteEn != null && p.SalidaPendienteEn <= ahora).ToListAsync(ct);
        if (vencidas.Count == 0) return 0;
        var enCurso = await _db.Sesiones.AsNoTracking().AnyAsync(x => x.Id == sesionId && x.Estado == EstadoSesion.EnCurso, ct);
        var salieron = vencidas.Where(p => p.Presente).Select(p => (p.UnidadPrivadaId, p.PersonaId)).ToList();
        foreach (var p in vencidas) { p.SalidaPendienteEn = null; p.UpdatedAt = ahora; }
        await _db.SaveChangesAsync(ct);
        if (!enCurso) return 0;
        foreach (var (unidad, persona) in salieron)
            await CheckInParticipanteCoreAsync(sesionId, new CheckInParticipanteRequest(unidad, false), persona, ct);
        return salieron.Count;
    }

    // ===================== Grabacion =====================

    public async Task<bool> IniciarGrabacionAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        ExigirSalaAbierta(s);
        var v = Video;
        if (!v.GrabacionConfigurada) throw new InvalidOperationException("La grabación no está configurada (falta el almacenamiento de LiveKit).");
        if (s.GrabacionEgressId is not null) throw new InvalidOperationException("La sesión ya se está grabando.");
        var ruta = $"asambleas/{s.TenantId:N}/{s.Id:N}/grabacion-{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)):yyyyMMdd-HHmmss}.mp4";
        s.GrabacionEgressId = await v.IniciarGrabacionAsync(v.NombreSala(s.TenantId, s.Id), ruta, ct);
        s.GrabacionIniciadaEn = DateTimeOffset.UtcNow;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        Evento(s.Id, s.PuntoActualId, "Se inicia la grabación de la sesión.");
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DetenerGrabacionAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.GrabacionEgressId is not { } egress) return true;
        await Video.DetenerGrabacionAsync(egress, ct);
        // El documento llega con el webhook egress_ended; aqui solo se libera el estado "grabando".
        s.GrabacionEgressId = null; s.GrabacionIniciadaEn = null; s.UpdatedAt = DateTimeOffset.UtcNow;
        Evento(s.Id, s.PuntoActualId, "Se detiene la grabación de la sesión.");
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task GrabacionTerminadaAsync(Guid sesionId, VideoSalaEvento ev, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return;
        if (s.GrabacionEgressId is not null && s.GrabacionEgressId == ev.EgressId)
        { s.GrabacionEgressId = null; s.GrabacionIniciadaEn = null; s.UpdatedAt = DateTimeOffset.UtcNow; }
        var completa = ev.EstadoEgress is "EGRESS_COMPLETE" or "3";
        if (!completa || string.IsNullOrWhiteSpace(ev.ArchivoUrl))
        {
            Evento(s.Id, null, "La grabación no se pudo guardar" + (string.IsNullOrWhiteSpace(ev.Error) ? "." : $": {Recortar(ev.Error!, 300)}"));
            await _db.SaveChangesAsync(ct);
            return;
        }
        // Idempotente: LiveKit puede reenviar el webhook.
        if (await _db.SesionDocumentos.AnyAsync(d => d.SesionId == sesionId && d.UrlStorage == ev.ArchivoUrl, ct)) { await _db.SaveChangesAsync(ct); return; }
        _db.SesionDocumentos.Add(new SesionDocumento
        {
            SesionId = sesionId,
            Tipo = TipoDocumentoSesion.Grabacion,
            Nombre = $"Grabación de la sesión ({DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)):dd/MM/yyyy HH:mm})",
            Descripcion = ev.DuracionSeg is double d ? $"Duración {TimeSpan.FromSeconds(d):hh\\:mm\\:ss}" : null,
            UrlStorage = ev.ArchivoUrl!,
            TipoArchivo = "video/mp4",
            TamanioBytes = ev.TamanioBytes ?? 0,
            Visibilidad = VisibilidadDocumento.PostAsamblea
        });
        Evento(s.Id, null, "La grabación de la sesión quedó guardada en Documentos.");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Al levantar la sesion se detiene la grabacion en curso (sin bloquear el cierre si el proveedor falla).</summary>
    private async Task DetenerGrabacionAlCerrarAsync(Sesion s, CancellationToken ct)
    {
        if (s.GrabacionEgressId is not { } egress || _video is not { Configurada: true } v) return;
        try { await v.DetenerGrabacionAsync(egress, ct); }
        catch (Exception) { /* el egress termina igual cuando la sala queda vacia */ }
        s.GrabacionEgressId = null; s.GrabacionIniciadaEn = null;
    }
}
