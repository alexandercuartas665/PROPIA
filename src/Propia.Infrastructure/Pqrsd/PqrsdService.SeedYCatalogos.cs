using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Pqrsd;

// Particion de PqrsdService por area (clase parcial: comparte _db/_tenantContext/_http/_noti/_tareas/_membrete
// y los helpers transversales del archivo principal). Mismo comportamiento.
public partial class PqrsdService
{
    // Seed lazy de estados/categorias, calculo de plazo en dias habiles, categorias, plazos y tipos configurables.
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

}
