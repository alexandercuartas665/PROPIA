using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.MiCopropiedad;

// Campos dinamicos tipados (catalogo + valor) de Mantenimiento: Programacion (cronograma) e Intervencion.
// Espejo exacto de los campos de Equipos/Zonas (ver MiCopropiedadService.ZonasYEquipos.cs). Reusa
// NormalizarOpcionesCampo (private static de la misma clase parcial) y los requests genericos.
public partial class MiCopropiedadService
{
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
        var def = new ProgramacionCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones), Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim() };
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
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones); def.Orden = req.Orden; def.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
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
        => await _db.ProgramacionCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new ProgramacionCampoValorFlatDto(v.ProgramacionTareaId, v.DefinicionId, v.Valor)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProgramacionCampoDinDto>> ListCamposDinProgramacionAsync(Guid programacionId, CancellationToken ct)
    {
        var defs = await _db.ProgramacionCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var vals = await _db.ProgramacionCamposValores.AsNoTracking().Where(v => v.ProgramacionTareaId == programacionId).ToListAsync(ct);
        return defs.Select(d => new ProgramacionCampoDinDto(d.Id, d.Label, d.Orden, vals.FirstOrDefault(v => v.DefinicionId == d.Id)?.Valor, d.Tipo, d.Opciones)).ToList();
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
        var def = new IntervencionCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones), Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim() };
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
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones); def.Orden = req.Orden; def.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
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
        => await _db.IntervencionCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new IntervencionCampoValorFlatDto(v.MantenimientoIntervencionId, v.DefinicionId, v.Valor)).ToListAsync(ct);

    public async Task<IReadOnlyList<IntervencionCampoDinDto>> ListCamposDinIntervencionAsync(Guid intervencionId, CancellationToken ct)
    {
        var defs = await _db.IntervencionCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var vals = await _db.IntervencionCamposValores.AsNoTracking().Where(v => v.MantenimientoIntervencionId == intervencionId).ToListAsync(ct);
        return defs.Select(d => new IntervencionCampoDinDto(d.Id, d.Label, d.Orden, vals.FirstOrDefault(v => v.DefinicionId == d.Id)?.Valor, d.Tipo, d.Opciones)).ToList();
    }
}
