using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.MiCopropiedad;

// Campos dinamicos tipados (catalogo + valor) de Mantenimiento: Programacion (cronograma) e Intervencion.
// Espejo exacto de los campos de Equipos/Zonas (ver MiCopropiedadService.ZonasYEquipos.cs). Reusa
// NormalizarOpcionesCampo + los 3 helpers de formula (PrepararOpcionesFormulaPropios / ComputarFormulasPropiasFlat
// / ComputarFormulaPropiaUno, private static de la misma clase parcial, en CamposVinculados.cs) y los requests
// genericos. Soporta campos Funcion sobre fuentes PROPIAS (cd:) y de SISTEMA numericas/fecha.
public partial class MiCopropiedadService
{
    // ---- Fuentes de SISTEMA para las Formulas de Mantenimiento (estilo ValorSistemaUnidad/Equipo) ----
    // El picker (ConfigCamposEntidad) ofrece las claves del catalogo con Tipo Numero/Moneda/Fecha; aqui va el
    // resolver clave->Tipo (validar) y clave->valor de la fila (computar). Fechas -> DayNumber (como ParseFuente).
    private static readonly IReadOnlyDictionary<string, TipoCampoTablero> FuentesSistemaProgramacion =
        new Dictionary<string, TipoCampoTablero>(StringComparer.Ordinal)
        {
            ["costo"] = TipoCampoTablero.Moneda, ["proxima"] = TipoCampoTablero.Fecha,
        };

    private static readonly IReadOnlyDictionary<string, TipoCampoTablero> FuentesSistemaIntervencion =
        new Dictionary<string, TipoCampoTablero>(StringComparer.Ordinal)
        {
            ["programada"] = TipoCampoTablero.Fecha, ["cierre"] = TipoCampoTablero.Fecha,
        };

    // Opciones polimorfica: Seleccion (lineas) o Formula (JSON validado contra campos propios cd: + sistema).
    private async Task<string?> OpcionesProgramacionAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo != TipoCampoTablero.Formula) return NormalizarOpcionesCampo(tipo, opciones);
        var defs = (await _db.ProgramacionCamposDefiniciones.AsNoTracking().Where(d => excluirId == null || d.Id != excluirId)
            .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct)).ToDictionary(d => d.Id, d => d.Tipo);
        return PrepararOpcionesFormulaPropios(tipo, opciones, defs, FuentesSistemaProgramacion);
    }

    private async Task<string?> OpcionesIntervencionAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo != TipoCampoTablero.Formula) return NormalizarOpcionesCampo(tipo, opciones);
        var defs = (await _db.IntervencionCamposDefiniciones.AsNoTracking().Where(d => excluirId == null || d.Id != excluirId)
            .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct)).ToDictionary(d => d.Id, d => d.Tipo);
        return PrepararOpcionesFormulaPropios(tipo, opciones, defs, FuentesSistemaIntervencion);
    }

    // ===================== PROGRAMACION =====================
    public async Task<IReadOnlyList<ProgramacionCampoDefinicionDto>> ListCamposDefProgramacionAsync(CancellationToken ct)
        => await _db.ProgramacionCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new ProgramacionCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones, d.Descripcion)).ToListAsync(ct);

    public async Task<ProgramacionCampoDefinicionDto> CrearCampoDefProgramacionAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.ProgramacionCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new ProgramacionCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones, existente.Descripcion);
        var maxOrden = await _db.ProgramacionCamposDefiniciones.AnyAsync(ct) ? await _db.ProgramacionCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new ProgramacionCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = await OpcionesProgramacionAsync(req.Tipo, req.Opciones, null, ct), Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim() };
        _db.ProgramacionCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new ProgramacionCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones, def.Descripcion);
    }

    public async Task<bool> ActualizarCampoDefProgramacionAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.ProgramacionCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = await OpcionesProgramacionAsync(req.Tipo, req.Opciones, def.Id, ct); def.Orden = req.Orden; def.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefProgramacionAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.ProgramacionCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.ProgramacionCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SetCampoValorProgramacionDefAsync(Guid programacionId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.ProgramacionCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.ProgramacionTareaId == programacionId, ct);
        if (existente is null)
            _db.ProgramacionCamposValores.Add(new ProgramacionCampoValor { TenantId = tid, DefinicionId = definicionId, ProgramacionTareaId = programacionId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProgramacionCampoValorFlatDto>> ListTodosCamposValoresProgramacionAsync(CancellationToken ct)
    {
        var valores = await _db.ProgramacionCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.ProgramacionTareaId, v.DefinicionId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new ProgramacionCampoValorFlatDto(v.ProgramacionTareaId, v.DefinicionId, v.Valor)).ToList();
        var defs = await _db.ProgramacionCamposDefiniciones.AsNoTracking().Select(d => new { d.Id, d.Tipo, d.Opciones }).ToListAsync(ct);
        if (defs.Any(d => d.Tipo == TipoCampoTablero.Formula))
        {
            var progs = await _db.ProgramacionTareas.AsNoTracking().Select(p => new { p.Id, p.CostoEstimado, p.FechaProximaEjecucion }).ToListAsync(ct);
            var ids = progs.Select(p => p.Id).ToList();
            var sistema = progs.ToDictionary(p => p.Id, p => (IReadOnlyDictionary<string, decimal?>)new Dictionary<string, decimal?>(StringComparer.Ordinal)
            {
                ["costo"] = p.CostoEstimado, ["proxima"] = p.FechaProximaEjecucion.DayNumber,
            });
            var calc = ComputarFormulasPropiasFlat(
                defs.Select(d => (d.Id, d.Tipo, d.Opciones)).ToList(),
                valores.Select(v => (v.ProgramacionTareaId, v.DefinicionId, v.Valor)).ToList(), ids, sistema);
            res.AddRange(calc.Select(c => new ProgramacionCampoValorFlatDto(c.RegistroId, c.DefId, c.Valor)));
        }
        return res;
    }

    public async Task<IReadOnlyList<ProgramacionCampoDinDto>> ListCamposDinProgramacionAsync(Guid programacionId, CancellationToken ct)
    {
        var defs = await _db.ProgramacionCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var vals = await _db.ProgramacionCamposValores.AsNoTracking().Where(v => v.ProgramacionTareaId == programacionId).ToListAsync(ct);
        var dict = (IReadOnlyDictionary<Guid, string?>)vals.GroupBy(v => v.DefinicionId).ToDictionary(g => g.Key, g => g.First().Valor);
        IReadOnlyDictionary<string, decimal?>? sistema = null;
        if (defs.Any(d => d.Tipo == TipoCampoTablero.Formula))
        {
            var p = await _db.ProgramacionTareas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == programacionId, ct);
            if (p is not null) sistema = new Dictionary<string, decimal?>(StringComparer.Ordinal)
            {
                ["costo"] = p.CostoEstimado, ["proxima"] = p.FechaProximaEjecucion.DayNumber,
            };
        }
        return defs.Select(d => new ProgramacionCampoDinDto(d.Id, d.Label, d.Orden,
            d.Tipo == TipoCampoTablero.Formula ? ComputarFormulaPropiaUno(d.Opciones, dict, sistema) : vals.FirstOrDefault(v => v.DefinicionId == d.Id)?.Valor,
            d.Tipo, d.Opciones)).ToList();
    }

    // ===================== INTERVENCION =====================
    public async Task<IReadOnlyList<IntervencionCampoDefinicionDto>> ListCamposDefIntervencionAsync(CancellationToken ct)
        => await _db.IntervencionCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new IntervencionCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones, d.Descripcion)).ToListAsync(ct);

    public async Task<IntervencionCampoDefinicionDto> CrearCampoDefIntervencionAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.IntervencionCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new IntervencionCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones, existente.Descripcion);
        var maxOrden = await _db.IntervencionCamposDefiniciones.AnyAsync(ct) ? await _db.IntervencionCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new IntervencionCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = await OpcionesIntervencionAsync(req.Tipo, req.Opciones, null, ct), Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim() };
        _db.IntervencionCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new IntervencionCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones, def.Descripcion);
    }

    public async Task<bool> ActualizarCampoDefIntervencionAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.IntervencionCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = await OpcionesIntervencionAsync(req.Tipo, req.Opciones, def.Id, ct); def.Orden = req.Orden; def.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefIntervencionAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.IntervencionCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.IntervencionCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SetCampoValorIntervencionDefAsync(Guid intervencionId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.IntervencionCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.MantenimientoIntervencionId == intervencionId, ct);
        if (existente is null)
            _db.IntervencionCamposValores.Add(new IntervencionCampoValor { TenantId = tid, DefinicionId = definicionId, MantenimientoIntervencionId = intervencionId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<IntervencionCampoValorFlatDto>> ListTodosCamposValoresIntervencionAsync(CancellationToken ct)
    {
        var valores = await _db.IntervencionCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.MantenimientoIntervencionId, v.DefinicionId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new IntervencionCampoValorFlatDto(v.MantenimientoIntervencionId, v.DefinicionId, v.Valor)).ToList();
        var defs = await _db.IntervencionCamposDefiniciones.AsNoTracking().Select(d => new { d.Id, d.Tipo, d.Opciones }).ToListAsync(ct);
        if (defs.Any(d => d.Tipo == TipoCampoTablero.Formula))
        {
            var ivs = await _db.MantenimientoIntervenciones.AsNoTracking().Select(i => new { i.Id, i.FechaProgramada, i.FechaCierre }).ToListAsync(ct);
            var ids = ivs.Select(i => i.Id).ToList();
            var sistema = ivs.ToDictionary(i => i.Id, i => (IReadOnlyDictionary<string, decimal?>)new Dictionary<string, decimal?>(StringComparer.Ordinal)
            {
                ["programada"] = i.FechaProgramada?.DayNumber, ["cierre"] = i.FechaCierre?.DayNumber,
            });
            var calc = ComputarFormulasPropiasFlat(
                defs.Select(d => (d.Id, d.Tipo, d.Opciones)).ToList(),
                valores.Select(v => (v.MantenimientoIntervencionId, v.DefinicionId, v.Valor)).ToList(), ids, sistema);
            res.AddRange(calc.Select(c => new IntervencionCampoValorFlatDto(c.RegistroId, c.DefId, c.Valor)));
        }
        return res;
    }

    public async Task<IReadOnlyList<IntervencionCampoDinDto>> ListCamposDinIntervencionAsync(Guid intervencionId, CancellationToken ct)
    {
        var defs = await _db.IntervencionCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var vals = await _db.IntervencionCamposValores.AsNoTracking().Where(v => v.MantenimientoIntervencionId == intervencionId).ToListAsync(ct);
        var dict = (IReadOnlyDictionary<Guid, string?>)vals.GroupBy(v => v.DefinicionId).ToDictionary(g => g.Key, g => g.First().Valor);
        IReadOnlyDictionary<string, decimal?>? sistema = null;
        if (defs.Any(d => d.Tipo == TipoCampoTablero.Formula))
        {
            var i = await _db.MantenimientoIntervenciones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == intervencionId, ct);
            if (i is not null) sistema = new Dictionary<string, decimal?>(StringComparer.Ordinal)
            {
                ["programada"] = i.FechaProgramada?.DayNumber, ["cierre"] = i.FechaCierre?.DayNumber,
            };
        }
        return defs.Select(d => new IntervencionCampoDinDto(d.Id, d.Label, d.Orden,
            d.Tipo == TipoCampoTablero.Formula ? ComputarFormulaPropiaUno(d.Opciones, dict, sistema) : vals.FirstOrDefault(v => v.DefinicionId == d.Id)?.Valor,
            d.Tipo, d.Opciones)).ToList();
    }
}
