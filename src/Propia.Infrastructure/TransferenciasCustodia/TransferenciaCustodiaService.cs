using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.TransferenciaCustodia;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.TransferenciasCustodia;

/// <summary>
/// Modulo 1.5 Transferencia de Custodia (spec v1.0 MVP).
///
/// Implementa los 3 escenarios:
///  - A: saliente inicia entrega voluntaria  (Iniciado -> PendienteAprobacion automatica).
///  - B: entrante reclama custodia            (Iniciado -> PendienteAprobacion del saliente).
///  - C: copropiedad gestiona el cambio       (Iniciado -> PendienteAprobacion del saliente).
///
/// State machine:
///   Iniciado -> PendienteAprobacion -> ActaEnValidacion -> AlertasActivas -> Ejecutado | Cancelado
///
/// MVP:
///   - Validacion IA del acta se queda en RequiereRevision (Fase 2 con T.1 Claude).
///   - Notificaciones diferidas (Fase 2 con T.2).
///   - Alertas dia 1/7/13 diferidas (cron job Fase 2).
///   - Reasignacion de tareas a "Sin asignar" diferida (hook 2.10 pendiente).
///   - Ajuste de facturacion 0.2 calculado y serializado a JSON, NO se aplica todavia.
///
/// RN-16: unicidad de proceso activo por copropiedad - se valida en codigo + UNIQUE parcial en migracion.
/// </summary>
public class TransferenciaCustodiaService : ITransferenciaCustodiaService
{
    private static readonly int VentanaAprobacionDias = 15; // RN-03

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly IBlobStorage _storage;
    private readonly Propia.Application.Notificaciones.INotificacionDispatcher _noti;

    public TransferenciaCustodiaService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        IBlobStorage storage,
        Propia.Application.Notificaciones.INotificacionDispatcher noti)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _storage = storage;
        _noti = noti;
    }

    /// <summary>
    /// T.2 hook: notifica a usuarios clave del proceso (saliente, entrante, iniciador).
    /// MVP: InApp para los usuarios admin de cada organizacion involucrada.
    /// El resolutor es best-effort (si no hay admin con vinculo activo, no falla).
    /// </summary>
    private async Task NotificarTransferenciaAsync(
        TransferenciaCustodia t, string asunto, string cuerpo, CancellationToken ct)
    {
        var requests = new List<Propia.Application.Notificaciones.EnviarNotificacionRequest>();

        async Task AgregarAdminsDeOrgAsync(Guid orgId)
        {
            // Admins = personas con UsuarioTenant Activo en cualquier tenant de la org.
            var personaIds = await _db.UsuariosTenant.AsNoTracking()
                .Where(u => u.Estado == EstadoUsuarioTenant.Activo)
                .Join(_db.Tenants.AsNoTracking(),
                    u => u.TenantId, te => te.Id,
                    (u, te) => new { u.PersonaId, te.OrganizacionId })
                .Where(x => x.OrganizacionId == orgId)
                .Select(x => x.PersonaId)
                .Distinct()
                .Take(10)
                .ToListAsync(ct);
            foreach (var pid in personaIds)
            {
                requests.Add(new Propia.Application.Notificaciones.EnviarNotificacionRequest(
                    Canal: Domain.Enums.CanalNotificacion.InApp,
                    Cuerpo: cuerpo,
                    TenantId: t.CopropiedadId,
                    PersonaDestinatariaId: pid,
                    Asunto: asunto,
                    Prioridad: Domain.Enums.PrioridadNotificacion.Alta,
                    ModuloOrigenCodigo: "1.5",
                    EntidadOrigenId: t.Id));
            }
        }

        if (t.OrganizacionSalienteId is { } sid) await AgregarAdminsDeOrgAsync(sid);
        if (t.OrganizacionEntranteId is { } eid && eid != t.OrganizacionSalienteId)
            await AgregarAdminsDeOrgAsync(eid);

        if (requests.Count > 0)
            await _noti.EnviarLoteAsync(requests, ct);
    }

    // ===========================================================================
    // Helpers
    // ===========================================================================

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private string? GetCanalActual() => _http.HttpContext?.Request?.Headers?["X-Canal"].ToString();

    /// <summary>Resuelve la organizacion actual a partir del tenant del usuario (entidades 1.5 son globales).</summary>
    private async Task<Guid?> GetOrganizacionIdActualAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return null;
        var orgId = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId).Select(t => t.OrganizacionId).FirstOrDefaultAsync(ct);
        return orgId;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat(name.Select(ch => invalid.Contains(ch) ? '_' : ch));
        return clean.Length > 200 ? clean.Substring(0, 200) : clean;
    }

    private async Task RegistrarEventoAsync(
        Guid transferenciaId,
        TipoEventoTransferencia tipo,
        Guid? actorUsuarioId,
        object? detalle,
        CancellationToken ct)
    {
        var evento = new TransferenciaEvento
        {
            TransferenciaId = transferenciaId,
            TipoEvento = tipo,
            ActorUsuarioId = actorUsuarioId,
            Canal = GetCanalActual(),
            DetalleJson = detalle is null ? null : JsonSerializer.Serialize(detalle)
        };
        _db.TransferenciaEventos.Add(evento);
        await _db.SaveChangesAsync(ct);
    }

    private async Task ValidarNoTransferenciaActivaAsync(Guid copropiedadId, CancellationToken ct)
    {
        var existe = await _db.TransferenciasCustodia.AsNoTracking()
            .AnyAsync(t => t.CopropiedadId == copropiedadId
                           && t.Estado != EstadoTransferencia.Ejecutado
                           && t.Estado != EstadoTransferencia.Cancelado, ct);
        if (existe)
            throw new InvalidOperationException(
                "RN-16: la copropiedad ya tiene una transferencia activa en curso.");
    }

    private async Task<TransferenciaCustodia> RequireAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.TransferenciasCustodia.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Transferencia no encontrada.");
        return t;
    }

    // ===========================================================================
    // Busqueda y listado
    // ===========================================================================

    public async Task<IReadOnlyList<CopropiedadBusquedaDto>> BuscarCopropiedadesAsync(string? query, CancellationToken ct)
    {
        var q = _db.Tenants.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var qLower = query.Trim().ToLower();
            q = q.Where(t => EF.Functions.ILike(t.Nombre, $"%{qLower}%")
                             || (t.Nit != null && EF.Functions.ILike(t.Nit, $"%{qLower}%"))
                             || (t.Ciudad != null && EF.Functions.ILike(t.Ciudad, $"%{qLower}%")));
        }
        var raw = await q
            .Where(t => t.Estado == EstadoCopropiedad.Activa || t.Estado == EstadoCopropiedad.Suspendida)
            .OrderBy(t => t.Nombre)
            .Take(50)
            .Select(t => new
            {
                t.Id,
                t.Nombre,
                t.Nit,
                t.Ciudad,
                t.EstadoCustodia,
                AdminId = t.OrganizacionId
            })
            .ToListAsync(ct);

        var orgIds = raw.Where(x => x.AdminId is not null).Select(x => x.AdminId!.Value).Distinct().ToList();
        var nombresOrg = await _db.Organizaciones.AsNoTracking()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Nombre, ct);

        return raw.Select(t => new CopropiedadBusquedaDto(
            t.Id,
            t.Nombre,
            t.Nit,
            t.Ciudad,
            t.AdminId is not null && nombresOrg.TryGetValue(t.AdminId.Value, out var n) ? n : null,
            t.EstadoCustodia)).ToList();
    }

    public async Task<IReadOnlyList<TransferenciaDto>> ListarMisTransferenciasAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var query = _db.TransferenciasCustodia.AsNoTracking().AsQueryable();
        if (orgId is not null)
        {
            query = query.Where(t => t.OrganizacionSalienteId == orgId
                                     || t.OrganizacionEntranteId == orgId);
        }
        else
        {
            return Array.Empty<TransferenciaDto>();
        }

        var lista = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return await ProyectarAsync(lista, ct);
    }

    public async Task<TransferenciaDto?> GetTransferenciaAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.TransferenciasCustodia.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;
        var dtos = await ProyectarAsync(new List<TransferenciaCustodia> { t }, ct);
        return dtos.FirstOrDefault();
    }

    public async Task<ExpedienteDto?> GetExpedienteAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.TransferenciasCustodia.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;

        var dtoLista = await ProyectarAsync(new List<TransferenciaCustodia> { t }, ct);
        var dto = dtoLista.First();

        var documentos = await _db.TransferenciaDocumentos.AsNoTracking()
            .Where(d => d.TransferenciaId == id)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new ActaDocumentoDto(
                d.Id, d.NombreArchivo, d.TipoMime, d.TamanioBytes, d.HashSha256,
                d.ResultadoValidacionIa, d.DetalleValidacionIaJson, d.SubidoPorUsuarioId, d.CreatedAt))
            .ToListAsync(ct);

        var eventos = await _db.TransferenciaEventos.AsNoTracking()
            .Where(e => e.TransferenciaId == id)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new EventoHistorialDto(
                e.Id, e.TipoEvento, e.ActorUsuarioId, e.Canal, e.DetalleJson, e.CreatedAt))
            .ToListAsync(ct);

        return new ExpedienteDto(dto, documentos, eventos, t.SnapshotEstadoJson, t.AjusteFacturacionJson);
    }

    private async Task<List<TransferenciaDto>> ProyectarAsync(
        List<TransferenciaCustodia> items, CancellationToken ct)
    {
        if (items.Count == 0) return new List<TransferenciaDto>();

        var copIds = items.Select(t => t.CopropiedadId).Distinct().ToList();
        var orgIds = items.SelectMany(t => new[] { t.OrganizacionSalienteId, t.OrganizacionEntranteId })
                          .Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();

        var cops = await _db.Tenants.AsNoTracking()
            .Where(t => copIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => new { t.Nombre, t.Nit }, ct);
        var orgs = await _db.Organizaciones.AsNoTracking()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Nombre, ct);

        var ids = items.Select(t => t.Id).ToList();
        var docsCount = await _db.TransferenciaDocumentos.AsNoTracking()
            .Where(d => ids.Contains(d.TransferenciaId))
            .GroupBy(d => d.TransferenciaId)
            .Select(g => new { Id = g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.C, ct);
        var evtCount = await _db.TransferenciaEventos.AsNoTracking()
            .Where(e => ids.Contains(e.TransferenciaId))
            .GroupBy(e => e.TransferenciaId)
            .Select(g => new { Id = g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.C, ct);

        return items.Select(t =>
        {
            cops.TryGetValue(t.CopropiedadId, out var c);
            string? salNombre = t.OrganizacionSalienteId is { } sid && orgs.TryGetValue(sid, out var sn) ? sn : null;
            string? entNombre = t.OrganizacionEntranteId is { } eid && orgs.TryGetValue(eid, out var en) ? en : null;
            return new TransferenciaDto(
                t.Id,
                t.CopropiedadId,
                c?.Nombre ?? "(desconocida)",
                c?.Nit,
                t.OrganizacionSalienteId,
                salNombre,
                t.OrganizacionEntranteId,
                entNombre,
                t.Escenario,
                t.Estado,
                t.IniciadoPorUsuarioId,
                t.FechaEfectivaSaliente,
                t.FechaVencimientoVentana,
                t.FechaCorte,
                t.ActaEntregaDocumentoId,
                t.CreatedAt,
                docsCount.TryGetValue(t.Id, out var dc) ? dc : 0,
                evtCount.TryGetValue(t.Id, out var ec) ? ec : 0);
        }).ToList();
    }

    // ===========================================================================
    // Escenario A: saliente entrega voluntariamente
    // ===========================================================================

    public async Task<TransferenciaDto> IniciarEntregaVoluntariaAsync(
        IniciarEntregaVoluntariaRequest req, CancellationToken ct)
    {
        if (req.FechaEfectivaTerminacion < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            throw new InvalidOperationException("La fecha efectiva no puede estar en el pasado.");

        await ValidarNoTransferenciaActivaAsync(req.CopropiedadId, ct);

        var cop = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == req.CopropiedadId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");

        var orgSaliente = await GetOrganizacionIdActualAsync(ct);
        if (orgSaliente is null || cop.OrganizacionId != orgSaliente)
            throw new InvalidOperationException(
                "Solo la organizacion actualmente a cargo puede iniciar entrega voluntaria.");

        var t = new TransferenciaCustodia
        {
            CopropiedadId = req.CopropiedadId,
            OrganizacionSalienteId = orgSaliente,
            OrganizacionEntranteId = req.OrganizacionEntranteId,
            Escenario = EscenarioTransferencia.EntregaVoluntaria,
            Estado = EstadoTransferencia.PendienteAprobacion, // saliente ya consintio al iniciar
            IniciadoPorUsuarioId = GetUsuarioActualId(),
            FechaEfectivaSaliente = req.FechaEfectivaTerminacion,
            FechaVencimientoVentana = req.FechaEfectivaTerminacion.AddDays(VentanaAprobacionDias)
        };
        _db.TransferenciasCustodia.Add(t);

        cop.EstadoCustodia = EstadoCustodia.EnTransferencia;
        await _db.SaveChangesAsync(ct);

        await RegistrarEventoAsync(t.Id, TipoEventoTransferencia.SolicitudEnviada,
            t.IniciadoPorUsuarioId,
            new { Escenario = "EntregaVoluntaria", FechaTerminacion = req.FechaEfectivaTerminacion },
            ct);
        await RegistrarEventoAsync(t.Id, TipoEventoTransferencia.AprobacionSaliente,
            t.IniciadoPorUsuarioId,
            new { Motivo = "Aprobacion implicita al iniciar voluntariamente." }, ct);

        await NotificarTransferenciaAsync(t,
            $"Transferencia iniciada - {cop.Nombre}",
            $"El saliente inicio el proceso de entrega voluntaria con fecha efectiva {req.FechaEfectivaTerminacion:yyyy-MM-dd}. Tienes 15 dias para coordinar.",
            ct);

        return (await GetTransferenciaAsync(t.Id, ct))!;
    }

    // ===========================================================================
    // Escenario B: entrante reclama custodia
    // ===========================================================================

    public async Task<TransferenciaDto> ReclamarCustodiaAsync(ReclamarCustodiaRequest req, CancellationToken ct)
    {
        if (!req.DeclaracionLegitimidad)
            throw new InvalidOperationException("Debe aceptar la declaracion de legitimidad.");

        await ValidarNoTransferenciaActivaAsync(req.CopropiedadId, ct);

        var cop = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == req.CopropiedadId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");

        var orgEntrante = await GetOrganizacionIdActualAsync(ct)
            ?? throw new InvalidOperationException("No hay organizacion entrante activa.");

        if (cop.OrganizacionId == orgEntrante)
            throw new InvalidOperationException("La organizacion ya administra esta copropiedad.");

        var t = new TransferenciaCustodia
        {
            CopropiedadId = req.CopropiedadId,
            OrganizacionSalienteId = cop.OrganizacionId, // puede ser null si la PH estaba sin admin
            OrganizacionEntranteId = orgEntrante,
            Escenario = EscenarioTransferencia.ReclamacionEntrante,
            Estado = EstadoTransferencia.PendienteAprobacion,
            IniciadoPorUsuarioId = GetUsuarioActualId(),
            FechaVencimientoVentana = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(VentanaAprobacionDias)
        };
        _db.TransferenciasCustodia.Add(t);

        cop.EstadoCustodia = EstadoCustodia.EnTransferencia;
        await _db.SaveChangesAsync(ct);

        await RegistrarEventoAsync(t.Id, TipoEventoTransferencia.SolicitudEnviada,
            t.IniciadoPorUsuarioId,
            new { Escenario = "ReclamacionEntrante", DeclaracionLegitimidad = true }, ct);

        await NotificarTransferenciaAsync(t,
            $"Reclamacion de custodia - {cop.Nombre}",
            "Una organizacion entrante reclama la custodia. Si eres el saliente, dispones de 15 dias para aprobar o rechazar.",
            ct);

        return (await GetTransferenciaAsync(t.Id, ct))!;
    }

    // ===========================================================================
    // Escenario C: la copropiedad gestiona el cambio
    // ===========================================================================

    public async Task<TransferenciaDto> IniciarPorCopropiedadAsync(
        IniciarPorCopropiedadRequest req, CancellationToken ct)
    {
        await ValidarNoTransferenciaActivaAsync(req.CopropiedadId, ct);

        var cop = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == req.CopropiedadId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");

        var orgEntrante = await _db.Organizaciones.AsNoTracking()
            .AnyAsync(o => o.Id == req.OrganizacionEntranteId, ct);
        if (!orgEntrante)
            throw new InvalidOperationException("Organizacion entrante no existe.");

        var t = new TransferenciaCustodia
        {
            CopropiedadId = req.CopropiedadId,
            OrganizacionSalienteId = cop.OrganizacionId,
            OrganizacionEntranteId = req.OrganizacionEntranteId,
            Escenario = EscenarioTransferencia.GestionCopropiedad,
            Estado = EstadoTransferencia.PendienteAprobacion,
            IniciadoPorUsuarioId = GetUsuarioActualId(),
            FechaVencimientoVentana = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(VentanaAprobacionDias)
        };
        _db.TransferenciasCustodia.Add(t);

        cop.EstadoCustodia = EstadoCustodia.EnTransferencia;
        await _db.SaveChangesAsync(ct);

        await RegistrarEventoAsync(t.Id, TipoEventoTransferencia.SolicitudEnviada,
            t.IniciadoPorUsuarioId,
            new { Escenario = "GestionCopropiedad", OrganizacionEntranteId = req.OrganizacionEntranteId }, ct);

        await NotificarTransferenciaAsync(t,
            $"Cambio de administrador - {cop.Nombre}",
            "La copropiedad inicio un proceso de cambio de administrador. Revisa el expediente y aprueba/rechaza si corresponde.",
            ct);

        return (await GetTransferenciaAsync(t.Id, ct))!;
    }

    // ===========================================================================
    // Acta de asamblea
    // ===========================================================================

    public async Task<ActaDocumentoDto> SubirActaAsync(
        Guid transferenciaId, SubirActaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ContenidoBase64))
            throw new InvalidOperationException("ContenidoBase64 obligatorio.");
        if (req.TamanioBytes <= 0) throw new InvalidOperationException("Tamanio invalido.");

        var t = await RequireAsync(transferenciaId, ct);
        if (t.Estado != EstadoTransferencia.PendienteAprobacion
            && t.Estado != EstadoTransferencia.ActaEnValidacion
            && t.Estado != EstadoTransferencia.AlertasActivas)
            throw new InvalidOperationException(
                $"No se puede subir acta en estado {t.Estado}.");

        var contenido = Convert.FromBase64String(req.ContenidoBase64);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var key = $"transferencias/{transferenciaId}/actas/{Guid.NewGuid():N}_{SanitizeFileName(req.NombreArchivo)}";
        using (var ms = new MemoryStream(contenido))
        {
            await _storage.UploadAsync(key, ms, req.TipoMime, ct);
        }

        var doc = new TransferenciaDocumento
        {
            TransferenciaId = transferenciaId,
            NombreArchivo = req.NombreArchivo.Trim(),
            TipoMime = req.TipoMime,
            TamanioBytes = req.TamanioBytes,
            UrlStorage = key,
            HashSha256 = hash,
            // MVP: la validacion IA real esta en Fase 2. Queda en RequiereRevision para revision manual.
            ResultadoValidacionIa = ResultadoValidacionActa.RequiereRevision,
            SubidoPorUsuarioId = GetUsuarioActualId()
        };
        _db.TransferenciaDocumentos.Add(doc);

        // El acta subida transiciona el estado a ActaEnValidacion (si venia de PendienteAprobacion).
        if (t.Estado == EstadoTransferencia.PendienteAprobacion)
            t.Estado = EstadoTransferencia.ActaEnValidacion;

        await _db.SaveChangesAsync(ct);

        await RegistrarEventoAsync(transferenciaId, TipoEventoTransferencia.ActaSubida,
            doc.SubidoPorUsuarioId,
            new { doc.NombreArchivo, doc.TamanioBytes, doc.HashSha256 }, ct);

        return new ActaDocumentoDto(doc.Id, doc.NombreArchivo, doc.TipoMime, doc.TamanioBytes,
            doc.HashSha256, doc.ResultadoValidacionIa, doc.DetalleValidacionIaJson,
            doc.SubidoPorUsuarioId, doc.CreatedAt);
    }

    // ===========================================================================
    // Aprobacion / Rechazo del saliente
    // ===========================================================================

    public async Task<bool> AprobarComoSalienteAsync(
        Guid transferenciaId, AprobarTransferenciaRequest req, CancellationToken ct)
    {
        var t = await RequireAsync(transferenciaId, ct);
        if (t.Estado != EstadoTransferencia.PendienteAprobacion)
            throw new InvalidOperationException(
                $"La aprobacion solo aplica en PendienteAprobacion (actual: {t.Estado}).");

        var orgActual = await GetOrganizacionIdActualAsync(ct);
        if (t.OrganizacionSalienteId != orgActual)
            throw new InvalidOperationException("Solo el saliente puede aprobar.");

        // Si ya hay acta subida -> ActaEnValidacion. Si no, queda PendienteAprobacion esperando acta.
        var hayActa = await _db.TransferenciaDocumentos.AnyAsync(d => d.TransferenciaId == transferenciaId, ct);
        t.Estado = hayActa ? EstadoTransferencia.ActaEnValidacion : EstadoTransferencia.PendienteAprobacion;

        await _db.SaveChangesAsync(ct);

        await RegistrarEventoAsync(transferenciaId, TipoEventoTransferencia.AprobacionSaliente,
            GetUsuarioActualId(),
            new { req.Notas }, ct);

        await NotificarTransferenciaAsync(t,
            "Saliente aprobo la transferencia",
            "El administrador saliente aprobo el proceso. Falta subir el acta de asamblea y ejecutar el corte.",
            ct);

        return true;
    }

    public async Task<bool> RechazarComoSalienteAsync(
        Guid transferenciaId, RechazarTransferenciaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Motivo))
            throw new InvalidOperationException("Motivo de rechazo obligatorio.");

        var t = await RequireAsync(transferenciaId, ct);
        if (t.Estado != EstadoTransferencia.PendienteAprobacion)
            throw new InvalidOperationException(
                $"El rechazo solo aplica en PendienteAprobacion (actual: {t.Estado}).");

        var orgActual = await GetOrganizacionIdActualAsync(ct);
        if (t.OrganizacionSalienteId != orgActual)
            throw new InvalidOperationException("Solo el saliente puede rechazar.");

        t.Estado = EstadoTransferencia.Cancelado;

        var cop = await _db.Tenants.FirstOrDefaultAsync(c => c.Id == t.CopropiedadId, ct);
        if (cop is not null && cop.EstadoCustodia == EstadoCustodia.EnTransferencia)
        {
            cop.EstadoCustodia = cop.OrganizacionId is null
                ? EstadoCustodia.SinAdmin : EstadoCustodia.ConAdmin;
        }

        await _db.SaveChangesAsync(ct);

        await RegistrarEventoAsync(transferenciaId, TipoEventoTransferencia.RechazoSaliente,
            GetUsuarioActualId(),
            new { req.Motivo, EscaladoAdmin = true }, ct);
        await RegistrarEventoAsync(transferenciaId, TipoEventoTransferencia.EscaladoAdmin,
            null,
            new { Razon = "Rechazo del saliente requiere arbitraje plataforma." }, ct);

        await NotificarTransferenciaAsync(t,
            "Saliente rechazo la transferencia",
            $"El saliente rechazo el proceso (motivo: {req.Motivo}). El caso fue escalado al equipo de la plataforma para arbitraje.",
            ct);

        return true;
    }

    // ===========================================================================
    // Cancelacion
    // ===========================================================================

    public async Task<bool> CancelarAsync(Guid transferenciaId, string motivo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Motivo de cancelacion obligatorio.");

        var t = await RequireAsync(transferenciaId, ct);
        if (t.Estado == EstadoTransferencia.Ejecutado || t.Estado == EstadoTransferencia.Cancelado)
            throw new InvalidOperationException(
                $"No se puede cancelar una transferencia en estado {t.Estado}.");

        var userId = GetUsuarioActualId();
        if (t.IniciadoPorUsuarioId != userId)
            throw new InvalidOperationException("Solo el iniciador puede cancelar (admin en Fase 2).");

        t.Estado = EstadoTransferencia.Cancelado;
        var cop = await _db.Tenants.FirstOrDefaultAsync(c => c.Id == t.CopropiedadId, ct);
        if (cop is not null && cop.EstadoCustodia == EstadoCustodia.EnTransferencia)
        {
            cop.EstadoCustodia = cop.OrganizacionId is null
                ? EstadoCustodia.SinAdmin : EstadoCustodia.ConAdmin;
        }
        await _db.SaveChangesAsync(ct);

        await RegistrarEventoAsync(transferenciaId, TipoEventoTransferencia.Cancelacion,
            userId, new { Motivo = motivo }, ct);

        return true;
    }

    // ===========================================================================
    // Ejecucion del corte
    // ===========================================================================

    public async Task<TransferenciaDto> EjecutarCorteAsync(Guid transferenciaId, CancellationToken ct)
    {
        var t = await RequireAsync(transferenciaId, ct);
        if (t.Estado != EstadoTransferencia.ActaEnValidacion
            && t.Estado != EstadoTransferencia.AlertasActivas)
            throw new InvalidOperationException(
                $"El corte solo aplica con acta validada (estado actual: {t.Estado}).");

        if (t.OrganizacionEntranteId is null)
            throw new InvalidOperationException("Se requiere una organizacion entrante identificada para el corte.");

        var hayActa = await _db.TransferenciaDocumentos.AnyAsync(d => d.TransferenciaId == transferenciaId, ct);
        if (!hayActa)
            throw new InvalidOperationException("No hay acta de asamblea adjunta.");

        // Snapshot del estado de la PH al momento del corte (MVP: contadores agregados cross-modulo).
        var snapshot = await ConstruirSnapshotAsync(t.CopropiedadId, ct);
        // Calculo de prorrateo (MVP: stub deterministico, no aplica facturacion real).
        var ajuste = ConstruirAjusteFacturacion(t);

        t.SnapshotEstadoJson = JsonSerializer.Serialize(snapshot);
        t.AjusteFacturacionJson = JsonSerializer.Serialize(ajuste);
        t.FechaCorte = DateTimeOffset.UtcNow;
        t.Estado = EstadoTransferencia.Ejecutado;

        // Cambio de organizacion en la copropiedad
        var cop = await _db.Tenants.FirstOrDefaultAsync(c => c.Id == t.CopropiedadId, ct);
        if (cop is not null)
        {
            cop.OrganizacionId = t.OrganizacionEntranteId;
            cop.EstadoCustodia = EstadoCustodia.ConAdmin;
        }

        // Revocacion de vinculos UsuarioTenant del saliente. MVP: marca Estado=Inactivo + motivo + fecha.
        // No hay FK directa Persona->Organizacion: se aplica a TODOS los usuarios del tenant cuyo Estado != Inactivo.
        // En Fase 2 se filtrara por organizacion una vez exista la relacion explicita (Persona->Organizacion).
        var usuariosVigentes = await _db.UsuariosTenant
            .Where(u => u.TenantId == t.CopropiedadId && u.Estado != EstadoUsuarioTenant.Inactivo)
            .ToListAsync(ct);
        foreach (var u in usuariosVigentes)
        {
            u.Estado = EstadoUsuarioTenant.Inactivo;
            u.MotivoRevocacion = $"Transferencia de custodia ejecutada ({transferenciaId}).";
            u.FechaRevocacion = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        await RegistrarEventoAsync(transferenciaId, TipoEventoTransferencia.CorteEjecutado,
            GetUsuarioActualId(),
            new
            {
                FechaCorte = t.FechaCorte,
                t.OrganizacionEntranteId,
                t.OrganizacionSalienteId,
                Snapshot = snapshot,
                Ajuste = ajuste
            }, ct);

        await NotificarTransferenciaAsync(t,
            $"Corte ejecutado - {cop?.Nombre ?? "copropiedad"}",
            $"La transferencia se ejecuto con fecha {t.FechaCorte:yyyy-MM-dd HH:mm}. La copropiedad cambio de administrador. Snapshot y ajuste de facturacion disponibles en el expediente.",
            ct);

        return (await GetTransferenciaAsync(transferenciaId, ct))!;
    }

    private async Task<object> ConstruirSnapshotAsync(Guid copropiedadId, CancellationToken ct)
    {
        // Conteos cross-modulo para auditoria del corte (queries IgnoreQueryFilters por ser servicio global 1.5).
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        // Estados de tarea son configurables (TareaEstado.EsTerminal=true => cerradas/canceladas).
        var tareasAbiertas = await _db.Tareas.IgnoreQueryFilters()
            .Where(x => x.TenantId == copropiedadId
                        && (x.Estado == null || !x.Estado.EsTerminal))
            .CountAsync(ct);

        var pqrsdAbiertos = await _db.PqrsdExpedientes.IgnoreQueryFilters()
            .Where(x => x.TenantId == copropiedadId
                        && x.Estado != EstadoPqrsd.Cerrada
                        && x.Estado != EstadoPqrsd.ViaInternaAgotada)
            .CountAsync(ct);

        // SaldoTotal es propiedad calculada (no mapeada) -> sumo SaldoCapital + SaldoIntereses en SQL.
        var moraTotal = await _db.CarteraUnidades.IgnoreQueryFilters()
            .Where(x => x.TenantId == copropiedadId)
            .SumAsync(x => (decimal?)(x.SaldoCapital + x.SaldoIntereses), ct) ?? 0m;

        var asambleasDelAnio = await _db.Sesiones.IgnoreQueryFilters()
            .Where(x => x.TenantId == copropiedadId && x.FechaSesion.Year == hoy.Year)
            .CountAsync(ct);

        return new
        {
            FechaSnapshot = DateTimeOffset.UtcNow,
            CopropiedadId = copropiedadId,
            TareasAbiertas = tareasAbiertas,
            PqrsdAbiertos = pqrsdAbiertos,
            MoraTotalCop = moraTotal,
            AsambleasAnio = asambleasDelAnio
        };
    }

    private static object ConstruirAjusteFacturacion(TransferenciaCustodia t)
    {
        // MVP: prorrateo deterministico simple. 0.2 lo materializara en Fase 2.
        var fechaCorte = t.FechaCorte ?? DateTimeOffset.UtcNow;
        var diaMes = fechaCorte.Day;
        var diasMes = DateTime.DaysInMonth(fechaCorte.Year, fechaCorte.Month);
        var pctSaliente = Math.Round((decimal)diaMes / diasMes, 4);
        var pctEntrante = 1m - pctSaliente;
        return new
        {
            ReferenciaTemporal = new { fechaCorte.Year, fechaCorte.Month, DiaCorte = diaMes, DiasMes = diasMes },
            PorcentajeSaliente = pctSaliente,
            PorcentajeEntrante = pctEntrante,
            Nota = "Calculo de referencia. Aplicacion real diferida a Fase 2 (modulo 0.2)."
        };
    }

    // ===========================================================================
    // Resumen
    // ===========================================================================

    public async Task<ResumenTransferenciasDto> GetResumenAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        if (orgId is null) return new ResumenTransferenciasDto(0, 0, 0, 0, 0);

        var miOrg = orgId.Value;

        var enCurso = await _db.TransferenciasCustodia.AsNoTracking()
            .CountAsync(t => (t.OrganizacionSalienteId == miOrg || t.OrganizacionEntranteId == miOrg)
                             && t.Estado != EstadoTransferencia.Ejecutado
                             && t.Estado != EstadoTransferencia.Cancelado, ct);

        var completadas = await _db.TransferenciasCustodia.AsNoTracking()
            .CountAsync(t => (t.OrganizacionSalienteId == miOrg || t.OrganizacionEntranteId == miOrg)
                             && t.Estado == EstadoTransferencia.Ejecutado, ct);

        var pendienteAprobacionMia = await _db.TransferenciasCustodia.AsNoTracking()
            .CountAsync(t => t.OrganizacionSalienteId == miOrg
                             && t.Estado == EstadoTransferencia.PendienteAprobacion, ct);

        var pendienteMiActa = await _db.TransferenciasCustodia.AsNoTracking()
            .CountAsync(t => t.OrganizacionEntranteId == miOrg
                             && (t.Estado == EstadoTransferencia.Iniciado
                                 || t.Estado == EstadoTransferencia.PendienteAprobacion), ct);

        var canceladas = await _db.TransferenciasCustodia.AsNoTracking()
            .CountAsync(t => (t.OrganizacionSalienteId == miOrg || t.OrganizacionEntranteId == miOrg)
                             && t.Estado == EstadoTransferencia.Cancelado, ct);

        return new ResumenTransferenciasDto(enCurso, completadas, pendienteAprobacionMia, pendienteMiActa, canceladas);
    }
}
