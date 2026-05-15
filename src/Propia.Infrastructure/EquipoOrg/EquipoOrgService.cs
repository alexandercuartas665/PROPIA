using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.EquipoOrg;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.EquipoOrg;

/// <summary>
/// Servicio del modulo 1.3 Gestion de Equipo (spec v1.0).
///
/// Decisiones clave:
///  - La organizacion activa se deriva del Tenant activo (Tenant.OrganizacionId). El JWT
///    no trae claim organizacion_id explicito en MVP - se resuelve por la copropiedad activa.
///  - Los 6 cargos por defecto se siembran lazily la primera vez que se lista el catalogo
///    de cargos de la organizacion. Cada cargo trae su plantilla de permisos Capa 1.
///  - Permiso efectivo: si existe override individual (org_colaborador_permiso) se usa,
///    si no, se hereda el nivel de la plantilla del cargo.
///  - Historial append-only via trigger SQL (org_colaborador_historial_append_only).
/// </summary>
public class EquipoOrgService : IEquipoOrgService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<ApplicationUser> _userManager;

    public EquipoOrgService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _userManager = userManager;
    }

    // ===================== Resolucion de organizacion activa =====================

    /// <summary>Resuelve la organizacion activa para la sesion actual via Tenant activo.</summary>
    private async Task<Guid> GetOrganizacionActivaAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa en la sesion.");
        // El Tenant tiene FK a Organizacion. Bypassamos query filter porque tenant es global.
        var tenant = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("La copropiedad activa no existe.");
        if (tenant.OrganizacionId is null)
            throw new InvalidOperationException("La copropiedad no tiene organizacion administradora vinculada.");
        return tenant.OrganizacionId.Value;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private async Task<Guid?> GetPersonaActualIdAsync(CancellationToken ct)
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("persona_id");
        if (Guid.TryParse(sub, out var id)) return id;
        // Fallback - obtener via ApplicationUser
        var userId = GetUsuarioActualId();
        if (userId == Guid.Empty) return null;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.PersonaId;
    }

    // ===================== Cargos =====================

    public async Task<IReadOnlyList<CargoDto>> ListarCargosAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        await AsegurarCargosDefaultAsync(orgId, ct);

        var rows = await _db.OrgCargos.AsNoTracking()
            .Where(c => c.OrganizacionId == orgId)
            .OrderByDescending(c => c.EsDefault).ThenBy(c => c.Nombre)
            .ToListAsync(ct);
        var counts = await _db.OrgColaboradores.AsNoTracking()
            .Where(c => c.OrganizacionId == orgId && c.Estado != EstadoColaborador.Inactivo)
            .GroupBy(c => c.CargoId)
            .Select(g => new { Id = g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Cant, ct);

        return rows.Select(c => new CargoDto(c.Id, c.Nombre, c.Descripcion, c.EsDefault, c.Activo,
            counts.GetValueOrDefault(c.Id, 0))).ToList();
    }

    public async Task<CargoDetalleDto?> GetCargoDetalleAsync(Guid cargoId, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgCargos.AsNoTracking()
            .Include(x => x.Permisos)
            .FirstOrDefaultAsync(x => x.Id == cargoId && x.OrganizacionId == orgId, ct);
        if (c is null) return null;
        var cant = await _db.OrgColaboradores.CountAsync(
            x => x.CargoId == cargoId && x.Estado != EstadoColaborador.Inactivo, ct);
        var permisos = c.Permisos.Select(p => new PermisoCapa1Dto(p.Modulo, p.Nivel))
            .OrderBy(p => p.Modulo).ToList();
        return new CargoDetalleDto(c.Id, c.Nombre, c.Descripcion, c.EsDefault, c.Activo, cant, permisos);
    }

    public async Task<CargoDto> CrearCargoAsync(CrearCargoRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Nombre) || req.Nombre.Trim().Length < 2)
            throw new InvalidOperationException("El nombre del cargo debe tener minimo 2 caracteres.");

        var nombreNorm = req.Nombre.Trim();
        var dup = await _db.OrgCargos.AnyAsync(c => c.OrganizacionId == orgId && c.Nombre == nombreNorm, ct);
        if (dup) throw new InvalidOperationException("Ya existe un cargo con este nombre.");

        var cargo = new OrgCargo
        {
            OrganizacionId = orgId,
            Nombre = nombreNorm,
            Descripcion = req.Descripcion?.Trim(),
            EsDefault = false,
            Activo = true
        };
        _db.OrgCargos.Add(cargo);
        // Permisos por defecto: todo SIN_ACCESO. El director ajusta despues.
        foreach (var modulo in Enum.GetValues<ModuloCapa1>())
            _db.OrgCargoPermisos.Add(new OrgCargoPermiso
            {
                Cargo = cargo,
                Modulo = modulo,
                Nivel = NivelPermisoCapa1.SinAcceso
            });
        await _db.SaveChangesAsync(ct);
        return new CargoDto(cargo.Id, cargo.Nombre, cargo.Descripcion, false, true, 0);
    }

    public async Task<bool> ActualizarCargoAsync(Guid cargoId, ActualizarCargoRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Nombre) || req.Nombre.Trim().Length < 2)
            throw new InvalidOperationException("El nombre del cargo debe tener minimo 2 caracteres.");

        var c = await _db.OrgCargos.FirstOrDefaultAsync(
            x => x.Id == cargoId && x.OrganizacionId == orgId, ct);
        if (c is null) return false;

        var nombreNorm = req.Nombre.Trim();
        if (!string.Equals(c.Nombre, nombreNorm, StringComparison.Ordinal))
        {
            var dup = await _db.OrgCargos.AnyAsync(
                x => x.OrganizacionId == orgId && x.Nombre == nombreNorm && x.Id != cargoId, ct);
            if (dup) throw new InvalidOperationException("Ya existe un cargo con este nombre.");
            c.Nombre = nombreNorm;
        }
        c.Descripcion = req.Descripcion?.Trim();
        c.Activo = req.Activo;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCargoAsync(Guid cargoId, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgCargos.FirstOrDefaultAsync(
            x => x.Id == cargoId && x.OrganizacionId == orgId, ct);
        if (c is null) return false;

        // RN-06: bloqueo si hay colaboradores activos
        var hayActivos = await _db.OrgColaboradores.AnyAsync(
            x => x.CargoId == cargoId && x.Estado != EstadoColaborador.Inactivo, ct);
        if (hayActivos)
            throw new InvalidOperationException(
                "No puedes eliminar un cargo con colaboradores activos. Reasignalos primero.");

        _db.OrgCargos.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AjustarPermisoCargoAsync(Guid cargoId, AjustarPermisoCargoRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgCargos.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == cargoId && x.OrganizacionId == orgId, ct);
        if (c is null) return false;

        var existing = await _db.OrgCargoPermisos.FirstOrDefaultAsync(
            x => x.CargoId == cargoId && x.Modulo == req.Modulo, ct);
        if (existing is null)
        {
            _db.OrgCargoPermisos.Add(new OrgCargoPermiso
            {
                CargoId = cargoId,
                Modulo = req.Modulo,
                Nivel = req.Nivel
            });
        }
        else
        {
            existing.Nivel = req.Nivel;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Si la organizacion no tiene cargos sembrados, crea los 6 por defecto + plantilla.</summary>
    private async Task AsegurarCargosDefaultAsync(Guid orgId, CancellationToken ct)
    {
        var hay = await _db.OrgCargos.AnyAsync(c => c.OrganizacionId == orgId, ct);
        if (hay) return;
        foreach (var (nombre, permisos) in CargoCatalogoBase.PermisosPorDefecto)
        {
            var cargo = new OrgCargo
            {
                OrganizacionId = orgId,
                Nombre = nombre,
                EsDefault = true,
                Activo = true
            };
            _db.OrgCargos.Add(cargo);
            foreach (var (modulo, nivel) in permisos)
                _db.OrgCargoPermisos.Add(new OrgCargoPermiso
                {
                    Cargo = cargo,
                    Modulo = modulo,
                    Nivel = nivel
                });
        }
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Colaboradores =====================

    public async Task<IReadOnlyList<ColaboradorListaDto>> ListarColaboradoresAsync(
        EstadoColaborador? estado, string? query, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        await AsegurarCargosDefaultAsync(orgId, ct);

        IQueryable<OrgColaborador> q = _db.OrgColaboradores.AsNoTracking()
            .Where(c => c.OrganizacionId == orgId);
        if (estado.HasValue) q = q.Where(c => c.Estado == estado.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var qNorm = query.Trim().ToLower();
            q = from c in q
                join p in _db.Personas on c.PersonaId equals p.Id
                where p.Documento.ToLower().Contains(qNorm)
                    || p.Nombres.ToLower().Contains(qNorm)
                    || p.Apellidos.ToLower().Contains(qNorm)
                    || (p.Email != null && p.Email.ToLower().Contains(qNorm))
                select c;
        }

        var rows = await (
            from c in q
            join p in _db.Personas on c.PersonaId equals p.Id
            join cg in _db.OrgCargos on c.CargoId equals cg.Id
            orderby c.Estado, p.Apellidos, p.Nombres
            select new
            {
                c.Id,
                PersonaId = p.Id,
                p.TipoDocumento,
                p.Documento,
                p.Nombres,
                p.Apellidos,
                p.Email,
                p.Telefono,
                c.CargoId,
                CargoNombre = cg.Nombre,
                c.Estado,
                c.FechaVinculacion
            }
        ).ToListAsync(ct);

        // Conteo de asignaciones activas por colaborador
        var ids = rows.Select(r => r.Id).ToList();
        var asignaciones = await _db.OrgColaboradorCopropiedades.AsNoTracking()
            .Where(a => ids.Contains(a.ColaboradorId) && a.Activo)
            .GroupBy(a => a.ColaboradorId)
            .Select(g => new { Id = g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Cant, ct);

        return rows.Select(r => new ColaboradorListaDto(
            r.Id, r.PersonaId, r.Nombres, r.Apellidos, r.TipoDocumento, r.Documento,
            r.Email, r.Telefono, r.CargoId, r.CargoNombre, r.Estado, r.FechaVinculacion,
            asignaciones.GetValueOrDefault(r.Id, 0))).ToList();
    }

    public async Task<ColaboradorDetalleDto?> GetColaboradorAsync(Guid colaboradorId, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgColaboradores.AsNoTracking()
            .Include(x => x.Persona)
            .Include(x => x.Cargo)!.ThenInclude(x => x!.Permisos)
            .Include(x => x.PermisosIndividuales)
            .FirstOrDefaultAsync(x => x.Id == colaboradorId && x.OrganizacionId == orgId, ct);
        if (c is null) return null;

        var asignaciones = await (
            from a in _db.OrgColaboradorCopropiedades.AsNoTracking().Where(a => a.ColaboradorId == c.Id && a.Activo)
            join t in _db.Tenants.IgnoreQueryFilters() on a.TenantId equals t.Id
            join r in _db.RolesCopropiedad.IgnoreQueryFilters() on a.RolCapa2Id equals r.Id
            orderby t.Nombre
            select new AsignacionCopropiedadDto(
                a.Id, t.Id, t.Nombre, t.CodigoPropia, r.Id, r.Nombre, a.FechaDesde)
        ).ToListAsync(ct);

        var permisosEf = CalcularPermisosEfectivos(c).ToList();

        var historial = await _db.OrgColaboradorHistorial.AsNoTracking()
            .Where(h => h.ColaboradorId == c.Id)
            .OrderByDescending(h => h.OcurridoAt)
            .Take(50)
            .Select(h => new EventoHistorialDto(h.TipoEvento, h.Descripcion, h.RealizadoPor, h.OcurridoAt))
            .ToListAsync(ct);

        return new ColaboradorDetalleDto(
            c.Id, c.PersonaId, c.Persona!.Nombres, c.Persona.Apellidos,
            c.Persona.TipoDocumento, c.Persona.Documento, c.Persona.Email, c.Persona.Telefono,
            c.CargoId, c.Cargo!.Nombre, c.Estado, c.FechaVinculacion, c.FechaDesvinculacion,
            asignaciones, permisosEf, historial);
    }

    private static IEnumerable<PermisoCapa1EfectivoDto> CalcularPermisosEfectivos(OrgColaborador c)
    {
        var plantilla = c.Cargo?.Permisos.ToDictionary(p => p.Modulo, p => p.Nivel)
            ?? new Dictionary<ModuloCapa1, NivelPermisoCapa1>();
        var overrides = c.PermisosIndividuales.ToDictionary(p => p.Modulo, p => p.Nivel);
        foreach (var modulo in Enum.GetValues<ModuloCapa1>())
        {
            var nivelCargo = plantilla.GetValueOrDefault(modulo, NivelPermisoCapa1.SinAcceso);
            var tieneOverride = overrides.TryGetValue(modulo, out var ov);
            var efectivo = tieneOverride ? ov : nivelCargo;
            yield return new PermisoCapa1EfectivoDto(
                modulo, efectivo, nivelCargo, tieneOverride ? ov : null, tieneOverride);
        }
    }

    public async Task<BusquedaIdentidadDto?> BuscarIdentidadAsync(
        string? documento, TipoDocumento? tipoDocumento, string? email, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        if (string.IsNullOrWhiteSpace(documento) && string.IsNullOrWhiteSpace(email))
            return null;

        IQueryable<Persona> q = _db.Personas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(documento))
        {
            var docNorm = documento.Trim();
            q = tipoDocumento.HasValue
                ? q.Where(p => p.Documento == docNorm && p.TipoDocumento == tipoDocumento.Value)
                : q.Where(p => p.Documento == docNorm);
        }
        else
        {
            var em = email!.Trim();
            q = q.Where(p => p.Email != null && p.Email.ToLower() == em.ToLower());
        }
        var p2 = await q.FirstOrDefaultAsync(ct);
        if (p2 is null) return null;

        var yaVinculada = await _db.OrgColaboradores.AnyAsync(
            x => x.OrganizacionId == orgId && x.PersonaId == p2.Id, ct);

        return new BusquedaIdentidadDto(
            p2.Id, p2.TipoDocumento, p2.Documento, p2.Nombres, p2.Apellidos,
            p2.Email, p2.Telefono, yaVinculada);
    }

    public async Task<ColaboradorDetalleDto> AgregarColaboradorAsync(
        AgregarColaboradorRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        await AsegurarCargosDefaultAsync(orgId, ct);

        // Validar cargo
        var cargo = await _db.OrgCargos.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == req.CargoId && x.OrganizacionId == orgId, ct)
            ?? throw new InvalidOperationException("Cargo invalido.");

        // Resolver persona (identidad unica RN-01)
        Persona? persona;
        if (req.PersonaIdExistente.HasValue)
        {
            persona = await _db.Personas.FirstOrDefaultAsync(p => p.Id == req.PersonaIdExistente.Value, ct)
                ?? throw new InvalidOperationException("La persona indicada no existe.");
        }
        else
        {
            // Crear nueva persona
            if (string.IsNullOrWhiteSpace(req.Documento) || req.TipoDocumento is null
                || string.IsNullOrWhiteSpace(req.Nombres) || string.IsNullOrWhiteSpace(req.Apellidos)
                || string.IsNullOrWhiteSpace(req.Email))
                throw new InvalidOperationException(
                    "Datos minimos requeridos: TipoDocumento, Documento, Nombres, Apellidos, Email.");

            // Doble check de identidad unica antes de crear
            var existePorDoc = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(
                p => p.TipoDocumento == req.TipoDocumento.Value && p.Documento == req.Documento.Trim(), ct);
            if (existePorDoc is not null)
                throw new InvalidOperationException(
                    "Ya existe una persona con este documento. Usa PersonaIdExistente para vincular.");

            var existePorEmail = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(
                p => p.Email != null && p.Email.ToLower() == req.Email.Trim().ToLower(), ct);
            if (existePorEmail is not null)
                throw new InvalidOperationException(
                    "Ya existe una persona con este email. Usa PersonaIdExistente para vincular.");

            persona = new Persona
            {
                TipoDocumento = req.TipoDocumento.Value,
                Documento = req.Documento.Trim(),
                Nombres = req.Nombres.Trim(),
                Apellidos = req.Apellidos.Trim(),
                Email = req.Email.Trim(),
                Telefono = req.Telefono?.Trim(),
                PerfilIncompleto = false
            };
            _db.Personas.Add(persona);
        }

        // RN-01: si la persona ya esta vinculada a esta organizacion, no duplicar
        var yaVinculado = await _db.OrgColaboradores.FirstOrDefaultAsync(
            x => x.OrganizacionId == orgId && x.PersonaId == persona.Id, ct);
        if (yaVinculado is not null)
        {
            if (yaVinculado.Estado == EstadoColaborador.Inactivo)
                throw new InvalidOperationException(
                    "Esta persona fue colaborador y esta inactiva. Usa /reactivar en su lugar.");
            throw new InvalidOperationException(
                "Esta persona ya esta vinculada al equipo de esta organizacion.");
        }

        // Estado inicial: si persona ya tenia ApplicationUser => Activo, si no => Pendiente
        var tieneUsuario = await _db.Users.AnyAsync(u => u.PersonaId == persona.Id, ct);
        var estadoInicial = tieneUsuario ? EstadoColaborador.Activo : EstadoColaborador.Pendiente;

        var colab = new OrgColaborador
        {
            OrganizacionId = orgId,
            PersonaId = persona.Id,
            CargoId = cargo.Id,
            Estado = estadoInicial,
            FechaVinculacion = DateOnly.FromDateTime(DateTime.UtcNow),
            InvitadoPor = GetUsuarioActualId()
        };
        _db.OrgColaboradores.Add(colab);
        await _db.SaveChangesAsync(ct);

        // Asignaciones a copropiedades
        var asignacionesAprocesar = new List<(Guid TenantId, Guid RolId)>();
        if (req.AsignarATodas && req.RolCapa2ParaTodas.HasValue)
        {
            var tenantsOrg = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.OrganizacionId == orgId && t.Estado == EstadoCopropiedad.Activa)
                .Select(t => t.Id)
                .ToListAsync(ct);
            foreach (var t in tenantsOrg)
                asignacionesAprocesar.Add((t, req.RolCapa2ParaTodas.Value));
        }
        if (req.Asignaciones is not null)
        {
            foreach (var a in req.Asignaciones)
                asignacionesAprocesar.Add((a.TenantId, a.RolCapa2Id));
        }

        foreach (var (tenantId, rolId) in asignacionesAprocesar.DistinctBy(x => x.TenantId))
        {
            await CrearAsignacionInternaAsync(colab, tenantId, rolId, orgId, ct);
        }

        // Historial Vinculacion
        await RegistrarHistorialAsync(colab.Id, TipoEventoEquipo.Vinculacion,
            $"Vinculado al equipo como {cargo.Nombre}",
            valorNuevo: new { cargo = cargo.Nombre, estado = estadoInicial.ToString() }, ct: ct);
        await _db.SaveChangesAsync(ct);

        return (await GetColaboradorAsync(colab.Id, ct))!;
    }

    private async Task CrearAsignacionInternaAsync(
        OrgColaborador colab, Guid tenantId, Guid rolId, Guid orgId, CancellationToken ct)
    {
        // Validar que el tenant pertenece a la organizacion
        var tenantOk = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.OrganizacionId == orgId, ct);
        if (!tenantOk)
            throw new InvalidOperationException(
                "La copropiedad indicada no pertenece a esta organizacion.");

        // Validar que el rol pertenece a esa copropiedad o es global (TenantId null)
        var rol = await _db.RolesCopropiedad.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rolId, ct)
            ?? throw new InvalidOperationException("Rol Capa 2 invalido.");
        if (rol.TenantId.HasValue && rol.TenantId.Value != tenantId)
            throw new InvalidOperationException(
                "El rol Capa 2 indicado no pertenece a la copropiedad seleccionada.");

        // Duplicado?
        var dup = await _db.OrgColaboradorCopropiedades.AnyAsync(
            a => a.ColaboradorId == colab.Id && a.TenantId == tenantId, ct);
        if (dup)
            throw new InvalidOperationException(
                "El colaborador ya tiene una asignacion activa a esta copropiedad.");

        _db.OrgColaboradorCopropiedades.Add(new OrgColaboradorCopropiedad
        {
            ColaboradorId = colab.Id,
            TenantId = tenantId,
            RolCapa2Id = rolId,
            FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow),
            Activo = true
        });

        await RegistrarHistorialAsync(colab.Id, TipoEventoEquipo.PhAsignada,
            $"Asignado a copropiedad con rol {rol.Nombre}",
            valorNuevo: new { tenantId, rolId, rol = rol.Nombre }, ct: ct);
    }

    public async Task<bool> CambiarCargoAsync(Guid colaboradorId, CambiarCargoRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgColaboradores.FirstOrDefaultAsync(
            x => x.Id == colaboradorId && x.OrganizacionId == orgId, ct);
        if (c is null) return false;
        var nuevo = await _db.OrgCargos.FirstOrDefaultAsync(
            x => x.Id == req.CargoId && x.OrganizacionId == orgId, ct)
            ?? throw new InvalidOperationException("Cargo invalido.");

        if (c.CargoId == nuevo.Id) return true;

        var anterior = await _db.OrgCargos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.CargoId, ct);
        c.CargoId = nuevo.Id;
        c.UpdatedAt = DateTimeOffset.UtcNow;

        if (req.ResetearPermisos)
        {
            var overrides = _db.OrgColaboradorPermisos.Where(p => p.ColaboradorId == c.Id);
            _db.OrgColaboradorPermisos.RemoveRange(overrides);
        }

        await RegistrarHistorialAsync(c.Id, TipoEventoEquipo.CambioCargo,
            $"Cargo cambiado a {nuevo.Nombre}",
            valorAnterior: anterior is null ? null : new { cargo = anterior.Nombre },
            valorNuevo: new { cargo = nuevo.Nombre, resetearPermisos = req.ResetearPermisos }, ct: ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AjustarPermisoColaboradorAsync(
        Guid colaboradorId, AjustarPermisoColaboradorRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgColaboradores.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == colaboradorId && x.OrganizacionId == orgId, ct);
        if (c is null) return false;

        var existing = await _db.OrgColaboradorPermisos.FirstOrDefaultAsync(
            x => x.ColaboradorId == colaboradorId && x.Modulo == req.Modulo, ct);
        if (existing is null)
        {
            _db.OrgColaboradorPermisos.Add(new OrgColaboradorPermiso
            {
                ColaboradorId = colaboradorId,
                Modulo = req.Modulo,
                Nivel = req.Nivel
            });
        }
        else
        {
            existing.Nivel = req.Nivel;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await RegistrarHistorialAsync(c.Id, TipoEventoEquipo.PermisoAjustado,
            $"Permiso individual ajustado: {req.Modulo} = {req.Nivel}",
            valorNuevo: new { modulo = req.Modulo.ToString(), nivel = req.Nivel.ToString() }, ct: ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ResetearPermisosColaboradorAsync(Guid colaboradorId, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgColaboradores.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == colaboradorId && x.OrganizacionId == orgId, ct);
        if (c is null) return false;

        var overrides = _db.OrgColaboradorPermisos.Where(p => p.ColaboradorId == colaboradorId);
        _db.OrgColaboradorPermisos.RemoveRange(overrides);
        await RegistrarHistorialAsync(c.Id, TipoEventoEquipo.PermisoAjustado,
            "Permisos individuales reseteados a la plantilla del cargo", ct: ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesactivarColaboradorAsync(
        Guid colaboradorId, DesactivarColaboradorRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgColaboradores.FirstOrDefaultAsync(
            x => x.Id == colaboradorId && x.OrganizacionId == orgId, ct);
        if (c is null) return false;
        if (c.Estado == EstadoColaborador.Inactivo) return true;

        // RN-04: revocar todas las asignaciones activas (acceso inmediato)
        var asignaciones = await _db.OrgColaboradorCopropiedades.Where(
            a => a.ColaboradorId == colaboradorId && a.Activo).ToListAsync(ct);
        foreach (var a in asignaciones)
        {
            a.Activo = false;
            a.FechaHasta = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        // Reasignacion opcional a otro colaborador
        if (req.ReasignarA.HasValue && req.ReasignarA.Value != colaboradorId)
        {
            var nuevoOwner = await _db.OrgColaboradores.AsNoTracking().FirstOrDefaultAsync(
                x => x.Id == req.ReasignarA.Value && x.OrganizacionId == orgId
                  && x.Estado == EstadoColaborador.Activo, ct)
                ?? throw new InvalidOperationException("El colaborador destino no esta activo en esta organizacion.");

            foreach (var a in asignaciones)
            {
                var dup = await _db.OrgColaboradorCopropiedades.AnyAsync(
                    x => x.ColaboradorId == nuevoOwner.Id && x.TenantId == a.TenantId && x.Activo, ct);
                if (!dup)
                {
                    _db.OrgColaboradorCopropiedades.Add(new OrgColaboradorCopropiedad
                    {
                        ColaboradorId = nuevoOwner.Id,
                        TenantId = a.TenantId,
                        RolCapa2Id = a.RolCapa2Id,
                        FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow),
                        Activo = true
                    });
                }
            }
        }

        c.Estado = EstadoColaborador.Inactivo;
        c.FechaDesvinculacion = DateOnly.FromDateTime(DateTime.UtcNow);
        c.UpdatedAt = DateTimeOffset.UtcNow;

        await RegistrarHistorialAsync(c.Id, TipoEventoEquipo.Desvinculacion,
            string.IsNullOrWhiteSpace(req.Motivo)
                ? "Colaborador desvinculado"
                : $"Colaborador desvinculado. Motivo: {req.Motivo}",
            valorNuevo: new { estado = "Inactivo", reasignadoA = req.ReasignarA }, ct: ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReactivarColaboradorAsync(Guid colaboradorId, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgColaboradores.FirstOrDefaultAsync(
            x => x.Id == colaboradorId && x.OrganizacionId == orgId, ct);
        if (c is null) return false;
        if (c.Estado != EstadoColaborador.Inactivo) return true;

        var tieneUsuario = await _db.Users.AnyAsync(u => u.PersonaId == c.PersonaId, ct);
        c.Estado = tieneUsuario ? EstadoColaborador.Activo : EstadoColaborador.Pendiente;
        c.FechaDesvinculacion = null;
        c.UpdatedAt = DateTimeOffset.UtcNow;

        await RegistrarHistorialAsync(c.Id, TipoEventoEquipo.EstadoCambiado,
            "Colaborador reactivado", valorNuevo: new { estado = c.Estado.ToString() }, ct: ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Asignaciones =====================

    public async Task<AsignacionCopropiedadDto> AgregarAsignacionAsync(
        Guid colaboradorId, AgregarAsignacionRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var c = await _db.OrgColaboradores.FirstOrDefaultAsync(
            x => x.Id == colaboradorId && x.OrganizacionId == orgId, ct)
            ?? throw new InvalidOperationException("Colaborador no encontrado.");

        await CrearAsignacionInternaAsync(c, req.TenantId, req.RolCapa2Id, orgId, ct);
        await _db.SaveChangesAsync(ct);

        var creado = await _db.OrgColaboradorCopropiedades.AsNoTracking()
            .Where(a => a.ColaboradorId == colaboradorId && a.TenantId == req.TenantId && a.Activo)
            .OrderByDescending(a => a.CreatedAt).FirstAsync(ct);

        var tNombre = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Id == req.TenantId).Select(t => new { t.Nombre, t.CodigoPropia }).FirstAsync(ct);
        var rNombre = await _db.RolesCopropiedad.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.Id == req.RolCapa2Id).Select(r => r.Nombre).FirstAsync(ct);
        return new AsignacionCopropiedadDto(
            creado.Id, req.TenantId, tNombre.Nombre, tNombre.CodigoPropia,
            req.RolCapa2Id, rNombre, creado.FechaDesde);
    }

    public async Task<bool> CambiarRolPhAsync(Guid asignacionId, CambiarRolPhRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var a = await _db.OrgColaboradorCopropiedades.FirstOrDefaultAsync(x => x.Id == asignacionId, ct);
        if (a is null) return false;

        // Asegurar que pertenece a la organizacion
        var perteneceOrg = await _db.OrgColaboradores.AnyAsync(
            x => x.Id == a.ColaboradorId && x.OrganizacionId == orgId, ct);
        if (!perteneceOrg) return false;

        var rol = await _db.RolesCopropiedad.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == req.RolCapa2Id, ct)
            ?? throw new InvalidOperationException("Rol Capa 2 invalido.");
        if (rol.TenantId.HasValue && rol.TenantId.Value != a.TenantId)
            throw new InvalidOperationException("El rol no pertenece a la copropiedad de esta asignacion.");

        a.RolCapa2Id = req.RolCapa2Id;
        a.UpdatedAt = DateTimeOffset.UtcNow;

        await RegistrarHistorialAsync(a.ColaboradorId, TipoEventoEquipo.RolPhCambiado,
            $"Rol Capa 2 cambiado a {rol.Nombre} en la copropiedad",
            valorNuevo: new { tenantId = a.TenantId, rolId = req.RolCapa2Id }, ct: ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> QuitarAsignacionAsync(Guid asignacionId, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var a = await _db.OrgColaboradorCopropiedades.FirstOrDefaultAsync(x => x.Id == asignacionId, ct);
        if (a is null) return false;
        var perteneceOrg = await _db.OrgColaboradores.AnyAsync(
            x => x.Id == a.ColaboradorId && x.OrganizacionId == orgId, ct);
        if (!perteneceOrg) return false;

        a.Activo = false;
        a.FechaHasta = DateOnly.FromDateTime(DateTime.UtcNow);
        a.UpdatedAt = DateTimeOffset.UtcNow;

        await RegistrarHistorialAsync(a.ColaboradorId, TipoEventoEquipo.PhRemovida,
            "Asignacion a copropiedad removida",
            valorAnterior: new { tenantId = a.TenantId, rolId = a.RolCapa2Id }, ct: ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Vista del colaborador =====================

    public async Task<ColaboradorDetalleDto?> GetMiPerfilAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var personaId = await GetPersonaActualIdAsync(ct);
        if (personaId is null) return null;
        var colab = await _db.OrgColaboradores.AsNoTracking().FirstOrDefaultAsync(
            x => x.OrganizacionId == orgId && x.PersonaId == personaId.Value, ct);
        return colab is null ? null : await GetColaboradorAsync(colab.Id, ct);
    }

    public async Task<IReadOnlyList<ColaboradorListaDto>> ListarCompanerosAsync(CancellationToken ct)
    {
        // Misma logica que ListarColaboradoresAsync pero forzando estado Activo (RN-11)
        return await ListarColaboradoresAsync(EstadoColaborador.Activo, null, ct);
    }

    // ===================== Helpers =====================

    private async Task RegistrarHistorialAsync(
        Guid colaboradorId, TipoEventoEquipo tipo, string descripcion,
        object? valorAnterior = null, object? valorNuevo = null, CancellationToken ct = default)
    {
        _db.OrgColaboradorHistorial.Add(new OrgColaboradorHistorial
        {
            ColaboradorId = colaboradorId,
            TipoEvento = tipo,
            Descripcion = descripcion.Length > 300 ? descripcion[..300] : descripcion,
            ValorAnterior = valorAnterior is null ? null : JsonSerializer.Serialize(valorAnterior),
            ValorNuevo = valorNuevo is null ? null : JsonSerializer.Serialize(valorNuevo),
            RealizadoPor = GetUsuarioActualId(),
            OcurridoAt = DateTimeOffset.UtcNow
        });
        await Task.CompletedTask;
    }
}
