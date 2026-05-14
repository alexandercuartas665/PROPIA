using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

public class MiCopropiedadService : IMiCopropiedadService
{
    private readonly PropiaDbContext _db;
    public MiCopropiedadService(PropiaDbContext db) => _db = db;

    // ----------------------------- Resumen -----------------------------

    public async Task<ResumenMiCopropiedadDto?> GetResumenAsync(Guid tenantId, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        if (t is null) return null;

        var torres = await _db.Torres.CountAsync(ct);
        var unidades = await _db.UnidadesPrivadas.CountAsync(ct);
        var coefSum = await _db.UnidadesPrivadas.SumAsync(u => (decimal?)u.CoeficientePropiedad, ct) ?? 0;
        var zonas = await _db.ZonasComunes.CountAsync(ct);
        var equipos = await _db.EquiposActivos.CountAsync(ct);
        var contratos = await _db.ContratosServicio.CountAsync(ct);
        var miembros = await _db.MiembrosConsejo.CountAsync(c => c.Activo, ct);

        // Heuristicas de completitud por seccion
        var completas = new Dictionary<string, bool>
        {
            ["Identidad"] = !string.IsNullOrWhiteSpace(t.Nombre)
                            && !string.IsNullOrWhiteSpace(t.Nit)
                            && !string.IsNullOrWhiteSpace(t.Direccion)
                            && t.TipoCopropiedad.HasValue,
            ["Distribucion"] = torres > 0 && unidades > 0 && Math.Abs(coefSum - 100m) <= 1m,
            ["EquipoTrabajo"] = miembros > 0,  // proxy - se reemplaza cuando este modulo 2.5
            ["Gobierno"] = miembros >= 3,  // minimo 3 miembros del consejo (presidente + 2)
            ["Servicios"] = contratos > 0,
            ["ZonasComunes"] = zonas > 0,
            ["Equipos"] = equipos > 0,
            ["Finanzas"] = false  // se completa cuando exista 2.6
        };
        var pct = (int)(completas.Values.Count(b => b) * 100.0 / completas.Count);

        return new ResumenMiCopropiedadDto(
            new IdentidadDto(t.Id, t.Nombre, t.Nit, t.DigitoVerificacion,
                t.Direccion, t.Ciudad, t.Departamento,
                t.CodigoPropia, t.TipoCopropiedad, t.Estrato,
                t.FotoFachadaUrl, t.LogoUrl, t.Descripcion),
            torres, unidades, coefSum, zonas, equipos, contratos, miembros,
            pct, completas);
    }

    // ----------------------------- Seccion 1: Identidad -----------------------------

    public async Task<IdentidadDto?> ActualizarIdentidadAsync(Guid tenantId, ActualizarIdentidadRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        if (t is null) return null;
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la copropiedad es obligatorio.");

        t.Nombre = req.Nombre;
        t.Nit = req.Nit;
        t.DigitoVerificacion = req.DigitoVerificacion;
        t.Direccion = req.Direccion;
        t.Ciudad = req.Ciudad;
        t.Departamento = req.Departamento;
        t.TipoCopropiedad = req.Tipo;
        t.Estrato = req.Estrato;
        t.FotoFachadaUrl = req.FotoFachadaUrl;
        t.LogoUrl = req.LogoUrl;
        t.Descripcion = req.Descripcion;
        await _db.SaveChangesAsync(ct);

        return new IdentidadDto(t.Id, t.Nombre, t.Nit, t.DigitoVerificacion,
            t.Direccion, t.Ciudad, t.Departamento,
            t.CodigoPropia, t.TipoCopropiedad, t.Estrato,
            t.FotoFachadaUrl, t.LogoUrl, t.Descripcion);
    }

    // ----------------------------- Seccion 2: Distribucion -----------------------------

    public async Task<IReadOnlyList<TorreDto>> ListTorresAsync(CancellationToken ct)
    {
        return await _db.Torres
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .Select(t => new TorreDto(t.Id, t.Nombre, t.CantidadPisos, t.Descripcion,
                _db.UnidadesPrivadas.Count(u => u.TorreId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<TorreDto> CrearTorreAsync(CrearTorreRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la torre es obligatorio.");
        var torre = new Torre { Nombre = req.Nombre, CantidadPisos = req.CantidadPisos, Descripcion = req.Descripcion };
        _db.Torres.Add(torre);
        await _db.SaveChangesAsync(ct);
        return new TorreDto(torre.Id, torre.Nombre, torre.CantidadPisos, torre.Descripcion, 0);
    }

    public async Task<bool> EliminarTorreAsync(Guid torreId, CancellationToken ct)
    {
        var t = await _db.Torres.FirstOrDefaultAsync(x => x.Id == torreId, ct);
        if (t is null) return false;
        _db.Torres.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<UnidadDto>> ListUnidadesAsync(CancellationToken ct)
    {
        return await _db.UnidadesPrivadas
            .AsNoTracking()
            .Include(u => u.Torre)
            .OrderBy(u => u.Torre!.Nombre).ThenBy(u => u.Numero)
            .Select(u => new UnidadDto(
                u.Id, u.Numero, u.Tipo,
                u.TorreId, u.Torre != null ? u.Torre.Nombre : null, u.Piso,
                u.CoeficientePropiedad, u.AreaM2,
                u.Habitaciones, u.Banos, u.Parqueaderos,
                u.Estado, u.Observaciones))
            .ToListAsync(ct);
    }

    public async Task<UnidadDto> CrearUnidadAsync(CrearUnidadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Numero))
            throw new InvalidOperationException("Numero de unidad obligatorio.");
        if (req.CoeficientePropiedad < 0 || req.CoeficientePropiedad > 100)
            throw new InvalidOperationException("Coeficiente debe estar entre 0 y 100.");

        var unidad = new UnidadPrivada
        {
            Numero = req.Numero,
            Tipo = req.Tipo,
            TorreId = req.TorreId,
            Piso = req.Piso,
            CoeficientePropiedad = req.CoeficientePropiedad,
            AreaM2 = req.AreaM2,
            Habitaciones = req.Habitaciones,
            Banos = req.Banos,
            Parqueaderos = req.Parqueaderos,
            Estado = req.Estado,
            Observaciones = req.Observaciones
        };
        _db.UnidadesPrivadas.Add(unidad);
        await _db.SaveChangesAsync(ct);
        var torreNombre = unidad.TorreId.HasValue
            ? await _db.Torres.Where(t => t.Id == unidad.TorreId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
            : null;
        return new UnidadDto(unidad.Id, unidad.Numero, unidad.Tipo,
            unidad.TorreId, torreNombre, unidad.Piso,
            unidad.CoeficientePropiedad, unidad.AreaM2,
            unidad.Habitaciones, unidad.Banos, unidad.Parqueaderos,
            unidad.Estado, unidad.Observaciones);
    }

    public async Task<bool> EliminarUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        var u = await _db.UnidadesPrivadas.FirstOrDefaultAsync(x => x.Id == unidadId, ct);
        if (u is null) return false;
        _db.UnidadesPrivadas.Remove(u);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 4: Gobierno -----------------------------

    public async Task<IReadOnlyList<MiembroConsejoDto>> ListMiembrosConsejoAsync(CancellationToken ct)
    {
        return await _db.MiembrosConsejo
            .AsNoTracking()
            .Include(m => m.Persona)
            .OrderBy(m => m.Cargo)
            .Select(m => new MiembroConsejoDto(
                m.Id, m.PersonaId,
                m.Persona != null ? $"{m.Persona.Nombres} {m.Persona.Apellidos}" : "Sin asignar",
                m.Cargo, m.FechaInicio, m.FechaFin, m.Activo))
            .ToListAsync(ct);
    }

    public async Task<MiembroConsejoDto> AgregarMiembroConsejoAsync(AgregarMiembroConsejoRequest req, CancellationToken ct)
    {
        var persona = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.PersonaId, ct);
        if (persona is null) throw new InvalidOperationException("Persona no encontrada en el Directorio.");

        // Regla: solo puede haber 1 miembro activo por cargo (excepto Vocal y Suplente)
        if (req.Cargo != CargoConsejo.Vocal && req.Cargo != CargoConsejo.Suplente)
        {
            var existe = await _db.MiembrosConsejo.AnyAsync(m => m.Cargo == req.Cargo && m.Activo, ct);
            if (existe) throw new InvalidOperationException($"Ya existe un miembro activo con cargo {req.Cargo}. Desactivalo primero.");
        }

        var m = new MiembroConsejo
        {
            PersonaId = req.PersonaId,
            Cargo = req.Cargo,
            FechaInicio = req.FechaInicio,
            FechaFin = req.FechaFin,
            Activo = true
        };
        _db.MiembrosConsejo.Add(m);
        await _db.SaveChangesAsync(ct);
        return new MiembroConsejoDto(m.Id, m.PersonaId,
            $"{persona.Nombres} {persona.Apellidos}",
            m.Cargo, m.FechaInicio, m.FechaFin, m.Activo);
    }

    public async Task<bool> DesactivarMiembroConsejoAsync(Guid miembroId, CancellationToken ct)
    {
        var m = await _db.MiembrosConsejo.FirstOrDefaultAsync(x => x.Id == miembroId, ct);
        if (m is null) return false;
        m.Activo = false;
        m.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 5: Servicios -----------------------------

    public async Task<IReadOnlyList<ContratoServicioDto>> ListContratosAsync(CancellationToken ct)
    {
        return await _db.ContratosServicio
            .AsNoTracking()
            .OrderBy(c => c.Tipo)
            .Select(c => new ContratoServicioDto(c.Id, c.Tipo, c.Proveedor, c.NitProveedor,
                c.Contacto, c.FechaInicio, c.FechaFin, c.ValorMensual, c.Observaciones))
            .ToListAsync(ct);
    }

    public async Task<ContratoServicioDto> CrearContratoAsync(CrearContratoServicioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Proveedor))
            throw new InvalidOperationException("Proveedor obligatorio.");
        var c = new ContratoServicio
        {
            Tipo = req.Tipo,
            Proveedor = req.Proveedor,
            NitProveedor = req.NitProveedor,
            Contacto = req.Contacto,
            FechaInicio = req.FechaInicio,
            FechaFin = req.FechaFin,
            ValorMensual = req.ValorMensual,
            Observaciones = req.Observaciones
        };
        _db.ContratosServicio.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ContratoServicioDto(c.Id, c.Tipo, c.Proveedor, c.NitProveedor,
            c.Contacto, c.FechaInicio, c.FechaFin, c.ValorMensual, c.Observaciones);
    }

    public async Task<bool> EliminarContratoAsync(Guid contratoId, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        _db.ContratosServicio.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 6: Zonas Comunes -----------------------------

    public async Task<IReadOnlyList<ZonaComunDto>> ListZonasComunesAsync(CancellationToken ct)
    {
        return await _db.ZonasComunes
            .AsNoTracking()
            .OrderBy(z => z.Nombre)
            .Select(z => new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion,
                z.EsReservable, z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso))
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
            z.EsReservable, z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso);
    }

    public async Task<bool> EliminarZonaComunAsync(Guid zonaId, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return false;
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
            .Select(e => new EquipoActivoDto(e.Id, e.Nombre, e.Categoria, e.Marca, e.Modelo,
                e.NumeroSerie, e.FechaInstalacion, e.GarantiaHasta, e.Ubicacion, e.Observaciones))
            .ToListAsync(ct);
    }

    public async Task<EquipoActivoDto> CrearEquipoAsync(CrearEquipoActivoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del equipo obligatorio.");
        var e = new EquipoActivo
        {
            Nombre = req.Nombre,
            Categoria = req.Categoria,
            Marca = req.Marca,
            Modelo = req.Modelo,
            NumeroSerie = req.NumeroSerie,
            FechaInstalacion = req.FechaInstalacion,
            GarantiaHasta = req.GarantiaHasta,
            Ubicacion = req.Ubicacion,
            Observaciones = req.Observaciones
        };
        _db.EquiposActivos.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EquipoActivoDto(e.Id, e.Nombre, e.Categoria, e.Marca, e.Modelo,
            e.NumeroSerie, e.FechaInstalacion, e.GarantiaHasta, e.Ubicacion, e.Observaciones);
    }

    public async Task<bool> EliminarEquipoAsync(Guid equipoId, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.FirstOrDefaultAsync(x => x.Id == equipoId, ct);
        if (e is null) return false;
        _db.EquiposActivos.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
