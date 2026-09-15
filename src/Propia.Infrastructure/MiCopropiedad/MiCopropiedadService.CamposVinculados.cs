using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;

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
        var def = new PersonaCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = await OpcionesPersonaAsync(req.Tipo, req.Opciones, null, ct) };
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
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = await OpcionesPersonaAsync(req.Tipo, req.Opciones, def.Id, ct); def.Orden = req.Orden;
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
    {
        var valores = await _db.PersonaCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.UnidadPersonaId, v.DefinicionId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new PersonaCampoValorFlatDto(v.UnidadPersonaId, v.DefinicionId, v.Valor)).ToList();
        var defs = await _db.PersonaCamposDefiniciones.AsNoTracking().Select(d => new { d.Id, d.Tipo, d.Opciones }).ToListAsync(ct);
        if (defs.Any(d => d.Tipo == TipoCampoTablero.Formula))
        {
            var ids = await _db.UnidadPersonas.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
            var calc = ComputarFormulasPropiasFlat(
                defs.Select(d => (d.Id, d.Tipo, d.Opciones)).ToList(),
                valores.Select(v => (v.UnidadPersonaId, v.DefinicionId, v.Valor)).ToList(), ids);
            res.AddRange(calc.Select(c => new PersonaCampoValorFlatDto(c.RegistroId, c.DefId, c.Valor)));
        }
        return res;
    }

    public async Task SetCampoValorPersonaDefAsync(Guid unidadPersonaId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var defP = await _db.PersonaCamposDefiniciones.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (defP is not null) await CamposAvanzados.ValidarValorAsync(_db, defP.Tipo, valor, ct);   // Fase 2
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
        var def = new VehiculoCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = await OpcionesVehiculoAsync(req.Tipo, req.Opciones, null, ct) };
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
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = await OpcionesVehiculoAsync(req.Tipo, req.Opciones, def.Id, ct); def.Orden = req.Orden;
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
    {
        var valores = await _db.VehiculoCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.UnidadPlacaId, v.DefinicionId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new VehiculoCampoValorFlatDto(v.UnidadPlacaId, v.DefinicionId, v.Valor)).ToList();
        var defs = await _db.VehiculoCamposDefiniciones.AsNoTracking().Select(d => new { d.Id, d.Tipo, d.Opciones }).ToListAsync(ct);
        if (defs.Any(d => d.Tipo == TipoCampoTablero.Formula))
        {
            var ids = await _db.UnidadPlacas.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
            var calc = ComputarFormulasPropiasFlat(
                defs.Select(d => (d.Id, d.Tipo, d.Opciones)).ToList(),
                valores.Select(v => (v.UnidadPlacaId, v.DefinicionId, v.Valor)).ToList(), ids);
            res.AddRange(calc.Select(c => new VehiculoCampoValorFlatDto(c.RegistroId, c.DefId, c.Valor)));
        }
        return res;
    }

    public async Task SetCampoValorVehiculoDefAsync(Guid unidadPlacaId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var defV = await _db.VehiculoCamposDefiniciones.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (defV is not null) await CamposAvanzados.ValidarValorAsync(_db, defV.Tipo, valor, ct);   // Fase 2: Formula solo lectura / Usuario/Directorio cross-tenant
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
        var def = new MascotaCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = await OpcionesMascotaAsync(req.Tipo, req.Opciones, null, ct) };
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
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = await OpcionesMascotaAsync(req.Tipo, req.Opciones, def.Id, ct); def.Orden = req.Orden;
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
    {
        var valores = await _db.MascotaCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.UnidadMascotaId, v.DefinicionId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new MascotaCampoValorFlatDto(v.UnidadMascotaId, v.DefinicionId, v.Valor)).ToList();
        var defs = await _db.MascotaCamposDefiniciones.AsNoTracking().Select(d => new { d.Id, d.Tipo, d.Opciones }).ToListAsync(ct);
        if (defs.Any(d => d.Tipo == TipoCampoTablero.Formula))
        {
            var ids = await _db.UnidadMascotas.AsNoTracking().Select(m => m.Id).ToListAsync(ct);
            var calc = ComputarFormulasPropiasFlat(
                defs.Select(d => (d.Id, d.Tipo, d.Opciones)).ToList(),
                valores.Select(v => (v.UnidadMascotaId, v.DefinicionId, v.Valor)).ToList(), ids);
            res.AddRange(calc.Select(c => new MascotaCampoValorFlatDto(c.RegistroId, c.DefId, c.Valor)));
        }
        return res;
    }

    public async Task SetCampoValorMascotaDefAsync(Guid unidadMascotaId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var defM = await _db.MascotaCamposDefiniciones.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (defM is not null) await CamposAvanzados.ValidarValorAsync(_db, defM.Tipo, valor, ct);   // Fase 2
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

    // ===================== Fase 2 (Formula) para entidades vinculadas =====================
    // Estas entidades NO tienen campos de sistema Numero/Moneda, asi que una Formula suma SOLO campos
    // PROPIOS (cd:{guid}). El computo/formato usa el helper compartido CampoFormulaConfig.ComputarTexto;
    // la validacion de valor (Usuario/Directorio/Formula) usa CamposAvanzados.ValidarValorAsync.

    // Opciones polimorfica: Seleccion (lineas) o Formula (JSON validado contra los campos propios). El
    // resolver de Tipo solo conoce campos propios (cd:{guid}) -> una fuente de sistema o no-numerica se rechaza.
    private static string? PrepararOpcionesFormulaPropios(TipoCampoTablero tipo, string? opciones,
        IReadOnlyDictionary<Guid, TipoCampoTablero> defsPropios)
    {
        if (tipo != TipoCampoTablero.Formula) return NormalizarOpcionesCampo(tipo, opciones);
        var cfg = CampoFormulaConfig.Parse(opciones)
            ?? throw new InvalidOperationException("La formula necesita una operacion y al menos un campo fuente.");
        var err = CampoFormulaConfig.Validar(cfg, clave =>
            clave.StartsWith("cd:", StringComparison.Ordinal) && Guid.TryParse(clave[3..], out var g) && defsPropios.TryGetValue(g, out var t)
                ? t : (TipoCampoTablero?)null);
        if (err is not null) throw new InvalidOperationException(err);
        return cfg.Serializar();
    }

    // Filas flat computadas de los campos Formula, para TODOS los registros (asi Conteo=0/agregado vacio
    // se ven bien). valores = {registro -> {defId -> valor}} de los campos propios. Fuentes: solo cd:{guid}.
    private static List<(Guid RegistroId, Guid DefId, string Valor)> ComputarFormulasPropiasFlat(
        List<(Guid Id, TipoCampoTablero Tipo, string? Opciones)> defs,
        List<(Guid RegistroId, Guid DefId, string? Valor)> valores,
        IReadOnlyList<Guid> registroIds)
    {
        var res = new List<(Guid, Guid, string)>();
        var formulaDefs = defs.Where(d => d.Tipo == TipoCampoTablero.Formula).ToList();
        if (formulaDefs.Count == 0) return res;
        var porReg = valores.GroupBy(v => v.RegistroId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, string?>)g.ToDictionary(x => x.DefId, x => x.Valor));
        IReadOnlyDictionary<Guid, string?> vacio = new Dictionary<Guid, string?>();
        foreach (var rid in registroIds)
        {
            var dict = porReg.TryGetValue(rid, out var d0) ? d0 : vacio;
            foreach (var fd in formulaDefs)
            {
                var txt = CampoFormulaConfig.ComputarTexto(fd.Opciones, clave =>
                    clave.StartsWith("cd:", StringComparison.Ordinal) && Guid.TryParse(clave[3..], out var g) && dict.TryGetValue(g, out var v)
                        ? ParseDecimalInvVinc(v) : (decimal?)null);
                if (txt is not null) res.Add((rid, fd.Id, txt));
            }
        }
        return res;
    }

    private static decimal? ParseDecimalInvVinc(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().Replace(",", ".");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
    }

    private async Task<string?> OpcionesPersonaAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo != TipoCampoTablero.Formula) return NormalizarOpcionesCampo(tipo, opciones);
        var defs = (await _db.PersonaCamposDefiniciones.AsNoTracking().Where(d => excluirId == null || d.Id != excluirId)
            .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct)).ToDictionary(d => d.Id, d => d.Tipo);
        return PrepararOpcionesFormulaPropios(tipo, opciones, defs);
    }

    private async Task<string?> OpcionesVehiculoAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo != TipoCampoTablero.Formula) return NormalizarOpcionesCampo(tipo, opciones);
        var defs = (await _db.VehiculoCamposDefiniciones.AsNoTracking().Where(d => excluirId == null || d.Id != excluirId)
            .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct)).ToDictionary(d => d.Id, d => d.Tipo);
        return PrepararOpcionesFormulaPropios(tipo, opciones, defs);
    }

    private async Task<string?> OpcionesMascotaAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo != TipoCampoTablero.Formula) return NormalizarOpcionesCampo(tipo, opciones);
        var defs = (await _db.MascotaCamposDefiniciones.AsNoTracking().Where(d => excluirId == null || d.Id != excluirId)
            .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct)).ToDictionary(d => d.Id, d => d.Tipo);
        return PrepararOpcionesFormulaPropios(tipo, opciones, defs);
    }
}
