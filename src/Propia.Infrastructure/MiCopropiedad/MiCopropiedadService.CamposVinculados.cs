using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.MiCopropiedad;

// Campos dinamicos tipados (catalogo por copropiedad + valor por registro) de las entidades
// vinculadas a una unidad: personas, vehiculos (placas), mascotas y terceros (empleadas).
// Calcado de la seccion de equipos/zonas en MiCopropiedadService.ZonasYEquipos.cs, que aporta
// NormalizarOpcionesCampo (misma clase parcial, no se duplica).
public partial class MiCopropiedadService
{
    // ===================== Campos dinamicos tipados (catalogo) - PERSONAS de unidad =====================
    public async Task<IReadOnlyList<PersonaCampoDefinicionDto>> ListCamposDefPersonaAsync(CancellationToken ct)
        => await _db.PersonaCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new PersonaCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones)).ToListAsync(ct);

    public async Task<PersonaCampoDefinicionDto> CrearCampoDefPersonaAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.PersonaCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new PersonaCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones);
        var maxOrden = await _db.PersonaCamposDefiniciones.AnyAsync(ct) ? await _db.PersonaCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new PersonaCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones) };
        _db.PersonaCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new PersonaCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones);
    }

    public async Task<bool> ActualizarCampoDefPersonaAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.PersonaCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones); def.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefPersonaAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.PersonaCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.PersonaCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PersonaCampoValorFlatDto>> ListTodosCamposValoresPersonaAsync(CancellationToken ct)
        => await _db.PersonaCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new PersonaCampoValorFlatDto(v.UnidadPersonaId, v.DefinicionId, v.Valor)).ToListAsync(ct);

    public async Task SetCampoValorPersonaDefAsync(Guid unidadPersonaId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.PersonaCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.UnidadPersonaId == unidadPersonaId, ct);
        if (existente is null)
            _db.PersonaCamposValores.Add(new PersonaCampoValor { TenantId = tid, DefinicionId = definicionId, UnidadPersonaId = unidadPersonaId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Campos dinamicos tipados (catalogo) - VEHICULOS de unidad =====================
    public async Task<IReadOnlyList<VehiculoCampoDefinicionDto>> ListCamposDefVehiculoAsync(CancellationToken ct)
        => await _db.VehiculoCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new VehiculoCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones)).ToListAsync(ct);

    public async Task<VehiculoCampoDefinicionDto> CrearCampoDefVehiculoAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.VehiculoCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new VehiculoCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones);
        var maxOrden = await _db.VehiculoCamposDefiniciones.AnyAsync(ct) ? await _db.VehiculoCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new VehiculoCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones) };
        _db.VehiculoCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new VehiculoCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones);
    }

    public async Task<bool> ActualizarCampoDefVehiculoAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.VehiculoCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones); def.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefVehiculoAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.VehiculoCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.VehiculoCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<VehiculoCampoValorFlatDto>> ListTodosCamposValoresVehiculoAsync(CancellationToken ct)
        => await _db.VehiculoCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new VehiculoCampoValorFlatDto(v.UnidadPlacaId, v.DefinicionId, v.Valor)).ToListAsync(ct);

    public async Task SetCampoValorVehiculoDefAsync(Guid unidadPlacaId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.VehiculoCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.UnidadPlacaId == unidadPlacaId, ct);
        if (existente is null)
            _db.VehiculoCamposValores.Add(new VehiculoCampoValor { TenantId = tid, DefinicionId = definicionId, UnidadPlacaId = unidadPlacaId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Campos dinamicos tipados (catalogo) - MASCOTAS de unidad =====================
    public async Task<IReadOnlyList<MascotaCampoDefinicionDto>> ListCamposDefMascotaAsync(CancellationToken ct)
        => await _db.MascotaCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new MascotaCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones)).ToListAsync(ct);

    public async Task<MascotaCampoDefinicionDto> CrearCampoDefMascotaAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.MascotaCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new MascotaCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones);
        var maxOrden = await _db.MascotaCamposDefiniciones.AnyAsync(ct) ? await _db.MascotaCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new MascotaCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones) };
        _db.MascotaCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new MascotaCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones);
    }

    public async Task<bool> ActualizarCampoDefMascotaAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.MascotaCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones); def.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefMascotaAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.MascotaCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.MascotaCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<MascotaCampoValorFlatDto>> ListTodosCamposValoresMascotaAsync(CancellationToken ct)
        => await _db.MascotaCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new MascotaCampoValorFlatDto(v.UnidadMascotaId, v.DefinicionId, v.Valor)).ToListAsync(ct);

    public async Task SetCampoValorMascotaDefAsync(Guid unidadMascotaId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.MascotaCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.UnidadMascotaId == unidadMascotaId, ct);
        if (existente is null)
            _db.MascotaCamposValores.Add(new MascotaCampoValor { TenantId = tid, DefinicionId = definicionId, UnidadMascotaId = unidadMascotaId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Campos dinamicos tipados (catalogo) - TERCEROS de unidad =====================
    public async Task<IReadOnlyList<TerceroCampoDefinicionDto>> ListCamposDefTerceroAsync(CancellationToken ct)
        => await _db.TerceroCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new TerceroCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones)).ToListAsync(ct);

    public async Task<TerceroCampoDefinicionDto> CrearCampoDefTerceroAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.TerceroCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new TerceroCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones);
        var maxOrden = await _db.TerceroCamposDefiniciones.AnyAsync(ct) ? await _db.TerceroCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new TerceroCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones) };
        _db.TerceroCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new TerceroCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones);
    }

    public async Task<bool> ActualizarCampoDefTerceroAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.TerceroCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = NormalizarOpcionesCampo(req.Tipo, req.Opciones); def.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefTerceroAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.TerceroCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.TerceroCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<TerceroCampoValorFlatDto>> ListTodosCamposValoresTerceroAsync(CancellationToken ct)
        => await _db.TerceroCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new TerceroCampoValorFlatDto(v.UnidadEmpleadaId, v.DefinicionId, v.Valor)).ToListAsync(ct);

    public async Task SetCampoValorTerceroDefAsync(Guid unidadEmpleadaId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.TerceroCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.UnidadEmpleadaId == unidadEmpleadaId, ct);
        if (existente is null)
            _db.TerceroCamposValores.Add(new TerceroCampoValor { TenantId = tid, DefinicionId = definicionId, UnidadEmpleadaId = unidadEmpleadaId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }
}
