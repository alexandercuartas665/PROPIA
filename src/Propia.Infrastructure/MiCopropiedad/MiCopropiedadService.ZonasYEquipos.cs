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
        // MERGE: la marca solo se toca si el cliente la envia (null = conservar, "" = limpiar).
        if (req.Marca is not null)
            e.Marca = string.IsNullOrWhiteSpace(req.Marca) ? null : req.Marca.Trim();
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

    // ===================== Campos dinamicos tipados (catalogo) - EQUIPOS =====================
    private static string? NormalizarOpcionesCampo(TipoCampoTablero tipo, string? opciones)
    {
        if (tipo != TipoCampoTablero.Seleccion) return null;
        if (string.IsNullOrWhiteSpace(opciones)) return null;
        var limpias = opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return limpias.Length == 0 ? null : string.Join('\n', limpias);
    }

    public async Task<IReadOnlyList<EquipoCampoDefinicionDto>> ListCamposDefEquipoAsync(CancellationToken ct)
        => await _db.EquipoCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new EquipoCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones)).ToListAsync(ct);

    public async Task<EquipoCampoDefinicionDto> CrearCampoDefEquipoAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.EquipoCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new EquipoCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones);
        var maxOrden = await _db.EquipoCamposDefiniciones.AnyAsync(ct) ? await _db.EquipoCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new EquipoCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = await PrepararOpcionesEquipoAsync(req.Tipo, req.Opciones, null, ct) };
        _db.EquipoCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new EquipoCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones);
    }

    public async Task<bool> ActualizarCampoDefEquipoAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.EquipoCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = await PrepararOpcionesEquipoAsync(req.Tipo, req.Opciones, definicionId, ct); def.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefEquipoAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.EquipoCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.EquipoCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SetCampoValorEquipoDefAsync(Guid equipoId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var def = await _db.EquipoCamposDefiniciones.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definicionId, ct)
            ?? throw new InvalidOperationException("El campo no existe.");
        // Type-gate del valor por el Tipo del campo (Fase 2): Formula solo lectura, Usuario/Directorio con
        // pertenencia al tenant (rechazo cross-tenant). Helper COMPARTIDO por las 8 superficies.
        await CamposAvanzados.ValidarValorAsync(_db, def.Tipo, valor, ct);
        var existente = await _db.EquipoCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.EquipoActivoId == equipoId, ct);
        if (existente is null)
            _db.EquipoCamposValores.Add(new EquipoCampoValor { TenantId = tid, DefinicionId = definicionId, EquipoActivoId = equipoId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EquipoCampoValorFlatDto>> ListTodosCamposValoresEquipoAsync(CancellationToken ct)
    {
        var valores = await _db.EquipoCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.EquipoActivoId, v.DefinicionId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new EquipoCampoValorFlatDto(v.EquipoActivoId, v.DefinicionId, v.Valor)).ToList();

        // Campos Formula (Fase 2): valor calculado en lectura por equipo (no hay fila almacenada). Se emite
        // para TODOS los equipos (asi Conteo=0 y agregados vacios se ven correctamente en la tabla).
        var formulaDefs = await _db.EquipoCamposDefiniciones.AsNoTracking()
            .Where(d => d.Tipo == TipoCampoTablero.Formula)
            .Select(d => new { d.Id, d.Opciones }).ToListAsync(ct);
        if (formulaDefs.Count > 0)
        {
            var equipos = await _db.EquiposActivos.AsNoTracking()
                .Select(e => new { e.Id, N = new NumerosEquipo(e.Cantidad, e.VidaUtilAnios, e.ValorAdquisicion) })
                .ToListAsync(ct);
            var porEquipo = valores.GroupBy(v => v.EquipoActivoId)
                .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, string?>)g.ToDictionary(x => x.DefinicionId, x => x.Valor));
            IReadOnlyDictionary<Guid, string?> vacio = new Dictionary<Guid, string?>();
            foreach (var e in equipos)
            {
                var dict = porEquipo.TryGetValue(e.Id, out var d0) ? d0 : vacio;
                foreach (var fd in formulaDefs)
                {
                    var txt = ComputarFormulaTextoEquipo(fd.Opciones, dict, e.N);
                    if (txt is not null) res.Add(new EquipoCampoValorFlatDto(e.Id, fd.Id, txt));
                }
            }
        }
        return res;
    }

    public async Task<IReadOnlyList<EquipoCampoDinDto>> ListCamposDinEquipoAsync(Guid equipoId, CancellationToken ct)
    {
        var defs = await _db.EquipoCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var vals = await _db.EquipoCamposValores.AsNoTracking().Where(v => v.EquipoActivoId == equipoId).ToListAsync(ct);
        var porDef = vals.GroupBy(v => v.DefinicionId).ToDictionary(g => g.Key, g => g.First().Valor);
        var nums = await _db.EquiposActivos.AsNoTracking().Where(e => e.Id == equipoId)
            .Select(e => new NumerosEquipo(e.Cantidad, e.VidaUtilAnios, e.ValorAdquisicion)).FirstOrDefaultAsync(ct);
        // Formula: se COMPUTA en lectura (no persiste); el resto toma su valor almacenado.
        return defs.Select(d => new EquipoCampoDinDto(d.Id, d.Label, d.Orden,
            d.Tipo == TipoCampoTablero.Formula ? ComputarFormulaTextoEquipo(d.Opciones, porDef, nums) : porDef.GetValueOrDefault(d.Id),
            d.Tipo, d.Opciones)).ToList();
    }

    // ===================== Campos dinamicos tipados (catalogo) - ZONAS =====================
    public async Task<IReadOnlyList<ZonaCampoDefinicionDto>> ListCamposDefZonaAsync(CancellationToken ct)
        => await _db.ZonaCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label)
            .Select(d => new ZonaCampoDefinicionDto(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones)).ToListAsync(ct);

    public async Task<ZonaCampoDefinicionDto> CrearCampoDefZonaAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        var existente = await _db.ZonaCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return new ZonaCampoDefinicionDto(existente.Id, existente.Label, existente.Orden, existente.Tipo, existente.Opciones);
        var maxOrden = await _db.ZonaCamposDefiniciones.AnyAsync(ct) ? await _db.ZonaCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new ZonaCampoDefinicion { TenantId = tid, Label = label, Orden = maxOrden + 1, Tipo = req.Tipo, Opciones = await PrepararOpcionesZonaAsync(req.Tipo, req.Opciones, null, ct) };
        _db.ZonaCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return new ZonaCampoDefinicionDto(def.Id, def.Label, def.Orden, def.Tipo, def.Opciones);
    }

    public async Task<bool> ActualizarCampoDefZonaAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.ZonaCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        var label = (req.Label ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        if (label.Length > 80) label = label[..80];
        def.Label = label; def.Tipo = req.Tipo; def.Opciones = await PrepararOpcionesZonaAsync(req.Tipo, req.Opciones, definicionId, ct); def.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoDefZonaAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.ZonaCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.ZonaCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SetCampoValorZonaDefAsync(Guid zonaId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var valor = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var def = await _db.ZonaCamposDefiniciones.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definicionId, ct)
            ?? throw new InvalidOperationException("El campo no existe.");
        // Type-gate del valor por el Tipo del campo (Fase 2): Formula solo lectura, Usuario/Directorio con
        // pertenencia al tenant (rechazo cross-tenant). Helper COMPARTIDO por las 8 superficies.
        await CamposAvanzados.ValidarValorAsync(_db, def.Tipo, valor, ct);
        var existente = await _db.ZonaCamposValores.FirstOrDefaultAsync(v => v.DefinicionId == definicionId && v.ZonaComunId == zonaId, ct);
        if (existente is null)
            _db.ZonaCamposValores.Add(new ZonaCampoValor { TenantId = tid, DefinicionId = definicionId, ZonaComunId = zonaId, Valor = valor });
        else existente.Valor = valor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ZonaCampoValorFlatDto>> ListTodosCamposValoresZonaAsync(CancellationToken ct)
    {
        var valores = await _db.ZonaCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.ZonaComunId, v.DefinicionId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new ZonaCampoValorFlatDto(v.ZonaComunId, v.DefinicionId, v.Valor)).ToList();

        // Campos Formula (Fase 2): valor calculado en lectura por zona (no hay fila almacenada). Se emite
        // para TODAS las zonas (asi Conteo=0 y agregados vacios se ven correctamente en la tabla).
        var formulaDefs = await _db.ZonaCamposDefiniciones.AsNoTracking()
            .Where(d => d.Tipo == TipoCampoTablero.Formula)
            .Select(d => new { d.Id, d.Opciones }).ToListAsync(ct);
        if (formulaDefs.Count > 0)
        {
            var zonas = await _db.ZonasComunes.AsNoTracking()
                .Select(z => new { z.Id, N = new NumerosZona(z.CapacidadPersonas, z.TarifaReserva) })
                .ToListAsync(ct);
            var porZona = valores.GroupBy(v => v.ZonaComunId)
                .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, string?>)g.ToDictionary(x => x.DefinicionId, x => x.Valor));
            IReadOnlyDictionary<Guid, string?> vacio = new Dictionary<Guid, string?>();
            foreach (var z in zonas)
            {
                var dict = porZona.TryGetValue(z.Id, out var d0) ? d0 : vacio;
                foreach (var fd in formulaDefs)
                {
                    var txt = ComputarFormulaTextoZona(fd.Opciones, dict, z.N);
                    if (txt is not null) res.Add(new ZonaCampoValorFlatDto(z.Id, fd.Id, txt));
                }
            }
        }
        return res;
    }

    public async Task<IReadOnlyList<ZonaCampoDinDto>> ListCamposDinZonaAsync(Guid zonaId, CancellationToken ct)
    {
        var defs = await _db.ZonaCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var vals = await _db.ZonaCamposValores.AsNoTracking().Where(v => v.ZonaComunId == zonaId).ToListAsync(ct);
        var porDef = vals.GroupBy(v => v.DefinicionId).ToDictionary(g => g.Key, g => g.First().Valor);
        var nums = await _db.ZonasComunes.AsNoTracking().Where(z => z.Id == zonaId)
            .Select(z => new NumerosZona(z.CapacidadPersonas, z.TarifaReserva)).FirstOrDefaultAsync(ct);
        // Formula: se COMPUTA en lectura (no persiste); el resto toma su valor almacenado.
        return defs.Select(d => new ZonaCampoDinDto(d.Id, d.Label, d.Orden,
            d.Tipo == TipoCampoTablero.Formula ? ComputarFormulaTextoZona(d.Opciones, porDef, nums) : porDef.GetValueOrDefault(d.Id),
            d.Tipo, d.Opciones)).ToList();
    }

    // ===================== Fase 2 (Formula/Usuario/Directorio) - helpers por superficie =====================
    // El shape/computo/validacion de la Formula es COMPARTIDO (CampoFormulaConfig); aqui SOLO vive lo propio
    // de cada superficie: el resolver de campos de SISTEMA Numero/Moneda (clave -> Tipo y clave -> valor).
    // Zona: aforo (capacidad de personas) y tarifa de reserva. Equipo: cantidad, vida util y valor de
    // adquisicion. Las claves son las del catalogo canonico (ZonaCamposSistema / EquipoCamposSistema).
    private static readonly IReadOnlyDictionary<string, TipoCampoTablero> FuentesSistemaZona =
        new Dictionary<string, TipoCampoTablero>(StringComparer.Ordinal)
        { ["aforo"] = TipoCampoTablero.Numero, ["tarifa"] = TipoCampoTablero.Moneda };

    private static readonly IReadOnlyDictionary<string, TipoCampoTablero> FuentesSistemaEquipo =
        new Dictionary<string, TipoCampoTablero>(StringComparer.Ordinal)
        { ["cantidad"] = TipoCampoTablero.Numero, ["vidautil"] = TipoCampoTablero.Numero, ["valoradq"] = TipoCampoTablero.Moneda };

    private sealed record NumerosZona(int? Aforo, decimal? Tarifa);
    private sealed record NumerosEquipo(int Cantidad, int? VidaUtil, decimal? ValorAdq);

    private static decimal? ValorSistemaZona(string clave, NumerosZona n) => clave switch
    { "aforo" => n.Aforo, "tarifa" => n.Tarifa, _ => (decimal?)null };

    private static decimal? ValorSistemaEquipo(string clave, NumerosEquipo n) => clave switch
    { "cantidad" => n.Cantidad, "vidautil" => n.VidaUtil, "valoradq" => n.ValorAdq, _ => (decimal?)null };

    private static string? ComputarFormulaTextoZona(string? opciones, IReadOnlyDictionary<Guid, string?> valoresPorDef, NumerosZona? nums)
        => CampoFormulaConfig.ComputarTexto(opciones, clave =>
        {
            if (clave.StartsWith("cd:", StringComparison.Ordinal))
                return Guid.TryParse(clave[3..], out var g) && valoresPorDef.TryGetValue(g, out var v) ? ParseDecimalInv(v) : null;
            return nums is not null ? ValorSistemaZona(clave, nums) : null;
        });

    private static string? ComputarFormulaTextoEquipo(string? opciones, IReadOnlyDictionary<Guid, string?> valoresPorDef, NumerosEquipo? nums)
        => CampoFormulaConfig.ComputarTexto(opciones, clave =>
        {
            if (clave.StartsWith("cd:", StringComparison.Ordinal))
                return Guid.TryParse(clave[3..], out var g) && valoresPorDef.TryGetValue(g, out var v) ? ParseDecimalInv(v) : null;
            return nums is not null ? ValorSistemaEquipo(clave, nums) : null;
        });

    // La columna Opciones es POLIMORFICA segun Tipo: para Seleccion guarda las opciones (una por linea);
    // para Formula (Fase 2) guarda el JSON de CampoFormulaConfig; para el resto va null. Se valida que las
    // fuentes existan y sean Numero/Moneda (propias cd:{guid} + las de sistema de la superficie).
    private async Task<string?> PrepararOpcionesZonaAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo == TipoCampoTablero.Seleccion) return NormalizarOpcionesCampo(tipo, opciones);
        if (tipo != TipoCampoTablero.Formula) return null;
        var cfg = CampoFormulaConfig.Parse(opciones)
            ?? throw new InvalidOperationException("La formula necesita una operacion y al menos un campo fuente.");
        var defs = await _db.ZonaCamposDefiniciones.AsNoTracking()
            .Where(d => excluirId == null || d.Id != excluirId)
            .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct);
        var porGuid = defs.ToDictionary(d => d.Id, d => d.Tipo);
        TipoCampoTablero? TipoDe(string clave)
        {
            if (clave.StartsWith("cd:", StringComparison.Ordinal))
                return Guid.TryParse(clave[3..], out var g) && porGuid.TryGetValue(g, out var t) ? t : (TipoCampoTablero?)null;
            return FuentesSistemaZona.TryGetValue(clave, out var ts) ? ts : (TipoCampoTablero?)null;
        }
        var err = CampoFormulaConfig.Validar(cfg, TipoDe);
        if (err is not null) throw new InvalidOperationException(err);
        return cfg.Serializar();
    }

    private async Task<string?> PrepararOpcionesEquipoAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo == TipoCampoTablero.Seleccion) return NormalizarOpcionesCampo(tipo, opciones);
        if (tipo != TipoCampoTablero.Formula) return null;
        var cfg = CampoFormulaConfig.Parse(opciones)
            ?? throw new InvalidOperationException("La formula necesita una operacion y al menos un campo fuente.");
        var defs = await _db.EquipoCamposDefiniciones.AsNoTracking()
            .Where(d => excluirId == null || d.Id != excluirId)
            .Select(d => new { d.Id, d.Tipo }).ToListAsync(ct);
        var porGuid = defs.ToDictionary(d => d.Id, d => d.Tipo);
        TipoCampoTablero? TipoDe(string clave)
        {
            if (clave.StartsWith("cd:", StringComparison.Ordinal))
                return Guid.TryParse(clave[3..], out var g) && porGuid.TryGetValue(g, out var t) ? t : (TipoCampoTablero?)null;
            return FuentesSistemaEquipo.TryGetValue(clave, out var ts) ? ts : (TipoCampoTablero?)null;
        }
        var err = CampoFormulaConfig.Validar(cfg, TipoDe);
        if (err is not null) throw new InvalidOperationException(err);
        return cfg.Serializar();
    }

}
