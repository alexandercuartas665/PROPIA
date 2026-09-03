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
    // Seccion 6 Zonas Comunes + Seccion 7 Equipos (ventanas de disponibilidad, ficha tecnica del activo).
    // ----------------------------- Seccion 6: Zonas Comunes -----------------------------

    public async Task<IReadOnlyList<ZonaComunDto>> ListZonasComunesAsync(CancellationToken ct)
    {
        return await _db.ZonasComunes
            .AsNoTracking()
            .OrderBy(z => z.Nombre)
            .Select(z => new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion,
                z.EsReservable, z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso, z.Estado))
            .ToListAsync(ct);
    }

    public async Task<ZonaComunDto> CrearZonaComunAsync(CrearZonaComunRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre de la zona obligatorio.");
        var z = new ZonaComun
        {
            Nombre = req.Nombre,
            Categoria = req.Categoria,
            Descripcion = req.Descripcion,
            EsReservable = req.EsReservable,
            TarifaReserva = req.TarifaReserva,
            CapacidadPersonas = req.CapacidadPersonas,
            HorariosUso = req.HorariosUso,
            ReglasUso = req.ReglasUso
        };
        _db.ZonasComunes.Add(z);
        await _db.SaveChangesAsync(ct);
        return new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion,
            z.EsReservable, z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso, z.Estado);
    }

    public async Task<ZonaComunDto?> ActualizarZonaComunAsync(Guid zonaId, ActualizarZonaComunRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre de la zona obligatorio.");
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return null;
        var prevEstado = z.Estado;
        z.Nombre = req.Nombre.Trim();
        z.Categoria = req.Categoria;
        z.Descripcion = req.Descripcion;
        z.TarifaReserva = req.TarifaReserva;
        z.ReglasUso = req.ReglasUso;
        z.Estado = req.Estado;
        // Reserva y aforo solo se tocan si vienen en el request (edicion inline de la tabla);
        // null = conservar el valor actual (la plantilla y la ficha no los mandan por aqui).
        if (req.EsReservable is bool r) z.EsReservable = r;
        if (req.CapacidadPersonas is not null) z.CapacidadPersonas = req.CapacidadPersonas;
        await _db.SaveChangesAsync(ct);
        if (prevEstado != req.Estado)
            await RegistrarBitacoraAsync("Zona", $"Zona '{z.Nombre}': estado {prevEstado} -> {req.Estado}.", ct);
        return new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion,
            z.EsReservable, z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso, z.Estado);
    }

    public async Task<bool> CambiarEstadoZonaAsync(Guid zonaId, CambiarEstadoZonaRequest req, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return false;
        var prev = z.Estado;
        z.Estado = req.Estado;  // RN-13: EnMantenimiento bloquea reservas en 2.13; RN-14: Inactiva conserva el registro
        await _db.SaveChangesAsync(ct);
        if (prev != req.Estado)
            await RegistrarBitacoraAsync("Zona", $"Zona '{z.Nombre}': estado {prev} -> {req.Estado}.", ct);
        return true;
    }

    public async Task<bool> EliminarZonaComunAsync(Guid zonaId, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return false;
        // Guarda: no eliminar una zona con reservas asociadas (FK Restrict en Reserva).
        var nReservas = await _db.Reservas.CountAsync(r => r.ZonaComunId == zonaId, ct);
        if (nReservas > 0)
            throw new InvalidOperationException($"No se puede eliminar: la zona tiene {nReservas} reserva(s) asociada(s).");
        _db.ZonasComunes.Remove(z);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 7: Equipos -----------------------------

    public async Task<IReadOnlyList<EquipoActivoDto>> ListEquiposAsync(CancellationToken ct)
    {
        return await _db.EquiposActivos
            .AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => ToEquipoActivoDto(e))
            .ToListAsync(ct);
    }

    private static EquipoActivoDto ToEquipoActivoDto(EquipoActivo e) => new(
        e.Id, e.Nombre, e.Categoria,
        e.Tipo, e.Cantidad, e.EsReservable,
        e.Marca, e.Modelo, e.NumeroSerie, e.CodigoBarra, e.FechaInstalacion, e.GarantiaHasta,
        e.Ubicacion, e.Observaciones,
        e.VidaUtilAnios, e.FechaAdquisicion, e.ValorAdquisicion,
        e.Proveedor, e.NumeroFactura, e.Estado, e.CondicionesUso);

    public async Task<EquipoActivoDto> CrearEquipoAsync(CrearEquipoActivoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del equipo obligatorio.");
        if (req.Cantidad < 1)
            throw new InvalidOperationException("Cantidad debe ser al menos 1.");
        var e = new EquipoActivo
        {
            Nombre = req.Nombre.Trim(),
            Categoria = req.Categoria,
            Tipo = req.Tipo,
            Cantidad = req.Tipo == TipoElemento.Activo ? req.Cantidad : 1,
            EsReservable = req.EsReservable
        };
        _db.EquiposActivos.Add(e);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Equipos", $"{e.Tipo} '{e.Nombre}' agregado (cantidad {e.Cantidad}).", ct);
        return ToEquipoActivoDto(e);
    }

    public async Task<EquipoActivoDto?> ActualizarEquipoAsync(Guid id, ActualizarEquipoActivoRequest req, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre obligatorio.");
        e.Nombre = req.Nombre.Trim();
        e.Categoria = req.Categoria;
        e.Tipo = req.Tipo;
        e.Cantidad = req.Tipo == TipoElemento.Activo ? Math.Max(1, req.Cantidad) : 1;
        e.EsReservable = req.EsReservable;
        e.Modelo = req.Modelo;
        e.NumeroSerie = req.NumeroSerie;
        e.CodigoBarra = string.IsNullOrWhiteSpace(req.CodigoBarra) ? null : req.CodigoBarra.Trim();
        e.CondicionesUso = string.IsNullOrWhiteSpace(req.CondicionesUso) ? null : req.CondicionesUso.Trim();
        e.FechaInstalacion = req.FechaInstalacion;
        e.GarantiaHasta = req.GarantiaHasta;
        e.Ubicacion = req.Ubicacion;
        e.Observaciones = req.Observaciones;
        e.VidaUtilAnios = req.VidaUtilAnios;
        e.FechaAdquisicion = req.FechaAdquisicion;
        e.ValorAdquisicion = req.ValorAdquisicion;
        e.Proveedor = req.Proveedor;
        e.NumeroFactura = req.NumeroFactura;
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Equipos", $"Ficha tecnica de '{e.Nombre}' actualizada.", ct);
        return ToEquipoActivoDto(e);
    }

    // ----------------------------- Ventanas de disponibilidad -----------------------------

    public async Task<IReadOnlyList<VentanaDisponibilidadDto>> ListVentanasAsync(TipoEntidadDisponibilidad tipo, Guid entidadId, CancellationToken ct)
    {
        return await _db.VentanasDisponibilidad.AsNoTracking()
            .Where(v => v.TipoEntidad == tipo && v.EntidadId == entidadId)
            .OrderBy(v => v.DiaSemana).ThenBy(v => v.HoraInicio)
            .Select(v => new VentanaDisponibilidadDto(v.Id, v.TipoEntidad, v.EntidadId, v.DiaSemana, v.HoraInicio, v.HoraFin, v.Activa))
            .ToListAsync(ct);
    }

    public async Task GuardarVentanasAsync(GuardarVentanasRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid)
            throw new InvalidOperationException("Sin tenant activo.");
        // Reemplazo total: borra las existentes y crea las nuevas. Simple y correcto para un editor.
        var existentes = _db.VentanasDisponibilidad.Where(v => v.TipoEntidad == req.TipoEntidad && v.EntidadId == req.EntidadId);
        _db.VentanasDisponibilidad.RemoveRange(existentes);
        foreach (var v in req.Ventanas ?? Array.Empty<NuevaVentanaItem>())
        {
            if (v.HoraFin <= v.HoraInicio) continue; // ignora rangos invertidos
            _db.VentanasDisponibilidad.Add(new VentanaDisponibilidad
            {
                TenantId = tid,
                TipoEntidad = req.TipoEntidad,
                EntidadId = req.EntidadId,
                DiaSemana = v.DiaSemana,
                HoraInicio = v.HoraInicio,
                HoraFin = v.HoraFin,
                Activa = v.Activa
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> CambiarEstadoEquipoAsync(Guid equipoId, CambiarEstadoEquipoRequest req, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.FirstOrDefaultAsync(x => x.Id == equipoId, ct);
        if (e is null) return false;
        var prev = e.Estado;
        e.Estado = req.Estado;
        await _db.SaveChangesAsync(ct);
        if (prev != req.Estado)
            await RegistrarBitacoraAsync("Equipo", $"Equipo '{e.Nombre}': estado {prev} -> {req.Estado}.", ct);
        return true;
    }

    public async Task<bool> EliminarEquipoAsync(Guid equipoId, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.FirstOrDefaultAsync(x => x.Id == equipoId, ct);
        if (e is null) return false;
        _db.EquiposActivos.Remove(e);
        // Los hijos de ficha (fotos/mejoras/campos) caen en cascada; si algo mas lo referencia, avisar.
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("No se puede eliminar: el equipo/activo tiene registros asociados.");
        }
        return true;
    }

    // ----------------------------- Ficha tecnica completa del equipo/activo -----------------------------

    public async Task<EquipoFichaDto?> GetEquipoFichaAsync(Guid equipoId, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == equipoId, ct);
        if (e is null) return null;

        var fotos = (await _db.EquipoFotos.AsNoTracking().Where(f => f.EquipoActivoId == equipoId)
            .Select(f => new { f.Id, f.Url }).ToListAsync(ct))
            .Select(f => new EquipoFotoDto(f.Id, _blob.ResolveUrl(f.Url) ?? f.Url)).ToList();

        var mejoras = (await _db.EquipoMejoras.AsNoTracking().Where(m => m.EquipoActivoId == equipoId)
            .OrderByDescending(m => m.Fecha)
            .Select(m => new { m.Id, m.Descripcion, m.Valor, m.Fecha, m.DocumentoUrl }).ToListAsync(ct))
            .Select(m => new EquipoMejoraDto(m.Id, m.Descripcion, m.Valor, m.Fecha, _blob.ResolveUrl(m.DocumentoUrl))).ToList();

        var campos = await _db.EquipoCamposPersonalizados.AsNoTracking().Where(c => c.EquipoActivoId == equipoId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new EquipoCampoDto(c.Id, c.Label, c.Valor)).ToListAsync(ct);

        var depreciacion = CalcularDepreciacion(e, mejoras.Sum(m => m.Valor));

        // Contratos vinculados + disponibles
        var contratosVinculadosIds = await _db.EquipoContratoVinculos.AsNoTracking()
            .Where(v => v.EquipoActivoId == equipoId).Select(v => v.ContratoServicioId).ToListAsync(ct);
        var contratos = await _db.ContratosServicio.AsNoTracking().ToListAsync(ct);
        var contratosVinculados = contratos.Where(c => contratosVinculadosIds.Contains(c.Id))
            .Select(c => new EquipoRefDto(c.Id, $"{c.Tipo} - {c.Proveedor}", c.Estado.ToString())).ToList();
        var contratosDisponibles = contratos.Where(c => !contratosVinculadosIds.Contains(c.Id))
            .Select(c => new EquipoRefDto(c.Id, $"{c.Tipo} - {c.Proveedor}", null)).ToList();

        // Activos vinculados + disponibles (otros equipos)
        var vinculadosIds = await _db.EquipoVinculos.AsNoTracking()
            .Where(v => v.EquipoActivoId == equipoId).Select(v => v.EquipoVinculadoId).ToListAsync(ct);
        var todos = await _db.EquiposActivos.AsNoTracking().Where(x => x.Id != equipoId)
            .Select(x => new { x.Id, x.Nombre, x.Categoria }).ToListAsync(ct);
        var activosVinculados = todos.Where(x => vinculadosIds.Contains(x.Id))
            .Select(x => new EquipoRefDto(x.Id, x.Nombre, x.Categoria.ToString())).ToList();
        var activosDisponibles = todos.Where(x => !vinculadosIds.Contains(x.Id))
            .Select(x => new EquipoRefDto(x.Id, x.Nombre, null)).ToList();

        // Mantenimientos (intervenciones de este activo)
        var intervenciones = await _db.MantenimientoIntervenciones.AsNoTracking()
            .Where(i => i.ActivoId == equipoId)
            .OrderByDescending(i => i.FechaProgramada)
            .Take(20)
            .ToListAsync(ct);
        var mantenimientos = intervenciones.Select(i => new MantenimientoRefDto(
            i.Id, i.Tipo.ToString(), i.Titulo, null, i.FechaProgramada, i.Estado.ToString(), i.TareaId)).ToList();

        // Tareas asociadas (las intervenciones que generaron tarea en 2.10)
        var tareaIds = intervenciones.Where(i => i.TareaId.HasValue).Select(i => i.TareaId!.Value).ToList();
        var tareas = await _db.Tareas.AsNoTracking().Where(t => tareaIds.Contains(t.Id))
            .Select(t => new TareaRefDto(t.Id, t.Titulo, "Mantenimiento", t.Estado != null ? t.Estado.Nombre : "—"))
            .ToListAsync(ct);

        return new EquipoFichaDto(
            ToEquipoActivoDto(e), depreciacion, fotos, mejoras, campos,
            contratosVinculados, contratosDisponibles,
            activosVinculados, activosDisponibles,
            mantenimientos, tareas);
    }

    /// <summary>Depreciacion lineal: base = costo + mejoras; depAnual = base/vidaUtil; acumulada por anios de uso.</summary>
    private static DepreciacionDto CalcularDepreciacion(EquipoActivo e, decimal mejoras)
    {
        var costo = e.ValorAdquisicion ?? 0m;
        var baseDep = costo + mejoras;
        var vida = e.VidaUtilAnios.GetValueOrDefault();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = e.FechaAdquisicion ?? e.FechaInstalacion;
        var aniosUso = 0;
        if (inicio.HasValue)
        {
            aniosUso = hoy.Year - inicio.Value.Year;
            if (hoy.Month < inicio.Value.Month || (hoy.Month == inicio.Value.Month && hoy.Day < inicio.Value.Day)) aniosUso--;
            if (aniosUso < 0) aniosUso = 0;
        }
        var depAnual = vida > 0 ? Math.Round(baseDep / vida, 2) : 0m;
        var depAcum = vida > 0 ? Math.Min(baseDep, depAnual * Math.Min(aniosUso, vida)) : 0m;
        var valorLibros = baseDep - depAcum;
        var pct = baseDep > 0 ? (int)Math.Round(depAcum / baseDep * 100) : 0;
        return new DepreciacionDto(costo, mejoras, baseDep, vida, aniosUso, depAnual, depAcum, valorLibros, pct);
    }

    public async Task<EquipoFotoDto?> AgregarFotoEquipoAsync(Guid equipoId, string url, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(url)) return null;
        var f = new EquipoFoto { TenantId = tid, EquipoActivoId = equipoId, Url = url.Trim() };
        _db.EquipoFotos.Add(f);
        await _db.SaveChangesAsync(ct);
        return new EquipoFotoDto(f.Id, _blob.ResolveUrl(f.Url) ?? f.Url);
    }

    public async Task<bool> EliminarFotoEquipoAsync(Guid fotoId, CancellationToken ct)
    {
        var f = await _db.EquipoFotos.FirstOrDefaultAsync(x => x.Id == fotoId, ct);
        if (f is null) return false;
        _db.EquipoFotos.Remove(f);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EquipoMejoraDto?> AgregarMejoraEquipoAsync(Guid equipoId, AgregarMejoraRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(req.Descripcion)) throw new InvalidOperationException("Descripcion de la mejora obligatoria.");
        if (req.Valor < 0) throw new InvalidOperationException("El valor no puede ser negativo.");
        var m = new EquipoMejora { TenantId = tid, EquipoActivoId = equipoId, Descripcion = req.Descripcion.Trim(), Valor = req.Valor, Fecha = req.Fecha };
        _db.EquipoMejoras.Add(m);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Equipos", $"Mejora capitalizada registrada: {m.Descripcion} ({m.Valor:N0}).", ct);
        return new EquipoMejoraDto(m.Id, m.Descripcion, m.Valor, m.Fecha, _blob.ResolveUrl(m.DocumentoUrl));
    }

    public async Task<bool> EliminarMejoraEquipoAsync(Guid mejoraId, CancellationToken ct)
    {
        var m = await _db.EquipoMejoras.FirstOrDefaultAsync(x => x.Id == mejoraId, ct);
        if (m is null) return false;
        _db.EquipoMejoras.Remove(m);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ToggleActivoVinculadoAsync(Guid equipoId, ToggleVinculoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return;
        var existente = await _db.EquipoVinculos.FirstOrDefaultAsync(v => v.EquipoActivoId == equipoId && v.EquipoVinculadoId == req.Id, ct);
        if (req.Vincular && existente is null)
            _db.EquipoVinculos.Add(new EquipoVinculo { TenantId = tid, EquipoActivoId = equipoId, EquipoVinculadoId = req.Id });
        else if (!req.Vincular && existente is not null)
            _db.EquipoVinculos.Remove(existente);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ToggleContratoVinculadoAsync(Guid equipoId, ToggleVinculoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return;
        var existente = await _db.EquipoContratoVinculos.FirstOrDefaultAsync(v => v.EquipoActivoId == equipoId && v.ContratoServicioId == req.Id, ct);
        if (req.Vincular && existente is null)
            _db.EquipoContratoVinculos.Add(new EquipoContratoVinculo { TenantId = tid, EquipoActivoId = equipoId, ContratoServicioId = req.Id });
        else if (!req.Vincular && existente is not null)
            _db.EquipoContratoVinculos.Remove(existente);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EquipoCampoDto?> AgregarCampoEquipoAsync(Guid equipoId, AgregarCampoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        var c = new EquipoCampoPersonalizado
        {
            TenantId = tid,
            EquipoActivoId = equipoId,
            Label = req.Label.Trim(),
            Valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim()
        };
        _db.EquipoCamposPersonalizados.Add(c);
        await _db.SaveChangesAsync(ct);
        return new EquipoCampoDto(c.Id, c.Label, c.Valor);
    }

    public async Task<bool> EliminarCampoEquipoAsync(Guid campoId, CancellationToken ct)
    {
        var c = await _db.EquipoCamposPersonalizados.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        _db.EquipoCamposPersonalizados.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

}
