using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

// Particion de MiCopropiedadService extraida por area tematica (mismo comportamiento,
// clase parcial: comparte _db/_tenant/_blob/_seed/_dir del constructor principal).
public partial class MiCopropiedadService
{
    // Prototipo v3: placas, arriendos, mascotas, empleadas, historico de titularidad y campos dinamicos por persona.
    // ===================== Prototipo v3: bloques nuevos de la ficha de inmueble =====================

    // -------- Placas habilitadas para ingreso --------
    public async Task<IReadOnlyList<UnidadPlacaDto>> ListPlacasUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadPlacas.AsNoTracking()
            .Where(p => p.UnidadId == unidadId)
            .OrderBy(p => p.Placa)
            .Select(p => new UnidadPlacaDto(p.Id, p.Placa, p.TipoVehiculo))
            .ToListAsync(ct);

    public async Task<UnidadPlacaDto?> AgregarPlacaUnidadAsync(Guid unidadId, CrearUnidadPlacaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var placa = (req.Placa ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(placa)) throw new InvalidOperationException("La placa es obligatoria.");
        if (placa.Length > 15) placa = placa[..15];
        var e = new UnidadPlaca { TenantId = tid, UnidadId = unidadId, Placa = placa, TipoVehiculo = req.TipoVehiculo };
        _db.UnidadPlacas.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Placa '{placa}' habilitada en la unidad.", ct, unidadId);
        return new UnidadPlacaDto(e.Id, e.Placa, e.TipoVehiculo);
    }

    public async Task<bool> EliminarPlacaUnidadAsync(Guid placaId, CancellationToken ct)
    {
        var e = await _db.UnidadPlacas.FirstOrDefaultAsync(x => x.Id == placaId, ct);
        if (e is null) return false;
        _db.UnidadPlacas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Arriendos y cobros mensuales --------
    public async Task<IReadOnlyList<UnidadArriendoDto>> ListArriendosUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadArriendos.AsNoTracking()
            .Where(a => a.UnidadId == unidadId)
            .OrderBy(a => a.Concepto)
            .Select(a => new UnidadArriendoDto(a.Id, a.Concepto, a.ValorMensual, a.Referencia))
            .ToListAsync(ct);

    public async Task<UnidadArriendoDto?> AgregarArriendoUnidadAsync(Guid unidadId, CrearUnidadArriendoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var concepto = (req.Concepto ?? "").Trim();
        if (string.IsNullOrWhiteSpace(concepto)) throw new InvalidOperationException("El concepto es obligatorio.");
        if (req.ValorMensual < 0) throw new InvalidOperationException("El valor no puede ser negativo.");
        if (concepto.Length > 120) concepto = concepto[..120];
        var refTxt = string.IsNullOrWhiteSpace(req.Referencia) ? null : req.Referencia.Trim();
        var e = new UnidadArriendo { TenantId = tid, UnidadId = unidadId, Concepto = concepto, ValorMensual = req.ValorMensual, Referencia = refTxt };
        _db.UnidadArriendos.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Arriendo/cobro '{concepto}' agregado a la unidad.", ct, unidadId);
        return new UnidadArriendoDto(e.Id, e.Concepto, e.ValorMensual, e.Referencia);
    }

    public async Task<bool> EliminarArriendoUnidadAsync(Guid arriendoId, CancellationToken ct)
    {
        var e = await _db.UnidadArriendos.FirstOrDefaultAsync(x => x.Id == arriendoId, ct);
        if (e is null) return false;
        _db.UnidadArriendos.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Mascotas --------
    public async Task<IReadOnlyList<UnidadMascotaDto>> ListMascotasUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadMascotas.AsNoTracking()
            .Where(m => m.UnidadId == unidadId)
            .OrderBy(m => m.Nombre)
            .Select(m => new UnidadMascotaDto(m.Id, m.Nombre, m.Tipo, m.Raza))
            .ToListAsync(ct);

    public async Task<UnidadMascotaDto?> AgregarMascotaUnidadAsync(Guid unidadId, CrearUnidadMascotaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre)) throw new InvalidOperationException("El nombre de la mascota es obligatorio.");
        if (nombre.Length > 80) nombre = nombre[..80];
        var raza = string.IsNullOrWhiteSpace(req.Raza) ? null : req.Raza.Trim();
        var e = new UnidadMascota { TenantId = tid, UnidadId = unidadId, Nombre = nombre, Tipo = req.Tipo, Raza = raza };
        _db.UnidadMascotas.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Mascota '{nombre}' registrada en la unidad.", ct, unidadId);
        return new UnidadMascotaDto(e.Id, e.Nombre, e.Tipo, e.Raza);
    }

    public async Task<bool> EliminarMascotaUnidadAsync(Guid mascotaId, CancellationToken ct)
    {
        var e = await _db.UnidadMascotas.FirstOrDefaultAsync(x => x.Id == mascotaId, ct);
        if (e is null) return false;
        _db.UnidadMascotas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Empleada(s) de servicio --------
    public async Task<IReadOnlyList<UnidadEmpleadaDto>> ListEmpleadasUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadEmpleadas.AsNoTracking()
            .Where(x => x.UnidadId == unidadId)
            .OrderBy(x => x.Nombre)
            .Select(x => new UnidadEmpleadaDto(x.Id, x.Nombre, x.Documento, x.Celular, x.Horario, x.PersonaId))
            .ToListAsync(ct);

    public async Task<UnidadEmpleadaDto?> AgregarEmpleadaUnidadAsync(Guid unidadId, CrearUnidadEmpleadaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre)) throw new InvalidOperationException("El nombre es obligatorio.");
        if (nombre.Length > 120) nombre = nombre[..120];
        var e = new UnidadEmpleada
        {
            TenantId = tid,
            UnidadId = unidadId,
            PersonaId = req.PersonaId,
            Nombre = nombre,
            Documento = string.IsNullOrWhiteSpace(req.Documento) ? null : req.Documento.Trim(),
            Celular = string.IsNullOrWhiteSpace(req.Celular) ? null : req.Celular.Trim(),
            Horario = string.IsNullOrWhiteSpace(req.Horario) ? null : req.Horario.Trim()
        };
        _db.UnidadEmpleadas.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Empleada de servicio '{nombre}' registrada en la unidad.", ct, unidadId);
        return new UnidadEmpleadaDto(e.Id, e.Nombre, e.Documento, e.Celular, e.Horario, e.PersonaId);
    }

    public async Task<bool> EliminarEmpleadaUnidadAsync(Guid empleadaId, CancellationToken ct)
    {
        var e = await _db.UnidadEmpleadas.FirstOrDefaultAsync(x => x.Id == empleadaId, ct);
        if (e is null) return false;
        _db.UnidadEmpleadas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Oleada 2: Historico de titularidad (propietarios) --------
    public async Task<IReadOnlyList<UnidadTitularidadDto>> ListTitularidadUnidadAsync(Guid unidadId, CancellationToken ct)
        => await _db.UnidadTitularidades.AsNoTracking()
            .Where(t => t.UnidadId == unidadId)
            .OrderByDescending(t => t.Desde)
            .Select(t => new UnidadTitularidadDto(t.Id, t.Nombre, t.Rol, t.Desde, t.Hasta, t.Hasta == null, t.PersonaId))
            .ToListAsync(ct);

    public async Task<UnidadTitularidadDto?> AgregarTitularidadUnidadAsync(Guid unidadId, CrearUnidadTitularidadRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadesPrivadas.AnyAsync(u => u.Id == unidadId, ct)) return null;
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre)) throw new InvalidOperationException("El nombre del titular es obligatorio.");
        if (nombre.Length > 160) nombre = nombre[..160];
        if (req.Hasta is { } h && h < req.Desde) throw new InvalidOperationException("La fecha 'hasta' no puede ser anterior a 'desde'.");
        var e = new UnidadTitularidad
        {
            TenantId = tid,
            UnidadId = unidadId,
            PersonaId = req.PersonaId,
            Nombre = nombre,
            Rol = string.IsNullOrWhiteSpace(req.Rol) ? null : req.Rol.Trim(),
            Desde = req.Desde,
            Hasta = req.Hasta
        };
        _db.UnidadTitularidades.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"Titularidad historica '{nombre}' agregada a la unidad.", ct, unidadId);
        return new UnidadTitularidadDto(e.Id, e.Nombre, e.Rol, e.Desde, e.Hasta, e.Hasta == null, e.PersonaId);
    }

    public async Task<bool> EliminarTitularidadUnidadAsync(Guid titularidadId, CancellationToken ct)
    {
        var e = await _db.UnidadTitularidades.FirstOrDefaultAsync(x => x.Id == titularidadId, ct);
        if (e is null) return false;
        _db.UnidadTitularidades.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Oleada 2: Campos dinamicos por persona vinculada --------
    public async Task<IReadOnlyList<UnidadPersonaCampoDto>> ListCamposPersonaAsync(Guid unidadPersonaId, CancellationToken ct)
        => await _db.UnidadPersonaCampos.AsNoTracking()
            .Where(c => c.UnidadPersonaId == unidadPersonaId)
            .OrderBy(c => c.Label)
            .Select(c => new UnidadPersonaCampoDto(c.Id, c.Label, c.Valor))
            .ToListAsync(ct);

    public async Task<UnidadPersonaCampoDto?> AgregarCampoPersonaAsync(Guid unidadPersonaId, CrearUnidadPersonaCampoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.UnidadPersonas.AnyAsync(p => p.Id == unidadPersonaId, ct)) return null;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var e = new UnidadPersonaCampo
        {
            TenantId = tid,
            UnidadPersonaId = unidadPersonaId,
            Label = label,
            Valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim()
        };
        _db.UnidadPersonaCampos.Add(e);
        await _db.SaveChangesAsync(ct);
        return new UnidadPersonaCampoDto(e.Id, e.Label, e.Valor);
    }

    public async Task<bool> EliminarCampoPersonaAsync(Guid campoId, CancellationToken ct)
    {
        var e = await _db.UnidadPersonaCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (e is null) return false;
        _db.UnidadPersonaCampos.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

}
