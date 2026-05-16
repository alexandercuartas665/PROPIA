using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Porteria;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Porteria;

/// <summary>
/// Modulo 2.12 Porteria - implementacion MVP (spec v1.0).
/// Ver IPorteriaService para alcance y diferidos.
/// </summary>
public class PorteriaService : IPorteriaService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    public PorteriaService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private Guid GetPersonaActualId()
    {
        var p = _http.HttpContext?.User?.FindFirstValue("persona_id");
        return Guid.TryParse(p, out var id) ? id : Guid.Empty;
    }

    private Guid RequireTenantId() =>
        _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

    private async Task<PorteriaConfiguracion> GetOrCreateConfigAsync(CancellationToken ct)
    {
        var cfg = await _db.PorteriaConfiguraciones.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new PorteriaConfiguracion { TenantId = RequireTenantId() };
            _db.PorteriaConfiguraciones.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    // ===========================================================================
    // Turno
    // ===========================================================================

    public async Task<TurnoDto?> GetTurnoActivoAsync(Guid guardaPersonaId, string puntoAcceso, CancellationToken ct)
    {
        var t = await _db.TurnosPorteria.AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuardaPersonaId == guardaPersonaId
                                      && x.PuntoAcceso == puntoAcceso
                                      && x.Estado == EstadoTurnoPorteria.Activo, ct);
        return t is null ? null : await MapTurnoAsync(t, ct);
    }

    public async Task<TurnoDto> AbrirTurnoAsync(AbrirTurnoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.PuntoAcceso))
            throw new InvalidOperationException("PuntoAcceso obligatorio.");
        var tenantId = RequireTenantId();
        // RN-01: no puede haber otro turno activo para este guarda+punto
        var existe = await _db.TurnosPorteria.AnyAsync(t =>
            t.GuardaPersonaId == req.GuardaPersonaId
            && t.PuntoAcceso == req.PuntoAcceso
            && t.Estado == EstadoTurnoPorteria.Activo, ct);
        if (existe)
            throw new InvalidOperationException("RN-01: ya existe un turno activo para este guarda y punto de acceso.");

        var existePersona = await _db.Personas.AnyAsync(p => p.Id == req.GuardaPersonaId, ct);
        if (!existePersona) throw new InvalidOperationException("Guarda no encontrado en el Directorio.");

        var t = new TurnoPorteria
        {
            TenantId = tenantId,
            GuardaPersonaId = req.GuardaPersonaId,
            PuntoAcceso = req.PuntoAcceso.Trim(),
            Estado = EstadoTurnoPorteria.Activo,
            FechaApertura = DateTimeOffset.UtcNow
        };
        _db.TurnosPorteria.Add(t);
        await _db.SaveChangesAsync(ct);
        return await MapTurnoAsync(t, ct);
    }

    public async Task<TurnoDto> CerrarTurnoAsync(Guid turnoId, CerrarTurnoRequest req, CancellationToken ct)
    {
        var t = await _db.TurnosPorteria.FirstOrDefaultAsync(x => x.Id == turnoId, ct)
            ?? throw new InvalidOperationException("Turno no encontrado.");
        if (t.Estado == EstadoTurnoPorteria.Cerrado)
            throw new InvalidOperationException("El turno ya esta cerrado.");
        t.Estado = EstadoTurnoPorteria.Cerrado;
        t.FechaCierre = DateTimeOffset.UtcNow;
        t.NotaCierre = req.NotaCierre?.Trim();
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await MapTurnoAsync(t, ct);
    }

    public async Task<ResumenTurnoDto> GetResumenTurnoAsync(Guid turnoId, CancellationToken ct)
    {
        var ingresos = await _db.RegistrosVisita.AsNoTracking()
            .CountAsync(r => r.TurnoId == turnoId && r.TipoEvento == TipoEventoAccesoPorteria.Ingreso, ct);
        var egresos = await _db.RegistrosVisita.AsNoTracking()
            .CountAsync(r => r.TurnoId == turnoId && r.TipoEvento == TipoEventoAccesoPorteria.Egreso, ct);
        var personasDentro = Math.Max(0, ingresos - egresos);
        var vehIn = await _db.RegistrosVehiculo.AsNoTracking()
            .CountAsync(r => r.TurnoId == turnoId && r.TipoEvento == TipoEventoAccesoPorteria.Ingreso, ct);
        var vehOut = await _db.RegistrosVehiculo.AsNoTracking()
            .CountAsync(r => r.TurnoId == turnoId && r.TipoEvento == TipoEventoAccesoPorteria.Egreso, ct);
        var vehDentro = Math.Max(0, vehIn - vehOut);
        var paqRecibidos = await _db.Correspondencias.AsNoTracking().CountAsync(c => c.TurnoId == turnoId, ct);
        var paqEntregados = await _db.Correspondencias.AsNoTracking()
            .CountAsync(c => c.TurnoId == turnoId && c.Estado == EstadoCorrespondencia.Entregado, ct);
        var paqPend = await _db.Correspondencias.AsNoTracking()
            .CountAsync(c => c.Estado == EstadoCorrespondencia.Recibido || c.Estado == EstadoCorrespondencia.Notificado, ct);
        var novedades = await _db.NovedadesTurno.AsNoTracking().CountAsync(n => n.TurnoId == turnoId, ct);
        return new ResumenTurnoDto(ingresos, egresos, personasDentro, vehDentro,
            paqRecibidos, paqEntregados, paqPend, novedades);
    }

    public async Task<IReadOnlyList<TurnoDto>> ListarTurnosAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var q = _db.TurnosPorteria.AsNoTracking().AsQueryable();
        if (desde is { } d)
        {
            var dt = new DateTimeOffset(DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
            q = q.Where(t => t.FechaApertura >= dt);
        }
        if (hasta is { } h)
        {
            var dt = new DateTimeOffset(DateTime.SpecifyKind(h.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));
            q = q.Where(t => t.FechaApertura <= dt);
        }
        var list = await q.OrderByDescending(t => t.FechaApertura).Take(100).ToListAsync(ct);
        var result = new List<TurnoDto>(list.Count);
        foreach (var t in list) result.Add(await MapTurnoAsync(t, ct));
        return result;
    }

    private async Task<TurnoDto> MapTurnoAsync(TurnoPorteria t, CancellationToken ct)
    {
        var guarda = await _db.Personas.AsNoTracking()
            .Where(p => p.Id == t.GuardaPersonaId)
            .Select(p => p.Nombres + " " + p.Apellidos)
            .FirstOrDefaultAsync(ct) ?? "(guarda)";
        var ingresos = await _db.RegistrosVisita.AsNoTracking()
            .CountAsync(r => r.TurnoId == t.Id && r.TipoEvento == TipoEventoAccesoPorteria.Ingreso, ct);
        var egresos = await _db.RegistrosVisita.AsNoTracking()
            .CountAsync(r => r.TurnoId == t.Id && r.TipoEvento == TipoEventoAccesoPorteria.Egreso, ct);
        var paquetes = await _db.Correspondencias.AsNoTracking().CountAsync(c => c.TurnoId == t.Id, ct);
        var novedades = await _db.NovedadesTurno.AsNoTracking().CountAsync(n => n.TurnoId == t.Id, ct);
        return new TurnoDto(t.Id, t.GuardaPersonaId, guarda, t.PuntoAcceso, t.Estado,
            t.FechaApertura, t.FechaCierre, t.NotaCierre, ingresos, egresos, paquetes, novedades);
    }

    // ===========================================================================
    // Visitantes frecuentes
    // ===========================================================================

    public async Task<IReadOnlyList<VisitanteFrecuenteDto>> BuscarVisitantesFrecuentesAsync(string? query, CancellationToken ct)
    {
        var q = _db.VisitantesFrecuentes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var t = query.Trim().ToLower();
            q = q.Where(v => v.Nombre.ToLower().Contains(t) || (v.Documento != null && v.Documento.Contains(t)));
        }
        return await q.OrderByDescending(v => v.UltimoIngresoAt ?? v.CreatedAt).Take(25)
            .Select(v => new VisitanteFrecuenteDto(v.Id, v.Nombre, v.Documento, v.TipoDocumento, v.UltimoIngresoAt, v.TotalVisitas))
            .ToListAsync(ct);
    }

    // ===========================================================================
    // Autorizaciones previas
    // ===========================================================================

    public async Task<IReadOnlyList<AutorizacionPreviaDto>> ListarAutorizacionesAsync(
        EstadoAutorizacion? estado, Guid? unidadId, CancellationToken ct)
    {
        var q = _db.AutorizacionesPrevia.AsNoTracking().AsQueryable();
        if (estado is { } e) q = q.Where(a => a.Estado == e);
        if (unidadId is { } u) q = q.Where(a => a.UnidadPrivadaId == u);
        var list = await q.OrderByDescending(a => a.CreatedAt).Take(200)
            .Select(a => new
            {
                a.Id,
                a.UnidadPrivadaId,
                UnidadNumero = _db.UnidadesPrivadas.Where(up => up.Id == a.UnidadPrivadaId).Select(up => up.Numero).FirstOrDefault() ?? "",
                a.Origen,
                a.NombreVisitante,
                a.DocumentoVisitante,
                a.TipoVisitante,
                a.VigenciaInicio,
                a.VigenciaFin,
                a.NotaPortero,
                a.Estado,
                a.UsosMaximos,
                a.UsosRealizados,
                Codigo = _db.CodigosIngreso.Where(c => c.AutorizacionId == a.Id).Select(c => c.CodigoNumerico).FirstOrDefault(),
                a.CreatedAt
            })
            .ToListAsync(ct);
        return list.Select(r => new AutorizacionPreviaDto(
            r.Id, r.UnidadPrivadaId, r.UnidadNumero, r.Origen, r.NombreVisitante, r.DocumentoVisitante,
            r.TipoVisitante, r.VigenciaInicio, r.VigenciaFin, r.NotaPortero, r.Estado,
            r.UsosMaximos, r.UsosRealizados, r.Codigo, r.CreatedAt)).ToList();
    }

    public async Task<AutorizacionPreviaDto?> GetAutorizacionAsync(Guid id, CancellationToken ct)
    {
        var all = await ListarAutorizacionesAsync(null, null, ct);
        return all.FirstOrDefault(a => a.Id == id);
    }

    public async Task<AutorizacionPreviaDto> CrearAutorizacionAsync(CrearAutorizacionRequest req, CancellationToken ct)
    {
        if (req.VigenciaFin <= req.VigenciaInicio)
            throw new InvalidOperationException("RN-04: VigenciaFin debe ser posterior a VigenciaInicio.");
        if (req.VigenciaFin <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("VigenciaFin debe ser futura.");
        if (req.UsosMaximos < 1)
            throw new InvalidOperationException("UsosMaximos debe ser >= 1.");
        var tenantId = RequireTenantId();
        var existeUnidad = await _db.UnidadesPrivadas.AnyAsync(u => u.Id == req.UnidadPrivadaId, ct);
        if (!existeUnidad) throw new InvalidOperationException("Unidad no encontrada.");

        var a = new AutorizacionPrevia
        {
            TenantId = tenantId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            CreadoPorPersonaId = GetPersonaActualId(),
            Origen = req.Origen,
            NombreVisitante = req.NombreVisitante.Trim(),
            DocumentoVisitante = req.DocumentoVisitante?.Trim(),
            TipoVisitante = req.TipoVisitante,
            VigenciaInicio = req.VigenciaInicio,
            VigenciaFin = req.VigenciaFin,
            NotaPortero = req.NotaPortero?.Trim(),
            UsosMaximos = req.UsosMaximos,
            Estado = EstadoAutorizacion.Activo
        };
        _db.AutorizacionesPrevia.Add(a);
        await _db.SaveChangesAsync(ct);

        // Generar codigo numerico unico por tenant
        var codigo = await GenerarCodigoUnicoAsync(tenantId, ct);
        var qrPayload = JsonSerializer.Serialize(new
        {
            autorizacionId = a.Id,
            tenantId = tenantId,
            generadoAt = DateTimeOffset.UtcNow,
            nonce = Guid.NewGuid().ToString("N").Substring(0, 16)
        });
        var c = new CodigoIngreso
        {
            TenantId = tenantId,
            AutorizacionId = a.Id,
            CodigoNumerico = codigo,
            QrPayload = qrPayload,
            Estado = EstadoAutorizacion.Activo
        };
        _db.CodigosIngreso.Add(c);
        await _db.SaveChangesAsync(ct);

        return (await GetAutorizacionAsync(a.Id, ct))!;
    }

    private async Task<string> GenerarCodigoUnicoAsync(Guid tenantId, CancellationToken ct)
    {
        var rnd = new Random();
        for (int i = 0; i < 30; i++)
        {
            var c = rnd.Next(10_000_000, 99_999_999).ToString();
            var ya = await _db.CodigosIngreso.AnyAsync(x => x.CodigoNumerico == c, ct);
            if (!ya) return c;
        }
        throw new InvalidOperationException("No se pudo generar codigo unico.");
    }

    public async Task<bool> RevocarAutorizacionAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.AutorizacionesPrevia.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return false;
        a.Estado = EstadoAutorizacion.Revocado;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        var codigos = await _db.CodigosIngreso.Where(c => c.AutorizacionId == id).ToListAsync(ct);
        foreach (var c in codigos)
        {
            c.Estado = EstadoAutorizacion.Revocado;
            c.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CodigoValidadoDto> ValidarCodigoAsync(ValidarCodigoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Codigo))
            return new CodigoValidadoDto(false, "Codigo vacio", null);
        var c = await _db.CodigosIngreso.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CodigoNumerico == req.Codigo.Trim(), ct);
        if (c is null) return new CodigoValidadoDto(false, "Codigo no encontrado", null);
        if (c.Estado == EstadoAutorizacion.Usado) return new CodigoValidadoDto(false, "Codigo ya usado", null);
        if (c.Estado == EstadoAutorizacion.Expirado) return new CodigoValidadoDto(false, "Codigo expirado", null);
        if (c.Estado == EstadoAutorizacion.Revocado) return new CodigoValidadoDto(false, "Codigo revocado", null);

        var a = await _db.AutorizacionesPrevia.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.AutorizacionId, ct);
        if (a is null) return new CodigoValidadoDto(false, "Autorizacion no encontrada", null);
        if (a.Estado != EstadoAutorizacion.Activo) return new CodigoValidadoDto(false, "Autorizacion " + a.Estado, null);
        if (DateTimeOffset.UtcNow < a.VigenciaInicio) return new CodigoValidadoDto(false, "Fuera del horario - aun no inicia", null);
        if (DateTimeOffset.UtcNow > a.VigenciaFin) return new CodigoValidadoDto(false, "Fuera del horario - expirado", null);
        if (a.UsosRealizados >= a.UsosMaximos) return new CodigoValidadoDto(false, "Cupo de usos agotado", null);

        var dto = await GetAutorizacionAsync(a.Id, ct);
        return new CodigoValidadoDto(true, null, dto);
    }

    // ===========================================================================
    // Registro visitas
    // ===========================================================================

    public async Task<RegistroVisitaDto> RegistrarVisitaAsync(Guid turnoId, RegistrarVisitaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var tenantId = RequireTenantId();
        var turno = await _db.TurnosPorteria.FirstOrDefaultAsync(t => t.Id == turnoId, ct)
            ?? throw new InvalidOperationException("Turno no encontrado.");
        if (turno.Estado != EstadoTurnoPorteria.Activo)
            throw new InvalidOperationException("El turno no esta activo.");

        var cfg = await GetOrCreateConfigAsync(ct);
        if (cfg.DestinoVisitanteObligatorio && req.DestinoTipo is null)
            throw new InvalidOperationException("RN-07: el destino del visitante es obligatorio.");
        if (req.AutorizacionId is null && !req.AvisoTratamientoConfirmado)
            throw new InvalidOperationException("RN-09: debe confirmar que se informo el aviso de tratamiento de datos.");

        // Si vino con autorizacion, consume cupo + marca codigo si aplica
        AutorizacionPrevia? a = null;
        if (req.AutorizacionId is { } authId)
        {
            a = await _db.AutorizacionesPrevia.FirstOrDefaultAsync(x => x.Id == authId, ct);
            if (a is null) throw new InvalidOperationException("Autorizacion no encontrada.");
            if (a.Estado != EstadoAutorizacion.Activo) throw new InvalidOperationException("Autorizacion no esta Activa.");
            if (a.UsosRealizados >= a.UsosMaximos) throw new InvalidOperationException("Autorizacion sin cupo de usos.");
            a.UsosRealizados++;
            if (a.UsosRealizados >= a.UsosMaximos)
            {
                a.Estado = EstadoAutorizacion.Usado;
                var cods = await _db.CodigosIngreso.Where(c => c.AutorizacionId == authId).ToListAsync(ct);
                foreach (var c in cods)
                {
                    c.Estado = EstadoAutorizacion.Usado;
                    c.UsadoAt = DateTimeOffset.UtcNow;
                }
            }
        }

        var r = new RegistroVisita
        {
            TenantId = tenantId,
            TurnoId = turnoId,
            TipoEvento = req.TipoEvento,
            TipoVisitante = req.TipoVisitante,
            VisitanteFrecuenteId = req.VisitanteFrecuenteId,
            Nombre = req.Nombre.Trim(),
            Documento = req.Documento?.Trim(),
            TipoDocumento = req.TipoDocumento,
            DestinoTipo = req.DestinoTipo,
            DestinoId = req.DestinoId,
            AutorizacionId = req.AutorizacionId,
            Observacion = req.Observacion?.Trim(),
            Timestamp = DateTimeOffset.UtcNow,
            GuardaPersonaId = turno.GuardaPersonaId,
            AvisoTratamientoConfirmado = req.AvisoTratamientoConfirmado
        };
        _db.RegistrosVisita.Add(r);

        // Actualizar visitante frecuente
        if (req.VisitanteFrecuenteId is { } vfId)
        {
            var vf = await _db.VisitantesFrecuentes.FirstOrDefaultAsync(v => v.Id == vfId, ct);
            if (vf is not null && req.TipoEvento == TipoEventoAccesoPorteria.Ingreso)
            {
                vf.UltimoIngresoAt = DateTimeOffset.UtcNow;
                vf.TotalVisitas++;
                if (req.AvisoTratamientoConfirmado && !vf.AvisoTratamientoAceptado)
                {
                    vf.AvisoTratamientoAceptado = true;
                    vf.AvisoAceptadoAt = DateTimeOffset.UtcNow;
                }
                vf.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        else if (req.TipoEvento == TipoEventoAccesoPorteria.Ingreso
                 && !string.IsNullOrWhiteSpace(req.Documento)
                 && req.AvisoTratamientoConfirmado)
        {
            // Crear visitante frecuente nuevo si confirma aviso y trae documento
            var ya = await _db.VisitantesFrecuentes.FirstOrDefaultAsync(v => v.Documento == req.Documento, ct);
            if (ya is null)
            {
                _db.VisitantesFrecuentes.Add(new VisitanteFrecuente
                {
                    TenantId = tenantId,
                    Nombre = req.Nombre.Trim(),
                    Documento = req.Documento.Trim(),
                    TipoDocumento = req.TipoDocumento,
                    AvisoTratamientoAceptado = true,
                    AvisoAceptadoAt = DateTimeOffset.UtcNow,
                    UltimoIngresoAt = DateTimeOffset.UtcNow,
                    TotalVisitas = 1,
                    PurgaProgramadaAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(cfg.RetencionDatosVisitantesDias))
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await MapVisitaAsync(r, ct);
    }

    private async Task<RegistroVisitaDto> MapVisitaAsync(RegistroVisita r, CancellationToken ct)
    {
        string? destinoNombre = null;
        if (r.DestinoTipo == DestinoVisita.UnidadPrivada && r.DestinoId.HasValue)
            destinoNombre = await _db.UnidadesPrivadas.Where(u => u.Id == r.DestinoId).Select(u => u.Numero).FirstOrDefaultAsync(ct);
        else if (r.DestinoTipo == DestinoVisita.ZonaComun && r.DestinoId.HasValue)
            destinoNombre = await _db.ZonasComunes.Where(z => z.Id == r.DestinoId).Select(z => z.Nombre).FirstOrDefaultAsync(ct);
        return new RegistroVisitaDto(r.Id, r.TurnoId, r.TipoEvento, r.TipoVisitante, r.Nombre, r.Documento,
            r.DestinoTipo, r.DestinoId, destinoNombre, r.AutorizacionId, r.Observacion, r.Timestamp, r.GuardaPersonaId);
    }

    public async Task<IReadOnlyList<RegistroVisitaDto>> ListarVisitasAsync(Guid? turnoId, DateOnly? fecha, CancellationToken ct)
    {
        var q = _db.RegistrosVisita.AsNoTracking().AsQueryable();
        if (turnoId is { } t) q = q.Where(r => r.TurnoId == t);
        if (fecha is { } f)
        {
            var ini = new DateTimeOffset(DateTime.SpecifyKind(f.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
            var fin = new DateTimeOffset(DateTime.SpecifyKind(f.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));
            q = q.Where(r => r.Timestamp >= ini && r.Timestamp <= fin);
        }
        var list = await q.OrderByDescending(r => r.Timestamp).Take(200).ToListAsync(ct);
        var result = new List<RegistroVisitaDto>(list.Count);
        foreach (var r in list) result.Add(await MapVisitaAsync(r, ct));
        return result;
    }

    // ===========================================================================
    // Vehiculos
    // ===========================================================================

    public async Task<IReadOnlyList<VehiculoAutorizadoDto>> ListarVehiculosAutorizadosAsync(Guid? unidadId, CancellationToken ct)
    {
        var q = _db.VehiculosAutorizados.AsNoTracking().Where(v => v.Activo);
        if (unidadId is { } u) q = q.Where(v => v.UnidadPrivadaId == u);
        var list = await q
            .Select(v => new
            {
                v.Id,
                v.UnidadPrivadaId,
                UnidadNumero = _db.UnidadesPrivadas.Where(up => up.Id == v.UnidadPrivadaId).Select(up => up.Numero).FirstOrDefault() ?? "",
                v.Placa,
                v.Tipo,
                v.Marca,
                v.Modelo,
                v.Color,
                v.ParqueaderoAsignado,
                v.Activo
            })
            .OrderBy(v => v.UnidadNumero).ThenBy(v => v.Placa)
            .ToListAsync(ct);
        return list.Select(v => new VehiculoAutorizadoDto(v.Id, v.UnidadPrivadaId, v.UnidadNumero,
            v.Placa, v.Tipo, v.Marca, v.Modelo, v.Color, v.ParqueaderoAsignado, v.Activo)).ToList();
    }

    public async Task<VehiculoAutorizadoDto> CrearVehiculoAutorizadoAsync(CrearVehiculoRequest req, CancellationToken ct)
    {
        var placa = NormalizarPlaca(req.Placa);
        if (string.IsNullOrWhiteSpace(placa)) throw new InvalidOperationException("Placa obligatoria.");
        var tenantId = RequireTenantId();
        var existeUnidad = await _db.UnidadesPrivadas.AnyAsync(u => u.Id == req.UnidadPrivadaId, ct);
        if (!existeUnidad) throw new InvalidOperationException("Unidad no encontrada.");
        var dup = await _db.VehiculosAutorizados.AnyAsync(v => v.Placa == placa && v.Activo, ct);
        if (dup) throw new InvalidOperationException("Ya existe un vehiculo activo con esa placa en la copropiedad.");

        var v = new VehiculoAutorizado
        {
            TenantId = tenantId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            Placa = placa,
            Tipo = req.Tipo,
            Marca = req.Marca?.Trim(),
            Modelo = req.Modelo?.Trim(),
            Color = req.Color?.Trim(),
            ParqueaderoAsignado = req.ParqueaderoAsignado?.Trim(),
            Activo = true
        };
        _db.VehiculosAutorizados.Add(v);
        await _db.SaveChangesAsync(ct);
        var unidad = await _db.UnidadesPrivadas.AsNoTracking()
            .Where(u => u.Id == v.UnidadPrivadaId).Select(u => u.Numero).FirstOrDefaultAsync(ct) ?? "";
        return new VehiculoAutorizadoDto(v.Id, v.UnidadPrivadaId, unidad, v.Placa, v.Tipo,
            v.Marca, v.Modelo, v.Color, v.ParqueaderoAsignado, v.Activo);
    }

    public async Task<bool> ActualizarVehiculoAutorizadoAsync(Guid id, ActualizarVehiculoRequest req, CancellationToken ct)
    {
        var v = await _db.VehiculosAutorizados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return false;
        var placa = NormalizarPlaca(req.Placa);
        if (placa != v.Placa)
        {
            var dup = await _db.VehiculosAutorizados.AnyAsync(x => x.Id != id && x.Placa == placa && x.Activo, ct);
            if (dup) throw new InvalidOperationException("Otro vehiculo activo ya usa esa placa.");
            v.Placa = placa;
        }
        v.Tipo = req.Tipo;
        v.Marca = req.Marca?.Trim();
        v.Modelo = req.Modelo?.Trim();
        v.Color = req.Color?.Trim();
        v.ParqueaderoAsignado = req.ParqueaderoAsignado?.Trim();
        v.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesactivarVehiculoAsync(Guid id, CancellationToken ct)
    {
        var v = await _db.VehiculosAutorizados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return false;
        v.Activo = false;
        v.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizarPlaca(string placa)
    {
        if (string.IsNullOrWhiteSpace(placa)) return string.Empty;
        var clean = new string(placa.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToUpperInvariant();
        return clean.Length > 10 ? clean.Substring(0, 10) : clean;
    }

    public async Task<RegistroVehiculoDto> RegistrarVehiculoAsync(Guid turnoId, RegistrarVehiculoRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var turno = await _db.TurnosPorteria.FirstOrDefaultAsync(t => t.Id == turnoId, ct)
            ?? throw new InvalidOperationException("Turno no encontrado.");
        if (turno.Estado != EstadoTurnoPorteria.Activo)
            throw new InvalidOperationException("El turno no esta activo.");

        var placa = NormalizarPlaca(req.Placa);
        if (string.IsNullOrEmpty(placa)) throw new InvalidOperationException("Placa obligatoria.");

        var auto = await _db.VehiculosAutorizados.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Placa == placa && v.Activo, ct);

        var r = new RegistroVehiculo
        {
            TenantId = tenantId,
            TurnoId = turnoId,
            TipoEvento = req.TipoEvento,
            Placa = placa,
            VehiculoAutorizadoId = auto?.Id,
            UnidadPrivadaId = auto?.UnidadPrivadaId,
            EsVisita = auto is null,
            Conductor = req.Conductor?.Trim(),
            Observacion = req.Observacion?.Trim(),
            OrigenRegistro = OrigenRegistroVehiculo.Manual,
            Timestamp = DateTimeOffset.UtcNow,
            GuardaPersonaId = turno.GuardaPersonaId
        };
        _db.RegistrosVehiculo.Add(r);
        await _db.SaveChangesAsync(ct);
        return await MapVehiculoAsync(r, ct);
    }

    private async Task<RegistroVehiculoDto> MapVehiculoAsync(RegistroVehiculo r, CancellationToken ct)
    {
        string? unidadNombre = null;
        if (r.UnidadPrivadaId.HasValue)
            unidadNombre = await _db.UnidadesPrivadas.Where(u => u.Id == r.UnidadPrivadaId).Select(u => u.Numero).FirstOrDefaultAsync(ct);
        return new RegistroVehiculoDto(r.Id, r.TurnoId, r.TipoEvento, r.Placa, r.VehiculoAutorizadoId,
            r.UnidadPrivadaId, unidadNombre, r.EsVisita, r.Conductor, r.OrigenRegistro, r.Timestamp);
    }

    public async Task<IReadOnlyList<RegistroVehiculoDto>> ListarRegistrosVehiculoAsync(Guid? turnoId, DateOnly? fecha, CancellationToken ct)
    {
        var q = _db.RegistrosVehiculo.AsNoTracking().AsQueryable();
        if (turnoId is { } t) q = q.Where(r => r.TurnoId == t);
        if (fecha is { } f)
        {
            var ini = new DateTimeOffset(DateTime.SpecifyKind(f.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
            var fin = new DateTimeOffset(DateTime.SpecifyKind(f.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));
            q = q.Where(r => r.Timestamp >= ini && r.Timestamp <= fin);
        }
        var list = await q.OrderByDescending(r => r.Timestamp).Take(200).ToListAsync(ct);
        var result = new List<RegistroVehiculoDto>(list.Count);
        foreach (var r in list) result.Add(await MapVehiculoAsync(r, ct));
        return result;
    }

    // ===========================================================================
    // Correspondencia
    // ===========================================================================

    public async Task<IReadOnlyList<CorrespondenciaDto>> ListarCorrespondenciaAsync(EstadoCorrespondencia? estado, Guid? unidadId, CancellationToken ct)
    {
        var cfg = await GetOrCreateConfigAsync(ct);
        var q = _db.Correspondencias.AsNoTracking().AsQueryable();
        if (estado is { } e) q = q.Where(c => c.Estado == e);
        if (unidadId is { } u) q = q.Where(c => c.UnidadPrivadaId == u);
        var list = await q.OrderByDescending(c => c.RecibidoAt).Take(200).ToListAsync(ct);
        var ahora = DateTimeOffset.UtcNow;
        var result = new List<CorrespondenciaDto>(list.Count);
        foreach (var c in list)
        {
            var unidad = await _db.UnidadesPrivadas.AsNoTracking().Where(up => up.Id == c.UnidadPrivadaId).Select(up => up.Numero).FirstOrDefaultAsync(ct) ?? "";
            result.Add(new CorrespondenciaDto(c.Id, c.TurnoId, c.UnidadPrivadaId, unidad,
                c.Tipo, c.Remitente, c.Descripcion, c.Estado, CalcularSemaforo(c, ahora, cfg),
                c.RecibidoAt, c.NotificadoAt, c.EntregadoAt, c.EntregadoA, c.DevueltoAt, c.MotivoDevolucion,
                c.GuardaRecibePersonaId, c.GuardaEntregaPersonaId));
        }
        return result;
    }

    private static SemaforoPaquete CalcularSemaforo(Correspondencia c, DateTimeOffset ahora, PorteriaConfiguracion cfg)
    {
        if (c.Estado != EstadoCorrespondencia.Recibido && c.Estado != EstadoCorrespondencia.Notificado)
            return SemaforoPaquete.Verde;
        var minutos = (ahora - c.RecibidoAt).TotalMinutes;
        if (minutos >= cfg.UmbralPaqueteRojoMin) return SemaforoPaquete.Rojo;
        if (minutos >= cfg.UmbralPaqueteAmarilloMin) return SemaforoPaquete.Amarillo;
        return SemaforoPaquete.Verde;
    }

    public async Task<CorrespondenciaDto> RegistrarCorrespondenciaAsync(Guid turnoId, RegistrarCorrespondenciaRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var turno = await _db.TurnosPorteria.FirstOrDefaultAsync(t => t.Id == turnoId, ct)
            ?? throw new InvalidOperationException("Turno no encontrado.");
        if (turno.Estado != EstadoTurnoPorteria.Activo)
            throw new InvalidOperationException("El turno no esta activo.");
        var existeUnidad = await _db.UnidadesPrivadas.AnyAsync(u => u.Id == req.UnidadPrivadaId, ct);
        if (!existeUnidad) throw new InvalidOperationException("Unidad no encontrada.");

        var c = new Correspondencia
        {
            TenantId = tenantId,
            TurnoId = turnoId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            Tipo = req.Tipo,
            Remitente = req.Remitente?.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Estado = EstadoCorrespondencia.Notificado, // MVP: T.2 esta pendiente, marcamos Notificado
            RecibidoAt = DateTimeOffset.UtcNow,
            NotificadoAt = DateTimeOffset.UtcNow,
            GuardaRecibePersonaId = turno.GuardaPersonaId
        };
        _db.Correspondencias.Add(c);
        await _db.SaveChangesAsync(ct);
        var cfg = await GetOrCreateConfigAsync(ct);
        var unidad = await _db.UnidadesPrivadas.AsNoTracking().Where(up => up.Id == c.UnidadPrivadaId).Select(up => up.Numero).FirstOrDefaultAsync(ct) ?? "";
        return new CorrespondenciaDto(c.Id, c.TurnoId, c.UnidadPrivadaId, unidad,
            c.Tipo, c.Remitente, c.Descripcion, c.Estado, CalcularSemaforo(c, DateTimeOffset.UtcNow, cfg),
            c.RecibidoAt, c.NotificadoAt, c.EntregadoAt, c.EntregadoA, c.DevueltoAt, c.MotivoDevolucion,
            c.GuardaRecibePersonaId, c.GuardaEntregaPersonaId);
    }

    public async Task<bool> EntregarCorrespondenciaAsync(Guid id, EntregarCorrespondenciaRequest req, CancellationToken ct)
    {
        var c = await _db.Correspondencias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (c.Estado == EstadoCorrespondencia.Entregado) return true;
        if (c.Estado == EstadoCorrespondencia.Devuelto)
            throw new InvalidOperationException("Correspondencia ya devuelta - no puede entregarse.");
        c.Estado = EstadoCorrespondencia.Entregado;
        c.EntregadoAt = DateTimeOffset.UtcNow;
        c.EntregadoA = req.EntregadoA?.Trim();
        c.GuardaEntregaPersonaId = GetPersonaActualId();
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DevolverCorrespondenciaAsync(Guid id, DevolverCorrespondenciaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.MotivoDevolucion))
            throw new InvalidOperationException("Motivo de devolucion obligatorio.");
        var c = await _db.Correspondencias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (c.Estado == EstadoCorrespondencia.Entregado)
            throw new InvalidOperationException("Correspondencia ya entregada - no puede devolverse.");
        c.Estado = EstadoCorrespondencia.Devuelto;
        c.DevueltoAt = DateTimeOffset.UtcNow;
        c.MotivoDevolucion = req.MotivoDevolucion.Trim();
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Novedades
    // ===========================================================================

    public async Task<NovedadDto> CrearNovedadAsync(Guid turnoId, CrearNovedadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Descripcion))
            throw new InvalidOperationException("Descripcion obligatoria.");
        var tenantId = RequireTenantId();
        var turno = await _db.TurnosPorteria.FirstOrDefaultAsync(t => t.Id == turnoId, ct)
            ?? throw new InvalidOperationException("Turno no encontrado.");
        var cfg = await GetOrCreateConfigAsync(ct);
        var n = new NovedadTurno
        {
            TenantId = tenantId,
            TurnoId = turnoId,
            GuardaPersonaId = turno.GuardaPersonaId,
            Descripcion = req.Descripcion.Trim(),
            GeneraTarea = req.GeneraTarea && cfg.GeneraTareaDesdeNovedad,
            TareaId = null  // RN-14 - integracion 2.10 diferida
        };
        _db.NovedadesTurno.Add(n);
        await _db.SaveChangesAsync(ct);
        return new NovedadDto(n.Id, n.TurnoId, n.GuardaPersonaId, n.Descripcion, n.GeneraTarea, n.TareaId, n.CreatedAt);
    }

    public async Task<IReadOnlyList<NovedadDto>> ListarNovedadesAsync(Guid? turnoId, DateOnly? fecha, CancellationToken ct)
    {
        var q = _db.NovedadesTurno.AsNoTracking().AsQueryable();
        if (turnoId is { } t) q = q.Where(n => n.TurnoId == t);
        if (fecha is { } f)
        {
            var ini = new DateTimeOffset(DateTime.SpecifyKind(f.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
            var fin = new DateTimeOffset(DateTime.SpecifyKind(f.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));
            q = q.Where(n => n.CreatedAt >= ini && n.CreatedAt <= fin);
        }
        return await q.OrderByDescending(n => n.CreatedAt).Take(100)
            .Select(n => new NovedadDto(n.Id, n.TurnoId, n.GuardaPersonaId, n.Descripcion, n.GeneraTarea, n.TareaId, n.CreatedAt))
            .ToListAsync(ct);
    }

    // ===========================================================================
    // Panel monitoreo + Configuracion
    // ===========================================================================

    public async Task<PanelMonitoreoDto> GetPanelMonitoreoAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var iniHoy = new DateTimeOffset(DateTime.SpecifyKind(hoy.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
        var finHoy = new DateTimeOffset(DateTime.SpecifyKind(hoy.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));

        var turnoAct = await _db.TurnosPorteria.AsNoTracking()
            .Where(t => t.Estado == EstadoTurnoPorteria.Activo)
            .OrderByDescending(t => t.FechaApertura)
            .FirstOrDefaultAsync(ct);
        TurnoDto? turnoDto = turnoAct is null ? null : await MapTurnoAsync(turnoAct, ct);

        var ingresosHoy = await _db.RegistrosVisita.AsNoTracking()
            .CountAsync(r => r.Timestamp >= iniHoy && r.Timestamp <= finHoy && r.TipoEvento == TipoEventoAccesoPorteria.Ingreso, ct);
        var egresosHoy = await _db.RegistrosVisita.AsNoTracking()
            .CountAsync(r => r.Timestamp >= iniHoy && r.Timestamp <= finHoy && r.TipoEvento == TipoEventoAccesoPorteria.Egreso, ct);
        var vehInHoy = await _db.RegistrosVehiculo.AsNoTracking()
            .CountAsync(r => r.Timestamp >= iniHoy && r.Timestamp <= finHoy && r.TipoEvento == TipoEventoAccesoPorteria.Ingreso, ct);
        var vehOutHoy = await _db.RegistrosVehiculo.AsNoTracking()
            .CountAsync(r => r.Timestamp >= iniHoy && r.Timestamp <= finHoy && r.TipoEvento == TipoEventoAccesoPorteria.Egreso, ct);
        var paqRecHoy = await _db.Correspondencias.AsNoTracking()
            .CountAsync(c => c.RecibidoAt >= iniHoy && c.RecibidoAt <= finHoy, ct);
        var paqEntHoy = await _db.Correspondencias.AsNoTracking()
            .CountAsync(c => c.EntregadoAt >= iniHoy && c.EntregadoAt <= finHoy, ct);
        var paqPend = await _db.Correspondencias.AsNoTracking()
            .CountAsync(c => c.Estado == EstadoCorrespondencia.Recibido || c.Estado == EstadoCorrespondencia.Notificado, ct);
        var novedHoy = await _db.NovedadesTurno.AsNoTracking()
            .CountAsync(n => n.CreatedAt >= iniHoy && n.CreatedAt <= finHoy, ct);

        var resumen = new ResumenTurnoDto(ingresosHoy, egresosHoy,
            Math.Max(0, ingresosHoy - egresosHoy), Math.Max(0, vehInHoy - vehOutHoy),
            paqRecHoy, paqEntHoy, paqPend, novedHoy);

        var paqPendList = await ListarCorrespondenciaAsync(EstadoCorrespondencia.Notificado, null, ct);
        var paqRecList = await ListarCorrespondenciaAsync(EstadoCorrespondencia.Recibido, null, ct);
        var pendientes = paqPendList.Concat(paqRecList).OrderByDescending(p => p.RecibidoAt).Take(10).ToList();

        var ultimas = new List<EventoRecienteDto>();
        var visitas = await _db.RegistrosVisita.AsNoTracking()
            .Where(r => r.Timestamp >= iniHoy && r.Timestamp <= finHoy)
            .OrderByDescending(r => r.Timestamp).Take(5).ToListAsync(ct);
        foreach (var v in visitas)
            ultimas.Add(new EventoRecienteDto("VISITA", $"{v.Nombre} - {v.TipoEvento}", v.Timestamp));
        var vehs = await _db.RegistrosVehiculo.AsNoTracking()
            .Where(r => r.Timestamp >= iniHoy && r.Timestamp <= finHoy)
            .OrderByDescending(r => r.Timestamp).Take(5).ToListAsync(ct);
        foreach (var v in vehs)
            ultimas.Add(new EventoRecienteDto("VEHICULO", $"{v.Placa} - {v.TipoEvento}", v.Timestamp));

        return new PanelMonitoreoDto(turnoDto, resumen, pendientes,
            ultimas.OrderByDescending(e => e.At).Take(10).ToList());
    }

    public async Task<ConfiguracionDto> GetConfiguracionAsync(CancellationToken ct)
    {
        var cfg = await GetOrCreateConfigAsync(ct);
        return new ConfiguracionDto(cfg.DestinoVisitanteObligatorio, cfg.CanalNotificacionPaquetes,
            cfg.UmbralPaqueteAmarilloMin, cfg.UmbralPaqueteRojoMin, cfg.AutoregistroQrActivo,
            cfg.GeneraTareaDesdeNovedad, cfg.RetencionDatosVisitantesDias);
    }

    public async Task<ConfiguracionDto> ActualizarConfiguracionAsync(ActualizarConfiguracionRequest req, CancellationToken ct)
    {
        if (req.RetencionDatosVisitantesDias < 365)
            throw new InvalidOperationException("RN-08: retencion minima de datos de visitantes es 365 dias.");
        if (req.UmbralPaqueteAmarilloMin >= req.UmbralPaqueteRojoMin)
            throw new InvalidOperationException("Umbral amarillo debe ser menor al rojo.");
        var cfg = await GetOrCreateConfigAsync(ct);
        cfg.DestinoVisitanteObligatorio = req.DestinoVisitanteObligatorio;
        cfg.CanalNotificacionPaquetes = req.CanalNotificacionPaquetes;
        cfg.UmbralPaqueteAmarilloMin = req.UmbralPaqueteAmarilloMin;
        cfg.UmbralPaqueteRojoMin = req.UmbralPaqueteRojoMin;
        cfg.AutoregistroQrActivo = req.AutoregistroQrActivo;
        cfg.GeneraTareaDesdeNovedad = req.GeneraTareaDesdeNovedad;
        cfg.RetencionDatosVisitantesDias = req.RetencionDatosVisitantesDias;
        cfg.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new ConfiguracionDto(cfg.DestinoVisitanteObligatorio, cfg.CanalNotificacionPaquetes,
            cfg.UmbralPaqueteAmarilloMin, cfg.UmbralPaqueteRojoMin, cfg.AutoregistroQrActivo,
            cfg.GeneraTareaDesdeNovedad, cfg.RetencionDatosVisitantesDias);
    }
}
