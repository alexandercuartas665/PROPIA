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
    private readonly Propia.Application.Tareas.ITareasService _tareas;
    private readonly Propia.Application.Documents.IMembreteDocumentBuilder _membrete;

    public PqrsdService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        Propia.Application.Notificaciones.INotificacionDispatcher noti,
        Propia.Application.Tareas.ITareasService tareas,
        Propia.Application.Documents.IMembreteDocumentBuilder membrete)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _noti = noti;
        _tareas = tareas;
        _membrete = membrete;
    }

    private (Guid? UsuarioId, string? Nombre) ActorActual()
    {
        var u = _http.HttpContext?.User;
        Guid? uid = Guid.TryParse(u?.FindFirst("user_id")?.Value, out var g) ? g : null;
        var nombre = u?.FindFirst("name")?.Value
            ?? u?.FindFirst(ClaimTypes.Name)?.Value
            ?? u?.FindFirst("email")?.Value
            ?? u?.FindFirst(ClaimTypes.Email)?.Value
            ?? u?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
        return (uid, nombre);
    }

    // Nombre legible del actor: resuelve por persona_id (Nombres + Apellidos); cae al email del token.
    private async Task<string?> ResolverNombreActorAsync(CancellationToken ct)
    {
        var u = _http.HttpContext?.User;
        if (Guid.TryParse(u?.FindFirst("persona_id")?.Value, out var pid))
        {
            var p = await _db.Personas.AsNoTracking()
                .Where(x => x.Id == pid)
                .Select(x => new { x.Nombres, x.Apellidos })
                .FirstOrDefaultAsync(ct);
            if (p is not null)
            {
                var nombre = $"{p.Nombres} {p.Apellidos}".Trim();
                if (!string.IsNullOrWhiteSpace(nombre)) return nombre;
            }
        }
        return ActorActual().Nombre;
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

    /// <summary>Columnas base del tablero PQRS: (Nombre, Color, Orden, EsTerminal, SemanticaLegal).</summary>
    private static readonly (string Nombre, string Color, int Orden, bool Terminal, EstadoPqrsd Semantica)[] EstadosBase = new[]
    {
        ("Recibida", "#94A3B8", 1, false, EstadoPqrsd.Recibida),
        ("En gestion", "#6D4FE3", 2, false, EstadoPqrsd.EnGestion),
        ("Respondida", "#0EA5E9", 3, false, EstadoPqrsd.Respondida),
        ("Cerrada", "#16A34A", 4, true, EstadoPqrsd.Cerrada),
        ("Via interna agotada", "#DC2626", 5, true, EstadoPqrsd.ViaInternaAgotada),
    };

    /// <summary>Siembra las 5 columnas legales y backfilea EstadoId de los expedientes existentes (una vez).</summary>
    private async Task AsegurarTableroBaseAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        if (await _db.PqrsdEstados.AnyAsync(ct)) return;

        foreach (var (nombre, color, orden, terminal, semantica) in EstadosBase)
        {
            _db.PqrsdEstados.Add(new PqrsdEstado
            {
                TenantId = tenantId,
                Nombre = nombre,
                Color = color,
                Orden = orden,
                EsTerminal = terminal,
                EsBase = true,
                Activo = true,
                SemanticaLegal = semantica
            });
        }
        await _db.SaveChangesAsync(ct);

        // Backfill EstadoId por semantica legal para expedientes ya existentes.
        var mapa = await _db.PqrsdEstados.Where(e => e.SemanticaLegal != null)
            .ToDictionaryAsync(e => e.SemanticaLegal!.Value, e => e.Id, ct);
        var pendientes = await _db.PqrsdExpedientes.Where(x => x.EstadoId == null).ToListAsync(ct);
        foreach (var x in pendientes)
            if (mapa.TryGetValue(x.Estado, out var eid)) x.EstadoId = eid;
        if (pendientes.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private async Task AsegurarCatalogoBaseAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        await AsegurarTableroBaseAsync(ct);

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

        // Tipos configurables: siembra los 8 tipos legales como base (con Legal mapeado al enum) y
        // backfilea TipoId de los expedientes existentes. El usuario puede crear tipos propios encima.
        var hayTipos = await _db.PqrsdTipos.AnyAsync(ct);
        if (!hayTipos)
        {
            foreach (var (tipo, dias, diasInc, urgencia) in PqrsdCatalogo.PlazosBase)
            {
                _db.PqrsdTipos.Add(new PqrsdTipo
                {
                    TenantId = tenantId,
                    Nombre = TipoNombreBase(tipo),
                    DiasHabiles = dias,
                    DiasInconformidad = diasInc,
                    NivelUrgencia = urgencia,
                    Legal = tipo,
                    EsBase = true,
                    Activo = true,
                    Orden = (int)tipo
                });
            }
            await _db.SaveChangesAsync(ct);

            var mapaTipo = await _db.PqrsdTipos.Where(t => t.EsBase)
                .ToDictionaryAsync(t => t.Legal, t => t.Id, ct);
            var expSinTipo = await _db.PqrsdExpedientes.Where(x => x.TipoId == null).ToListAsync(ct);
            foreach (var x in expSinTipo)
                if (mapaTipo.TryGetValue(x.Tipo, out var tid)) x.TipoId = tid;
            if (expSinTipo.Count > 0) await _db.SaveChangesAsync(ct);
        }

        // Backfill de tildes en los nombres BASE sembrados antes con ASCII (convencion nueva: el texto
        // de cara al usuario lleva acentos). Idempotente y tenant-scoped: tras el rename el WHERE deja de
        // matchear, asi que en llamadas siguientes es un UPDATE de 0 filas.
        await _db.PqrsdCategorias.Where(c => c.EsPredeterminada && c.Nombre == "Administracion")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Nombre, "Administración"), ct);
        await _db.PqrsdCategorias.Where(c => c.EsPredeterminada && c.Nombre == "Consejo de Administracion")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Nombre, "Consejo de Administración"), ct);
        await _db.PqrsdTipos.Where(t => t.EsBase && t.Nombre == "Peticion")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Nombre, "Petición"), ct);
        await _db.PqrsdTipos.Where(t => t.EsBase && t.Nombre == "Felicitacion")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Nombre, "Felicitación"), ct);
    }

    private static string TipoNombreBase(TipoPqrsd t) => t switch
    {
        TipoPqrsd.Peticion => "Petición",
        TipoPqrsd.SolicitudDocumentos => "Solicitud de documentos",
        TipoPqrsd.Consulta => "Consulta",
        TipoPqrsd.Queja => "Queja",
        TipoPqrsd.Reclamo => "Reclamo",
        TipoPqrsd.Sugerencia => "Sugerencia",
        TipoPqrsd.Denuncia => "Denuncia",
        TipoPqrsd.Felicitacion => "Felicitación",
        _ => t.ToString()
    };

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

    /// <summary>Mueve el expediente a la columna del tablero cuya semantica legal coincide con el nuevo estado.</summary>
    private async Task SincronizarColumnaLegalAsync(PqrsdExpediente x, EstadoPqrsd nuevoEstado, CancellationToken ct)
    {
        var col = await _db.PqrsdEstados.Where(e => e.SemanticaLegal == nuevoEstado)
            .Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
        if (col.HasValue) x.EstadoId = col.Value;
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
        // Sincroniza el tipo base espejo (fuente de verdad de la UI nueva).
        var tb = await _db.PqrsdTipos.FirstOrDefaultAsync(t => t.EsBase && t.Legal == tipo, ct);
        if (tb is not null)
        {
            tb.DiasHabiles = req.DiasHabiles;
            tb.DiasInconformidad = req.DiasInconformidad;
            tb.NivelUrgencia = req.NivelUrgencia;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Tipos configurables =====================

    private static PqrsdTipoDto MapTipo(PqrsdTipo t) => new(
        t.Id, t.Nombre, t.DiasHabiles, t.DiasInconformidad, t.NivelUrgencia, t.Legal, t.EsBase, t.Activo, t.Orden);

    public async Task<IReadOnlyList<PqrsdTipoDto>> ListarTiposAsync(bool incluirInactivos, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        var q = _db.PqrsdTipos.AsNoTracking().AsQueryable();
        if (!incluirInactivos) q = q.Where(t => t.Activo);
        return await q.OrderBy(t => t.Orden).ThenBy(t => t.Nombre)
            .Select(t => new PqrsdTipoDto(t.Id, t.Nombre, t.DiasHabiles, t.DiasInconformidad, t.NivelUrgencia, t.Legal, t.EsBase, t.Activo, t.Orden))
            .ToListAsync(ct);
    }

    public async Task<PqrsdTipoDto> CrearTipoAsync(GuardarTipoPqrsdRequest req, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre del tipo es obligatorio.");
        var nom = req.Nombre.Trim();
        if (await _db.PqrsdTipos.AnyAsync(t => t.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe un tipo con este nombre.");
        var maxOrden = await _db.PqrsdTipos.AnyAsync(ct) ? await _db.PqrsdTipos.MaxAsync(t => t.Orden, ct) : 0;
        var t = new PqrsdTipo
        {
            Nombre = nom,
            DiasHabiles = req.DiasHabiles < 1 ? 1 : req.DiasHabiles,
            DiasInconformidad = req.DiasInconformidad < 0 ? 0 : req.DiasInconformidad,
            NivelUrgencia = req.NivelUrgencia,
            Legal = req.Legal,
            EsBase = false,
            Activo = true,
            Orden = maxOrden + 1
        };
        _db.PqrsdTipos.Add(t);
        await _db.SaveChangesAsync(ct);
        return MapTipo(t);
    }

    public async Task<bool> ActualizarTipoAsync(Guid id, GuardarTipoPqrsdRequest req, CancellationToken ct)
    {
        var t = await _db.PqrsdTipos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre del tipo es obligatorio.");
        var nom = req.Nombre.Trim();
        if (await _db.PqrsdTipos.AnyAsync(x => x.Id != id && x.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe un tipo con este nombre.");
        t.Nombre = nom;
        t.DiasHabiles = req.DiasHabiles < 1 ? 1 : req.DiasHabiles;
        t.DiasInconformidad = req.DiasInconformidad < 0 ? 0 : req.DiasInconformidad;
        t.NivelUrgencia = req.NivelUrgencia;
        // La conducta legal de los tipos base NO se cambia (protege reserva/comite); los custom si.
        if (!t.EsBase) t.Legal = req.Legal;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        // Mantener el plazo legacy en sync para los tipos base (misma fuente que el semaforo/urgencia).
        if (t.EsBase)
        {
            var plazo = await _db.PqrsdConfiguracionPlazos.FirstOrDefaultAsync(p => p.Tipo == t.Legal, ct);
            if (plazo is not null)
            {
                plazo.DiasHabiles = t.DiasHabiles;
                plazo.DiasInconformidad = t.DiasInconformidad;
                plazo.NivelUrgencia = t.NivelUrgencia;
            }
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarTipoAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.PqrsdTipos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        if (t.EsBase) throw new InvalidOperationException("Los tipos legales base no se pueden eliminar; puedes editarlos o desactivarlos.");
        var enUso = await _db.PqrsdExpedientes.AnyAsync(e => e.TipoId == id, ct);
        if (enUso)
        {
            t.Activo = false; // conserva historia
            t.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.PqrsdTipos.Remove(t);
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Bandeja + ficha =====================

    public async Task<PqrsdBandejaDto> GetBandejaAsync(EstadoPqrsd? estado, TipoPqrsd? tipo, Guid? categoriaId, string? query, bool incluirArchivados, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        IQueryable<PqrsdExpediente> q = _db.PqrsdExpedientes.AsNoTracking().Include(x => x.Categoria);
        // incluirArchivados=false => solo activos (tablero/tabla); true => solo archivados (tab Archivados).
        q = q.Where(x => x.Archivado == incluirArchivados);
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
        ).Take(500).ToListAsync(ct);

        var ids = rows.Select(r => r.x.Id).ToList();

        var sesionesActivas = await _db.PqrsdComiteSesiones.AsNoTracking()
            .Where(s => s.Resultado == null)
            .Select(s => s.ExpedienteId).ToHashSetAsync(ct);

        // Valores de campos dinamicos por expediente (para la vista tabla).
        var valores = await _db.PqrsdCampoValores.AsNoTracking()
            .Where(v => ids.Contains(v.ExpedienteId))
            .Select(v => new { v.ExpedienteId, v.PqrsdCampoId, v.Valor })
            .ToListAsync(ct);
        var valoresPorExp = valores.GroupBy(v => v.ExpedienteId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PqrsdCampoValorDto>)g
                .Select(v => new PqrsdCampoValorDto(v.PqrsdCampoId, v.Valor)).ToList());

        // Numero de unidad relacionada (si el expediente la tiene fijada).
        var unidadIds = rows.Where(r => r.x.UnidadPrivadaId.HasValue).Select(r => r.x.UnidadPrivadaId!.Value).Distinct().ToList();
        var unidadNumeros = unidadIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.UnidadesPrivadas.AsNoTracking().Where(u => unidadIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Numero, ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var plazos = await _db.PqrsdConfiguracionPlazos.AsNoTracking().ToDictionaryAsync(p => p.Tipo, ct);
        var tiposNombres = await _db.PqrsdTipos.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        var items = rows.Select(r =>
        {
            var fechaCreacion = DateOnly.FromDateTime(r.x.CreatedAt.UtcDateTime);
            var semaforo = CalcularSemaforo(r.x.Estado, r.x.TutelaActiva, fechaCreacion, r.x.FechaVencimiento, hoy);
            var diasHasta = r.x.FechaVencimiento.DayNumber - hoy.DayNumber;
            var urgencia = plazos.TryGetValue(r.x.Tipo, out var pl) ? pl.NivelUrgencia : NivelUrgenciaPqrsd.Media;
            var nombre = r.x.IdentidadReservada ? null : r.RadicadorNombre;
            var resumen = r.x.Descripcion.Length > 100 ? r.x.Descripcion[..100] + "..." : r.x.Descripcion;
            var unidadNumero = r.x.UnidadPrivadaId.HasValue ? unidadNumeros.GetValueOrDefault(r.x.UnidadPrivadaId.Value) : null;
            var radId = r.x.IdentidadReservada ? (Guid?)null : r.x.RadicadorPersonaId;
            var tipoNombre = (r.x.TipoId.HasValue && tiposNombres.TryGetValue(r.x.TipoId.Value, out var tn)) ? tn : TipoNombreBase(r.x.Tipo);
            return new PqrsdBandejaItemDto(
                r.x.Id, r.x.NumeroRadicado, r.x.Tipo, r.x.Categoria!.Nombre, resumen, r.x.Estado,
                semaforo, nombre, unidadNumero, r.x.IdentidadReservada, r.x.TutelaActiva,
                r.x.FechaVencimiento, diasHasta, urgencia,
                sesionesActivas.Contains(r.x.Id), r.x.CreatedAt,
                r.x.EstadoId, r.x.Archivado, r.x.UnidadPrivadaId, radId,
                valoresPorExp.GetValueOrDefault(r.x.Id), tipoNombre);
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
            .Include(e => e.CamposValores)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return null;

        string? unidadNumero = x.UnidadPrivadaId.HasValue
            ? await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == x.UnidadPrivadaId).Select(u => u.Numero).FirstOrDefaultAsync(ct)
            : null;
        var camposValores = x.CamposValores
            .Select(v => new PqrsdCampoValorDto(v.PqrsdCampoId, v.Valor)).ToList();

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaCreacion = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
        var semaforo = CalcularSemaforo(x.Estado, x.TutelaActiva, fechaCreacion, x.FechaVencimiento, hoy);
        var diasHasta = x.FechaVencimiento.DayNumber - hoy.DayNumber;

        var plazo = await _db.PqrsdConfiguracionPlazos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Tipo == x.Tipo, ct);
        var urgencia = plazo?.NivelUrgencia ?? NivelUrgenciaPqrsd.Media;

        string? tipoNombre = x.TipoId.HasValue
            ? await _db.PqrsdTipos.AsNoTracking().Where(t => t.Id == x.TipoId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
            : null;
        tipoNombre ??= TipoNombreBase(x.Tipo);

        // Radicador (filtrar nombre si hay reserva)
        var rad = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == x.RadicadorPersonaId, ct);
        string? radNombre = x.IdentidadReservada ? null : (rad is null ? null : $"{rad.Nombres} {rad.Apellidos}".Trim());
        Guid? radId = x.IdentidadReservada ? null : x.RadicadorPersonaId;

        // Nombre de quien subio cada adjunto (Users -> PersonaId -> Personas), para pintar la burbuja.
        var subidoIds = x.Adjuntos.Select(a => a.SubidoPorUsuarioId).Where(g => g != Guid.Empty).Distinct().ToList();
        var nombresSubido = new Dictionary<Guid, string>();
        if (subidoIds.Count > 0)
        {
            var users = await _db.Users.AsNoTracking().Where(u => subidoIds.Contains(u.Id))
                .Select(u => new { u.Id, u.PersonaId }).ToListAsync(ct);
            var personaIds = users.Where(u => u.PersonaId != null).Select(u => u.PersonaId!.Value).Distinct().ToList();
            var personasN = personaIds.Count == 0 ? new() : await _db.Personas.AsNoTracking()
                .Where(p => personaIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
            foreach (var u in users)
                if (u.PersonaId is { } pid && personasN.TryGetValue(pid, out var nm)) nombresSubido[u.Id] = nm;
        }

        var adjuntos = x.Adjuntos.OrderBy(a => a.CreatedAt)
            .Select(a => new PqrsdAdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt,
                a.SubidoPorUsuarioId != Guid.Empty && nombresSubido.TryGetValue(a.SubidoPorUsuarioId, out var sn) ? sn : null,
                a.SubidoPorUsuarioId == Guid.Empty ? null : a.SubidoPorUsuarioId,
                a.Texto, a.Compartido))
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

        // Asignado (persona responsable) - nombre por join (sin FK dura)
        string? asignadoNombre = null;
        if (x.AsignadoPersonaId is { } apid)
        {
            var asig = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == apid, ct);
            asignadoNombre = asig is null ? null : $"{asig.Nombres} {asig.Apellidos}".Trim();
        }

        // Reportes de actividad (comentarios libres), mas recientes primero
        var comentarios = await _db.PqrsdComentarios.AsNoTracking()
            .Where(c => c.PqrsdExpedienteId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PqrsdComentarioDto(c.Id, c.Texto, c.AutorNombre, c.CreatedAt, c.AutorUsuarioId))
            .ToListAsync(ct);

        return new PqrsdExpedienteDetalleDto(
            x.Id, x.NumeroRadicado, x.Tipo, x.CategoriaId, x.Categoria!.Nombre, x.Descripcion,
            x.Estado, semaforo, radNombre, radId, unidadNumero, x.IdentidadReservada, x.TutelaActiva,
            x.TutelaActivadaAt, x.FechaVencimiento, diasHasta, urgencia,
            x.RespuestaAdmin, x.RespuestaAdminAt, x.InconformidadTexto, x.InconformidadAt,
            x.RespuestaDefinitiva, x.RespuestaDefinitivaAt, x.FechaCierre, x.TareaId,
            x.CreatedAt, adjuntos, historial, comiteDto,
            x.EstadoId, x.UnidadPrivadaId, x.Archivado, camposValores, x.TipoId, tipoNombre,
            x.AsignadoPersonaId, asignadoNombre, x.Progreso, comentarios, x.ProrrogaDias,
            x.IdentidadReservada ? null : rad?.Email,
            x.IdentidadReservada ? null : rad?.Telefono);
    }

    // ===================== Radicacion =====================

    public async Task<PqrsdExpedienteDetalleDto> RadicarAsync(RadicarPqrsdRequest req, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Descripcion) || req.Descripcion.Trim().Length < 20)
            throw new InvalidOperationException("Descripcion obligatoria, minimo 20 caracteres.");
        if (req.Descripcion.Length > 2000)
            throw new InvalidOperationException("Descripcion maxima 2000 caracteres.");

        var categoria = await _db.PqrsdCategorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CategoriaId, ct)
            ?? throw new InvalidOperationException("Categoria invalida.");
        if (!categoria.Activa) throw new InvalidOperationException("La categoria no esta activa.");

        // Tipo: si viene TipoId se usa el tipo configurable (nombre + plazo + conducta legal Legal);
        // si no, se resuelve el tipo base del enum recibido (compatibilidad con el flujo viejo).
        PqrsdTipo? tipoConfig = req.TipoId is { } tid
            ? (await _db.PqrsdTipos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid && t.Activo, ct)
                ?? throw new InvalidOperationException("El tipo seleccionado no existe o esta inactivo."))
            : await _db.PqrsdTipos.AsNoTracking().FirstOrDefaultAsync(t => t.EsBase && t.Legal == req.Tipo, ct);
        var tipoLegal = tipoConfig?.Legal ?? req.Tipo;

        if (req.IdentidadReservada && tipoLegal != TipoPqrsd.Denuncia)
            throw new InvalidOperationException("La reserva de identidad solo aplica al tipo Denuncia (RN-02).");

        // Radicador: si el admin selecciona una persona del directorio, se usa esa; si no, la del usuario actual.
        Guid personaId;
        if (req.RadicadorPersonaId is { } radPid)
        {
            var existe = await _db.Personas.AsNoTracking().AnyAsync(p => p.Id == radPid, ct);
            if (!existe) throw new InvalidOperationException("La persona seleccionada como radicador no existe.");
            personaId = radPid;
        }
        else
        {
            personaId = await GetPersonaActualIdAsync(ct)
                ?? throw new InvalidOperationException("No se pudo resolver el radicador (persona del usuario autenticado).");
        }

        // Plazo: del tipo configurable si existe; si no, del plazo legacy por enum legal.
        int diasHabiles;
        if (tipoConfig is not null) { diasHabiles = tipoConfig.DiasHabiles; }
        else
        {
            var plazo = await _db.PqrsdConfiguracionPlazos.AsNoTracking().FirstOrDefaultAsync(p => p.Tipo == tipoLegal, ct)
                ?? throw new InvalidOperationException("No hay plazo configurado para este tipo.");
            diasHabiles = plazo.DiasHabiles;
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaVencimiento = SumarDiasHabiles(hoy, diasHabiles);
        var numero = await GenerarNumeroRadicadoAsync(ct);

        var columnaRecibida = await _db.PqrsdEstados.AsNoTracking()
            .Where(e => e.SemanticaLegal == EstadoPqrsd.Recibida).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);

        var exp = new PqrsdExpediente
        {
            NumeroRadicado = numero,
            Tipo = tipoLegal,
            TipoId = tipoConfig?.Id,
            CategoriaId = req.CategoriaId,
            Descripcion = req.Descripcion.Trim(),
            Estado = EstadoPqrsd.Recibida,
            EstadoId = columnaRecibida,
            RadicadorPersonaId = personaId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            IdentidadReservada = req.IdentidadReservada,
            FechaVencimiento = fechaVencimiento
        };
        _db.PqrsdExpedientes.Add(exp);

        // Valores de campos dinamicos capturados al radicar.
        if (req.Campos is { Count: > 0 })
        {
            var camposActivos = await _db.PqrsdCampos.AsNoTracking().Where(c => c.Activo).Select(c => c.Id).ToHashSetAsync(ct);
            foreach (var cv in req.Campos)
            {
                if (!camposActivos.Contains(cv.CampoId)) continue;
                if (string.IsNullOrWhiteSpace(cv.Valor)) continue;
                _db.PqrsdCampoValores.Add(new PqrsdCampoValor { Expediente = exp, PqrsdCampoId = cv.CampoId, Valor = cv.Valor });
            }
        }

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

    // ===================== Formulario publico (sin login) =====================

    /// <summary>Fija el tenant en el contexto y reabre la conexion para que el interceptor aplique app.tenant_id (patron de escrituras publicas).</summary>
    private async Task ActivarTenantPublicoAsync(Guid tenantId)
    {
        _tenantContext.SetTenant(tenantId);
        await _db.Database.CloseConnectionAsync();
    }

    public async Task<PqrsdPublicoConfigDto?> GetConfigPublicoAsync(Guid tenantId, CancellationToken ct)
    {
        // Tenants es entidad global: se puede leer el branding sin tenant en sesion.
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || tenant.Estado != EstadoCopropiedad.Activa) return null;

        await ActivarTenantPublicoAsync(tenantId);
        await AsegurarCatalogoBaseAsync(ct);   // siembra tipos/categorias si el modulo nunca se abrio en esta copropiedad

        var tipos = await _db.PqrsdTipos.AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden).ThenBy(t => t.Nombre)
            .Select(t => new PqrsdTipoPublicoDto(t.Id, t.Nombre, t.DiasHabiles))
            .ToListAsync(ct);

        var cats = await _db.PqrsdCategorias.AsNoTracking()
            .Where(c => c.Activa)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new PqrsdCategoriaPublicaDto(c.Id, c.Nombre))
            .ToListAsync(ct);

        // Toggles de campos opcionales del formulario + textos de encabezado/pie (default: todo visible).
        var fcfg = await _db.PqrsdFormularioPublicoConfigs.AsNoTracking().FirstOrDefaultAsync(ct);

        // Campos dinamicos marcados para pedirse en el formulario publico (en su orden).
        var camposPub = await _db.PqrsdCampos.AsNoTracking()
            .Where(c => c.Activo && c.MostrarEnPublico)
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new PqrsdCampoPublicoDto(c.Id, c.Label, c.Tipo, c.Opciones, c.Requerido, c.Descripcion))
            .ToListAsync(ct);

        // LogoUrl se guarda RELATIVA al mismo origen (convencion host unificado): la pagina publica la usa tal cual.
        return new PqrsdPublicoConfigDto(tenant.Nombre, tenant.LogoUrl, tipos, cats,
            fcfg?.MostrarTorre ?? true, fcfg?.MostrarCorreo ?? true, fcfg?.MostrarTelefono ?? true,
            fcfg?.EncabezadoTexto, fcfg?.PieTexto, camposPub,
            ParseOrdenCamposFijos(fcfg?.OrdenCamposFijosJson));
    }

    // ===================== Seguimiento publico (link compartible con el radicador) =====================

    public async Task<bool> SetAdjuntoCompartidoAsync(Guid expedienteId, Guid adjuntoId, bool compartido, CancellationToken ct)
    {
        var adj = await _db.PqrsdAdjuntos
            .FirstOrDefaultAsync(a => a.Id == adjuntoId && a.ExpedienteId == expedienteId, ct);
        if (adj is null) return false;
        adj.Compartido = compartido;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Guid?> ObtenerOCrearShareTokenAsync(Guid expedienteId, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;
        if (exp.ShareToken is null)
        {
            exp.ShareToken = Guid.NewGuid();
            await _db.SaveChangesAsync(ct);
        }
        return exp.ShareToken;
    }

    public async Task<PqrsdSeguimientoPublicoDto?> GetSeguimientoPublicoAsync(Guid tenantId, Guid token, CancellationToken ct)
    {
        // Tenants es entidad global: se lee el branding sin tenant en sesion.
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || tenant.Estado != EstadoCopropiedad.Activa) return null;

        await ActivarTenantPublicoAsync(tenantId);

        var exp = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.Categoria)
            .Include(e => e.TipoConfig)
            .Include(e => e.EstadoColumna)
            .Include(e => e.Adjuntos)
            .FirstOrDefaultAsync(e => e.ShareToken == token, ct);
        if (exp is null) return null;

        var tipoNombre = exp.TipoConfig?.Nombre ?? exp.Tipo.ToString();
        var estadoNombre = exp.EstadoColumna?.Nombre ?? exp.Estado.ToString();

        var adjuntos = exp.Adjuntos
            .Where(a => a.Compartido)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new PqrsdSeguimientoAdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt))
            .ToList();

        return new PqrsdSeguimientoPublicoDto(
            tenant.Nombre, tenant.LogoUrl,
            exp.NumeroRadicado, tipoNombre, exp.Categoria?.Nombre ?? "-", estadoNombre,
            exp.CreatedAt, exp.RespuestaAdmin, exp.RespuestaAdminAt, adjuntos);
    }

    // ===================== Respuestas tipo correo (borradores con editor enriquecido) =====================

    public async Task<IReadOnlyList<PqrsdRespuestaDto>> ListarRespuestasAsync(Guid expedienteId, CancellationToken ct)
    {
        var respuestas = await _db.PqrsdRespuestas.AsNoTracking()
            .Where(r => r.ExpedienteId == expedienteId)
            .Include(r => r.Adjuntos)
            .Include(r => r.Destinatarios)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        // Nombre del autor: resuelve por AutorUsuarioId (User -> Persona) para que salga en todas
        // las respuestas (incluidas las viejas sin AutorNombre guardado). Fallback: email o "Sistema".
        var autorNombres = await ResolverNombresUsuariosAsync(
            respuestas.Select(r => r.AutorUsuarioId).Where(g => g != Guid.Empty), ct);

        // Numero de version actual por respuesta (para el badge "vN"). Sin historial => 1.
        var respIds = respuestas.Select(r => r.Id).ToList();
        var verMax = await _db.PqrsdRespuestaVersiones.AsNoTracking()
            .Where(v => respIds.Contains(v.RespuestaId))
            .GroupBy(v => v.RespuestaId)
            .Select(g => new { RespuestaId = g.Key, Max = g.Max(x => x.Numero) })
            .ToDictionaryAsync(x => x.RespuestaId, x => x.Max, ct);

        return respuestas.Select(r => new PqrsdRespuestaDto(
            r.Id, r.Asunto, r.CuerpoHtml,
            !string.IsNullOrWhiteSpace(r.AutorNombre) ? r.AutorNombre
                : (r.AutorUsuarioId != Guid.Empty && autorNombres.TryGetValue(r.AutorUsuarioId, out var an) ? an : null),
            r.CreatedAt, r.Enviada, r.EnviadaAt,
            r.Adjuntos.OrderBy(a => a.CreatedAt).Select(a => new PqrsdAdjuntoDto(
                a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt,
                null, a.SubidoPorUsuarioId == Guid.Empty ? null : a.SubidoPorUsuarioId, a.Texto, a.Compartido)).ToList(),
            r.Archivada, r.ArchivadaAt, verMax.GetValueOrDefault(r.Id, 1),
            r.Destinatarios.Select(d => new DestinatarioRespuestaDto(d.PersonaId, d.Nombre, d.Email)).ToList()))
            .ToList();
    }

    // Mapea los destinatarios del request a entidades (dedup por email, descarta emails invalidos).
    private static IEnumerable<PqrsdRespuestaDestinatario> MapDestinatarios(IEnumerable<DestinatarioRespuestaDto>? dtos)
    {
        if (dtos is null) yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in dtos)
        {
            var email = d.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) continue;
            if (!seen.Add(email)) continue;
            yield return new PqrsdRespuestaDestinatario
            {
                PersonaId = d.PersonaId,
                Nombre = string.IsNullOrWhiteSpace(d.Nombre) ? null : d.Nombre.Trim(),
                Email = email
            };
        }
    }

    // Resuelve nombres legibles de usuarios (User.Id -> Persona "Nombres Apellidos", fallback email).
    private async Task<Dictionary<Guid, string>> ResolverNombresUsuariosAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        var res = new Dictionary<Guid, string>();
        if (ids.Count == 0) return res;
        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.PersonaId, u.Email })
            .ToListAsync(ct);
        var personaIds = users.Where(u => u.PersonaId != null).Select(u => u.PersonaId!.Value).Distinct().ToList();
        var personas = personaIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Personas.AsNoTracking().Where(p => personaIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
        foreach (var u in users)
        {
            string? nombre = null;
            if (u.PersonaId is { } pid && personas.TryGetValue(pid, out var pn) && !string.IsNullOrWhiteSpace(pn))
                nombre = pn;
            nombre ??= u.Email;
            if (!string.IsNullOrWhiteSpace(nombre)) res[u.Id] = nombre!;
        }
        return res;
    }

    // Archiva/desarchiva una respuesta. Las archivadas salen de las tarjetas activas y van a la tabla de archivados.
    public async Task<bool> ArchivarRespuestaAsync(Guid expedienteId, Guid respuestaId, bool archivar, CancellationToken ct)
    {
        var r = await _db.PqrsdRespuestas.FirstOrDefaultAsync(x => x.Id == respuestaId && x.ExpedienteId == expedienteId, ct);
        if (r is null) return false;
        r.Archivada = archivar;
        r.ArchivadaAt = archivar ? DateTimeOffset.UtcNow : null;
        r.ArchivadaPorUsuarioId = archivar ? ActorActual().UsuarioId : null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PqrsdRespuestaDto?> CrearRespuestaBorradorAsync(Guid expedienteId, CrearRespuestaBorradorRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CuerpoHtml)) return null;
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;
        var (uid, _) = ActorActual();
        var nombre = await ResolverNombreActorAsync(ct);
        var r = new PqrsdRespuesta
        {
            ExpedienteId = expedienteId,
            Asunto = string.IsNullOrWhiteSpace(req.Asunto) ? null : req.Asunto.Trim(),
            CuerpoHtml = req.CuerpoHtml,
            AutorUsuarioId = uid ?? Guid.Empty,
            AutorNombre = nombre
        };
        // v1: snapshot inicial del documento.
        r.Versiones.Add(new PqrsdRespuestaVersion
        {
            Numero = 1,
            CuerpoHtml = r.CuerpoHtml,
            Asunto = r.Asunto,
            AutorUsuarioId = r.AutorUsuarioId,
            AutorNombre = nombre
        });
        foreach (var dst in MapDestinatarios(req.Destinatarios)) r.Destinatarios.Add(dst);
        _db.PqrsdRespuestas.Add(r);
        await _db.SaveChangesAsync(ct);
        return new PqrsdRespuestaDto(r.Id, r.Asunto, r.CuerpoHtml, r.AutorNombre, r.CreatedAt,
            r.Enviada, r.EnviadaAt, new List<PqrsdAdjuntoDto>(), false, null, 1,
            r.Destinatarios.Select(d => new DestinatarioRespuestaDto(d.PersonaId, d.Nombre, d.Email)).ToList());
    }

    public async Task<PqrsdRespuestaDto?> ActualizarRespuestaBorradorAsync(Guid expedienteId, Guid respuestaId, CrearRespuestaBorradorRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CuerpoHtml)) return null;
        var r = await _db.PqrsdRespuestas
            .Include(x => x.Adjuntos)
            .Include(x => x.Destinatarios)
            .FirstOrDefaultAsync(x => x.Id == respuestaId && x.ExpedienteId == expedienteId, ct);
        if (r is null || r.Enviada) return null;   // una respuesta ya enviada no se edita

        // Snapshot del estado previo antes de sobrescribir (para el historial de versiones).
        var oldCuerpo = r.CuerpoHtml;
        var oldAsunto = r.Asunto;

        r.Asunto = string.IsNullOrWhiteSpace(req.Asunto) ? null : req.Asunto.Trim();
        r.CuerpoHtml = req.CuerpoHtml;

        var existentes = await _db.PqrsdRespuestaVersiones
            .Where(v => v.RespuestaId == r.Id).Select(v => v.Numero).ToListAsync(ct);
        int nextNum;
        if (existentes.Count == 0)
        {
            // Respuesta creada antes de existir el historial: siembra v1 con el estado previo.
            _db.PqrsdRespuestaVersiones.Add(new PqrsdRespuestaVersion
            {
                RespuestaId = r.Id,
                Numero = 1,
                CuerpoHtml = oldCuerpo,
                Asunto = oldAsunto,
                AutorUsuarioId = r.AutorUsuarioId,
                AutorNombre = r.AutorNombre
            });
            nextNum = 2;
        }
        else nextNum = existentes.Max() + 1;

        var (uid, _) = ActorActual();
        var editorNombre = await ResolverNombreActorAsync(ct);
        _db.PqrsdRespuestaVersiones.Add(new PqrsdRespuestaVersion
        {
            RespuestaId = r.Id,
            Numero = nextNum,
            CuerpoHtml = r.CuerpoHtml,
            Asunto = r.Asunto,
            AutorUsuarioId = uid ?? Guid.Empty,
            AutorNombre = editorNombre
        });

        // Reemplaza los destinatarios por los enviados (si el request los trae).
        if (req.Destinatarios is not null)
        {
            _db.PqrsdRespuestaDestinatarios.RemoveRange(r.Destinatarios);
            r.Destinatarios.Clear();
            foreach (var dst in MapDestinatarios(req.Destinatarios)) r.Destinatarios.Add(dst);
        }

        await _db.SaveChangesAsync(ct);
        return new PqrsdRespuestaDto(r.Id, r.Asunto, r.CuerpoHtml, r.AutorNombre, r.CreatedAt, r.Enviada, r.EnviadaAt,
            r.Adjuntos.OrderBy(a => a.CreatedAt).Select(a => new PqrsdAdjuntoDto(
                a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt,
                null, a.SubidoPorUsuarioId == Guid.Empty ? null : a.SubidoPorUsuarioId, a.Texto, a.Compartido)).ToList(),
            r.Archivada, r.ArchivadaAt, nextNum,
            r.Destinatarios.Select(d => new DestinatarioRespuestaDto(d.PersonaId, d.Nombre, d.Email)).ToList());
    }

    // Lista el historial de versiones de una respuesta (mas reciente primero).
    public async Task<IReadOnlyList<PqrsdRespuestaVersionDto>> ListarVersionesRespuestaAsync(Guid expedienteId, Guid respuestaId, CancellationToken ct)
    {
        var existe = await _db.PqrsdRespuestas.AsNoTracking()
            .AnyAsync(x => x.Id == respuestaId && x.ExpedienteId == expedienteId, ct);
        if (!existe) return Array.Empty<PqrsdRespuestaVersionDto>();

        var vs = await _db.PqrsdRespuestaVersiones.AsNoTracking()
            .Where(v => v.RespuestaId == respuestaId)
            .OrderByDescending(v => v.Numero)
            .ToListAsync(ct);
        var nombres = await ResolverNombresUsuariosAsync(vs.Select(v => v.AutorUsuarioId).Where(g => g != Guid.Empty), ct);
        return vs.Select(v => new PqrsdRespuestaVersionDto(
            v.Numero, v.Asunto, v.CuerpoHtml,
            !string.IsNullOrWhiteSpace(v.AutorNombre) ? v.AutorNombre
                : (v.AutorUsuarioId != Guid.Empty && nombres.TryGetValue(v.AutorUsuarioId, out var n) ? n : null),
            v.CreatedAt)).ToList();
    }

    // Compone el HTML del documento oficial (membrete + cuerpo) para vista previa o generacion de PDF.
    // Usa la identidad del Tenant + su config de membrete; el cuerpoHtml es el texto de la respuesta.
    public async Task<string?> ComponerDocumentoRespuestaAsync(Guid expedienteId, string cuerpoHtml, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.TipoConfig)
            .Include(e => e.RadicadorPersona)
            .FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == exp.TenantId, ct);
        if (tenant is null) return null;

        var tipoNombre = exp.TipoConfig?.Nombre ?? exp.Tipo.ToString();
        var destinatario = exp.IdentidadReservada
            ? "Identidad reservada"
            : exp.RadicadorPersona is null
                ? null
                : $"{exp.RadicadorPersona.Nombres} {exp.RadicadorPersona.Apellidos}".Trim();

        var contenido = new Propia.Application.Documents.MembreteDocContenido(
            TipoBadge: "Respuesta PQRSD",
            RadicadoLabel: "Radicado",
            Radicado: exp.NumeroRadicado,
            Fecha: exp.RespuestaAdminAt ?? DateTimeOffset.UtcNow,
            CuerpoHtml: cuerpoHtml ?? "",
            DestinatarioNombre: destinatario,
            // Referencia del expediente (tipo) como linea del destinatario; el badge dice "Respuesta PQRSD".
            DestinatarioLinea: string.IsNullOrWhiteSpace(tipoNombre) ? null : $"Ref. {tipoNombre} - {exp.NumeroRadicado}");

        return _membrete.Construir(tenant, contenido);
    }

    // ===================== Plantillas de respuesta (combinacion de correspondencia) =====================

    private static readonly (string Token, string Desc)[] _tokensPlantilla = new[]
    {
        ("copropiedad.nombre", "Nombre de la copropiedad"),
        ("copropiedad.nit", "NIT de la copropiedad"),
        ("copropiedad.direccion", "Direccion de la copropiedad"),
        ("copropiedad.ciudad", "Ciudad"),
        ("radicado.numero", "Numero de radicado"),
        ("radicado.tipo", "Tipo de PQRSD"),
        ("radicado.categoria", "Categoria"),
        ("radicado.fecha", "Fecha de radicacion"),
        ("radicado.estado", "Estado actual"),
        ("solicitante.nombre", "Nombre del solicitante"),
        ("solicitante.identificacion", "Identificacion del solicitante"),
        ("solicitante.correo", "Correo del solicitante"),
        ("solicitante.telefono", "Telefono del solicitante"),
        ("usuario.nombre", "Nombre del solicitante (alias)"),
        ("usuario.identificacion", "Identificacion del solicitante (alias)"),
        ("unidad.numero", "Numero de la unidad"),
        ("unidad.torre", "Torre/bloque de la unidad"),
        ("unidad_privada.propietario", "Propietario de la unidad"),
        ("unidad.propietario", "Propietario de la unidad (alias)"),
        ("gestor.nombre", "Nombre de quien responde (usuario actual)"),
        ("fecha.hoy", "Fecha de hoy"),
    };

    public IReadOnlyList<PqrsdTokenDto> ListarTokensPlantilla()
        => _tokensPlantilla.Select(t => new PqrsdTokenDto("{" + t.Token + "}", t.Desc)).ToList();

    public async Task<IReadOnlyList<PqrsdPlantillaDto>> ListarPlantillasAsync(CancellationToken ct)
    {
        await SembrarPlantillasDesdeSemillaSiVacioAsync(ct);
        return await _db.PqrsdPlantillasRespuesta.AsNoTracking().Where(p => p.Activa)
            .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new PqrsdPlantillaDto(p.Id, p.Nombre, p.CuerpoHtml)).ToListAsync(ct);
    }

    // "Nace con" las plantillas: si la copropiedad no tiene NINGUNA plantilla propia, copia las
    // semillas activas del catalogo global (Super Admin). Idempotente: solo actua si esta vacio,
    // asi el admin puede borrar las que no quiera sin que reaparezcan. Corre en el contexto del
    // tenant actual (RLS ok: inserta filas de su propio tenant_id).
    private async Task SembrarPlantillasDesdeSemillaSiVacioAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return;
        if (await _db.PqrsdPlantillasRespuesta.AnyAsync(ct)) return;   // ya tiene (query filter -> del tenant)

        var semillas = await _db.PqrsdPlantillasSemilla.AsNoTracking()
            .Where(s => s.Activa).OrderBy(s => s.Orden).ThenBy(s => s.Nombre).ToListAsync(ct);
        if (semillas.Count == 0) return;

        foreach (var s in semillas)
            _db.PqrsdPlantillasRespuesta.Add(new PqrsdPlantillaRespuesta
            {
                TenantId = tenantId.Value,
                Nombre = s.Nombre,
                CuerpoHtml = s.CuerpoHtml,
                Activa = true,
                Orden = s.Orden
            });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PqrsdPlantillaDto> CrearPlantillaAsync(GuardarPlantillaRequest req, CancellationToken ct)
    {
        var count = await _db.PqrsdPlantillasRespuesta.CountAsync(ct);
        var p = new PqrsdPlantillaRespuesta { Nombre = (req.Nombre ?? "Plantilla").Trim(), CuerpoHtml = req.CuerpoHtml ?? "", Activa = true, Orden = count };
        _db.PqrsdPlantillasRespuesta.Add(p);
        await _db.SaveChangesAsync(ct);
        return new PqrsdPlantillaDto(p.Id, p.Nombre, p.CuerpoHtml);
    }

    public async Task<bool> ActualizarPlantillaAsync(Guid id, GuardarPlantillaRequest req, CancellationToken ct)
    {
        var p = await _db.PqrsdPlantillasRespuesta.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        p.Nombre = (req.Nombre ?? p.Nombre).Trim();
        p.CuerpoHtml = req.CuerpoHtml ?? "";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarPlantillaAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.PqrsdPlantillasRespuesta.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        _db.PqrsdPlantillasRespuesta.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string?> ResolverPlantillaAsync(Guid expedienteId, Guid plantillaId, CancellationToken ct)
    {
        var plantilla = await _db.PqrsdPlantillasRespuesta.AsNoTracking().FirstOrDefaultAsync(p => p.Id == plantillaId, ct);
        if (plantilla is null) return null;
        var exp = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.Categoria).Include(e => e.TipoConfig).Include(e => e.RadicadorPersona)
            .FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == exp.TenantId, ct);
        var (_, gestorNombre) = ActorActual();
        var esCO = new System.Globalization.CultureInfo("es-CO");

        string unidadNum = "", unidadTorre = "", propietario = "";
        if (exp.UnidadPrivadaId is { } uid)
        {
            var unidad = await _db.UnidadesPrivadas.AsNoTracking().Include(u => u.Torre).FirstOrDefaultAsync(u => u.Id == uid, ct);
            if (unidad is not null) { unidadNum = unidad.Numero; unidadTorre = unidad.Torre?.Nombre ?? ""; }
            var propId = await _db.UnidadPersonas.AsNoTracking()
                .Where(up => up.UnidadId == uid && up.Rol == Domain.Enums.RolUnidadPersona.Propietario && up.PersonaId != null)
                .Select(up => up.PersonaId).FirstOrDefaultAsync(ct);
            if (propId is { } pid)
            {
                var prop = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct);
                if (prop is not null) propietario = $"{prop.Nombres} {prop.Apellidos}".Trim();
            }
        }

        var rad = exp.RadicadorPersona;
        var solNombre = exp.IdentidadReservada ? "(reservada)" : (rad is null ? "" : $"{rad.Nombres} {rad.Apellidos}".Trim());
        var solDoc = exp.IdentidadReservada ? "" : (rad?.Documento ?? "");

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["copropiedad.nombre"] = tenant?.Nombre ?? "",
            ["copropiedad.nit"] = tenant?.Nit ?? "",
            ["copropiedad.direccion"] = tenant?.Direccion ?? "",
            ["copropiedad.ciudad"] = tenant?.Ciudad ?? "",
            ["radicado.numero"] = exp.NumeroRadicado,
            ["radicado.tipo"] = exp.TipoConfig?.Nombre ?? exp.Tipo.ToString(),
            ["radicado.categoria"] = exp.Categoria?.Nombre ?? "",
            ["radicado.fecha"] = exp.CreatedAt.ToLocalTime().ToString("dd 'de' MMMM 'de' yyyy", esCO),
            ["radicado.estado"] = exp.Estado.ToString(),
            ["solicitante.nombre"] = solNombre,
            ["solicitante.identificacion"] = solDoc,
            ["solicitante.correo"] = exp.IdentidadReservada ? "" : (rad?.Email ?? ""),
            ["solicitante.telefono"] = exp.IdentidadReservada ? "" : (rad?.Telefono ?? ""),
            ["usuario.nombre"] = solNombre,
            ["usuario.identificacion"] = solDoc,
            ["unidad.numero"] = unidadNum,
            ["unidad.torre"] = unidadTorre,
            ["unidad_privada.propietario"] = propietario,
            ["unidad.propietario"] = propietario,
            ["gestor.nombre"] = gestorNombre ?? "",
            ["fecha.hoy"] = DateTimeOffset.Now.ToLocalTime().ToString("dd 'de' MMMM 'de' yyyy", esCO),
        };

        return System.Text.RegularExpressions.Regex.Replace(plantilla.CuerpoHtml, @"\{([a-zA-Z_]+\.[a-zA-Z_]+)\}", m =>
        {
            var key = m.Groups[1].Value;
            return map.TryGetValue(key, out var val) ? System.Net.WebUtility.HtmlEncode(val) : m.Value;
        });
    }

    // ---- Config del formulario publico (admin): campos opcionales + textos + orden de campos fijos ----
    // Claves canonicas de los campos FIJOS del formulario publico, en su orden por defecto.
    private static readonly string[] CamposFijosDefault =
        { "tipo", "categoria", "torre", "unidad", "tipoDoc", "documento", "nombres", "apellidos", "correo", "telefono", "descripcion" };

    private static IReadOnlyList<string> ParseOrdenCamposFijos(string? json)
    {
        List<string>? guardado = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { guardado = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json!); } catch { }
        }
        if (guardado is null || guardado.Count == 0) return CamposFijosDefault;
        // Solo claves conocidas, en el orden guardado; anexar las que falten (robustez ante nuevas claves).
        var res = guardado.Where(CamposFijosDefault.Contains).Distinct().ToList();
        foreach (var k in CamposFijosDefault) if (!res.Contains(k)) res.Add(k);
        return res;
    }

    public async Task<PqrsdFormularioPublicoConfigDto> GetFormularioPublicoConfigAsync(CancellationToken ct)
    {
        var c = await _db.PqrsdFormularioPublicoConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return new PqrsdFormularioPublicoConfigDto(
            c?.MostrarTorre ?? true, c?.MostrarCorreo ?? true, c?.MostrarTelefono ?? true,
            c?.EncabezadoTexto, c?.PieTexto, ParseOrdenCamposFijos(c?.OrdenCamposFijosJson));
    }

    public async Task<bool> GuardarFormularioPublicoConfigAsync(PqrsdFormularioPublicoConfigDto req, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId ?? throw new InvalidOperationException("No hay copropiedad activa.");
        var c = await _db.PqrsdFormularioPublicoConfigs.FirstOrDefaultAsync(ct);
        if (c is null)
        {
            c = new PqrsdFormularioPublicoConfig { TenantId = tenantId };
            _db.PqrsdFormularioPublicoConfigs.Add(c);
        }
        c.MostrarTorre = req.MostrarTorre;
        c.MostrarCorreo = req.MostrarCorreo;
        c.MostrarTelefono = req.MostrarTelefono;
        c.EncabezadoTexto = string.IsNullOrWhiteSpace(req.EncabezadoTexto) ? null : req.EncabezadoTexto.Trim();
        c.PieTexto = string.IsNullOrWhiteSpace(req.PieTexto) ? null : req.PieTexto.Trim();
        // Orden de campos fijos: guardar solo claves conocidas; null si es el orden por defecto.
        var orden = (req.OrdenCamposFijos ?? new List<string>()).Where(CamposFijosDefault.Contains).Distinct().ToList();
        c.OrdenCamposFijosJson = (orden.Count > 0 && !orden.SequenceEqual(CamposFijosDefault))
            ? System.Text.Json.JsonSerializer.Serialize(orden) : null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Marca/desmarca un campo dinamico para que se pida (o no) en el formulario publico.
    public async Task<bool> SetCampoPublicoAsync(Guid campoId, bool mostrar, CancellationToken ct)
    {
        var c = await _db.PqrsdCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        c.MostrarEnPublico = mostrar;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RadicarPublicoResultDto> RadicarPublicoAsync(Guid tenantId, RadicarPublicoRequest req, string? ipOrigen, CancellationToken ct)
    {
        // Honeypot: bots que llenan el campo oculto se descartan silenciosamente (no es error visible).
        if (!string.IsNullOrWhiteSpace(req.Website))
            throw new InvalidOperationException("No se pudo procesar la solicitud.");

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || tenant.Estado != EstadoCopropiedad.Activa)
            throw new InvalidOperationException("La copropiedad no esta disponible para radicar.");

        if (!req.AceptaTratamiento)
            throw new InvalidOperationException("Debes autorizar el tratamiento de tus datos personales para radicar.");
        if (string.IsNullOrWhiteSpace(req.Documento))
            throw new InvalidOperationException("El numero de identificacion es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.Nombres))
            throw new InvalidOperationException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.UnidadTexto))
            throw new InvalidOperationException("Debes indicar tu unidad (ej. 101, A-203).");
        var descr = (req.Descripcion ?? "").Trim();
        if (descr.Length < 20)
            throw new InvalidOperationException("La descripcion es obligatoria (minimo 20 caracteres).");

        await ActivarTenantPublicoAsync(tenantId);
        await AsegurarCatalogoBaseAsync(ct);

        var tipo = await _db.PqrsdTipos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TipoId && t.Activo, ct)
            ?? throw new InvalidOperationException("El tipo de solicitud seleccionado no es valido.");
        var categoria = await _db.PqrsdCategorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CategoriaId && c.Activa, ct)
            ?? throw new InvalidOperationException("La categoria seleccionada no es valida.");

        // --- Radicador: resolver/crear la Persona GLOBAL por (tipoDocumento, documento) ---
        var doc = req.Documento.Trim();
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.TipoDocumento == req.TipoDocumento && p.Documento == doc, ct);
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        var telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim();
        if (persona is null)
        {
            // Email es unico global (citext): si ya lo usa otra persona, no lo asignamos para no romper el indice.
            if (email is not null && await _db.Personas.AnyAsync(p => p.Email == email, ct)) email = null;
            persona = new Persona
            {
                TipoDocumento = req.TipoDocumento,
                Documento = doc,
                Nombres = req.Nombres.Trim(),
                Apellidos = (req.Apellidos ?? "").Trim(),
                Email = email,
                Telefono = telefono,
                PerfilIncompleto = true,
                EstadoDirectorio = EstadoDirectorio.Activo,
                AceptoTratamientoDatos = true,
                FechaAceptacionDatos = DateTimeOffset.UtcNow,
                CanalAceptacion = CanalAceptacionDatos.FormularioWeb,
                IpAceptacion = ipOrigen
            };
            _db.Personas.Add(persona);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // No sobreescribir datos existentes: solo completar contacto vacio.
            var cambio = false;
            if (string.IsNullOrWhiteSpace(persona.Email) && email is not null
                && !await _db.Personas.AnyAsync(p => p.Id != persona.Id && p.Email == email, ct))
            { persona.Email = email; cambio = true; }
            if (string.IsNullOrWhiteSpace(persona.Telefono) && telefono is not null)
            { persona.Telefono = telefono; cambio = true; }
            if (cambio) await _db.SaveChangesAsync(ct);
        }

        // --- Unidad: match EXACTO por numero (+ torre opcional). Sin busqueda: el residente conoce su unidad. ---
        var unidadTxt = req.UnidadTexto.Trim();
        var torreTxt = req.TorreTexto?.Trim();
        var unidadTxtLower = unidadTxt.ToLower();
        var qUnidad = _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Numero.ToLower() == unidadTxtLower);
        if (!string.IsNullOrWhiteSpace(torreTxt))
        {
            var torreTxtLower = torreTxt.ToLower();
            var torreId = await _db.Torres.AsNoTracking()
                .Where(t => t.Nombre.ToLower() == torreTxtLower).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            if (torreId is not null) qUnidad = qUnidad.Where(u => u.TorreId == torreId);
        }
        var unidadId = await qUnidad.Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);

        // Si no se pudo enlazar la unidad, conservamos el dato escrito al inicio de la descripcion (no se pierde).
        if (unidadId is null)
        {
            var encab = string.IsNullOrWhiteSpace(torreTxt)
                ? $"[Radicado externo] Unidad indicada por el solicitante: {unidadTxt}"
                : $"[Radicado externo] Unidad indicada por el solicitante: {torreTxt} - {unidadTxt}";
            descr = (encab + "\n\n" + descr);
            if (descr.Length > 2000) descr = descr[..2000];
        }

        // Campos dinamicos del formulario publico: solo se aceptan los que realmente estan marcados
        // para el publico (seguridad: un submit externo no puede setear cualquier campo interno).
        List<PqrsdCampoValorDto>? camposVals = null;
        if (req.CamposDinamicos is { Count: > 0 })
        {
            var permitidos = (await _db.PqrsdCampos.AsNoTracking()
                .Where(c => c.Activo && c.MostrarEnPublico).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
            camposVals = req.CamposDinamicos
                .Where(kv => permitidos.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => new PqrsdCampoValorDto(kv.Key, kv.Value)).ToList();
            if (camposVals.Count == 0) camposVals = null;
        }

        var radReq = new RadicarPqrsdRequest(
            Tipo: tipo.Legal,
            CategoriaId: categoria.Id,
            Descripcion: descr,
            IdentidadReservada: false,
            Adjuntos: null,
            UnidadPrivadaId: unidadId,
            RadicadorPersonaId: persona.Id,
            Campos: camposVals,
            TipoId: tipo.Id);

        var detalle = await RadicarAsync(radReq, ct);
        return new RadicarPublicoResultDto(detalle.NumeroRadicado);
    }

    private async Task<string> GenerarNumeroRadicadoAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefijo = $"PQRSD-{year}-";
        var prefijoLegacy = $"PQRS-{year}-";   // radicados emitidos antes del cambio de sigla; no se reescriben
        var ultimos = await _db.PqrsdExpedientes.AsNoTracking()
            .Where(x => x.NumeroRadicado.StartsWith(prefijo) || x.NumeroRadicado.StartsWith(prefijoLegacy))
            .Select(x => x.NumeroRadicado)
            .ToListAsync(ct);
        int max = 0;
        foreach (var n in ultimos)
        {
            // El consecutivo son los digitos tras el ultimo guion, valga cual valga el prefijo.
            if (int.TryParse(n[(n.LastIndexOf('-') + 1)..], out var s) && s > max) max = s;
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
        await SincronizarColumnaLegalAsync(x, EstadoPqrsd.EnGestion, ct);
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
            // Segunda respuesta tras inconformidad -> cierra definitivamente (y archiva: desaparece del tablero)
            x.RespuestaDefinitiva = req.Texto.Trim();
            x.RespuestaDefinitivaAt = DateTimeOffset.UtcNow;
            x.Estado = EstadoPqrsd.Cerrada;
            x.FechaCierre = DateTimeOffset.UtcNow;
            x.CerradoPorUsuarioId = GetUsuarioActualId();
            x.Archivado = true;
            x.ArchivadoAt = DateTimeOffset.UtcNow;
            x.ArchivadoPorUsuarioId = GetUsuarioActualId();
        }
        else
        {
            x.RespuestaAdmin = req.Texto.Trim();
            x.RespuestaAdminAt = DateTimeOffset.UtcNow;
            x.RespuestaAdminPorUsuarioId = GetUsuarioActualId();
            x.Estado = EstadoPqrsd.Respondida;
        }
        await SincronizarColumnaLegalAsync(x, x.Estado, ct);
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

        // Enviar la respuesta al radicador por los canales elegidos (correo / celular).
        var canales = new List<Domain.Enums.CanalNotificacion>();
        if (req.Correo) canales.Add(Domain.Enums.CanalNotificacion.Email);
        if (req.Celular) canales.Add(Domain.Enums.CanalNotificacion.WhatsApp);
        var tenantIdResp = _tenantContext.CurrentTenantId;
        if (canales.Count > 0 && tenantIdResp is not null && x.RadicadorPersonaId != Guid.Empty)
        {
            var cuerpo = $"Respuesta a tu PQR {x.NumeroRadicado}:\n\n{req.Texto.Trim()}";
            var lote = canales.Select(canal => new Propia.Application.Notificaciones.EnviarNotificacionRequest(
                Canal: canal,
                Cuerpo: cuerpo,
                TenantId: tenantIdResp,
                PersonaDestinatariaId: x.RadicadorPersonaId,
                Asunto: $"Respuesta a tu PQR {x.NumeroRadicado}",
                Prioridad: Domain.Enums.PrioridadNotificacion.Normal,
                ModuloOrigenCodigo: "2.9",
                EntidadOrigenId: x.Id));
            await _noti.EnviarLoteAsync(lote, ct);
        }

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
        await SincronizarColumnaLegalAsync(x, EstadoPqrsd.EnGestion, ct);
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
        if (req.MotivoCierreId is not Guid motivoId)
            throw new InvalidOperationException("Debes elegir un motivo de cierre.");
        var motivo = await _db.MotivosCierre.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == motivoId && m.Modulo == "pqrsd", ct)
            ?? throw new InvalidOperationException("Motivo de cierre invalido.");

        // La clasificacion del motivo define el estado legal terminal.
        var estadoDestino = motivo.Clasificacion == ClasificacionCierre.ViaInternaAgotada
            ? EstadoPqrsd.ViaInternaAgotada : EstadoPqrsd.Cerrada;

        var anterior = x.Estado;
        x.RespuestaDefinitiva = req.RespuestaDefinitiva.Trim();
        x.RespuestaDefinitivaAt = DateTimeOffset.UtcNow;
        x.Estado = estadoDestino;
        x.MotivoCierreId = motivoId;
        await SincronizarColumnaLegalAsync(x, estadoDestino, ct);
        x.FechaCierre = DateTimeOffset.UtcNow;
        x.CerradoPorUsuarioId = GetUsuarioActualId();
        // Cerrar = archivar: la tarjeta desaparece del tablero activo y queda en "Cerrados".
        x.Archivado = true;
        x.ArchivadoAt = DateTimeOffset.UtcNow;
        x.ArchivadoPorUsuarioId = GetUsuarioActualId();
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = estadoDestino,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = $"Cierre por admin - motivo: {motivo.Nombre}"
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

    // ===================== Prorroga (ampliacion de plazo) =====================

    public async Task<bool> AmpliarPlazoAsync(Guid id, AmpliarPlazoRequest req, CancellationToken ct)
    {
        if (req.Dias < 1) throw new InvalidOperationException("La prorroga debe ser de al menos 1 dia habil.");
        if (req.Dias > 60) throw new InvalidOperationException("La prorroga no puede superar 60 dias habiles.");
        if (string.IsNullOrWhiteSpace(req.Motivo))
            throw new InvalidOperationException("Debes indicar el motivo de la prorroga (queda registrado en la traza).");

        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado is EstadoPqrsd.Cerrada or EstadoPqrsd.ViaInternaAgotada)
            throw new InvalidOperationException("No se puede prorrogar un expediente cerrado.");

        var anterior = x.FechaVencimiento;
        // La prorroga se suma en dias habiles a la fecha de vencimiento vigente (aumenta el tiempo de entrega).
        x.FechaVencimiento = SumarDiasHabiles(x.FechaVencimiento, req.Dias);
        x.ProrrogaDias += req.Dias;
        x.UpdatedAt = DateTimeOffset.UtcNow;

        var motivo = req.Motivo.Trim();
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = x.Estado,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Prorroga,
            Nota = $"Prorroga de {req.Dias} dia(s) habil(es). Vencimiento {anterior:yyyy-MM-dd} -> {x.FechaVencimiento:yyyy-MM-dd}. Motivo: {motivo}"
        });
        await _db.SaveChangesAsync(ct);

        await NotificarAdminsTenantAsync("2.9", id,
            $"Prorroga PQRSD: {x.NumeroRadicado}",
            $"Se amplio el plazo en {req.Dias} dia(s) habil(es). Nueva fecha de vencimiento: {x.FechaVencimiento:yyyy-MM-dd}. Motivo: {motivo}",
            Domain.Enums.PrioridadNotificacion.Normal, ct);

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
            await SincronizarColumnaLegalAsync(x, EstadoPqrsd.ViaInternaAgotada, ct);
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

    // ===================== Tablero: columnas (estados) configurables =====================

    private static PqrsdEstadoDto MapEstado(PqrsdEstado e) => new(
        e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal, e.EsBase, e.Activo, e.SemanticaLegal);

    public async Task<IReadOnlyList<PqrsdEstadoDto>> ListarEstadosAsync(CancellationToken ct)
    {
        await AsegurarTableroBaseAsync(ct);
        return await _db.PqrsdEstados.AsNoTracking()
            .OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new PqrsdEstadoDto(e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal, e.EsBase, e.Activo, e.SemanticaLegal))
            .ToListAsync(ct);
    }

    public async Task<PqrsdEstadoDto> CrearEstadoAsync(CrearEstadoPqrsdRequest req, CancellationToken ct)
    {
        await AsegurarTableroBaseAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var nom = req.Nombre.Trim();
        if (await _db.PqrsdEstados.AnyAsync(e => e.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe una columna con este nombre.");
        var maxOrden = await _db.PqrsdEstados.AnyAsync(ct) ? await _db.PqrsdEstados.MaxAsync(e => e.Orden, ct) : 0;
        var estado = new PqrsdEstado
        {
            Nombre = nom,
            Color = string.IsNullOrWhiteSpace(req.Color) ? "#6D4FE3" : req.Color!.Trim(),
            Orden = maxOrden + 1,
            EsTerminal = false,
            EsBase = false,
            Activo = true,
            SemanticaLegal = null
        };
        _db.PqrsdEstados.Add(estado);
        await _db.SaveChangesAsync(ct);
        return MapEstado(estado);
    }

    public async Task<bool> ActualizarEstadoAsync(Guid id, ActualizarEstadoPqrsdRequest req, CancellationToken ct)
    {
        var e = await _db.PqrsdEstados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var nom = req.Nombre.Trim();
        if (await _db.PqrsdEstados.AnyAsync(x => x.Id != id && x.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe una columna con este nombre.");
        e.Nombre = nom;
        if (!string.IsNullOrWhiteSpace(req.Color)) e.Color = req.Color!.Trim();
        e.Orden = req.Orden;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarEstadoAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.PqrsdEstados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        var total = await _db.PqrsdEstados.CountAsync(ct);
        if (total <= 1) throw new InvalidOperationException("El tablero debe tener al menos una columna.");
        var enUso = await _db.PqrsdExpedientes.AnyAsync(x => x.EstadoId == id, ct);
        if (enUso) throw new InvalidOperationException("Hay PQR en esta columna. Muevelos a otra columna antes de eliminarla.");
        _db.PqrsdEstados.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReordenarEstadoAsync(Guid id, string direccion, CancellationToken ct)
    {
        var lista = await _db.PqrsdEstados.OrderBy(e => e.Orden).ThenBy(e => e.Nombre).ToListAsync(ct);
        var idx = lista.FindIndex(e => e.Id == id);
        if (idx < 0) return false;
        var dir = (direccion ?? "").ToLowerInvariant();
        var j = dir is "arriba" or "up" or "-1" ? idx - 1 : idx + 1;
        if (j < 0 || j >= lista.Count) return true;
        (lista[idx].Orden, lista[j].Orden) = (lista[j].Orden, lista[idx].Orden);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MoverAEstadoAsync(Guid expedienteId, Guid estadoId, CancellationToken ct)
    {
        await AsegurarTableroBaseAsync(ct);
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (x is null) return false;
        var col = await _db.PqrsdEstados.FirstOrDefaultAsync(e => e.Id == estadoId, ct)
            ?? throw new InvalidOperationException("Columna no encontrada.");
        x.EstadoId = col.Id;
        // Si la columna arrastrada tiene semantica legal, sincronizar el enum legal (plazos/semaforo).
        if (col.SemanticaLegal is { } sem && sem != x.Estado)
        {
            var anterior = x.Estado;
            x.Estado = sem;
            if (sem == EstadoPqrsd.Cerrada || sem == EstadoPqrsd.ViaInternaAgotada)
            {
                x.FechaCierre ??= DateTimeOffset.UtcNow;
                x.CerradoPorUsuarioId ??= GetUsuarioActualId();
            }
            _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
            {
                ExpedienteId = x.Id,
                EstadoAnterior = anterior,
                EstadoNuevo = sem,
                ActorUsuarioId = GetUsuarioActualId(),
                Origen = OrigenCambioEstado.Manual,
                Nota = $"Movido a columna '{col.Nombre}'"
            });
        }
        x.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Tablero: campos dinamicos =====================

    private static PqrsdCampoDto MapCampo(PqrsdCampo c) => new(
        c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna,
        c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo);

    public async Task<IReadOnlyList<PqrsdCampoDto>> ListarCamposAsync(CancellationToken ct)
    {
        return await _db.PqrsdCampos.AsNoTracking().Where(c => c.Activo)
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new PqrsdCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna,
                c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo, c.MostrarEnPublico))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PqrsdCampoDto>> ListarCamposArchivadosAsync(CancellationToken ct)
    {
        return await _db.PqrsdCampos.AsNoTracking().Where(c => !c.Activo)
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new PqrsdCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna,
                c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo, c.MostrarEnPublico))
            .ToListAsync(ct);
    }

    public async Task<PqrsdCampoDto> CrearCampoAsync(GuardarCampoPqrsdRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("La etiqueta del campo es obligatoria.");
        var label = req.Label.Trim();
        if (await _db.PqrsdCampos.AnyAsync(c => c.Activo && c.Label == label, ct))
            throw new InvalidOperationException("Ya existe un campo activo con esta etiqueta.");
        var maxOrden = await _db.PqrsdCampos.AnyAsync(ct) ? await _db.PqrsdCampos.MaxAsync(c => c.Orden, ct) : 0;
        var c = new PqrsdCampo
        {
            Label = label,
            Orden = maxOrden + 1,
            Tipo = req.Tipo,
            Opciones = req.Opciones,
            MostrarEnFiltro = req.MostrarEnFiltro,
            Columna = Math.Clamp(req.Columna, 1, 2),
            Descripcion = req.Descripcion,
            Requerido = req.Requerido,
            ValorPorDefecto = req.ValorPorDefecto,
            PermiteVarios = req.PermiteVarios,
            CamposSuma = req.CamposSuma,
            Activo = true
        };
        _db.PqrsdCampos.Add(c);
        await _db.SaveChangesAsync(ct);
        return MapCampo(c);
    }

    public async Task<bool> ActualizarCampoAsync(Guid id, GuardarCampoPqrsdRequest req, CancellationToken ct)
    {
        var c = await _db.PqrsdCampos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("La etiqueta del campo es obligatoria.");
        var label = req.Label.Trim();
        if (await _db.PqrsdCampos.AnyAsync(x => x.Id != id && x.Activo && x.Label == label, ct))
            throw new InvalidOperationException("Ya existe un campo activo con esta etiqueta.");
        c.Label = label;
        c.Tipo = req.Tipo;
        c.Opciones = req.Opciones;
        c.MostrarEnFiltro = req.MostrarEnFiltro;
        c.Columna = Math.Clamp(req.Columna, 1, 2);
        c.Descripcion = req.Descripcion;
        c.Requerido = req.Requerido;
        c.ValorPorDefecto = req.ValorPorDefecto;
        c.PermiteVarios = req.PermiteVarios;
        c.CamposSuma = req.CamposSuma;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.PqrsdCampos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        await _db.PqrsdCampoValores.Where(v => v.PqrsdCampoId == id).ExecuteDeleteAsync(ct);
        _db.PqrsdCampos.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetCampoActivoAsync(Guid id, bool activo, CancellationToken ct)
    {
        var c = await _db.PqrsdCampos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (activo && await _db.PqrsdCampos.AnyAsync(x => x.Id != id && x.Activo && x.Label == c.Label, ct))
            throw new InvalidOperationException("Ya existe un campo activo con esta etiqueta. Renombra antes de restaurar.");
        c.Activo = activo;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReordenarCampoAsync(Guid id, string direccion, CancellationToken ct)
    {
        var lista = await _db.PqrsdCampos.Where(c => c.Activo).OrderBy(c => c.Orden).ThenBy(c => c.Label).ToListAsync(ct);
        var idx = lista.FindIndex(c => c.Id == id);
        if (idx < 0) return false;
        var dir = (direccion ?? "").ToLowerInvariant();
        var j = dir is "arriba" or "up" or "-1" ? idx - 1 : idx + 1;
        if (j < 0 || j >= lista.Count) return true;
        (lista[idx].Orden, lista[j].Orden) = (lista[j].Orden, lista[idx].Orden);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Expediente: archivar + actualizar =====================

    public async Task<bool> ArchivarExpedienteAsync(Guid id, bool archivar, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        x.Archivado = archivar;
        x.ArchivadoAt = archivar ? DateTimeOffset.UtcNow : null;
        x.ArchivadoPorUsuarioId = archivar ? GetUsuarioActualId() : null;
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = x.Estado,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = archivar ? "Expediente archivado" : "Expediente restaurado desde archivados"
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Reportar actividad: agrega un comentario libre al expediente (chat estilo Tareas).
    // Devuelve el comentario creado para que la UI lo agregue sin recargar, y notifica @menciones.
    public async Task<PqrsdComentarioDto?> ReportarActividadAsync(Guid id, ReportarActividadPqrsdRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Texto)) throw new InvalidOperationException("El texto de la actividad es obligatorio.");
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (exp is null) return null;
        var (uid, nombre) = ActorActual();
        var c = new PqrsdComentario
        {
            PqrsdExpedienteId = id,
            Texto = req.Texto.Trim(),
            AutorUsuarioId = uid,
            AutorNombre = nombre
        };
        _db.PqrsdComentarios.Add(c);
        await _db.SaveChangesAsync(ct);
        await NotificarMencionesAsync(exp, c.Texto, ct);
        return new PqrsdComentarioDto(c.Id, c.Texto, c.AutorNombre, c.CreatedAt, c.AutorUsuarioId);
    }

    // Notifica @menciones escritas en un comentario/caption (canal InApp). Port del feed de Tareas.
    public async Task NotificarMencionComentarioAsync(Guid expedienteId, string? texto, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is not null) await NotificarMencionesAsync(exp, texto, ct);
    }

    // Detecta tokens @nombre.apellido y avisa a las personas del directorio que coincidan.
    private async Task NotificarMencionesAsync(PqrsdExpediente exp, string? texto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return;
        var matches = System.Text.RegularExpressions.Regex.Matches(texto, @"@([A-Za-z0-9._-]{2,40})");
        if (matches.Count == 0) return;
        var tokens = matches.Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToHashSet();

        var personas = await _db.Personas.AsNoTracking()
            .Select(p => new { p.Id, Nombre = (p.Nombres + " " + p.Apellidos).Trim() })
            .ToListAsync(ct);
        var destinatarios = personas
            .Where(p => tokens.Contains(p.Nombre.ToLowerInvariant().Replace(" ", ".")))
            .Select(p => p.Id).Distinct().Take(20).ToList();
        if (destinatarios.Count == 0) return;

        var resumen = texto.Length > 120 ? texto[..120] + "..." : texto;
        var lote = destinatarios.Select(pid =>
            new Propia.Application.Notificaciones.EnviarNotificacionRequest(
                Canal: Domain.Enums.CanalNotificacion.InApp,
                Cuerpo: $"Te mencionaron en el PQR {exp.NumeroRadicado}: {resumen}",
                TenantId: tenantId,
                PersonaDestinatariaId: pid,
                Asunto: $"Mencion en PQR {exp.NumeroRadicado}",
                Prioridad: Domain.Enums.PrioridadNotificacion.Normal,
                ModuloOrigenCodigo: "2.9",
                EntidadOrigenId: exp.Id));
        await _noti.EnviarLoteAsync(lote, ct);
    }

    // Genera una tarea interna (modulo 2.10) a partir del PQR y la vincula (TareaId). Idempotente.
    public async Task<Guid?> GenerarTareaAsync(Guid id, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return null;
        if (x.TareaId is { } yaExiste) return yaExiste;

        var resumen = x.Descripcion.Length > 60 ? x.Descripcion[..60] + "..." : x.Descripcion;
        var tarea = await _tareas.CrearTareaAsync(new Propia.Application.Tareas.CrearTareaRequest(
            Titulo: $"PQR {x.NumeroRadicado}: {resumen}",
            Descripcion: x.Descripcion,
            Prioridad: PrioridadTarea.Alta,
            EstadoId: null,
            AsignadoPersonaId: x.AsignadoPersonaId,
            FechaInicio: null,
            FechaVencimiento: x.FechaVencimiento,
            PadreId: null,
            EtiquetaIds: null), ct);

        x.TareaId = tarea.Id;
        x.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return tarea.Id;
    }

    public async Task<bool> ActualizarExpedienteAsync(Guid id, ActualizarExpedienteRequest req, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;

        x.UnidadPrivadaId = req.UnidadPrivadaId;

        // Persona asignada (Guid.Empty = quitar; null = no tocar; otro = asignar validando)
        if (req.AsignadoPersonaId is { } aPid)
        {
            if (aPid == Guid.Empty) x.AsignadoPersonaId = null;
            else if (await _db.Personas.AsNoTracking().AnyAsync(p => p.Id == aPid, ct)) x.AsignadoPersonaId = aPid;
            else throw new InvalidOperationException("La persona asignada no existe.");
        }
        if (req.Progreso is { } prog) x.Progreso = Math.Clamp(prog, 0, 100);

        if (req.RadicadorPersonaId is { } radPid && radPid != x.RadicadorPersonaId)
        {
            if (!await _db.Personas.AsNoTracking().AnyAsync(p => p.Id == radPid, ct))
                throw new InvalidOperationException("La persona seleccionada no existe.");
            x.RadicadorPersonaId = radPid;
        }

        if (!string.IsNullOrWhiteSpace(req.Descripcion))
        {
            var desc = req.Descripcion.Trim();
            if (desc.Length > 2000) throw new InvalidOperationException("Descripcion maxima 2000 caracteres.");
            x.Descripcion = desc;
        }

        // Upsert de campos dinamicos.
        if (req.Campos is not null)
        {
            var existentes = await _db.PqrsdCampoValores.Where(v => v.ExpedienteId == id).ToListAsync(ct);
            var camposActivos = await _db.PqrsdCampos.AsNoTracking().Where(c => c.Activo).Select(c => c.Id).ToHashSetAsync(ct);
            foreach (var cv in req.Campos)
            {
                if (!camposActivos.Contains(cv.CampoId)) continue;
                var actual = existentes.FirstOrDefault(v => v.PqrsdCampoId == cv.CampoId);
                if (string.IsNullOrWhiteSpace(cv.Valor))
                {
                    if (actual is not null) _db.PqrsdCampoValores.Remove(actual);
                }
                else if (actual is null)
                {
                    _db.PqrsdCampoValores.Add(new PqrsdCampoValor { ExpedienteId = id, PqrsdCampoId = cv.CampoId, Valor = cv.Valor });
                }
                else
                {
                    actual.Valor = cv.Valor;
                    actual.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        x.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Tareas enlazadas al PQR (tablero configurable) =====================
    private const string TableroPqrsdNombre = "PQRSD";

    private async Task<Guid> AsegurarTableroPqrsdAsync(CancellationToken ct)
    {
        // Si el administrador configuro un tablero destino (y sigue existiendo), se respeta.
        var cfg = await _db.PqrsdTareasConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg?.TableroId is Guid elegido && await _db.Tableros.AnyAsync(t => t.Id == elegido, ct))
            return elegido;
        // Fallback: tablero "PQRSD" por defecto (se crea si no existe).
        var board = await _db.Tableros.FirstOrDefaultAsync(t => t.Nombre == TableroPqrsdNombre, ct);
        if (board is not null) return board.Id;
        var dto = await _tareas.CrearTableroAsync(
            new Propia.Application.Tareas.GuardarTableroRequest(TableroPqrsdNombre, "Tareas generadas desde PQRSD", "#7C5CFA", new List<Guid>()), ct);
        return dto.Id;
    }

    public async Task<Guid?> ObtenerTableroTareasConfigAsync(CancellationToken ct)
        => (await _db.PqrsdTareasConfigs.AsNoTracking().FirstOrDefaultAsync(ct))?.TableroId;

    public async Task GuardarTableroTareasConfigAsync(Guid? tableroId, CancellationToken ct)
    {
        if (tableroId is Guid tid && !await _db.Tableros.AnyAsync(t => t.Id == tid, ct))
            throw new InvalidOperationException("El tablero elegido no existe.");
        var cfg = await _db.PqrsdTareasConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null) { cfg = new PqrsdTareasConfig { TableroId = tableroId }; _db.PqrsdTareasConfigs.Add(cfg); }
        else cfg.TableroId = tableroId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Guid?> CrearTareaDePqrAsync(Guid pqrId, CrearPqrTareaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo)) return null;
        if (!await _db.PqrsdExpedientes.AnyAsync(x => x.Id == pqrId, ct)) return null;
        var boardId = await AsegurarTableroPqrsdAsync(ct);
        var det = await _tareas.CrearTareaAsync(new Propia.Application.Tareas.CrearTareaRequest(
            req.Titulo.Trim(), null, PrioridadTarea.Normal, null, req.AsignadoPersonaId,
            null, null, null, null, TableroId: boardId), ct);
        // Enlazar la tarea al PQR (Origen = modulo externo). CrearTareaRequest no lleva estos campos.
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == det.Id, ct);
        if (t is not null)
        {
            t.Origen = OrigenTarea.ModuloExterno;
            t.ModuloOrigenCodigo = TableroPqrsdNombre;
            t.ModuloOrigenEntidadId = pqrId;
            await _db.SaveChangesAsync(ct);
        }
        return det.Id;
    }

    public async Task<PqrTareasDto> ListTareasDePqrAsync(Guid pqrId, CancellationToken ct)
    {
        var boardId = await AsegurarTableroPqrsdAsync(ct);
        var etapas = await _db.TareasEstados.AsNoTracking()
            .Where(e => e.TableroId == boardId).OrderBy(e => e.Orden)
            .Select(e => new PqrEtapaDto(e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal))
            .ToListAsync(ct);
        var tareas = await _db.Tareas.AsNoTracking()
            .Where(t => t.ModuloOrigenCodigo == TableroPqrsdNombre && t.ModuloOrigenEntidadId == pqrId && !t.Eliminada)
            .Include(t => t.Estado).Include(t => t.AsignadoPersona)
            .OrderBy(t => t.NumeroTarea)
            .Select(t => new PqrTareaDto(t.Id, t.NumeroTarea, t.Titulo, t.EstadoId,
                t.Estado!.Nombre, t.Estado.Color, t.Estado.EsTerminal,
                t.AsignadoPersona != null ? (t.AsignadoPersona.Nombres + " " + t.AsignadoPersona.Apellidos).Trim() : null,
                t.Prioridad.ToString(), t.FechaVencimiento, t.Progreso))
            .ToListAsync(ct);
        var pct = tareas.Count == 0 ? 0 : (int)Math.Round(100.0 * tareas.Count(x => x.EstadoEsTerminal) / tareas.Count);
        return new PqrTareasDto(etapas, tareas, pct);
    }
}
