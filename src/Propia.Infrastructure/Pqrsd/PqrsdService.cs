using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Pqrsd;

/// <summary>
/// Modulo 2.9 PQRSD y Convivencia (spec v1.0 - MVP).
/// Implementa radicacion, ciclo completo de estados, semaforo en dias habiles,
/// reserva de identidad, marca de Tutela y flujo del Comite de Convivencia.
/// </summary>
public class PqrsdService : IPqrsdService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly Propia.Application.Notificaciones.INotificacionDispatcher _noti;

    public PqrsdService(
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

    private async Task NotificarAdminsTenantAsync(
        string codigoModulo, Guid? entidadOrigen, string asunto, string cuerpo,
        Domain.Enums.PrioridadNotificacion prioridad, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return;
        var personaIds = await _db.UsuariosTenant.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Estado == Domain.Enums.EstadoUsuarioTenant.Activo)
            .Select(u => u.PersonaId).Distinct().Take(20).ToListAsync(ct);
        if (personaIds.Count == 0) return;
        var lote = personaIds.Select(pid =>
            new Propia.Application.Notificaciones.EnviarNotificacionRequest(
                Canal: Domain.Enums.CanalNotificacion.InApp,
                Cuerpo: cuerpo,
                TenantId: tenantId,
                PersonaDestinatariaId: pid,
                Asunto: asunto,
                Prioridad: prioridad,
                ModuloOrigenCodigo: codigoModulo,
                EntidadOrigenId: entidadOrigen));
        await _noti.EnviarLoteAsync(lote, ct);
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private async Task<Guid?> GetPersonaActualIdAsync(CancellationToken ct)
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("persona_id");
        if (Guid.TryParse(sub, out var id)) return id;
        var uid = GetUsuarioActualId();
        if (uid == Guid.Empty) return null;
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        return u?.PersonaId;
    }

    // ===================== Seed lazy =====================

    private async Task AsegurarCatalogoBaseAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        var hayCats = await _db.PqrsdCategorias.AnyAsync(ct);
        if (!hayCats)
        {
            foreach (var (nombre, orden) in PqrsdCatalogo.CategoriasBase)
            {
                _db.PqrsdCategorias.Add(new PqrsdCategoria
                {
                    TenantId = tenantId,
                    Nombre = nombre,
                    EsPredeterminada = true,
                    Activa = true,
                    Orden = orden
                });
            }
        }

        var hayPlazos = await _db.PqrsdConfiguracionPlazos.AnyAsync(ct);
        if (!hayPlazos)
        {
            foreach (var (tipo, dias, diasInc, urgencia) in PqrsdCatalogo.PlazosBase)
            {
                _db.PqrsdConfiguracionPlazos.Add(new PqrsdConfiguracionPlazo
                {
                    TenantId = tenantId,
                    Tipo = tipo,
                    DiasHabiles = dias,
                    DiasInconformidad = diasInc,
                    NivelUrgencia = urgencia
                });
            }
        }

        if (!hayCats || !hayPlazos) await _db.SaveChangesAsync(ct);
    }

    // ===================== Calculo de plazo en dias habiles =====================

    /// <summary>Suma N dias habiles a partir de una fecha (excluye sabados y domingos).
    /// En MVP no se consideran festivos colombianos - se anadiran via tabla de festivos en Fase 2.</summary>
    private static DateOnly SumarDiasHabiles(DateOnly desde, int dias)
    {
        var d = desde;
        var anadidos = 0;
        while (anadidos < dias)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                anadidos++;
        }
        return d;
    }

    /// <summary>Cuenta dias habiles entre dos fechas (incluyendo `desde`).</summary>
    private static int ContarDiasHabilesEntre(DateOnly desde, DateOnly hasta)
    {
        if (hasta < desde) return -ContarDiasHabilesEntre(hasta, desde);
        var count = 0;
        var d = desde;
        while (d < hasta)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    private static SemaforoPqrsd CalcularSemaforo(EstadoPqrsd estado, bool tutelaActiva, DateOnly fechaCreacion, DateOnly fechaVencimiento, DateOnly hoy)
    {
        if (tutelaActiva) return SemaforoPqrsd.TutelaActiva;
        if (estado == EstadoPqrsd.Cerrada || estado == EstadoPqrsd.ViaInternaAgotada) return SemaforoPqrsd.Verde;

        // Si ya vencio sin respuesta -> Negro
        if (hoy > fechaVencimiento) return SemaforoPqrsd.Negro;

        var totalHabiles = ContarDiasHabilesEntre(fechaCreacion, fechaVencimiento);
        var consumidos = ContarDiasHabilesEntre(fechaCreacion, hoy);
        var pct = totalHabiles == 0 ? 0 : (double)consumidos / totalHabiles;
        if (pct >= 0.8) return SemaforoPqrsd.Rojo;
        if (pct >= 0.5) return SemaforoPqrsd.Amarillo;
        return SemaforoPqrsd.Verde;
    }

    // ===================== Categorias =====================

    public async Task<IReadOnlyList<PqrsdCategoriaDto>> ListarCategoriasAsync(CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        return await _db.PqrsdCategorias.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new PqrsdCategoriaDto(c.Id, c.Nombre, c.EsPredeterminada, c.Activa, c.Orden))
            .ToListAsync(ct);
    }

    public async Task<PqrsdCategoriaDto> CrearCategoriaAsync(CrearCategoriaRequest req, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var nom = req.Nombre.Trim();
        if (await _db.PqrsdCategorias.AnyAsync(c => c.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe una categoria con este nombre.");
        var c = new PqrsdCategoria { Nombre = nom, EsPredeterminada = false, Activa = true, Orden = req.Orden };
        _db.PqrsdCategorias.Add(c);
        await _db.SaveChangesAsync(ct);
        return new PqrsdCategoriaDto(c.Id, c.Nombre, false, true, c.Orden);
    }

    public async Task<bool> ActualizarCategoriaAsync(Guid id, ActualizarCategoriaRequest req, CancellationToken ct)
    {
        var c = await _db.PqrsdCategorias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.Nombre = req.Nombre.Trim();
        c.Activa = req.Activa;
        c.Orden = req.Orden;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCategoriaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.PqrsdCategorias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        // RN-12: eliminar no afecta expedientes existentes (la categoria queda en sus FKs aunque sea desactivada)
        // Como tenemos RESTRICT en el FK, mejor solo desactivar si tiene expedientes
        var enUso = await _db.PqrsdExpedientes.AnyAsync(e => e.CategoriaId == id, ct);
        if (enUso)
        {
            c.Activa = false;
            c.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.PqrsdCategorias.Remove(c);
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RestablecerCategoriasBaseAsync(CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        int restablecidas = 0;
        foreach (var (nombre, orden) in PqrsdCatalogo.CategoriasBase)
        {
            var existe = await _db.PqrsdCategorias.FirstOrDefaultAsync(c => c.Nombre == nombre, ct);
            if (existe is null)
            {
                _db.PqrsdCategorias.Add(new PqrsdCategoria
                {
                    Nombre = nombre,
                    EsPredeterminada = true,
                    Activa = true,
                    Orden = orden
                });
                restablecidas++;
            }
            else if (!existe.Activa)
            {
                existe.Activa = true;
                existe.UpdatedAt = DateTimeOffset.UtcNow;
                restablecidas++;
            }
        }
        await _db.SaveChangesAsync(ct);
        return restablecidas;
    }

    // ===================== Plazos =====================

    public async Task<IReadOnlyList<PqrsdPlazoDto>> ListarPlazosAsync(CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        return await _db.PqrsdConfiguracionPlazos.AsNoTracking()
            .OrderBy(p => p.Tipo)
            .Select(p => new PqrsdPlazoDto(p.Tipo, p.DiasHabiles, p.DiasInconformidad, p.NivelUrgencia))
            .ToListAsync(ct);
    }

    public async Task<bool> ActualizarPlazoAsync(TipoPqrsd tipo, ActualizarPlazoRequest req, CancellationToken ct)
    {
        var p = await _db.PqrsdConfiguracionPlazos.FirstOrDefaultAsync(x => x.Tipo == tipo, ct);
        if (p is null) return false;
        p.DiasHabiles = req.DiasHabiles;
        p.DiasInconformidad = req.DiasInconformidad;
        p.NivelUrgencia = req.NivelUrgencia;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Bandeja + ficha =====================

    public async Task<PqrsdBandejaDto> GetBandejaAsync(EstadoPqrsd? estado, TipoPqrsd? tipo, Guid? categoriaId, string? query, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        IQueryable<PqrsdExpediente> q = _db.PqrsdExpedientes.AsNoTracking().Include(x => x.Categoria);
        if (estado.HasValue) q = q.Where(x => x.Estado == estado.Value);
        if (tipo.HasValue) q = q.Where(x => x.Tipo == tipo.Value);
        if (categoriaId.HasValue) q = q.Where(x => x.CategoriaId == categoriaId.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var qn = query.Trim().ToLower();
            q = q.Where(x => x.NumeroRadicado.ToLower().Contains(qn) || x.Descripcion.ToLower().Contains(qn));
        }

        var rows = await (
            from x in q
            join p in _db.Personas.AsNoTracking() on x.RadicadorPersonaId equals p.Id into pj
            from p in pj.DefaultIfEmpty()
            orderby x.CreatedAt descending
            select new { x, RadicadorNombre = p == null ? null : (p.Nombres + " " + p.Apellidos).Trim() }
        ).Take(200).ToListAsync(ct);

        var sesionesActivas = await _db.PqrsdComiteSesiones.AsNoTracking()
            .Where(s => s.Resultado == null)
            .Select(s => s.ExpedienteId).ToHashSetAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var plazos = await _db.PqrsdConfiguracionPlazos.AsNoTracking().ToDictionaryAsync(p => p.Tipo, ct);

        var items = rows.Select(r =>
        {
            var fechaCreacion = DateOnly.FromDateTime(r.x.CreatedAt.UtcDateTime);
            var semaforo = CalcularSemaforo(r.x.Estado, r.x.TutelaActiva, fechaCreacion, r.x.FechaVencimiento, hoy);
            var diasHasta = r.x.FechaVencimiento.DayNumber - hoy.DayNumber;
            var urgencia = plazos.TryGetValue(r.x.Tipo, out var pl) ? pl.NivelUrgencia : NivelUrgenciaPqrsd.Media;
            var nombre = r.x.IdentidadReservada ? null : r.RadicadorNombre;
            var resumen = r.x.Descripcion.Length > 100 ? r.x.Descripcion[..100] + "..." : r.x.Descripcion;
            return new PqrsdBandejaItemDto(
                r.x.Id, r.x.NumeroRadicado, r.x.Tipo, r.x.Categoria!.Nombre, resumen, r.x.Estado,
                semaforo, nombre, null, r.x.IdentidadReservada, r.x.TutelaActiva,
                r.x.FechaVencimiento, diasHasta, urgencia,
                sesionesActivas.Contains(r.x.Id), r.x.CreatedAt);
        }).ToList();

        var kpis = new PqrsdKpisDto(
            items.Count,
            items.Count(i => i.Estado == EstadoPqrsd.Recibida),
            items.Count(i => i.Estado == EstadoPqrsd.EnGestion),
            items.Count(i => i.Estado == EstadoPqrsd.Respondida),
            items.Count(i => i.Estado == EstadoPqrsd.Cerrada || i.Estado == EstadoPqrsd.ViaInternaAgotada),
            items.Count(i => i.Semaforo == SemaforoPqrsd.Rojo && i.Estado != EstadoPqrsd.Cerrada && i.Estado != EstadoPqrsd.ViaInternaAgotada),
            items.Count(i => i.Semaforo == SemaforoPqrsd.Negro && i.Estado != EstadoPqrsd.Cerrada && i.Estado != EstadoPqrsd.ViaInternaAgotada),
            items.Count(i => i.TutelaActiva),
            items.Count(i => i.TieneComiteActivo));

        return new PqrsdBandejaDto(kpis, items);
    }

    public async Task<PqrsdExpedienteDetalleDto?> GetExpedienteAsync(Guid id, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        var x = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.Categoria)
            .Include(e => e.Adjuntos)
            .Include(e => e.Historial)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return null;

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaCreacion = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
        var semaforo = CalcularSemaforo(x.Estado, x.TutelaActiva, fechaCreacion, x.FechaVencimiento, hoy);
        var diasHasta = x.FechaVencimiento.DayNumber - hoy.DayNumber;

        var plazo = await _db.PqrsdConfiguracionPlazos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Tipo == x.Tipo, ct);
        var urgencia = plazo?.NivelUrgencia ?? NivelUrgenciaPqrsd.Media;

        // Radicador (filtrar nombre si hay reserva)
        var rad = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == x.RadicadorPersonaId, ct);
        string? radNombre = x.IdentidadReservada ? null : (rad is null ? null : $"{rad.Nombres} {rad.Apellidos}".Trim());
        Guid? radId = x.IdentidadReservada ? null : x.RadicadorPersonaId;

        var adjuntos = x.Adjuntos.OrderBy(a => a.CreatedAt)
            .Select(a => new PqrsdAdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt))
            .ToList();

        var historial = x.Historial.OrderByDescending(h => h.CreatedAt)
            .Select(h => new PqrsdHistorialDto(h.EstadoAnterior, h.EstadoNuevo, h.ActorUsuarioId, h.Origen, h.Nota, h.CreatedAt))
            .ToList();

        // Comite
        var sesion = await _db.PqrsdComiteSesiones.AsNoTracking()
            .Include(s => s.Miembros)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(s => s.ExpedienteId == id, ct);
        PqrsdComiteSesionDto? comiteDto = null;
        if (sesion is not null)
        {
            var personaIds = sesion.Miembros.Select(m => m.PersonaId).ToList();
            var personas = await _db.Personas.AsNoTracking()
                .Where(p => personaIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
            var miembros = sesion.Miembros.Select(m => new PqrsdComiteMiembroDto(
                m.Id, m.PersonaId, personas.GetValueOrDefault(m.PersonaId, ""))).ToList();
            comiteDto = new PqrsdComiteSesionDto(
                sesion.Id, sesion.FechaSesion, sesion.Modalidad, sesion.EnlaceReunion,
                sesion.Resultado, sesion.BorradorActa, sesion.ActaFinal,
                sesion.ActivadaPorUsuarioId, sesion.CreatedAt, miembros);
        }

        return new PqrsdExpedienteDetalleDto(
            x.Id, x.NumeroRadicado, x.Tipo, x.CategoriaId, x.Categoria!.Nombre, x.Descripcion,
            x.Estado, semaforo, radNombre, radId, null, x.IdentidadReservada, x.TutelaActiva,
            x.TutelaActivadaAt, x.FechaVencimiento, diasHasta, urgencia,
            x.RespuestaAdmin, x.RespuestaAdminAt, x.InconformidadTexto, x.InconformidadAt,
            x.RespuestaDefinitiva, x.RespuestaDefinitivaAt, x.FechaCierre, x.TareaId,
            x.CreatedAt, adjuntos, historial, comiteDto);
    }

    // ===================== Radicacion =====================

    public async Task<PqrsdExpedienteDetalleDto> RadicarAsync(RadicarPqrsdRequest req, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Descripcion) || req.Descripcion.Trim().Length < 20)
            throw new InvalidOperationException("Descripcion obligatoria, minimo 20 caracteres.");
        if (req.Descripcion.Length > 2000)
            throw new InvalidOperationException("Descripcion maxima 2000 caracteres.");
        if (req.IdentidadReservada && req.Tipo != TipoPqrsd.Denuncia)
            throw new InvalidOperationException("La reserva de identidad solo aplica al tipo Denuncia (RN-02).");

        var categoria = await _db.PqrsdCategorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CategoriaId, ct)
            ?? throw new InvalidOperationException("Categoria invalida.");
        if (!categoria.Activa) throw new InvalidOperationException("La categoria no esta activa.");

        var personaId = await GetPersonaActualIdAsync(ct)
            ?? throw new InvalidOperationException("No se pudo resolver el radicador (persona del usuario autenticado).");

        var plazo = await _db.PqrsdConfiguracionPlazos.AsNoTracking().FirstOrDefaultAsync(p => p.Tipo == req.Tipo, ct)
            ?? throw new InvalidOperationException("No hay plazo configurado para este tipo.");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaVencimiento = SumarDiasHabiles(hoy, plazo.DiasHabiles);
        var numero = await GenerarNumeroRadicadoAsync(ct);

        var exp = new PqrsdExpediente
        {
            NumeroRadicado = numero,
            Tipo = req.Tipo,
            CategoriaId = req.CategoriaId,
            Descripcion = req.Descripcion.Trim(),
            Estado = EstadoPqrsd.Recibida,
            RadicadorPersonaId = personaId,
            IdentidadReservada = req.IdentidadReservada,
            FechaVencimiento = fechaVencimiento
        };
        _db.PqrsdExpedientes.Add(exp);

        // Adjuntos iniciales
        if (req.Adjuntos is { Count: > 0 })
        {
            foreach (var a in req.Adjuntos)
            {
                _db.PqrsdAdjuntos.Add(new PqrsdAdjunto
                {
                    Expediente = exp,
                    NombreArchivo = a.NombreArchivo,
                    TipoMime = a.TipoMime,
                    TamanioBytes = a.TamanioBytes,
                    UrlStorage = a.UrlStorage,
                    SubidoPorUsuarioId = GetUsuarioActualId()
                });
            }
        }

        // Historial inicial
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            Expediente = exp,
            EstadoAnterior = null,
            EstadoNuevo = EstadoPqrsd.Recibida,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = $"Expediente radicado: {numero}"
        });

        await _db.SaveChangesAsync(ct);

        await NotificarAdminsTenantAsync("2.9", exp.Id,
            $"PQRSD radicado: {numero}",
            $"Se radico un expediente {exp.Tipo} con plazo legal. Asignar y responder dentro del SLA.",
            exp.Tipo == TipoPqrsd.Denuncia ? Domain.Enums.PrioridadNotificacion.Alta : Domain.Enums.PrioridadNotificacion.Normal,
            ct);

        return (await GetExpedienteAsync(exp.Id, ct))!;
    }

    private async Task<string> GenerarNumeroRadicadoAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefijo = $"PQRS-{year}-";
        var ultimos = await _db.PqrsdExpedientes.AsNoTracking()
            .Where(x => x.NumeroRadicado.StartsWith(prefijo))
            .Select(x => x.NumeroRadicado)
            .ToListAsync(ct);
        int max = 0;
        foreach (var n in ultimos)
        {
            if (int.TryParse(n.Substring(prefijo.Length), out var s) && s > max) max = s;
        }
        return $"{prefijo}{(max + 1):D4}";
    }

    // ===================== Vista residente =====================

    public async Task<IReadOnlyList<PqrsdBandejaItemDto>> ListarMisPqrsdAsync(CancellationToken ct)
    {
        var personaId = await GetPersonaActualIdAsync(ct);
        if (personaId is null) return new List<PqrsdBandejaItemDto>();

        var rows = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(x => x.Categoria)
            .Where(x => x.RadicadorPersonaId == personaId.Value)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var sesionesActivas = await _db.PqrsdComiteSesiones.AsNoTracking()
            .Where(s => s.Resultado == null && rows.Select(r => r.Id).Contains(s.ExpedienteId))
            .Select(s => s.ExpedienteId).ToHashSetAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var plazos = await _db.PqrsdConfiguracionPlazos.AsNoTracking().ToDictionaryAsync(p => p.Tipo, ct);

        return rows.Select(x =>
        {
            var fechaCreacion = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
            var semaforo = CalcularSemaforo(x.Estado, x.TutelaActiva, fechaCreacion, x.FechaVencimiento, hoy);
            var diasHasta = x.FechaVencimiento.DayNumber - hoy.DayNumber;
            var urgencia = plazos.TryGetValue(x.Tipo, out var pl) ? pl.NivelUrgencia : NivelUrgenciaPqrsd.Media;
            var resumen = x.Descripcion.Length > 100 ? x.Descripcion[..100] + "..." : x.Descripcion;
            return new PqrsdBandejaItemDto(
                x.Id, x.NumeroRadicado, x.Tipo, x.Categoria!.Nombre, resumen, x.Estado,
                semaforo, null, null, x.IdentidadReservada, x.TutelaActiva,
                x.FechaVencimiento, diasHasta, urgencia,
                sesionesActivas.Contains(x.Id), x.CreatedAt);
        }).ToList();
    }

    // ===================== Ciclo de gestion =====================

    public async Task<bool> TomarExpedienteAsync(Guid id, TomarExpedienteRequest req, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado != EstadoPqrsd.Recibida) return true;
        var anterior = x.Estado;
        x.Estado = EstadoPqrsd.EnGestion;
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = EstadoPqrsd.EnGestion,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = string.IsNullOrWhiteSpace(req.Nota) ? "Admin tomo el expediente" : req.Nota
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ResponderAsync(Guid id, ResponderExpedienteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Texto) || req.Texto.Trim().Length < 20)
            throw new InvalidOperationException("Respuesta minima 20 caracteres.");
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado == EstadoPqrsd.Cerrada || x.Estado == EstadoPqrsd.ViaInternaAgotada)
            throw new InvalidOperationException("No se puede responder un expediente cerrado.");

        var anterior = x.Estado;
        var esRespuestaDefinitiva = x.InconformidadTexto != null;

        if (esRespuestaDefinitiva)
        {
            // Segunda respuesta tras inconformidad -> cierra definitivamente
            x.RespuestaDefinitiva = req.Texto.Trim();
            x.RespuestaDefinitivaAt = DateTimeOffset.UtcNow;
            x.Estado = EstadoPqrsd.Cerrada;
            x.FechaCierre = DateTimeOffset.UtcNow;
            x.CerradoPorUsuarioId = GetUsuarioActualId();
        }
        else
        {
            x.RespuestaAdmin = req.Texto.Trim();
            x.RespuestaAdminAt = DateTimeOffset.UtcNow;
            x.RespuestaAdminPorUsuarioId = GetUsuarioActualId();
            x.Estado = EstadoPqrsd.Respondida;
        }
        x.UpdatedAt = DateTimeOffset.UtcNow;

        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = esRespuestaDefinitiva ? "Respuesta definitiva - cierre" : "Respuesta del admin"
        });
        await _db.SaveChangesAsync(ct);

        var asunto = esRespuestaDefinitiva
            ? $"PQRSD cerrado: {x.NumeroRadicado}"
            : $"PQRSD respondido: {x.NumeroRadicado}";
        await NotificarAdminsTenantAsync("2.9", id, asunto,
            esRespuestaDefinitiva
                ? "El expediente quedo cerrado tras la respuesta definitiva."
                : "El admin respondio el expediente. Si el ciudadano queda inconforme tiene una oportunidad de inconformidad (RN-06).",
            Domain.Enums.PrioridadNotificacion.Normal, ct);

        return true;
    }

    public async Task<bool> ManifestarInconformidadAsync(Guid id, ManifestarInconformidadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Texto))
            throw new InvalidOperationException("Texto de inconformidad obligatorio.");
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado != EstadoPqrsd.Respondida)
            throw new InvalidOperationException("Solo se puede manifestar inconformidad sobre expedientes Respondidos.");
        if (x.InconformidadTexto != null)
            throw new InvalidOperationException("RN-06: solo se permite una inconformidad por expediente.");

        var anterior = x.Estado;
        x.InconformidadTexto = req.Texto.Trim();
        x.InconformidadAt = DateTimeOffset.UtcNow;
        x.Estado = EstadoPqrsd.EnGestion;
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = EstadoPqrsd.EnGestion,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = "Radicador manifesto inconformidad"
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CerrarDefinitivoAsync(Guid id, CerrarDefinitivoRequest req, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado == EstadoPqrsd.Cerrada || x.Estado == EstadoPqrsd.ViaInternaAgotada) return true;
        if (string.IsNullOrWhiteSpace(req.RespuestaDefinitiva))
            throw new InvalidOperationException("Respuesta definitiva obligatoria al cerrar.");

        var anterior = x.Estado;
        x.RespuestaDefinitiva = req.RespuestaDefinitiva.Trim();
        x.RespuestaDefinitivaAt = DateTimeOffset.UtcNow;
        x.Estado = EstadoPqrsd.Cerrada;
        x.FechaCierre = DateTimeOffset.UtcNow;
        x.CerradoPorUsuarioId = GetUsuarioActualId();
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = EstadoPqrsd.Cerrada,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = "Cierre definitivo por admin"
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Tutela =====================

    public async Task<bool> ActivarTutelaAsync(Guid id, ActivarTutelaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Justificacion))
            throw new InvalidOperationException("Justificacion obligatoria al activar Tutela (RN-11).");
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.TutelaActiva) return true;
        x.TutelaActiva = true;
        x.TutelaActivadaAt = DateTimeOffset.UtcNow;
        x.TutelaActivadaPorUsuarioId = GetUsuarioActualId();
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = x.Estado,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = $"Tutela activada - {req.Justificacion}"
        });
        await _db.SaveChangesAsync(ct);

        await NotificarAdminsTenantAsync("2.9", id,
            $"TUTELA marcada: {x.NumeroRadicado}",
            $"Se marco tutela activa sobre el expediente. Atender con prioridad maxima. Justificacion: {req.Justificacion}",
            Domain.Enums.PrioridadNotificacion.Critica, ct);

        return true;
    }

    // ===================== Comite =====================

    public async Task<PqrsdComiteSesionDto> EscalarAComiteAsync(Guid expedienteId, EscalarAComiteRequest req, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct)
            ?? throw new InvalidOperationException("Expediente no encontrado.");
        if (x.Tipo != TipoPqrsd.Denuncia)
            throw new InvalidOperationException("Solo se puede escalar al Comite de Convivencia un expediente de tipo Denuncia (Ley 675 Art. 58).");
        if (req.PersonaIds is null || req.PersonaIds.Count == 0)
            throw new InvalidOperationException("Debes seleccionar al menos un miembro del Comite.");

        // Validar que las personas existan
        var personasValidas = await _db.Personas.AsNoTracking()
            .Where(p => req.PersonaIds.Contains(p.Id))
            .Select(p => p.Id).ToListAsync(ct);
        if (personasValidas.Count != req.PersonaIds.Count)
            throw new InvalidOperationException("Una o mas personas seleccionadas no existen.");

        var sesion = new PqrsdComiteSesion
        {
            ExpedienteId = expedienteId,
            FechaSesion = req.FechaPropuestaSesion,
            Modalidad = req.Modalidad,
            EnlaceReunion = req.EnlaceReunion,
            ActivadaPorUsuarioId = GetUsuarioActualId()
        };
        _db.PqrsdComiteSesiones.Add(sesion);

        foreach (var pid in personasValidas.Distinct())
        {
            _db.PqrsdComiteMiembros.Add(new PqrsdComiteMiembroSesion
            {
                Sesion = sesion,
                PersonaId = pid
            });
        }

        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = expedienteId,
            EstadoAnterior = x.Estado,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = $"Escalado al Comite de Convivencia ({req.Modalidad}, {personasValidas.Count} miembros)"
        });

        await _db.SaveChangesAsync(ct);

        var personas = await _db.Personas.AsNoTracking()
            .Where(p => personasValidas.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
        var miembrosDto = sesion.Miembros.Select(m => new PqrsdComiteMiembroDto(
            m.Id, m.PersonaId, personas.GetValueOrDefault(m.PersonaId, ""))).ToList();
        return new PqrsdComiteSesionDto(
            sesion.Id, sesion.FechaSesion, sesion.Modalidad, sesion.EnlaceReunion,
            sesion.Resultado, sesion.BorradorActa, sesion.ActaFinal,
            sesion.ActivadaPorUsuarioId, sesion.CreatedAt, miembrosDto);
    }

    public async Task<bool> RegistrarSesionComiteAsync(Guid sesionId, RegistrarSesionComiteRequest req, CancellationToken ct)
    {
        var s = await _db.PqrsdComiteSesiones.Include(x => x.Expediente).FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Resultado != null)
            throw new InvalidOperationException("La sesion ya fue cerrada con resultado registrado.");

        s.FechaSesion = req.FechaSesion;
        s.ActaFinal = req.Acta;
        s.Resultado = req.Resultado;
        s.UpdatedAt = DateTimeOffset.UtcNow;

        // Si el resultado es SinAcuerdo, marcar el expediente como ViaInternaAgotada
        if (req.Resultado == ResultadoComite.SinAcuerdo)
        {
            var x = s.Expediente!;
            var anterior = x.Estado;
            x.Estado = EstadoPqrsd.ViaInternaAgotada;
            x.FechaCierre = DateTimeOffset.UtcNow;
            x.CerradoPorUsuarioId = GetUsuarioActualId();
            x.UpdatedAt = DateTimeOffset.UtcNow;
            _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
            {
                ExpedienteId = x.Id,
                EstadoAnterior = anterior,
                EstadoNuevo = EstadoPqrsd.ViaInternaAgotada,
                ActorUsuarioId = GetUsuarioActualId(),
                Origen = OrigenCambioEstado.Sistema,
                Nota = "Comite: sin acuerdo - via interna agotada (Ley 675 Art. 58)"
            });
        }
        else
        {
            // Acuerdo: el admin debe cerrar manualmente con respuesta definitiva
            _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
            {
                ExpedienteId = s.ExpedienteId,
                EstadoAnterior = s.Expediente!.Estado,
                EstadoNuevo = s.Expediente.Estado,
                ActorUsuarioId = GetUsuarioActualId(),
                Origen = OrigenCambioEstado.Sistema,
                Nota = "Comite: acuerdo alcanzado - admin debe cerrar con respuesta definitiva"
            });
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
