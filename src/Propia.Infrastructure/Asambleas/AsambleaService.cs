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
public class AsambleaService : IAsambleaService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    public AsambleaService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
    {
        _db = db; _tenantContext = tenantContext; _http = http;
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
        var s = await _db.Sesiones.AsNoTracking()
            .Include(x => x.Puntos)
            .Include(x => x.Participantes)
            .Include(x => x.Poderes)
            .Include(x => x.Documentos)
            .Include(x => x.Acta)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;

        // Quorum actual = suma de coeficientes de participantes Presente=true
        var coefRepresentado = s.Participantes.Where(p => p.Presente).Sum(p => p.Coeficiente);
        var coefTotal = s.Participantes.Sum(p => p.Coeficiente);
        var pctRepresentado = coefTotal > 0 ? Math.Round(coefRepresentado / coefTotal * 100m, 2) : 0m;
        var alcanzado = !s.QuorumRequeridoPct.HasValue || pctRepresentado >= s.QuorumRequeridoPct.Value;
        var quorum = new QuorumDto(coefRepresentado, coefTotal, pctRepresentado,
            s.QuorumRequeridoPct, alcanzado,
            s.Participantes.Count(p => p.Presente), s.Participantes.Count);

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
                var votosDto = (p.ModalidadVoto == ModalidadVoto.Secreto && v.Estado == EstadoVotacion.Abierta)
                    ? new List<VotoDto>()
                    : v.Votos.Select(x => new VotoDto(
                        x.Id, x.PersonaId, x.UnidadPrivadaId, x.CoeficienteAportado,
                        x.Opcion, x.EsSecreto, x.CreatedAt)).ToList();
                votDto = new VotacionDto(v.Id, v.PuntoId, v.Estado, v.HoraApertura, v.HoraCierre,
                    v.QuorumAlAbrirPct, v.CoeficienteTotalSala,
                    v.ResultadoOpcion, v.ResultadoPct, v.ResultadoFinal, votosDto);
            }
            var opciones = string.IsNullOrEmpty(p.OpcionesVoto)
                ? OpcionesVotoBase.Default
                : JsonSerializer.Deserialize<string[]>(p.OpcionesVoto) ?? OpcionesVotoBase.Default;
            return new SesionPuntoDto(p.Id, p.Numero, p.Titulo, p.Descripcion, p.RequiereVotacion,
                p.TipoMayoria, p.MayoriaPct, p.ModalidadVoto, opciones, p.PresupuestoId,
                p.NarrativaSecretario, p.Estado, votDto);
        }).ToList();

        var documentosDto = s.Documentos.OrderBy(d => d.CreatedAt).Select(d =>
            new SesionDocumentoDto(d.Id, d.PuntoId, d.Nombre, d.Descripcion, d.UrlStorage,
                d.TipoArchivo, d.TamanioBytes, d.Visibilidad, d.CreatedAt)).ToList();

        ActaDto? actaDto = s.Acta is null ? null : new ActaDto(
            s.Acta.Id, s.Acta.Estado, s.Acta.ContenidoGenerado, s.Acta.NarrativaSecretario,
            s.Acta.DocumentoUrl, s.Acta.HashDocumento, s.Acta.FirmadoPorUsuarioId,
            s.Acta.TipoFirma, s.Acta.TimestampFirma, s.Acta.PublicadaEn);

        return new SesionDetalleDto(s.Id, s.Tipo, s.Modalidad, s.Estado, s.Titulo, s.FechaSesion,
            s.LugarFisico, s.EnlaceVideo, s.PlazoCitacionDias, s.FechaCitacionEnviada,
            s.SegundaConvocatoria, s.SesionPadreId, s.QuorumRequeridoPct,
            s.HoraApertura, s.HoraCierre, s.QuorumAlcanzado,
            quorum, puntosDto, participantesDto, poderesDto, documentosDto, actaDto);
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
            FechaSesion = req.FechaSesion,
            LugarFisico = req.LugarFisico,
            EnlaceVideo = req.EnlaceVideo,
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
                OpcionesVoto = JsonSerializer.Serialize(OpcionesVotoBase.Default)
            });
        }

        await _db.SaveChangesAsync(ct);
        return (await GetSesionAsync(s.Id, ct))!;
    }

    public async Task<bool> ActualizarPuntoAsync(Guid puntoId, ActualizarPuntoRequest req, CancellationToken ct)
    {
        var p = await _db.SesionPuntos.Include(x => x.Sesion).FirstOrDefaultAsync(x => x.Id == puntoId, ct);
        if (p is null) return false;
        if (p.Sesion!.Estado == EstadoSesion.Cerrada && p.Sesion.Acta?.Estado == EstadoActa.Firmada)
            throw new InvalidOperationException("No se puede editar un punto despues de firmar el acta.");
        p.Titulo = req.Titulo.Trim();
        p.Descripcion = req.Descripcion;
        p.RequiereVotacion = req.RequiereVotacion;
        p.TipoMayoria = req.TipoMayoria;
        p.MayoriaPct = req.MayoriaPct;
        p.ModalidadVoto = req.ModalidadVoto;
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
            SubidoPorUsuarioId = GetUsuarioActualId()
        };
        _db.SesionDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);
        return new SesionDocumentoDto(d.Id, d.PuntoId, d.Nombre, d.Descripcion, d.UrlStorage,
            d.TipoArchivo, d.TamanioBytes, d.Visibilidad, d.CreatedAt);
    }

    public async Task<bool> EliminarDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        var d = await _db.SesionDocumentos.Include(x => x.Sesion).FirstOrDefaultAsync(x => x.Id == documentoId, ct);
        if (d is null) return false;
        if (d.Sesion!.Estado == EstadoSesion.Cerrada)
            throw new InvalidOperationException("No se puede eliminar documentos de una sesion cerrada (RN-20).");
        _db.SesionDocumentos.Remove(d);
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
                PersonaId = personaId.Value,  // MVP placeholder hasta materializar 2.4-2.3
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
        if (s.Estado == EstadoSesion.Cerrada || s.Estado == EstadoSesion.Cancelada)
            throw new InvalidOperationException("Sesion no admite mas poderes.");

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
        p.Estado = req.Aprobar ? EstadoPoder.Aprobado : EstadoPoder.Rechazado;
        p.AprobadoPorUsuarioId = GetUsuarioActualId();
        if (!req.Aprobar && !string.IsNullOrWhiteSpace(req.Motivo)) p.NotaRechazo = req.Motivo;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
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
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CheckInParticipanteAsync(Guid sesionId, CheckInParticipanteRequest req, CancellationToken ct)
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
            PersonaId = p.PersonaId,
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
        var existente = await _db.Votaciones.AnyAsync(v => v.PuntoId == req.PuntoId && v.Estado == EstadoVotacion.Abierta, ct);
        if (existente) throw new InvalidOperationException("Ya hay una votacion abierta para este punto.");

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
        p.Estado = EstadoPunto.EnVotacion;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new VotacionDto(v.Id, v.PuntoId, v.Estado, v.HoraApertura, v.HoraCierre,
            v.QuorumAlAbrirPct, v.CoeficienteTotalSala, null, null, null, new List<VotoDto>());
    }

    public async Task<bool> EmitirVotoAsync(Guid votacionId, EmitirVotoRequest req, CancellationToken ct)
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
            PersonaId = p.PersonaId,
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
            var totalCoefVotado = grupos.Sum(g => g.Coef);
            var ganadora = grupos.First();
            v.ResultadoOpcion = ganadora.Opcion;
            v.ResultadoPct = v.CoeficienteTotalSala > 0
                ? Math.Round(ganadora.Coef / v.CoeficienteTotalSala * 100m, 2) : 0m;

            // Determinar Aprobado/Rechazado: si la opcion "Si" supera la mayoria configurada
            var siCoef = grupos.FirstOrDefault(g => g.Opcion == OpcionesVotoBase.Si)?.Coef ?? 0m;
            var siPct = v.CoeficienteTotalSala > 0 ? siCoef / v.CoeficienteTotalSala * 100m : 0m;
            v.ResultadoFinal = siPct >= v.Punto!.MayoriaPct ? ResultadoVotacion.Aprobado : ResultadoVotacion.Rechazado;
        }
        else
        {
            v.ResultadoFinal = ResultadoVotacion.SinResultado;
        }

        v.Punto!.Estado = EstadoPunto.Cerrado;
        v.Punto.UpdatedAt = DateTimeOffset.UtcNow;
        v.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var votosDto = v.Votos.Select(x => new VotoDto(x.Id, x.PersonaId, x.UnidadPrivadaId,
            x.CoeficienteAportado, x.Opcion, x.EsSecreto, x.CreatedAt)).ToList();
        return new VotacionDto(v.Id, v.PuntoId, v.Estado, v.HoraApertura, v.HoraCierre,
            v.QuorumAlAbrirPct, v.CoeficienteTotalSala, v.ResultadoOpcion, v.ResultadoPct,
            v.ResultadoFinal, votosDto);
    }

    // ===================== Cierre =====================

    public async Task<bool> CerrarSesionAsync(Guid sesionId, CerrarSesionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado != EstadoSesion.EnCurso)
            throw new InvalidOperationException("Solo se puede cerrar una sesion EnCurso.");
        s.Estado = req.QuorumAlcanzado ? EstadoSesion.Cerrada : EstadoSesion.QuorumFallido;
        s.HoraCierre = DateTimeOffset.UtcNow;
        s.QuorumAlcanzado = req.QuorumAlcanzado;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Si quorum alcanzado, generar borrador del acta automaticamente
        if (req.QuorumAlcanzado) await GenerarActaAsync(sesionId, ct);
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
            ParticipantesPresentes = s.Participantes.Count(p => p.Presente),
            ParticipantesCitados = s.Participantes.Count,
            CoeficienteRepresentado = s.Participantes.Where(p => p.Presente).Sum(p => p.Coeficiente),
            Puntos = s.Puntos.OrderBy(p => p.Numero).Select(p => new
            {
                p.Numero,
                p.Titulo,
                p.Descripcion,
                p.RequiereVotacion,
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

    public async Task<bool> FirmarActaAsync(Guid actaId, FirmarActaRequest req, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct);
        if (a is null) return false;
        if (a.Estado == EstadoActa.Firmada) throw new InvalidOperationException("Acta ya firmada (RN-10 inmutable).");

        a.NarrativaSecretario = req.NarrativaSecretario;
        a.TipoFirma = req.TipoFirma;
        a.TimestampFirma = DateTimeOffset.UtcNow;
        a.FirmadoPorUsuarioId = GetUsuarioActualId();
        a.FirmanteIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

        // Hash SHA-256 del contenido + narrativa
        var src = $"{a.ContenidoGenerado}|{a.NarrativaSecretario}|{a.TimestampFirma:O}|{a.FirmadoPorUsuarioId}";
        using var sha = SHA256.Create();
        a.HashDocumento = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(src)));

        a.Estado = EstadoActa.Firmada;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> PublicarActaAsync(Guid actaId, PublicarActaRequest req, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct);
        if (a is null) return false;
        if (a.Estado != EstadoActa.Firmada)
            throw new InvalidOperationException("Solo se puede publicar un acta firmada.");
        a.PublicadaEn = DateTimeOffset.UtcNow;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Configuracion =====================

    public async Task<AsambleaConfigDto> GetConfigAsync(CancellationToken ct)
    {
        await AsegurarConfigAsync(ct);
        var c = await _db.AsambleaConfigs.AsNoTracking().FirstAsync(ct);
        return new AsambleaConfigDto(c.PlazoCitacionDias, c.LimitePoderesPorPersona,
            c.GraciaReconexionSeg, c.NotifRecordatorioDias);
    }

    public async Task<bool> ActualizarConfigAsync(AsambleaConfigDto req, CancellationToken ct)
    {
        await AsegurarConfigAsync(ct);
        var c = await _db.AsambleaConfigs.FirstAsync(ct);
        c.PlazoCitacionDias = req.PlazoCitacionDias;
        c.LimitePoderesPorPersona = req.LimitePoderesPorPersona;
        c.GraciaReconexionSeg = req.GraciaReconexionSeg;
        c.NotifRecordatorioDias = req.NotifRecordatorioDias;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
