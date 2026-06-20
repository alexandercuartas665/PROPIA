using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Reservas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Reservas;

/// <summary>
/// Modulo 2.13 Reservas de Zonas Comunes - implementacion MVP (spec v1.0).
/// Ver IReservasService para alcance y diferidos.
/// </summary>
public class ReservasService : IReservasService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly Propia.Application.Notificaciones.INotificacionDispatcher _noti;

    public ReservasService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        Propia.Application.Notificaciones.INotificacionDispatcher noti)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _noti = noti;
    }

    private async Task NotificarResidenteAsync(
        Guid? personaId, Guid? entidadOrigen, string asunto, string cuerpo,
        Domain.Enums.PrioridadNotificacion prioridad, CancellationToken ct)
    {
        if (personaId is null) return;
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return;
        // Best-effort: la notificacion es un efecto secundario. Si falla (ej. la persona aun no
        // tiene usuario al que enrutar el InApp) NO debe tumbar la operacion de negocio (crear /
        // aprobar / cancelar reserva), que ya se persistio antes de llamar aqui.
        try
        {
            await _noti.EnviarAsync(new Propia.Application.Notificaciones.EnviarNotificacionRequest(
                Canal: Domain.Enums.CanalNotificacion.InApp,
                Cuerpo: cuerpo, TenantId: tenantId, PersonaDestinatariaId: personaId,
                Asunto: asunto, Prioridad: prioridad,
                ModuloOrigenCodigo: "2.13", EntidadOrigenId: entidadOrigen), ct);
        }
        catch { /* notificacion best-effort: no propagar */ }
    }

    private Guid GetPersonaActualId()
    {
        var p = _http.HttpContext?.User?.FindFirstValue("persona_id");
        return Guid.TryParse(p, out var id) ? id : Guid.Empty;
    }

    private Guid RequireTenantId() =>
        _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

    // ===========================================================================
    // Configuracion de zona
    // ===========================================================================

    public async Task<IReadOnlyList<ZonaConfigDto>> ListarConfigsAsync(CancellationToken ct)
    {
        var configs = await _db.ZonaConfigReservas.AsNoTracking().Include(c => c.Franjas).ToListAsync(ct);
        var zonaIds = configs.Select(c => c.ZonaComunId).Distinct().ToList();
        var zonas = await _db.ZonasComunes.AsNoTracking()
            .Where(z => zonaIds.Contains(z.Id))
            .Select(z => new { z.Id, z.Nombre })
            .ToListAsync(ct);
        var nombrePorZona = zonas.ToDictionary(z => z.Id, z => z.Nombre);
        return configs.Select(c => MapConfig(c, nombrePorZona.TryGetValue(c.ZonaComunId, out var n) ? n : "(zona)")).ToList();
    }

    public async Task<ZonaConfigDto?> GetConfigAsync(Guid zonaComunId, CancellationToken ct)
    {
        var c = await _db.ZonaConfigReservas.AsNoTracking().Include(x => x.Franjas)
            .FirstOrDefaultAsync(x => x.ZonaComunId == zonaComunId, ct);
        if (c is null) return null;
        var n = await _db.ZonasComunes.AsNoTracking().Where(z => z.Id == zonaComunId).Select(z => z.Nombre).FirstOrDefaultAsync(ct) ?? "(zona)";
        return MapConfig(c, n);
    }

    private static ZonaConfigDto MapConfig(ZonaConfigReserva c, string zonaNombre) => new(
        c.Id, c.ZonaComunId, zonaNombre,
        c.RequiereAprobacion, c.MaxReservasActivasPorUnidad, c.BloqueoPorCartera,
        c.AnticipacionMinimaHoras, c.AnticipacionMaximaDias,
        c.DuracionMinimaMinutos, c.DuracionMaximaMinutos, c.IntervaloBloqueMinutos,
        c.TieneTarifa, c.ModalidadCobro, c.ValorTarifa, c.PoliticaReembolso, c.ValorPenalidadCancelacion,
        c.LimiteCancelacionHoras, c.PermiteCancelacionResidente, c.CancelacionTardia,
        c.ReglamentoTexto, c.RequiereAceptacionReglamento, c.VisibleParaResidentes,
        c.Franjas.Select(f => new FranjaDto(f.Id, f.DiaSemana, f.HoraApertura, f.HoraCierre, f.Activa)).ToList());

    public async Task<ZonaConfigDto> GuardarConfigAsync(GuardarConfigRequest req, CancellationToken ct)
    {
        if (req.DuracionMinimaMinutos < 15) throw new InvalidOperationException("DuracionMinima muy corta (>=15 min).");
        if (req.DuracionMaximaMinutos < req.DuracionMinimaMinutos)
            throw new InvalidOperationException("DuracionMaxima debe ser >= DuracionMinima.");
        if (req.TieneTarifa && req.ValorTarifa <= 0)
            throw new InvalidOperationException("Tarifa > 0 cuando TieneTarifa=true.");
        var tenantId = RequireTenantId();
        var existeZona = await _db.ZonasComunes.AnyAsync(z => z.Id == req.ZonaComunId, ct);
        if (!existeZona) throw new InvalidOperationException("Zona no encontrada.");

        // No incluimos Franjas en el load: las reemplazamos con un DELETE directo (ExecuteDelete)
        // para evitar AggregateUpdateConcurrencyException que EF/Npgsql emite cuando un batch mezcla
        // varios DELETE de hijos con el UPDATE del padre y el conteo de rows-affected no le cuadra.
        var c = await _db.ZonaConfigReservas
            .FirstOrDefaultAsync(x => x.ZonaComunId == req.ZonaComunId, ct);
        var esNueva = c is null;
        if (c is null)
        {
            c = new ZonaConfigReserva { Id = Guid.NewGuid(), TenantId = tenantId, ZonaComunId = req.ZonaComunId };
            _db.ZonaConfigReservas.Add(c);
        }
        c.RequiereAprobacion = req.RequiereAprobacion;
        c.MaxReservasActivasPorUnidad = req.MaxReservasActivasPorUnidad;
        c.BloqueoPorCartera = req.BloqueoPorCartera;
        c.AnticipacionMinimaHoras = req.AnticipacionMinimaHoras;
        c.AnticipacionMaximaDias = req.AnticipacionMaximaDias;
        c.DuracionMinimaMinutos = req.DuracionMinimaMinutos;
        c.DuracionMaximaMinutos = req.DuracionMaximaMinutos;
        c.IntervaloBloqueMinutos = req.IntervaloBloqueMinutos;
        c.TieneTarifa = req.TieneTarifa;
        c.ModalidadCobro = req.ModalidadCobro;
        c.ValorTarifa = req.ValorTarifa;
        c.PoliticaReembolso = req.PoliticaReembolso;
        c.ValorPenalidadCancelacion = req.ValorPenalidadCancelacion;
        c.LimiteCancelacionHoras = req.LimiteCancelacionHoras;
        c.PermiteCancelacionResidente = req.PermiteCancelacionResidente;
        c.CancelacionTardia = req.CancelacionTardia;
        c.ReglamentoTexto = req.ReglamentoTexto?.Trim();
        c.RequiereAceptacionReglamento = req.RequiereAceptacionReglamento;
        c.VisibleParaResidentes = req.VisibleParaResidentes;
        c.UpdatedAt = DateTimeOffset.UtcNow;

        // Reemplazar franjas: borrar las viejas con un solo DELETE WHERE (sin tracking) y crear las nuevas.
        if (!esNueva)
            await _db.ZonaFranjas.Where(f => f.ZonaConfigReservaId == c.Id).ExecuteDeleteAsync(ct);
        c.Franjas.Clear();
        foreach (var f in req.Franjas)
        {
            if (f.HoraCierre <= f.HoraApertura) continue;
            // Add via DbSet (no via la navegacion): ZonaFranja trae un Id no-default de BaseEntity,
            // y EF marcaria como Unchanged una entidad nueva agregada a la navegacion de un padre
            // trackeado (la cree como UPDATE y afecte 0 filas). Add explicito fuerza el estado Added.
            _db.ZonaFranjas.Add(new ZonaFranja
            {
                TenantId = tenantId,
                ZonaConfigReservaId = c.Id,
                DiaSemana = f.DiaSemana,
                HoraApertura = f.HoraApertura,
                HoraCierre = f.HoraCierre,
                Activa = f.Activa
            });
        }
        await _db.SaveChangesAsync(ct);
        return (await GetConfigAsync(c.ZonaComunId, ct))!;
    }

    // ===========================================================================
    // Galeria + disponibilidad
    // ===========================================================================

    public async Task<IReadOnlyList<ZonaGaleriaDto>> ListarGaleriaAsync(bool soloVisibles, CancellationToken ct)
    {
        var zonas = await _db.ZonasComunes.AsNoTracking()
            .Where(z => z.EsReservable)
            .Select(z => new
            {
                z.Id,
                z.Nombre,
                z.Descripcion,
                z.CapacidadPersonas,
                z.EsReservable,
                Estado = z.Estado.ToString(),
                Cfg = _db.ZonaConfigReservas.Where(c => c.ZonaComunId == z.Id).Select(c => new
                {
                    c.TieneTarifa,
                    c.ValorTarifa,
                    c.ModalidadCobro,
                    c.VisibleParaResidentes
                }).FirstOrDefault()
            })
            .ToListAsync(ct);

        var list = zonas.Where(z => !soloVisibles || z.Cfg is null || z.Cfg.VisibleParaResidentes);
        return list.Select(z => new ZonaGaleriaDto(
            z.Id, z.Nombre, z.Descripcion, null, z.CapacidadPersonas, z.EsReservable, z.Estado,
            z.Cfg?.TieneTarifa ?? false,
            z.Cfg?.TieneTarifa == true ? z.Cfg.ValorTarifa : null,
            z.Cfg?.TieneTarifa == true ? z.Cfg.ModalidadCobro : null)).ToList();
    }

    public async Task<DisponibilidadDto> CalcularDisponibilidadAsync(Guid zonaComunId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde) throw new InvalidOperationException("Periodo invalido.");
        if ((hasta.DayNumber - desde.DayNumber) > 60)
            throw new InvalidOperationException("Periodo maximo de calculo: 60 dias.");

        var cfg = await _db.ZonaConfigReservas.AsNoTracking().Include(c => c.Franjas)
            .FirstOrDefaultAsync(c => c.ZonaComunId == zonaComunId, ct);
        if (cfg is null) return new DisponibilidadDto(zonaComunId, desde, hasta, Array.Empty<SlotDisponibilidadDto>());

        var reservas = await _db.Reservas.AsNoTracking()
            .Where(r => r.ZonaComunId == zonaComunId
                       && r.Fecha >= desde && r.Fecha <= hasta
                       && r.Estado != EstadoReserva.CanceladaResidente
                       && r.Estado != EstadoReserva.CanceladaAdmin
                       && r.Estado != EstadoReserva.CanceladaSistema
                       && r.Estado != EstadoReserva.Expirada)
            .Select(r => new { r.Fecha, r.HoraInicio, r.HoraFin })
            .ToListAsync(ct);

        var bloqueos = await _db.ZonaBloqueos.AsNoTracking()
            .Where(b => b.ZonaComunId == zonaComunId
                       && b.FechaInicio <= hasta && b.FechaFin >= desde)
            .ToListAsync(ct);

        var slots = new List<SlotDisponibilidadDto>();
        for (var d = desde; d <= hasta; d = d.AddDays(1))
        {
            var dia = ConvertirDiaSemana(d.DayOfWeek);
            var franjasDia = cfg.Franjas.Where(f => f.DiaSemana == dia && f.Activa).ToList();
            foreach (var f in franjasDia)
            {
                var paso = TimeSpan.FromMinutes(cfg.IntervaloBloqueMinutos);
                for (var h = f.HoraApertura; h.Add(paso) <= f.HoraCierre; h = h.Add(paso))
                {
                    var horaIni = h;
                    var horaFin = h.Add(paso);
                    var estado = "DISPONIBLE";
                    string? motivo = null;

                    // bloqueo manual
                    var bloq = bloqueos.FirstOrDefault(b =>
                        b.FechaInicio <= d && b.FechaFin >= d
                        && (b.HoraInicio is null || (b.HoraInicio <= horaIni && b.HoraFin >= horaFin)));
                    if (bloq is not null)
                    {
                        estado = "BLOQUEADO";
                        if (cfg.MotivoBloqueoVisible)
                            motivo = bloq.MotivoPersonalizado ?? bloq.Etiqueta.ToString();
                    }
                    // reserva
                    else if (reservas.Any(r => r.Fecha == d && r.HoraInicio < horaFin && r.HoraFin > horaIni))
                    {
                        estado = "OCUPADO";
                    }
                    slots.Add(new SlotDisponibilidadDto(d, horaIni, horaFin, estado, motivo));
                }
            }
        }
        return new DisponibilidadDto(zonaComunId, desde, hasta, slots);
    }

    private static DiaSemanaReserva ConvertirDiaSemana(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => DiaSemanaReserva.Lunes,
        DayOfWeek.Tuesday => DiaSemanaReserva.Martes,
        DayOfWeek.Wednesday => DiaSemanaReserva.Miercoles,
        DayOfWeek.Thursday => DiaSemanaReserva.Jueves,
        DayOfWeek.Friday => DiaSemanaReserva.Viernes,
        DayOfWeek.Saturday => DiaSemanaReserva.Sabado,
        _ => DiaSemanaReserva.Domingo
    };

    // ===========================================================================
    // Reservas
    // ===========================================================================

    public async Task<IReadOnlyList<ReservaDto>> ListarReservasAsync(
        DateOnly? desde, DateOnly? hasta, Guid? zonaId, Guid? personaId,
        EstadoReserva? estado, CancellationToken ct)
    {
        var q = _db.Reservas.AsNoTracking().AsQueryable();
        if (desde is { } d) q = q.Where(r => r.Fecha >= d);
        if (hasta is { } h) q = q.Where(r => r.Fecha <= h);
        if (zonaId is { } z) q = q.Where(r => r.ZonaComunId == z);
        if (personaId is { } p) q = q.Where(r => r.PersonaId == p);
        if (estado is { } e) q = q.Where(r => r.Estado == e);
        var list = await q.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.HoraInicio).Take(200).ToListAsync(ct);
        var result = new List<ReservaDto>(list.Count);
        foreach (var r in list) result.Add(await MapReservaAsync(r, ct));
        return result;
    }

    public async Task<ReservaDto?> GetReservaAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.Reservas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? null : await MapReservaAsync(r, ct);
    }

    private async Task<ReservaDto> MapReservaAsync(Reserva r, CancellationToken ct)
    {
        var zona = await _db.ZonasComunes.AsNoTracking().Where(z => z.Id == r.ZonaComunId).Select(z => z.Nombre).FirstOrDefaultAsync(ct) ?? "(zona)";
        var per = await _db.Personas.AsNoTracking().Where(p => p.Id == r.PersonaId).Select(p => p.Nombres + " " + p.Apellidos).FirstOrDefaultAsync(ct) ?? "";
        var unid = await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == r.UnidadPrivadaId).Select(u => u.Numero).FirstOrDefaultAsync(ct) ?? "";
        ReservaPago? pago = r.ReservaPagoId is null ? null : await _db.ReservaPagos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == r.ReservaPagoId, ct);
        return new ReservaDto(r.Id, r.Codigo, r.ZonaComunId, zona, r.PersonaId, per, r.UnidadPrivadaId, unid,
            r.Fecha, r.HoraInicio, r.HoraFin, r.Estado, r.EsRecurrente, r.ReservaRecurrenteId,
            r.ReglamentoAceptado, r.MotivoCancelacion, r.CanceladaAt,
            pago?.Monto, pago?.EstadoPago, r.CreatedAt);
    }

    public async Task<ReservaDto> CrearReservaAsync(CrearReservaRequest req, CancellationToken ct)
    {
        if (req.HoraFin <= req.HoraInicio) throw new InvalidOperationException("HoraFin debe ser posterior a HoraInicio.");
        var tenantId = RequireTenantId();
        var cfg = await _db.ZonaConfigReservas.AsNoTracking().Include(c => c.Franjas)
            .FirstOrDefaultAsync(c => c.ZonaComunId == req.ZonaComunId, ct)
            ?? throw new InvalidOperationException("La zona no tiene configuracion de reservas.");

        // Validar duracion
        var minutos = (req.HoraFin - req.HoraInicio).TotalMinutes;
        if (minutos < cfg.DuracionMinimaMinutos) throw new InvalidOperationException($"Duracion minima: {cfg.DuracionMinimaMinutos} min.");
        if (minutos > cfg.DuracionMaximaMinutos) throw new InvalidOperationException($"Duracion maxima: {cfg.DuracionMaximaMinutos} min.");

        // Validar anticipacion
        var ahora = DateTimeOffset.UtcNow;
        var inicioReserva = new DateTimeOffset(DateTime.SpecifyKind(req.Fecha.ToDateTime(req.HoraInicio), DateTimeKind.Utc));
        if (inicioReserva < ahora.AddHours(cfg.AnticipacionMinimaHoras))
            throw new InvalidOperationException($"RN-07: anticipacion minima {cfg.AnticipacionMinimaHoras}h.");
        if (inicioReserva > ahora.AddDays(cfg.AnticipacionMaximaDias))
            throw new InvalidOperationException($"RN-07: anticipacion maxima {cfg.AnticipacionMaximaDias}d.");

        // Validar franja activa
        var dia = ConvertirDiaSemana(req.Fecha.DayOfWeek);
        var franja = cfg.Franjas.FirstOrDefault(f => f.DiaSemana == dia && f.Activa
            && f.HoraApertura <= req.HoraInicio && f.HoraCierre >= req.HoraFin);
        if (franja is null) throw new InvalidOperationException("La zona no acepta reservas en ese horario.");

        // Validar reglamento
        if (cfg.RequiereAceptacionReglamento && !req.ReglamentoAceptado)
            throw new InvalidOperationException("RN-12: debes aceptar el reglamento de uso.");

        // Validar limite por unidad
        if (cfg.MaxReservasActivasPorUnidad is { } maxAct)
        {
            var activas = await _db.Reservas.CountAsync(r =>
                r.UnidadPrivadaId == req.UnidadPrivadaId
                && r.ZonaComunId == req.ZonaComunId
                && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.PendienteAprobacion || r.Estado == EstadoReserva.PendientePago)
                && r.Fecha >= DateOnly.FromDateTime(DateTime.UtcNow), ct);
            if (activas >= maxAct)
                throw new InvalidOperationException("RN-17: limite de reservas activas alcanzado.");
        }

        // Validar solape (RN-05)
        var solape = await _db.Reservas.AnyAsync(r =>
            r.ZonaComunId == req.ZonaComunId
            && r.Fecha == req.Fecha
            && r.Estado != EstadoReserva.CanceladaResidente
            && r.Estado != EstadoReserva.CanceladaAdmin
            && r.Estado != EstadoReserva.CanceladaSistema
            && r.Estado != EstadoReserva.Expirada
            && r.HoraInicio < req.HoraFin && r.HoraFin > req.HoraInicio, ct);
        if (solape) throw new InvalidOperationException("RN-05: existe otra reserva activa en esa franja.");

        // Validar bloqueo manual (RN-15)
        var bloq = await _db.ZonaBloqueos.AnyAsync(b =>
            b.ZonaComunId == req.ZonaComunId
            && b.FechaInicio <= req.Fecha && b.FechaFin >= req.Fecha
            && (b.HoraInicio == null || (b.HoraInicio <= req.HoraInicio && b.HoraFin >= req.HoraFin)), ct);
        if (bloq) throw new InvalidOperationException("Existe un bloqueo activo en esa franja.");

        // Determinar estado y crear pago si aplica
        EstadoReserva estado;
        ReservaPago? pago = null;
        if (cfg.TieneTarifa)
        {
            estado = EstadoReserva.PendientePago;
            var monto = cfg.ModalidadCobro switch
            {
                ModalidadCobroReserva.PorHora => cfg.ValorTarifa * (decimal)(minutos / 60.0),
                ModalidadCobroReserva.PorFranja => cfg.ValorTarifa,
                ModalidadCobroReserva.PorEvento => cfg.ValorTarifa,
                _ => cfg.ValorTarifa
            };
            pago = new ReservaPago
            {
                TenantId = tenantId,
                Monto = Math.Round(monto, 2),
                EstadoPago = EstadoPagoReserva.Pendiente,
                WompiReference = $"RSV-{tenantId:N}-{Guid.NewGuid():N}".Substring(0, 60)
            };
            _db.ReservaPagos.Add(pago);
        }
        else
        {
            estado = cfg.RequiereAprobacion ? EstadoReserva.PendienteAprobacion : EstadoReserva.Confirmada;
        }

        var codigo = await GenerarCodigoReservaAsync(tenantId, ct);
        var r = new Reserva
        {
            TenantId = tenantId,
            Codigo = codigo,
            ZonaComunId = req.ZonaComunId,
            PersonaId = req.PersonaId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            Fecha = req.Fecha,
            HoraInicio = req.HoraInicio,
            HoraFin = req.HoraFin,
            Estado = estado,
            EsRecurrente = false,
            ReglamentoAceptado = req.ReglamentoAceptado,
            ReglamentoAceptadoAt = req.ReglamentoAceptado ? DateTimeOffset.UtcNow : null,
            ReservaPagoId = pago?.Id
        };
        _db.Reservas.Add(r);
        await _db.SaveChangesAsync(ct);
        if (pago is not null)
        {
            pago.ReservaId = r.Id;
            await _db.SaveChangesAsync(ct);
        }

        var asunto = estado switch
        {
            EstadoReserva.Confirmada => $"Reserva confirmada: {r.Codigo}",
            EstadoReserva.PendienteAprobacion => $"Reserva pendiente de aprobacion: {r.Codigo}",
            EstadoReserva.PendientePago => $"Reserva pendiente de pago: {r.Codigo}",
            _ => $"Reserva creada: {r.Codigo}"
        };
        await NotificarResidenteAsync(r.PersonaId, r.Id,
            asunto,
            $"Tu reserva de zona comun para {r.Fecha:yyyy-MM-dd} ({r.HoraInicio:hh\\:mm}-{r.HoraFin:hh\\:mm}) quedo en estado {estado}.",
            Domain.Enums.PrioridadNotificacion.Normal, ct);

        return (await GetReservaAsync(r.Id, ct))!;
    }

    private async Task<string> GenerarCodigoReservaAsync(Guid tenantId, CancellationToken ct)
    {
        var ano = DateTime.UtcNow.Year;
        var count = await _db.Reservas.CountAsync(r => r.Codigo.StartsWith($"RSV-{ano}-"), ct);
        return $"RSV-{ano}-{(count + 1):D5}";
    }

    public async Task<bool> CancelarComoResidenteAsync(Guid id, CancelarReservaRequest req, CancellationToken ct)
    {
        var r = await _db.Reservas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;
        var cfg = await _db.ZonaConfigReservas.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ZonaComunId == r.ZonaComunId, ct);
        if (cfg is null || !cfg.PermiteCancelacionResidente)
            throw new InvalidOperationException("Esta zona no permite cancelacion por el residente.");
        if (r.Estado != EstadoReserva.Confirmada && r.Estado != EstadoReserva.PendienteAprobacion && r.Estado != EstadoReserva.PendientePago)
            throw new InvalidOperationException("Solo se pueden cancelar reservas activas.");
        var inicio = new DateTimeOffset(DateTime.SpecifyKind(r.Fecha.ToDateTime(r.HoraInicio), DateTimeKind.Utc));
        var horasHasta = (inicio - DateTimeOffset.UtcNow).TotalHours;
        if (horasHasta < cfg.LimiteCancelacionHoras && cfg.CancelacionTardia == ComportamientoCancelacionTardia.Bloqueada)
            throw new InvalidOperationException("Fuera del limite de cancelacion permitida.");

        r.Estado = EstadoReserva.CanceladaResidente;
        r.MotivoCancelacion = req.Motivo?.Trim();
        r.CanceladaAt = DateTimeOffset.UtcNow;
        r.CanceladaPorPersonaId = GetPersonaActualId();
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelarComoAdminAsync(Guid id, CancelarReservaRequest req, CancellationToken ct)
    {
        var r = await _db.Reservas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;
        if (r.Estado != EstadoReserva.Confirmada && r.Estado != EstadoReserva.PendienteAprobacion && r.Estado != EstadoReserva.PendientePago)
            throw new InvalidOperationException("La reserva ya no esta activa.");
        r.Estado = EstadoReserva.CanceladaAdmin;
        r.MotivoCancelacion = req.Motivo?.Trim();
        r.CanceladaAt = DateTimeOffset.UtcNow;
        r.CanceladaPorPersonaId = GetPersonaActualId();
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await NotificarResidenteAsync(r.PersonaId, r.Id,
            $"Reserva cancelada por administracion: {r.Codigo}",
            $"Tu reserva del {r.Fecha:yyyy-MM-dd} fue cancelada por la administracion. Motivo: {r.MotivoCancelacion ?? "sin motivo especificado"}.",
            Domain.Enums.PrioridadNotificacion.Alta, ct);

        return true;
    }

    public async Task<bool> AprobarAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.Reservas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;
        if (r.Estado != EstadoReserva.PendienteAprobacion)
            throw new InvalidOperationException("La reserva no esta pendiente de aprobacion.");
        r.Estado = EstadoReserva.Confirmada;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await NotificarResidenteAsync(r.PersonaId, r.Id,
            $"Reserva confirmada: {r.Codigo}",
            $"Tu reserva del {r.Fecha:yyyy-MM-dd} de {r.HoraInicio:HH\\:mm} a {r.HoraFin:HH\\:mm} fue aprobada por la administracion.",
            Domain.Enums.PrioridadNotificacion.Normal, ct);

        return true;
    }

    // ===========================================================================
    // Bloqueos
    // ===========================================================================

    public async Task<IReadOnlyList<BloqueoDto>> ListarBloqueosAsync(Guid? zonaId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var q = _db.ZonaBloqueos.AsNoTracking().AsQueryable();
        if (zonaId is { } z) q = q.Where(b => b.ZonaComunId == z);
        if (desde is { } d) q = q.Where(b => b.FechaFin >= d);
        if (hasta is { } h) q = q.Where(b => b.FechaInicio <= h);
        var list = await q.OrderByDescending(b => b.FechaInicio).Take(200).ToListAsync(ct);
        var result = new List<BloqueoDto>(list.Count);
        foreach (var b in list)
        {
            var zonaNombre = await _db.ZonasComunes.AsNoTracking().Where(z => z.Id == b.ZonaComunId).Select(z => z.Nombre).FirstOrDefaultAsync(ct) ?? "";
            result.Add(new BloqueoDto(b.Id, b.ZonaComunId, zonaNombre, b.Tipo, b.FechaInicio, b.FechaFin,
                b.HoraInicio, b.HoraFin, b.Etiqueta, b.MotivoPersonalizado, b.VisibleParaResidentes, b.Origen));
        }
        return result;
    }

    public async Task<BloqueoDto> CrearBloqueoAsync(CrearBloqueoRequest req, CancellationToken ct)
    {
        if (req.FechaFin < req.FechaInicio) throw new InvalidOperationException("FechaFin debe ser >= FechaInicio.");
        var tenantId = RequireTenantId();
        var existeZona = await _db.ZonasComunes.AnyAsync(z => z.Id == req.ZonaComunId, ct);
        if (!existeZona) throw new InvalidOperationException("Zona no encontrada.");
        var b = new ZonaBloqueo
        {
            TenantId = tenantId,
            ZonaComunId = req.ZonaComunId,
            Tipo = req.Tipo,
            FechaInicio = req.FechaInicio,
            FechaFin = req.FechaFin,
            HoraInicio = req.HoraInicio,
            HoraFin = req.HoraFin,
            Etiqueta = req.Etiqueta,
            MotivoPersonalizado = req.MotivoPersonalizado?.Trim(),
            VisibleParaResidentes = req.VisibleParaResidentes,
            Origen = OrigenBloqueoZona.ManualAdmin,
            CreadoPorPersonaId = GetPersonaActualId()
        };
        _db.ZonaBloqueos.Add(b);
        await _db.SaveChangesAsync(ct);
        var zonaNombre = await _db.ZonasComunes.AsNoTracking().Where(z => z.Id == b.ZonaComunId).Select(z => z.Nombre).FirstOrDefaultAsync(ct) ?? "";
        return new BloqueoDto(b.Id, b.ZonaComunId, zonaNombre, b.Tipo, b.FechaInicio, b.FechaFin,
            b.HoraInicio, b.HoraFin, b.Etiqueta, b.MotivoPersonalizado, b.VisibleParaResidentes, b.Origen);
    }

    public async Task<bool> EliminarBloqueoAsync(Guid id, CancellationToken ct)
    {
        var b = await _db.ZonaBloqueos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return false;
        _db.ZonaBloqueos.Remove(b);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Vista portero
    // ===========================================================================

    public async Task<IReadOnlyList<ReservaDelDiaDto>> ListarReservasDelDiaAsync(DateOnly fecha, CancellationToken ct)
    {
        var rs = await _db.Reservas.AsNoTracking()
            .Where(r => r.Fecha == fecha && r.Estado == EstadoReserva.Confirmada)
            .OrderBy(r => r.HoraInicio)
            .Select(r => new
            {
                r.ZonaComunId,
                r.PersonaId,
                r.UnidadPrivadaId,
                r.HoraInicio,
                r.HoraFin,
                r.Codigo
            })
            .ToListAsync(ct);
        var result = new List<ReservaDelDiaDto>(rs.Count);
        foreach (var r in rs)
        {
            var zona = await _db.ZonasComunes.AsNoTracking().Where(z => z.Id == r.ZonaComunId).Select(z => z.Nombre).FirstOrDefaultAsync(ct) ?? "";
            var per = await _db.Personas.AsNoTracking().Where(p => p.Id == r.PersonaId).Select(p => p.Nombres + " " + p.Apellidos).FirstOrDefaultAsync(ct) ?? "";
            var unid = await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == r.UnidadPrivadaId).Select(u => u.Numero).FirstOrDefaultAsync(ct) ?? "";
            result.Add(new ReservaDelDiaDto(zona, r.HoraInicio, r.HoraFin, unid, per, r.Codigo));
        }
        return result;
    }

    public async Task<ResumenReservasDto> GetResumenAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var hace30 = hoy.AddDays(-30);
        var confirmadas = await _db.Reservas.AsNoTracking().CountAsync(r => r.Estado == EstadoReserva.Confirmada && r.Fecha >= hoy, ct);
        var pendientes = await _db.Reservas.AsNoTracking().CountAsync(r =>
            (r.Estado == EstadoReserva.PendienteAprobacion || r.Estado == EstadoReserva.PendientePago) && r.Fecha >= hoy, ct);
        var canceladas = await _db.Reservas.AsNoTracking().CountAsync(r =>
            (r.Estado == EstadoReserva.CanceladaResidente || r.Estado == EstadoReserva.CanceladaAdmin)
            && r.Fecha >= hace30, ct);
        var completadas = await _db.Reservas.AsNoTracking().CountAsync(r => r.Estado == EstadoReserva.Completada && r.Fecha >= hace30, ct);
        var ingresos = await _db.ReservaPagos.AsNoTracking()
            .Where(p => p.EstadoPago == EstadoPagoReserva.Pagado && p.PaidAt >= new DateTimeOffset(DateTime.SpecifyKind(hace30.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)))
            .Select(p => (decimal?)p.Monto).SumAsync(ct) ?? 0m;
        var total = completadas + canceladas;
        var tasa = total > 0 ? Math.Round((decimal)canceladas / total * 100m, 2) : 0m;
        return new ResumenReservasDto(confirmadas, pendientes, canceladas, completadas, ingresos, tasa);
    }
}
