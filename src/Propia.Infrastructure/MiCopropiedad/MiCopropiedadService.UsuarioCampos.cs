using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

// Campos dinamicos propios de los USUARIOS del tenant (catalogo + valor EAV). Calcado del patron
// de Zonas/Equipos; el registro del valor es el UsuarioTenant.Id (membresia en la copropiedad).
public partial class MiCopropiedadService
{
    public async Task<IReadOnlyList<UsuarioCampoDefinicionDto>> ListCamposDefUsuarioAsync(CancellationToken ct)
        => await _db.UsuarioCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new UsuarioCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones, d.Descripcion)).ToListAsync(ct);

    // Opciones polimorfica: Seleccion (lineas) o Formula (JSON validado contra los campos propios cd:). Espejo
    // de OpcionesVehiculoAsync (sin fuentes de sistema).
    private async Task<string?> OpcionesUsuarioAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo != TipoCampoTablero.Formula) return NormalizarOpcionesCampo(tipo, opciones);
        var defs = (await _db.UsuarioCamposDefiniciones.AsNoTracking().Where(d => excluirId == null || d.Id != excluirId)
            .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct)).ToDictionary(d => d.Id, d => d.Tipo);
        return PrepararOpcionesFormulaPropios(tipo, opciones, defs);
    }

    public async Task<UsuarioCampoDefinicionDto> CrearCampoDefUsuarioAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.UsuarioCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new UsuarioCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones, existente.Descripcion);
        var maxOrden = await _db.UsuarioCamposDefiniciones.AnyAsync(ct) ? await _db.UsuarioCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new UsuarioCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = await OpcionesUsuarioAsync(req.Tipo, req.Opciones, null, ct), Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim() };
        _db.UsuarioCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new UsuarioCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones, def.Descripcion);
    }

    public async Task<bool> ActualizarCampoDefUsuarioAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.UsuarioCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = await OpcionesUsuarioAsync(req.Tipo, req.Opciones, def.Id, ct); def.Orden = req.Orden; def.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefUsuarioAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.UsuarioCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.UsuarioCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SetCampoValorUsuarioDefAsync(Guid usuarioTenantId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.UsuarioCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.UsuarioTenantId == usuarioTenantId, ct);
        if (existente is null)
            _db.UsuarioCamposValores.Add(new UsuarioCampoValor { TenantId = tid, DefinicionId = definicionId, UsuarioTenantId = usuarioTenantId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UsuarioCampoValorFlatDto>> ListTodosCamposValoresUsuarioAsync(CancellationToken ct)
    {
        var valores = await _db.UsuarioCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.UsuarioTenantId, v.DefinicionId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new UsuarioCampoValorFlatDto(v.UsuarioTenantId, v.DefinicionId, v.Valor)).ToList();
        var defs = await _db.UsuarioCamposDefiniciones.AsNoTracking().Select(d => new { d.Id, d.Tipo, d.Opciones }).ToListAsync(ct);
        if (defs.Any(d => d.Tipo == TipoCampoTablero.Formula))
        {
            var ids = await _db.UsuariosTenant.AsNoTracking().Select(u => u.Id).ToListAsync(ct);
            var calc = ComputarFormulasPropiasFlat(
                defs.Select(d => (d.Id, d.Tipo, d.Opciones)).ToList(),
                valores.Select(v => (v.UsuarioTenantId, v.DefinicionId, v.Valor)).ToList(), ids);
            res.AddRange(calc.Select(c => new UsuarioCampoValorFlatDto(c.RegistroId, c.DefId, c.Valor)));
        }
        return res;
    }
}
