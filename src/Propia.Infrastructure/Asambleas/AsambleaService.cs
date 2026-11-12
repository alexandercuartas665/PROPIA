using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Asambleas;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Modulo 2.8 Asambleas y Organos de Gobierno - spec v1.0 MVP.
/// Ciclo: Borrador -> Citada -> EnCurso -> (Cerrada | QuorumFallido).
/// </summary>
public partial class AsambleaService : IAsambleaService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    public AsambleaService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http, IVideoSalaProvider? video = null)
    {
        _db = db; _tenantContext = tenantContext; _http = http; _video = video;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    // ===================== Seed lazy de configuracion =====================

    private async Task AsegurarConfigAsync(CancellationToken ct)
    {
        if (await _db.AsambleaConfigs.AnyAsync(ct)) return;
        _db.AsambleaConfigs.Add(new AsambleaConfig
        {
            PlazoCitacionDias = 5,
            LimitePoderesPorPersona = null,
            GraciaReconexionSeg = 60,
            NotifRecordatorioDias = 1
        });
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Bandeja + ficha =====================

    public async Task<SesionBandejaDto> GetBandejaAsync(EstadoSesion? estado, TipoSesion? tipo, CancellationToken ct)
    {
        await AsegurarConfigAsync(ct);
        IQueryable<Sesion> q = _db.Sesiones.AsNoTracking();
        if (estado.HasValue) q = q.Where(s => s.Estado == estado.Value);
        if (tipo.HasValue) q = q.Where(s => s.Tipo == tipo.Value);

        var rows = await q.OrderByDescending(s => s.FechaSesion).Take(200).ToListAsync(ct);
        var ids = rows.Select(s => s.Id).ToList();

        var puntosPorSesion = await _db.SesionPuntos.AsNoTracking()
            .Where(p => ids.Contains(p.SesionId))
            .GroupBy(p => p.SesionId).Select(g => new { g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cant, ct);
        var participantesPorSesion = await _db.SesionParticipantes.AsNoTracking()
            .Where(p => ids.Contains(p.SesionId))
            .GroupBy(p => p.SesionId).Select(g => new { g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cant, ct);
        var actasPublicadas = await _db.Actas.AsNoTracking()
            .Where(a => ids.Contains(a.SesionId) && a.PublicadaEn != null)
            .Select(a => a.SesionId).ToHashSetAsync(ct);

        var ahora = DateTimeOffset.UtcNow;
        var items = rows.Select(s => new SesionListaDto(
            s.Id, s.Titulo, s.Tipo, s.Modalidad, s.Estado, s.FechaSesion, s.SegundaConvocatoria,
            puntosPorSesion.GetValueOrDefault(s.Id, 0),
            participantesPorSesion.GetValueOrDefault(s.Id, 0),
            s.QuorumAlcanzado,
            actasPublicadas.Contains(s.Id))).ToList();

        var kpis = new SesionKpisDto(
            items.Count,
            items.Count(i => i.Estado == EstadoSesion.Borrador),
            items.Count(i => i.Estado == EstadoSesion.Citada),
            items.Count(i => i.Estado == EstadoSesion.EnCurso),
            items.Count(i => i.Estado == EstadoSesion.Cerrada),
            items.Count(i => i.Estado == EstadoSesion.QuorumFallido),
            items.Count(i => i.Estado == EstadoSesion.Citada && i.FechaSesion > ahora && i.FechaSesion < ahora.AddDays(7)));

        return new SesionBandejaDto(kpis, items);
    }

    public async Task<SesionDetalleDto?> GetSesionAsync(Guid id, CancellationToken ct)
    {
        // Salidas de la sala virtual cuya gracia vencio sin que se confirmaran (p. ej. el API se reinicio en la gracia).
        await ConfirmarSalidasPendientesAsync(id, ct);
        var s = await _db.Sesiones.AsNoTracking()
            .Include(x => x.Puntos)
            .Include(x => x.Participantes)
            .Include(x => x.Poderes)
            .Include(x => x.Documentos)
            .Include(x => x.Acta)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;

        // Quorum actual = suma de coeficientes de participantes Presente=true
        var cfgSesion = await _db.AsambleaConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        var quorum = CalcularQuorum(s, s.Participantes.Select(p => (p.Coeficiente, p.Presente)).ToList(),
            cfgSesion?.CriterioMayoria ?? CriterioMayoria.MasDe);

        // Resolver nombres de personas y unidades
        var personaIds = s.Participantes.Select(p => p.PersonaId)
            .Union(s.Poderes.Select(p => p.ApoderadoPersonaId)).Distinct().ToList();
        var personas = await _db.Personas.AsNoTracking()
            .Where(p => personaIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
        var unidadIds = s.Participantes.Select(p => p.UnidadPrivadaId)
            .Union(s.Poderes.Select(p => p.OtorganteUnidadId)).Distinct().ToList();
        var unidades = await _db.UnidadesPrivadas.AsNoTracking()
            .Where(u => unidadIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Numero, ct);

        var participantesDto = s.Participantes
            .OrderBy(p => unidades.GetValueOrDefault(p.UnidadPrivadaId, ""))
            .Select(p => new SesionParticipanteDto(
                p.Id, p.PersonaId, personas.GetValueOrDefault(p.PersonaId, ""),
                p.UnidadPrivadaId, unidades.GetValueOrDefault(p.UnidadPrivadaId, ""),
                p.Coeficiente, p.Calidad, p.Presente, p.HoraIngreso, p.HoraSalida))
            .ToList();

        var poderesDto = s.Poderes
            .Select(p => new SesionPoderDto(
                p.Id, p.OtorganteUnidadId, unidades.GetValueOrDefault(p.OtorganteUnidadId, ""),
                p.ApoderadoPersonaId, personas.GetValueOrDefault(p.ApoderadoPersonaId, ""),
                p.TipoPoder, p.Estado, p.DocumentoUrl, p.HashPoder, p.TimestampFirma, p.NotaRechazo))
            .ToList();

        // Votaciones por punto
        var votaciones = await _db.Votaciones.AsNoTracking()
            .Include(v => v.Votos)
            .Where(v => v.SesionId == id)
            .ToListAsync(ct);
        var votacionesPorPunto = votaciones.GroupBy(v => v.PuntoId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(v => v.HoraApertura).First());

        var puntosDto = s.Puntos.OrderBy(p => p.Numero).Select(p =>
        {
            VotacionDto? votDto = null;
            if (votacionesPorPunto.TryGetValue(p.Id, out var v))
            {
                var votosDto = MapearVotos(v.Votos);
                votDto = new VotacionDto(v.Id, v.PuntoId, v.Estado, v.HoraApertura, v.HoraCierre,
                    v.QuorumAlAbrirPct, v.CoeficienteTotalSala,
                    v.ResultadoOpcion, v.ResultadoPct, v.ResultadoFinal, votosDto, Desglose(v, OpcionesDe(p)));
            }
            var opciones = OpcionesDe(p);
            return new SesionPuntoDto(p.Id, p.Numero, p.Titulo, p.Descripcion, p.RequiereVotacion,
                p.TipoMayoria, p.MayoriaPct, p.ModalidadVoto, opciones, p.PresupuestoId,
                p.NarrativaSecretario, p.Estado, votDto);
        }).ToList();

        var documentosDto = s.Documentos.OrderBy(d => d.CreatedAt).Select(d =>
            new SesionDocumentoDto(d.Id, d.PuntoId, d.Nombre, d.Descripcion, d.UrlStorage,
                d.TipoArchivo, d.TamanioBytes, d.Visibilidad, d.CreatedAt, d.Tipo)).ToList();

        ActaDto? actaDto = null;
        if (s.Acta is { } a)
        {
            // Ley 675 art. 47: el acta se pone a disposicion dentro de los 20 dias habiles siguientes a la reunion.
            DateOnly? plazo = s.HoraCierre is { } hc
                ? await SumarDiasHabilesAsync(DateOnly.FromDateTime(hc.ToOffset(TimeSpan.FromHours(-5)).DateTime), 20, ct) : null;
            actaDto = new ActaDto(a.Id, a.Estado, a.ContenidoGenerado, a.NarrativaSecretario, a.DocumentoUrl, a.HashDocumento,
                a.FirmadoPorUsuarioId, a.TipoFirma, a.TimestampFirma, a.PublicadaEn,
                a.Numero, a.BorradorHtml, a.EstadoRedaccion, a.ErrorRedaccion, a.RedactadaPorIa, a.Editada,
                a.FirmaPresidenteEn is { } fp ? new FirmaActaDto(fp, a.FirmaPresidenteMetodo ?? MetodoFirmaActa.Cuenta) : null,
                a.FirmaSecretarioEn is { } fs ? new FirmaActaDto(fs, a.FirmaSecretarioMetodo ?? MetodoFirmaActa.Cuenta) : null,
                plazo, a.ComunicadoId);
        }

        return new SesionDetalleDto(s.Id, s.Tipo, s.Modalidad, s.Estado, s.Titulo, s.FechaSesion,
            s.LugarFisico, s.EnlaceVideo, s.PlazoCitacionDias, s.FechaCitacionEnviada,
            s.SegundaConvocatoria, s.SesionPadreId, s.QuorumRequeridoPct,
            s.HoraApertura, s.HoraCierre, s.QuorumAlcanzado,
            quorum, puntosDto, participantesDto, poderesDto, documentosDto, actaDto,
            s.SalaPropia,
            s.InstaladaEn is { } inst ? new InstalacionDto(inst, s.QuorumInstalacionPct ?? 0m, s.PresidentePersonaId,
                s.PresidenteNombre ?? "", s.SecretarioPersonaId, s.SecretarioNombre ?? "") : null,
            s.JustificacionQuorum, s.MotivoCancelacion, s.PuntoActualId,
            await _db.Sesiones.AsNoTracking().Where(x => x.SesionPadreId == s.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct),
            cfgSesion?.LimitePoderesPorPersona, s.TranscripcionActiva, s.GrabacionEgressId is null ? null : s.GrabacionIniciadaEn);
    }

    // ===================== Creacion y configuracion =====================

    public async Task<SesionDetalleDto> CrearSesionAsync(CrearSesionRequest req, CancellationToken ct)
    {
        await AsegurarConfigAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo obligatorio.");
        if (req.Puntos is null || req.Puntos.Count == 0)
            throw new InvalidOperationException("Debes definir al menos un punto del orden del dia.");

        var cfg = await _db.AsambleaConfigs.AsNoTracking().FirstAsync(ct);
        // Quorum requerido por defecto: 50% para 1ra convocatoria de Asamblea, null para Consejo/Comite
        decimal? quorumReq = req.Tipo switch
        {
            TipoSesion.AsambleaOrdinaria or TipoSesion.AsambleaExtraordinaria => 50m,
            _ => null
        };

        var s = new Sesion
        {
            Tipo = req.Tipo,
            Modalidad = req.Modalidad,
            Titulo = req.Titulo.Trim(),
            FechaSesion = req.FechaSesion.ToUniversalTime(),   // Npgsql: timestamptz solo admite offset 0
            LugarFisico = req.LugarFisico,
            EnlaceVideo = req.SalaPropia && req.Modalidad != ModalidadSesion.Presencial ? null : req.EnlaceVideo,
            SalaPropia = req.SalaPropia && req.Modalidad != ModalidadSesion.Presencial,
            PlazoCitacionDias = cfg.PlazoCitacionDias,
            QuorumRequeridoPct = quorumReq,
            Estado = EstadoSesion.Borrador,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.Sesiones.Add(s);

        foreach (var p in req.Puntos.OrderBy(p => p.Numero))
        {
            _db.SesionPuntos.Add(new SesionPunto
            {
                Sesion = s,
                Numero = p.Numero,
                Titulo = p.Titulo.Trim(),
                Descripcion = p.Descripcion,
                RequiereVotacion = p.RequiereVotacion,
                TipoMayoria = p.TipoMayoria,
                MayoriaPct = p.MayoriaPct,
                ModalidadVoto = p.ModalidadVoto,
                PresupuestoId = p.PresupuestoId,
                OpcionesVoto = SerializarOpciones(p.OpcionesVoto)
            });
        }

        await _db.SaveChangesAsync(ct);
        return (await GetSesionAsync(s.Id, ct))!;
    }

    public async Task<bool> ActualizarPuntoAsync(Guid puntoId, ActualizarPuntoRequest req, CancellationToken ct)
    {
        var p = await _db.SesionPuntos.Include(x => x.Sesion).FirstOrDefaultAsync(x => x.Id == puntoId, ct);
        if (p is null) return false;
        var acta = await _db.Actas.AsNoTracking().FirstOrDefaultAsync(a => a.SesionId == p.SesionId, ct);
        if (acta?.Estado == EstadoActa.Firmada)
            throw new InvalidOperationException("No se puede editar un punto despues de firmar el acta.");
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo del punto obligatorio.");
        var opciones = SerializarOpciones(req.OpcionesVoto);
        var cambiaOrden = p.Titulo != req.Titulo.Trim() || p.Descripcion != req.Descripcion || p.RequiereVotacion != req.RequiereVotacion
            || p.TipoMayoria != req.TipoMayoria || p.MayoriaPct != req.MayoriaPct || p.ModalidadVoto != req.ModalidadVoto
            || (req.OpcionesVoto is not null && opciones != p.OpcionesVoto);
        if (cambiaOrden && p.Sesion!.Estado != EstadoSesion.Borrador)
            throw new InvalidOperationException("El orden del dia solo se edita en Borrador: la citacion ya salio con este orden.");
        p.Titulo = req.Titulo.Trim();
        p.Descripcion = req.Descripcion;
        p.RequiereVotacion = req.RequiereVotacion;
        p.TipoMayoria = req.TipoMayoria;
        p.MayoriaPct = req.MayoriaPct;
        p.ModalidadVoto = req.ModalidadVoto;
        if (req.OpcionesVoto is not null) p.OpcionesVoto = opciones;
        p.NarrativaSecretario = req.NarrativaSecretario;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SesionDocumentoDto> AgregarDocumentoAsync(Guid sesionId, AgregarDocumentoRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("Sesion no encontrada.");
        var d = new SesionDocumento
        {
            SesionId = sesionId,
            PuntoId = req.PuntoId,
            Nombre = req.Nombre.Trim(),
            Descripcion = req.Descripcion,
            UrlStorage = req.UrlStorage,
            TipoArchivo = req.TipoArchivo,
            TamanioBytes = req.TamanioBytes,
            Visibilidad = req.Visibilidad,
            Tipo = req.Tipo,
            SubidoPorUsuarioId = GetUsuarioActualId()
        };
        _db.SesionDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);
        return new SesionDocumentoDto(d.Id, d.PuntoId, d.Nombre, d.Descripcion, d.UrlStorage,
            d.TipoArchivo, d.TamanioBytes, d.Visibilidad, d.CreatedAt, d.Tipo);
    }

    public async Task<bool> EliminarDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.SesionDocumentos.Include(x => x.Sesion).FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return false;
        if (d.Sesion!.Estado == EstadoSesion.Cerrada)
            throw new InvalidOperationException("No se puede eliminar documentos de una sesion cerrada (RN-20).");
        if (d.Tipo is TipoDocumentoSesion.Convocatoria or TipoDocumentoSesion.Acta or TipoDocumentoSesion.Grabacion)
            throw new InvalidOperationException("Los documentos que genera el sistema (convocatoria, acta, grabacion) no se eliminan.");
        _db.SesionDocumentos.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarSesionAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado != EstadoSesion.Borrador && s.Estado != EstadoSesion.Cancelada)
            throw new InvalidOperationException("Solo se pueden eliminar sesiones en Borrador o Canceladas.");
        // Los hijos (puntos, participantes, poderes, documentos) caen por la FK en cascada.
        _db.Sesiones.Remove(s);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Citacion =====================

    public async Task<bool> EnviarCitacionAsync(Guid sesionId, EnviarCitacionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.Include(x => x.Puntos).FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado != EstadoSesion.Borrador)
            throw new InvalidOperationException("Solo se puede citar una sesion en estado Borrador.");
        if (s.Puntos.Count == 0)
            throw new InvalidOperationException("La sesion debe tener al menos un punto del orden del dia.");
        var dias = (int)Math.Floor((s.FechaSesion - DateTimeOffset.UtcNow).TotalDays);
        if (s.FechaSesion < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("La fecha de la sesion ya paso: cambiala antes de citar.");
        // Plazo de citacion (Ley 675 art. 39). La segunda convocatoria tiene su propia fecha legal (art. 41).
        var plazo = await _db.AsambleaConfigs.AsNoTracking().Select(c => (int?)c.PlazoCitacionDias).FirstOrDefaultAsync(ct) ?? s.PlazoCitacionDias;
        if (!s.SegundaConvocatoria && dias < plazo)
        {
            if (s.Tipo == TipoSesion.AsambleaOrdinaria)
                throw new InvalidOperationException($"La asamblea ordinaria se convoca con al menos {plazo} dias de anticipacion (Ley 675 art. 39): faltan {Math.Max(0, dias)}. Cambia la fecha.");
            if (!req.ConfirmarFueraDePlazo)
                throw new InvalidOperationException($"FUERA_DE_PLAZO: faltan {Math.Max(0, dias)} dias para la sesion y el plazo de citacion es de {plazo}.");
        }

        // Cargar participantes: en MVP, agregamos una fila por unidad activa del tenant.
        // Como la relacion propietario-unidad no esta materializada en una FK directa
        // (vive en DirectorioVinculo con tipos polimorficos del modulo 2.4), en MVP
        // asignamos el usuario actual como representante placeholder. En Fase 2,
        // cuando se conecte 2.4 con la unidad, el seed automatico vendra de alli.
        var unidades = await _db.UnidadesPrivadas.AsNoTracking().ToListAsync(ct);
        if (unidades.Count == 0)
            throw new InvalidOperationException("La copropiedad no tiene unidades privadas configuradas.");

        // Resolver la persona del usuario actual
        var personaId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == GetUsuarioActualId())
            .Select(u => u.PersonaId)
            .FirstOrDefaultAsync(ct);
        if (personaId is null)
            throw new InvalidOperationException("No se pudo resolver la persona del administrador para registrar la citacion.");

        // Ley 675: vota el propietario. Cada unidad se cita a nombre de su propietario activo (el primero
        // registrado); si la unidad no tiene propietario en el Directorio, queda la administracion como referencia.
        var propietarios = (await _db.UnidadPersonas.AsNoTracking()
                .Where(x => x.Activo && x.Rol == RolUnidadPersona.Propietario && x.PersonaId != null)
                .Select(x => new { x.UnidadId, PersonaId = x.PersonaId!.Value, x.CreatedAt })
                .ToListAsync(ct))
            .GroupBy(x => x.UnidadId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedAt).First().PersonaId);

        var existentes = await _db.SesionParticipantes.AsNoTracking()
            .Where(p => p.SesionId == sesionId)
            .Select(p => p.UnidadPrivadaId)
            .ToHashSetAsync(ct);

        foreach (var u in unidades)
        {
            if (existentes.Contains(u.Id)) continue;
            _db.SesionParticipantes.Add(new SesionParticipante
            {
                SesionId = sesionId,
                PersonaId = propietarios.TryGetValue(u.Id, out var prop) ? prop : personaId.Value,
                UnidadPrivadaId = u.Id,
                Coeficiente = u.CoeficientePropiedad,
                Calidad = CalidadParticipante.Propietario
            });
        }

        s.Estado = EstadoSesion.Citada;
        s.FechaCitacionEnviada = DateTimeOffset.UtcNow;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Poderes =====================

    public async Task<SesionPoderDto> OtorgarPoderAsync(Guid sesionId, OtorgarPoderRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("Sesion no encontrada.");
        if (s.Estado is EstadoSesion.Cerrada or EstadoSesion.Cancelada or EstadoSesion.QuorumFallido)
            throw new InvalidOperationException("La sesion ya termino: no admite mas poderes.");
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == req.OtorganteUnidadId, ct))
            throw new InvalidOperationException("Elige la unidad que otorga el poder.");
        if (await _db.SesionPoderes.AnyAsync(x => x.SesionId == sesionId && x.OtorganteUnidadId == req.OtorganteUnidadId
                && (x.Estado == EstadoPoder.Pendiente || x.Estado == EstadoPoder.Aprobado), ct))
            throw new InvalidOperationException("Esta unidad ya tiene un poder registrado en la sesion.");
        // personas es global: el apoderado debe tener vinculo con ESTA copropiedad (directorio o unidad).
        if (!await _db.DirectorioVinculos.AnyAsync(v => v.EntidadTipo == EntidadDirectorio.Persona && v.EntidadId == req.ApoderadoPersonaId, ct)
            && !await _db.UnidadPersonas.AnyAsync(up => up.PersonaId == req.ApoderadoPersonaId, ct))
            throw new InvalidOperationException("El apoderado no pertenece al directorio de la copropiedad.");
        if (await _db.UnidadPersonas.AnyAsync(up => up.UnidadId == req.OtorganteUnidadId && up.PersonaId == req.ApoderadoPersonaId
                && up.Activo && up.Rol == RolUnidadPersona.Propietario, ct))
            throw new InvalidOperationException("El apoderado no puede ser el mismo propietario de la unidad.");
        if (req.TipoPoder == TipoPoder.Digital) await ValidarLimitePoderesAsync(sesionId, req.ApoderadoPersonaId, ct);

        var hashSrc = $"{sesionId}|{req.OtorganteUnidadId}|{req.ApoderadoPersonaId}|{DateTimeOffset.UtcNow:O}";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(hashSrc)));

        var p = new SesionPoder
        {
            SesionId = sesionId,
            OtorganteUsuarioPersonaId = GetUsuarioActualId(),  // En MVP: el usuario actual es el otorgante
            OtorganteUnidadId = req.OtorganteUnidadId,
            ApoderadoPersonaId = req.ApoderadoPersonaId,
            TipoPoder = req.TipoPoder,
            Estado = req.TipoPoder == TipoPoder.Digital ? EstadoPoder.Aprobado : EstadoPoder.Pendiente,
            DocumentoUrl = req.DocumentoUrl,
            HashPoder = hash,
            TimestampFirma = req.TipoPoder == TipoPoder.Digital ? DateTimeOffset.UtcNow : null,
            FirmanteIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString()
        };
        _db.SesionPoderes.Add(p);
        await _db.SaveChangesAsync(ct);

        // Recargar con nombres
        var apoderado = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.ApoderadoPersonaId, ct);
        var unidad = await _db.UnidadesPrivadas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.OtorganteUnidadId, ct);
        return new SesionPoderDto(p.Id, p.OtorganteUnidadId, unidad?.Numero ?? "",
            p.ApoderadoPersonaId, apoderado is null ? "" : $"{apoderado.Nombres} {apoderado.Apellidos}".Trim(),
            p.TipoPoder, p.Estado, p.DocumentoUrl, p.HashPoder, p.TimestampFirma, null);
    }

    public async Task<bool> DecidirPoderAsync(Guid poderId, DecidirPoderRequest req, CancellationToken ct)
    {
        var p = await _db.SesionPoderes.FirstOrDefaultAsync(x => x.Id == poderId, ct);
        if (p is null) return false;
        if (p.Estado != EstadoPoder.Pendiente)
            throw new InvalidOperationException("El poder ya fue decidido.");
        if (req.Aprobar) await ValidarLimitePoderesAsync(p.SesionId, p.ApoderadoPersonaId, ct);
        p.Estado = req.Aprobar ? EstadoPoder.Aprobado : EstadoPoder.Rechazado;
        p.AprobadoPorUsuarioId = GetUsuarioActualId();
        if (!req.Aprobar && !string.IsNullOrWhiteSpace(req.Motivo)) p.NotaRechazo = req.Motivo;
        if (await _db.Sesiones.AnyAsync(x => x.Id == p.SesionId && x.Estado == EstadoSesion.EnCurso, ct))
        {
            var unidad = await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == p.OtorganteUnidadId).Select(u => u.Numero).FirstOrDefaultAsync(ct);
            var apoderado = await _db.Personas.AsNoTracking().Where(x => x.Id == p.ApoderadoPersonaId).Select(x => (x.Nombres + " " + x.Apellidos).Trim()).FirstOrDefaultAsync(ct);
            Evento(p.SesionId, null, $"Poder de la unidad {unidad} a {apoderado}: {(req.Aprobar ? "aprobado" : "rechazado")}.");
        }
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Limite de poderes por persona (Configuracion de asambleas): cuantas unidades puede representar un apoderado.</summary>
    private async Task ValidarLimitePoderesAsync(Guid sesionId, Guid apoderadoId, CancellationToken ct)
    {
        var lim = await _db.AsambleaConfigs.AsNoTracking().Select(c => c.LimitePoderesPorPersona).FirstOrDefaultAsync(ct);
        if (lim is not int l || l <= 0) return;
        var n = await _db.SesionPoderes.CountAsync(x => x.SesionId == sesionId && x.ApoderadoPersonaId == apoderadoId && x.Estado == EstadoPoder.Aprobado, ct);
        if (n >= l)
            throw new InvalidOperationException($"El apoderado ya representa {n} unidad(es): el limite por persona es {l} (Configuracion de asambleas).");
    }

    // ===================== Sala =====================

    public async Task<bool> AbrirSalaAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado != EstadoSesion.Citada)
            throw new InvalidOperationException("Solo se puede abrir sala en sesion Citada.");
        s.Estado = EstadoSesion.EnCurso;
        s.HoraApertura = DateTimeOffset.UtcNow;
        Evento(s.Id, null, "Se abre la sala.");
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> CheckInParticipanteAsync(Guid sesionId, CheckInParticipanteRequest req, CancellationToken ct)
        => CheckInParticipanteCoreAsync(sesionId, req, personaActuanteId: null, ct);

    // personaActuanteId: quien registra su propia asistencia (flujo self-service). Null = flujo admin,
    // que conserva la persona del participante en el quorum log.
    private async Task<bool> CheckInParticipanteCoreAsync(Guid sesionId, CheckInParticipanteRequest req,
        Guid? personaActuanteId, CancellationToken ct)
    {
        var p = await _db.SesionParticipantes
            .FirstOrDefaultAsync(x => x.SesionId == sesionId && x.UnidadPrivadaId == req.UnidadPrivadaId, ct);
        if (p is null) return false;
        var s = await _db.Sesiones.FirstAsync(x => x.Id == sesionId, ct);
        if (s.Estado != EstadoSesion.EnCurso)
            throw new InvalidOperationException("La sala debe estar abierta para hacer check-in.");

        var antes = p.Presente;
        p.Presente = req.Presente;
        if (req.Presente && !antes)
        {
            p.HoraIngreso = DateTimeOffset.UtcNow;
            p.HoraSalida = null;
        }
        else if (!req.Presente && antes)
        {
            p.HoraSalida = DateTimeOffset.UtcNow;
        }

        // Registrar en quorum log
        var totalCoef = await _db.SesionParticipantes.AsNoTracking()
            .Where(x => x.SesionId == sesionId).SumAsync(x => x.Coeficiente, ct);
        var coefRepresentado = await _db.SesionParticipantes.AsNoTracking()
            .Where(x => x.SesionId == sesionId && x.Presente).SumAsync(x => x.Coeficiente, ct);
        // Ajustar por el cambio que se hara
        if (req.Presente && !antes) coefRepresentado += p.Coeficiente;
        if (!req.Presente && antes) coefRepresentado -= p.Coeficiente;
        var pct = totalCoef > 0 ? Math.Round(coefRepresentado / totalCoef * 100m, 2) : 0m;

        _db.SesionQuorumLog.Add(new SesionQuorumLog
        {
            SesionId = sesionId,
            Evento = req.Presente ? EventoQuorum.Ingreso : EventoQuorum.Salida,
            PersonaId = personaActuanteId ?? p.PersonaId,
            UnidadPrivadaId = p.UnidadPrivadaId,
            Coeficiente = p.Coeficiente,
            QuorumAcumuladoPct = pct
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Votaciones =====================

    public async Task<VotacionDto> AbrirVotacionAsync(Guid sesionId, AbrirVotacionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("Sesion no encontrada.");
        if (s.Estado != EstadoSesion.EnCurso)
            throw new InvalidOperationException("La sesion debe estar EnCurso para abrir votaciones.");
        var p = await _db.SesionPuntos.FirstOrDefaultAsync(x => x.Id == req.PuntoId && x.SesionId == sesionId, ct)
            ?? throw new InvalidOperationException("Punto no pertenece a esta sesion.");
        if (!p.RequiereVotacion)
            throw new InvalidOperationException("Este punto no requiere votacion formal.");
        if (s.InstaladaEn is null)
            throw new InvalidOperationException("Instala la asamblea (presidente y secretario) antes de abrir votaciones.");
        if (p.Estado is EstadoPunto.Cerrado or EstadoPunto.NoTratado)
            throw new InvalidOperationException("Este punto ya se voto.");
        var abierta = await _db.Votaciones.Where(v => v.SesionId == sesionId && v.Estado == EstadoVotacion.Abierta)
            .Select(v => (int?)v.Punto!.Numero).FirstOrDefaultAsync(ct);
        if (abierta is int nAbierta)
            throw new InvalidOperationException($"Ya hay una votacion abierta (punto {nAbierta}): una votacion a la vez.");
        if (!(await QuorumActualAsync(s, ct)).Alcanzado)
            throw new InvalidOperationException("No hay quorum en este momento: no se pueden abrir votaciones.");

        var coefTotal = await _db.SesionParticipantes.AsNoTracking()
            .Where(x => x.SesionId == sesionId && x.Presente).SumAsync(x => x.Coeficiente, ct);
        var totalSala = await _db.SesionParticipantes.AsNoTracking()
            .Where(x => x.SesionId == sesionId).SumAsync(x => x.Coeficiente, ct);
        var pct = totalSala > 0 ? Math.Round(coefTotal / totalSala * 100m, 2) : 0m;

        var v = new Votacion
        {
            SesionId = sesionId,
            PuntoId = req.PuntoId,
            Estado = EstadoVotacion.Abierta,
            HoraApertura = DateTimeOffset.UtcNow,
            QuorumAlAbrirPct = pct,
            CoeficienteTotalSala = coefTotal
        };
        _db.Votaciones.Add(v);
        s.PuntoActualId = p.Id;
        p.Estado = EstadoPunto.EnVotacion;
        Evento(sesionId, p.Id, $"Se abre la votación del punto {p.Numero} ("
            + (OpcionesDe(p).Contains(OpcionesVotoBase.Si) ? $"mayoría {p.TipoMayoria.ToString().ToLowerInvariant()} de más del {p.MayoriaPct:0.##} %" : "elección")
            + $", voto {(p.ModalidadVoto == ModalidadVoto.Secreto ? "secreto" : "público")}).");
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new VotacionDto(v.Id, v.PuntoId, v.Estado, v.HoraApertura, v.HoraCierre,
            v.QuorumAlAbrirPct, v.CoeficienteTotalSala, null, null, null, new List<VotoDto>());
    }

    public Task<bool> EmitirVotoAsync(Guid votacionId, EmitirVotoRequest req, CancellationToken ct)
        => EmitirVotoCoreAsync(votacionId, req, personaActuanteId: null, ct);

    // personaActuanteId: quien emite el voto (propietario o apoderado, flujo self-service). Null = flujo
    // admin, que conserva la persona del participante en el voto.
    private async Task<bool> EmitirVotoCoreAsync(Guid votacionId, EmitirVotoRequest req,
        Guid? personaActuanteId, CancellationToken ct)
    {
        var v = await _db.Votaciones.Include(x => x.Punto).FirstOrDefaultAsync(x => x.Id == votacionId, ct);
        if (v is null) return false;
        if (v.Estado != EstadoVotacion.Abierta)
            throw new InvalidOperationException("La votacion esta cerrada.");

        var p = await _db.SesionParticipantes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SesionId == v.SesionId && x.UnidadPrivadaId == req.UnidadPrivadaId, ct)
            ?? throw new InvalidOperationException("La unidad no es participante de esta sesion.");
        if (!p.Presente)
            throw new InvalidOperationException("La unidad no esta presente en la sala.");

        // RN-09: solo un voto por unidad
        var yaVoto = await _db.Votos.AnyAsync(x => x.VotacionId == votacionId && x.UnidadPrivadaId == req.UnidadPrivadaId, ct);
        if (yaVoto)
            throw new InvalidOperationException("Esta unidad ya emitio su voto en esta votacion.");

        // Validar opcion contra el catalogo del punto
        var opciones = string.IsNullOrEmpty(v.Punto!.OpcionesVoto)
            ? OpcionesVotoBase.Default
            : JsonSerializer.Deserialize<string[]>(v.Punto.OpcionesVoto) ?? OpcionesVotoBase.Default;
        if (!opciones.Contains(req.Opcion))
            throw new InvalidOperationException($"Opcion invalida. Opciones validas: {string.Join(", ", opciones)}");

        _db.Votos.Add(new Voto
        {
            VotacionId = votacionId,
            PersonaId = personaActuanteId ?? p.PersonaId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            CoeficienteAportado = p.Coeficiente,
            Opcion = req.Opcion,
            EsSecreto = v.Punto.ModalidadVoto == ModalidadVoto.Secreto
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<VotacionDto> CerrarVotacionAsync(Guid votacionId, CerrarVotacionRequest req, CancellationToken ct)
    {
        var v = await _db.Votaciones.Include(x => x.Punto).Include(x => x.Votos)
            .FirstOrDefaultAsync(x => x.Id == votacionId, ct)
            ?? throw new InvalidOperationException("Votacion no encontrada.");
        if (v.Estado != EstadoVotacion.Abierta) throw new InvalidOperationException("Ya esta cerrada.");

        v.Estado = EstadoVotacion.Cerrada;
        v.HoraCierre = DateTimeOffset.UtcNow;

        if (v.Votos.Count > 0)
        {
            // Ponderado por coeficiente
            var grupos = v.Votos.GroupBy(x => x.Opcion)
                .Select(g => new { Opcion = g.Key, Coef = g.Sum(x => x.CoeficienteAportado) })
                .OrderByDescending(x => x.Coef).ToList();
            var ganadora = grupos.First();
            v.ResultadoOpcion = ganadora.Opcion;

            // Base del porcentaje, calculada AL CERRAR (no al abrir: con el voto self-service la gente
            // llega despues de abrir la votacion). Ley 675: mayoria simple (art. 45) = coeficientes
            // representados en la reunion -> presentes al cierre + quien voto y luego se retiro (su voto
            // cuenta y no puede pasar del 100 %); mayoria calificada (art. 46) = total de coeficientes de
            // la copropiedad (todas las unidades citadas a la sesion).
            var participantes = await _db.SesionParticipantes.AsNoTracking()
                .Where(x => x.SesionId == v.SesionId)
                .Select(x => new { x.UnidadPrivadaId, x.Coeficiente, x.Presente })
                .ToListAsync(ct);
            var votaron = v.Votos.Select(x => x.UnidadPrivadaId).ToHashSet();
            var baseCoef = v.Punto!.TipoMayoria == TipoMayoria.Calificada
                ? participantes.Sum(x => x.Coeficiente)
                : participantes.Where(x => x.Presente || votaron.Contains(x.UnidadPrivadaId)).Sum(x => x.Coeficiente);

            v.ResultadoPct = baseCoef > 0 ? Math.Round(ganadora.Coef / baseCoef * 100m, 2) : 0m;

            // Aprobado/Rechazado: el % del "Si" contra la mayoria del punto, con el criterio de la
            // copropiedad (MasDe = mayoria absoluta, un empate no aprueba; AlMenos = basta alcanzarla).
            // Sin fila de config = default de la entidad (MasDe).
            var criterio = await _db.AsambleaConfigs.AsNoTracking()
                .Select(c => (CriterioMayoria?)c.CriterioMayoria)
                .FirstOrDefaultAsync(ct) ?? CriterioMayoria.MasDe;
            var opcionesPunto = OpcionesDe(v.Punto);
            if (!opcionesPunto.Contains(OpcionesVotoBase.Si))
            {
                // Eleccion (planchas o candidatos): gana la opcion con mas coeficiente; empate = sin resultado.
                var empate = grupos.Count > 1 && grupos[1].Coef == ganadora.Coef;
                v.ResultadoFinal = empate ? ResultadoVotacion.SinResultado : ResultadoVotacion.Elegido;
                if (empate) v.ResultadoOpcion = null;
                goto Fin;
            }
            var siCoef = grupos.FirstOrDefault(g => g.Opcion == OpcionesVotoBase.Si)?.Coef ?? 0m;
            var siPct = baseCoef > 0 ? siCoef / baseCoef * 100m : 0m;
            var aprobado = criterio == CriterioMayoria.AlMenos
                ? siPct >= v.Punto.MayoriaPct
                : siPct > v.Punto.MayoriaPct;
            v.ResultadoFinal = aprobado ? ResultadoVotacion.Aprobado : ResultadoVotacion.Rechazado;
        }
        else
        {
            v.ResultadoFinal = ResultadoVotacion.SinResultado;
        }
    Fin:

        v.Punto!.Estado = EstadoPunto.Cerrado;
        Evento(v.SesionId, v.PuntoId, $"Se cierra la votación del punto {v.Punto.Numero}: {ResultadoEvento(v)} con {v.Votos.Count} votos.");
        v.Punto.UpdatedAt = DateTimeOffset.UtcNow;
        v.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new VotacionDto(v.Id, v.PuntoId, v.Estado, v.HoraApertura, v.HoraCierre,
            v.QuorumAlAbrirPct, v.CoeficienteTotalSala, v.ResultadoOpcion, v.ResultadoPct,
            v.ResultadoFinal, MapearVotos(v.Votos), Desglose(v, OpcionesDe(v.Punto!)));
    }

    // Voto secreto: nunca sale la opcion de una unidad, ni con la votacion abierta ni cerrada (antes se
    // revelaba al cerrar a cualquier usuario del tenant). Se conserva quien voto (participacion) y el
    // coeficiente para el conteo; el resultado agregado vive en ResultadoOpcion/ResultadoPct.
    private static List<VotoDto> MapearVotos(IEnumerable<Voto> votos)
        => votos.Select(x => new VotoDto(x.Id, x.PersonaId, x.UnidadPrivadaId, x.CoeficienteAportado,
            x.EsSecreto ? "" : x.Opcion, x.EsSecreto, x.CreatedAt)).ToList();

    // ===================== Cierre =====================

    public async Task<bool> CerrarSesionAsync(Guid sesionId, CerrarSesionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.Include(x => x.Puntos).FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado != EstadoSesion.EnCurso)
            throw new InvalidOperationException("Solo se puede cerrar una sesion EnCurso.");
        var abierta = await _db.Votaciones.Where(v => v.SesionId == sesionId && v.Estado == EstadoVotacion.Abierta)
            .Select(v => (int?)v.Punto!.Numero).FirstOrDefaultAsync(ct);
        if (abierta is int nAbierta)
            throw new InvalidOperationException($"Hay una votacion abierta (punto {nAbierta}). Cierrala antes de cerrar la sesion.");

        // El quorum de la sesion es el de la INSTALACION (no una casilla manual). Corregirlo exige justificacion,
        // que queda en el acta.
        var calculado = s.InstaladaEn is not null;
        var final = calculado;
        if (req.QuorumAlcanzado is bool q && q != calculado)
        {
            if (string.IsNullOrWhiteSpace(req.Justificacion))
                throw new InvalidOperationException("Para corregir el quorum calculado escribe la justificacion (queda en el acta).");
            final = q;
            s.JustificacionQuorum = Recortar(req.Justificacion.Trim(), 1000);
        }

        foreach (var p in s.Puntos)
        {
            if (p.Estado == EstadoPunto.Pendiente || (p.Estado == EstadoPunto.EnDiscusion && p.RequiereVotacion))
            { p.Estado = EstadoPunto.NoTratado; p.UpdatedAt = DateTimeOffset.UtcNow; }
            else if (p.Estado == EstadoPunto.EnDiscusion) { p.Estado = EstadoPunto.Cerrado; p.UpdatedAt = DateTimeOffset.UtcNow; }
        }
        s.PuntoActualId = null;
        s.TranscripcionActiva = false;
        await DetenerGrabacionAlCerrarAsync(s, ct);
        Evento(s.Id, null, final ? "Se levanta la sesión." : "Se levanta la sesión sin quórum.");
        s.Estado = final ? EstadoSesion.Cerrada : EstadoSesion.QuorumFallido;
        s.HoraCierre = DateTimeOffset.UtcNow;
        s.QuorumAlcanzado = final;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Con quorum se genera el borrador del acta
        if (final) await GenerarActaAsync(sesionId, ct);
        return true;
    }

    // ===================== Acta =====================

    public async Task<ActaDto?> GenerarActaAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones
            .Include(x => x.Puntos)
            .Include(x => x.Participantes)
            .FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return null;

        var existente = await _db.Actas.FirstOrDefaultAsync(a => a.SesionId == sesionId, ct);
        if (existente is not null) return new ActaDto(existente.Id, existente.Estado,
            existente.ContenidoGenerado, existente.NarrativaSecretario,
            existente.DocumentoUrl, existente.HashDocumento, existente.FirmadoPorUsuarioId,
            existente.TipoFirma, existente.TimestampFirma, existente.PublicadaEn);

        // Construir contenido JSON estructurado (en MVP, sin IA - solo datos duros)
        var votaciones = await _db.Votaciones.AsNoTracking()
            .Include(v => v.Votos)
            .Where(v => v.SesionId == sesionId)
            .ToListAsync(ct);

        var contenido = new
        {
            Tipo = s.Tipo.ToString(),
            Modalidad = s.Modalidad.ToString(),
            Titulo = s.Titulo,
            FechaSesion = s.FechaSesion,
            LugarOEnlace = s.LugarFisico ?? s.EnlaceVideo,
            HoraApertura = s.HoraApertura,
            HoraCierre = s.HoraCierre,
            QuorumAlcanzado = s.QuorumAlcanzado,
            SegundaConvocatoria = s.SegundaConvocatoria,
            Instalacion = s.InstaladaEn is null ? null : new
            {
                Hora = s.InstaladaEn,
                QuorumPct = s.QuorumInstalacionPct,
                Presidente = s.PresidenteNombre,
                Secretario = s.SecretarioNombre
            },
            JustificacionQuorum = s.JustificacionQuorum,
            ParticipantesPresentes = s.Participantes.Count(p => p.Presente),
            ParticipantesCitados = s.Participantes.Count,
            CoeficienteRepresentado = s.Participantes.Where(p => p.Presente).Sum(p => p.Coeficiente),
            Puntos = s.Puntos.OrderBy(p => p.Numero).Select(p => new
            {
                p.Numero,
                p.Titulo,
                p.Descripcion,
                p.RequiereVotacion,
                Estado = p.Estado.ToString(),
                Votacion = votaciones.FirstOrDefault(v => v.PuntoId == p.Id) is { } v
                    ? new
                    {
                        v.ResultadoOpcion,
                        v.ResultadoPct,
                        ResultadoFinal = v.ResultadoFinal?.ToString(),
                        TotalVotos = v.Votos.Count
                    }
                    : null
            })
        };

        var acta = new Acta
        {
            SesionId = sesionId,
            Estado = EstadoActa.Borrador,
            ContenidoGenerado = JsonSerializer.Serialize(contenido, new JsonSerializerOptions { WriteIndented = true })
        };
        _db.Actas.Add(acta);
        await _db.SaveChangesAsync(ct);
        return new ActaDto(acta.Id, acta.Estado, acta.ContenidoGenerado, null, null, null, null, null, null, null);
    }

    // ===================== Configuracion =====================

    public async Task<AsambleaConfigDto> GetConfigAsync(CancellationToken ct)
    {
        await AsegurarConfigAsync(ct);
        var c = await _db.AsambleaConfigs.AsNoTracking().FirstAsync(ct);
        return new AsambleaConfigDto(c.PlazoCitacionDias, c.LimitePoderesPorPersona,
            c.GraciaReconexionSeg, c.NotifRecordatorioDias, c.CriterioMayoria);
    }

    // MERGE: solo lo que viene en el request (null = no tocar). Rangos sanos para que un valor absurdo no
    // rompa la citacion o los recordatorios.
    public async Task<bool> ActualizarConfigAsync(ActualizarAsambleaConfigRequest req, CancellationToken ct)
    {
        if (req.PlazoCitacionDias is < 0 or > 60)
            throw new InvalidOperationException("El plazo de citacion debe estar entre 0 y 60 dias.");
        if (req.LimitePoderesPorPersona is < 0 or > 50)
            throw new InvalidOperationException("El limite de poderes por persona debe estar entre 0 (sin limite) y 50.");
        if (req.GraciaReconexionSeg is < 0 or > 3600)
            throw new InvalidOperationException("La gracia de reconexion debe estar entre 0 y 3600 segundos.");
        if (req.NotifRecordatorioDias is < 0 or > 30)
            throw new InvalidOperationException("El recordatorio debe estar entre 0 y 30 dias antes.");
        if (req.CriterioMayoria is { } cm && !Enum.IsDefined(cm))
            throw new InvalidOperationException("Criterio de mayoria invalido.");

        await AsegurarConfigAsync(ct);
        var c = await _db.AsambleaConfigs.FirstAsync(ct);
        if (req.PlazoCitacionDias is int plazo) c.PlazoCitacionDias = plazo;
        if (req.LimitePoderesPorPersona is int lim) c.LimitePoderesPorPersona = lim == 0 ? null : lim;
        if (req.GraciaReconexionSeg is int gracia) c.GraciaReconexionSeg = gracia;
        if (req.NotifRecordatorioDias is int rec) c.NotifRecordatorioDias = rec;
        if (req.CriterioMayoria is { } criterio) c.CriterioMayoria = criterio;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
