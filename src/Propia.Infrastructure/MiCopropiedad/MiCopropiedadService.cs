using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

public class MiCopropiedadService : IMiCopropiedadService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    public MiCopropiedadService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

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
        var miembrosEquipoActivos = await _db.MiembrosEquipo.CountAsync(m => m.Activo, ct);
        var comitesActivos = await _db.Comites.CountAsync(c => c.Activo, ct);
        var hayRevisorFiscal = await _db.RevisoresFiscales.AnyAsync(r => r.Activo, ct);

        // Heuristicas de completitud por seccion (spec 2.3 v1.0)
        var completas = new Dictionary<string, bool>
        {
            ["Identidad"] = !string.IsNullOrWhiteSpace(t.Nombre)
                            && !string.IsNullOrWhiteSpace(t.Nit)
                            && !string.IsNullOrWhiteSpace(t.Direccion)
                            && t.TipoCopropiedad.HasValue,
            ["Distribucion"] = torres > 0 && unidades > 0 && Math.Abs(coefSum - 100m) <= 1m,
            ["EquipoTrabajo"] = miembrosEquipoActivos > 0,
            // Gobierno completo: consejo con minimo 3 activos + revisor fiscal (si la PH lo requiere) + al menos un comite
            ["Gobierno"] = miembros >= 3
                           && (unidades <= 30 || hayRevisorFiscal)
                           && comitesActivos > 0,
            ["Servicios"] = contratos > 0,
            ["ZonasComunes"] = zonas > 0,
            ["Equipos"] = equipos > 0,
            ["Finanzas"] = t.FinanzasConfiguradas  // el admin guardo los parametros de la seccion 8
        };
        var pct = (int)(completas.Values.Count(b => b) * 100.0 / completas.Count);

        return new ResumenMiCopropiedadDto(
            ToIdentidadDto(t),
            torres, unidades, coefSum, zonas, equipos, contratos, miembros,
            pct, completas);
    }

    private static IdentidadDto ToIdentidadDto(Tenant t) =>
        new(t.Id, t.Nombre, t.Nit, t.DigitoVerificacion,
            t.Direccion, t.Ciudad, t.Departamento,
            t.CodigoPropia, t.TipoCopropiedad, t.Estrato,
            t.FotoFachadaUrl, t.LogoUrl, t.Descripcion,
            t.NumeroReglamentoPh, t.NotariaRegistro,
            t.MatriculaInmobiliaria, t.LicenciaConstruccion,
            t.FechaConstitucion,
            t.LabelAgrupacion, t.LabelPiso,
            t.TelefonoContacto, t.EmailContacto,
            t.Pais);

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
        t.Pais = string.IsNullOrWhiteSpace(req.Pais) ? null : req.Pais.Trim();
        t.TipoCopropiedad = req.Tipo;
        t.Estrato = req.Estrato;
        t.FotoFachadaUrl = req.FotoFachadaUrl;
        t.LogoUrl = req.LogoUrl;
        t.Descripcion = req.Descripcion;
        // Identidad registral
        t.NumeroReglamentoPh = req.NumeroReglamentoPh;
        t.NotariaRegistro = req.NotariaRegistro;
        t.MatriculaInmobiliaria = req.MatriculaInmobiliaria;
        t.LicenciaConstruccion = req.LicenciaConstruccion;
        t.FechaConstitucion = req.FechaConstitucion;
        // Labels personalizables
        t.LabelAgrupacion = string.IsNullOrWhiteSpace(req.LabelAgrupacion) ? null : req.LabelAgrupacion.Trim();
        t.LabelPiso = string.IsNullOrWhiteSpace(req.LabelPiso) ? null : req.LabelPiso.Trim();
        t.TelefonoContacto = string.IsNullOrWhiteSpace(req.TelefonoContacto) ? null : req.TelefonoContacto.Trim();
        t.EmailContacto = string.IsNullOrWhiteSpace(req.EmailContacto) ? null : req.EmailContacto.Trim();
        await _db.SaveChangesAsync(ct);

        return ToIdentidadDto(t);
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
                u.Estado, u.Observaciones, u.MatriculaInmobiliaria, u.PagaAdministracion))
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
            Observaciones = req.Observaciones,
            MatriculaInmobiliaria = req.MatriculaInmobiliaria,
            PagaAdministracion = req.PagaAdministracion
        };
        _db.UnidadesPrivadas.Add(unidad);
        await _db.SaveChangesAsync(ct);
        var torreNombre = unidad.TorreId.HasValue
            ? await _db.Torres.Where(t => t.Id == unidad.TorreId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
            : null;
        await RegistrarBitacoraAsync("Unidad", $"Unidad '{unidad.Numero}' creada ({unidad.Tipo}, coef {unidad.CoeficientePropiedad}%).", ct);
        return new UnidadDto(unidad.Id, unidad.Numero, unidad.Tipo,
            unidad.TorreId, torreNombre, unidad.Piso,
            unidad.CoeficientePropiedad, unidad.AreaM2,
            unidad.Habitaciones, unidad.Banos, unidad.Parqueaderos,
            unidad.Estado, unidad.Observaciones, unidad.MatriculaInmobiliaria, unidad.PagaAdministracion);
    }

    public async Task<UnidadDto?> ActualizarUnidadAsync(Guid unidadId, ActualizarUnidadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Numero))
            throw new InvalidOperationException("Numero de unidad obligatorio.");
        if (req.CoeficientePropiedad < 0 || req.CoeficientePropiedad > 100)
            throw new InvalidOperationException("Coeficiente debe estar entre 0 y 100.");

        var u = await _db.UnidadesPrivadas.FirstOrDefaultAsync(x => x.Id == unidadId, ct);
        if (u is null) return null;

        u.Numero = req.Numero.Trim();
        u.Tipo = req.Tipo;
        u.TorreId = req.TorreId;
        u.Piso = req.Piso;
        u.CoeficientePropiedad = req.CoeficientePropiedad;
        u.AreaM2 = req.AreaM2;
        u.Habitaciones = req.Habitaciones;
        u.Banos = req.Banos;
        u.Parqueaderos = req.Parqueaderos;
        u.Estado = req.Estado;
        u.Observaciones = req.Observaciones;
        u.MatriculaInmobiliaria = req.MatriculaInmobiliaria;
        u.PagaAdministracion = req.PagaAdministracion;

        await _db.SaveChangesAsync(ct);

        var torreNombre = u.TorreId.HasValue
            ? await _db.Torres.Where(t => t.Id == u.TorreId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
            : null;

        await RegistrarBitacoraAsync("Unidad", $"Unidad '{u.Numero}' actualizada.", ct);

        return new UnidadDto(u.Id, u.Numero, u.Tipo,
            u.TorreId, torreNombre, u.Piso,
            u.CoeficientePropiedad, u.AreaM2,
            u.Habitaciones, u.Banos, u.Parqueaderos,
            u.Estado, u.Observaciones, u.MatriculaInmobiliaria, u.PagaAdministracion);
    }

    // ----------------------------- Vinculos entre unidades (RN-09) -----------------------------

    public async Task<IReadOnlyList<UnidadVinculoDto>> ListVinculosAsync(Guid unidadPrincipalId, CancellationToken ct)
    {
        return await _db.UnidadVinculos
            .AsNoTracking()
            .Include(v => v.UnidadAsociada)
            .Where(v => v.UnidadPrincipalId == unidadPrincipalId)
            .OrderBy(v => v.UnidadAsociada!.Numero)
            .Select(v => new UnidadVinculoDto(v.Id, v.UnidadAsociadaId,
                v.UnidadAsociada!.Numero, v.UnidadAsociada.Tipo, v.IncluyeEnFacturacion))
            .ToListAsync(ct);
    }

    public async Task<UnidadVinculoDto> CrearVinculoAsync(Guid unidadPrincipalId, CrearVinculoUnidadRequest req, CancellationToken ct)
    {
        if (unidadPrincipalId == req.UnidadAsociadaId)
            throw new InvalidOperationException("Una unidad no puede asociarse a si misma.");

        var principal = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == unidadPrincipalId, ct)
            ?? throw new InvalidOperationException("Unidad principal no encontrada.");
        var asociada = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == req.UnidadAsociadaId, ct)
            ?? throw new InvalidOperationException("Unidad asociada no encontrada.");

        // RN-09 (no circular): la asociada no puede ser, a su vez, principal de la unidad principal,
        // y una asociada solo puede tener un principal.
        if (await _db.UnidadVinculos.AnyAsync(v => v.UnidadAsociadaId == req.UnidadAsociadaId, ct))
            throw new InvalidOperationException($"La unidad {asociada.Numero} ya esta asociada a otra unidad.");
        if (await _db.UnidadVinculos.AnyAsync(v => v.UnidadPrincipalId == req.UnidadAsociadaId && v.UnidadAsociadaId == unidadPrincipalId, ct))
            throw new InvalidOperationException("Vinculo circular no permitido (RN-09).");

        var v = new UnidadVinculo
        {
            UnidadPrincipalId = unidadPrincipalId,
            UnidadAsociadaId = req.UnidadAsociadaId,
            IncluyeEnFacturacion = req.IncluyeEnFacturacion
        };
        _db.UnidadVinculos.Add(v);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Unidad", $"{asociada.Tipo} '{asociada.Numero}' vinculado a unidad '{principal.Numero}' ({(req.IncluyeEnFacturacion ? "factura" : "no factura")}).", ct);
        return new UnidadVinculoDto(v.Id, v.UnidadAsociadaId, asociada.Numero, asociada.Tipo, v.IncluyeEnFacturacion);
    }

    public async Task<bool> EliminarVinculoAsync(Guid vinculoId, CancellationToken ct)
    {
        var v = await _db.UnidadVinculos.FirstOrDefaultAsync(x => x.Id == vinculoId, ct);
        if (v is null) return false;
        _db.UnidadVinculos.Remove(v);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Personas vinculadas a una unidad -----------------------------

    public async Task<IReadOnlyList<UnidadPersonaDto>> ListPersonasUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        return await (from up in _db.UnidadPersonas.AsNoTracking()
                      join p in _db.Personas on up.PersonaId equals p.Id
                      where up.UnidadId == unidadId
                      orderby up.Rol, p.Apellidos, p.Nombres
                      select new UnidadPersonaDto(
                          up.Id, up.PersonaId,
                          (p.Nombres + " " + p.Apellidos).Trim(),
                          p.Documento, p.Email, p.Telefono,
                          up.Rol, up.Habita, up.Parentesco))
                      .ToListAsync(ct);
    }

    public async Task<UnidadPersonaDto> AgregarPersonaUnidadAsync(Guid unidadId, AgregarPersonaUnidadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Documento)) throw new InvalidOperationException("Documento obligatorio.");
        if (string.IsNullOrWhiteSpace(req.Nombres)) throw new InvalidOperationException("Nombres obligatorios.");
        if (string.IsNullOrWhiteSpace(req.Apellidos)) throw new InvalidOperationException("Apellidos obligatorios.");

        var unidad = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == unidadId, ct)
            ?? throw new InvalidOperationException("Unidad no encontrada.");

        // Reusa el helper que busca o crea Persona por documento (ya existente)
        var personaId = await VincularPersonaPorDocumentoAsync(
            new VincularPersonaPorDocumentoRequest(req.Documento, req.Nombres, req.Apellidos, req.Email, req.Telefono), ct);

        // Evita duplicar mismo (unidad + persona + rol)
        var existente = await _db.UnidadPersonas
            .FirstOrDefaultAsync(x => x.UnidadId == unidadId && x.PersonaId == personaId && x.Rol == req.Rol, ct);
        if (existente is not null)
            throw new InvalidOperationException($"Esta persona ya es {req.Rol} de la unidad {unidad.Numero}.");

        var up = new UnidadPersona
        {
            UnidadId = unidadId,
            PersonaId = personaId,
            Rol = req.Rol,
            Habita = req.Habita,
            Parentesco = string.IsNullOrWhiteSpace(req.Parentesco) ? null : req.Parentesco.Trim()
        };
        _db.UnidadPersonas.Add(up);
        await _db.SaveChangesAsync(ct);

        var persona = await _db.Personas.AsNoTracking().FirstAsync(p => p.Id == personaId, ct);
        await RegistrarBitacoraAsync("Unidad", $"{req.Rol} '{persona.Nombres} {persona.Apellidos}' vinculado a unidad '{unidad.Numero}'.", ct);

        return new UnidadPersonaDto(up.Id, personaId,
            ($"{persona.Nombres} {persona.Apellidos}").Trim(), persona.Documento, persona.Email, persona.Telefono,
            up.Rol, up.Habita, up.Parentesco);
    }

    public async Task<bool> EliminarPersonaUnidadAsync(Guid unidadPersonaId, CancellationToken ct)
    {
        var up = await _db.UnidadPersonas.FirstOrDefaultAsync(x => x.Id == unidadPersonaId, ct);
        if (up is null) return false;
        _db.UnidadPersonas.Remove(up);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Rebalanceo de coeficientes (Ley 675 art. 26: suma = 100) -----------------------------

    public async Task<RebalanceoCoeficientesDto> RebalancearCoeficientesAsync(CancellationToken ct)
    {
        var unidades = await _db.UnidadesPrivadas.ToListAsync(ct);
        var sumaAnterior = unidades.Sum(u => u.CoeficientePropiedad);

        // Estrategia: solo unidades con PagaAdministracion=true reciben coeficiente > 0.
        // Las que no pagan (parqueadero, deposito) van a 0. Distribucion equitativa con
        // compensacion en la ultima para que el total sea exactamente 100.0000.
        var pagan = unidades.Where(u => u.PagaAdministracion).OrderBy(u => u.Numero).ToList();
        var noPagan = unidades.Where(u => !u.PagaAdministracion).ToList();

        if (pagan.Count == 0)
        {
            // Nada que rebalancear, dejar todas en 0
            foreach (var u in unidades) u.CoeficientePropiedad = 0m;
            await _db.SaveChangesAsync(ct);
            return new RebalanceoCoeficientesDto(unidades.Count, sumaAnterior, 0m, 0m, 0, noPagan.Count);
        }

        var coefBase = Math.Round(100m / pagan.Count, 4, MidpointRounding.ToZero);
        decimal acumulado = 0m;
        for (int i = 0; i < pagan.Count; i++)
        {
            if (i == pagan.Count - 1)
            {
                // Compensacion en la ultima para evitar floating drift y forzar suma=100
                pagan[i].CoeficientePropiedad = Math.Round(100m - acumulado, 4);
            }
            else
            {
                pagan[i].CoeficientePropiedad = coefBase;
                acumulado += coefBase;
            }
        }
        foreach (var u in noPagan) u.CoeficientePropiedad = 0m;

        await _db.SaveChangesAsync(ct);
        var sumaNueva = unidades.Sum(u => u.CoeficientePropiedad);
        await RegistrarBitacoraAsync("Distribucion",
            $"Coeficientes rebalanceados: {pagan.Count} apartamentos a {coefBase}% (ultima ajustada), {noPagan.Count} en 0%. Suma anterior {sumaAnterior:0.##}% -> nueva {sumaNueva:0.##}%.", ct);
        return new RebalanceoCoeficientesDto(unidades.Count, sumaAnterior, sumaNueva, coefBase, pagan.Count, noPagan.Count);
    }

    // ----------------------------- Cuota consolidada (principal + asociadas con factura) -----------------------------

    public async Task<CuotaConsolidadaDto?> GetCuotaConsolidadaAsync(Guid unidadId, CancellationToken ct)
    {
        var principal = await _db.UnidadesPrivadas.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unidadId, ct);
        if (principal is null) return null;

        var asociadas = await _db.UnidadVinculos.AsNoTracking()
            .Include(v => v.UnidadAsociada)
            .Where(v => v.UnidadPrincipalId == unidadId)
            .ToListAsync(ct);

        var coefAsociadasFactura = asociadas
            .Where(v => v.IncluyeEnFacturacion)
            .Sum(v => v.UnidadAsociada!.CoeficientePropiedad);

        return new CuotaConsolidadaDto(
            principal.Id, principal.Numero,
            principal.CoeficientePropiedad,
            coefAsociadasFactura,
            principal.CoeficientePropiedad + coefAsociadasFactura,
            asociadas.Count,
            asociadas.Count(v => v.IncluyeEnFacturacion));
    }

    public async Task<UnidadDto?> ObtenerUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        return await _db.UnidadesPrivadas
            .AsNoTracking()
            .Include(u => u.Torre)
            .Where(u => u.Id == unidadId)
            .Select(u => new UnidadDto(
                u.Id, u.Numero, u.Tipo,
                u.TorreId, u.Torre != null ? u.Torre.Nombre : null, u.Piso,
                u.CoeficientePropiedad, u.AreaM2,
                u.Habitaciones, u.Banos, u.Parqueaderos,
                u.Estado, u.Observaciones, u.MatriculaInmobiliaria, u.PagaAdministracion))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TipoUnidadCustomDto>> ListTiposUnidadCustomAsync(CancellationToken ct)
    {
        return await _db.TiposUnidadCustom
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoUnidadCustomDto(t.Id, t.Nombre, t.PagaAdministracionPorDefecto, t.Descripcion, t.Activo))
            .ToListAsync(ct);
    }

    public async Task<TipoUnidadCustomDto> CrearTipoUnidadCustomAsync(CrearTipoUnidadCustomRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del tipo es obligatorio.");
        var nombre = req.Nombre.Trim();
        if (await _db.TiposUnidadCustom.AnyAsync(t => t.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un tipo de unidad llamado '{nombre}' en esta copropiedad.");

        var t = new TipoUnidadCustom
        {
            Nombre = nombre,
            PagaAdministracionPorDefecto = req.PagaAdministracionPorDefecto,
            Descripcion = req.Descripcion,
            Activo = true
        };
        _db.TiposUnidadCustom.Add(t);
        await _db.SaveChangesAsync(ct);
        return new TipoUnidadCustomDto(t.Id, t.Nombre, t.PagaAdministracionPorDefecto, t.Descripcion, t.Activo);
    }

    public async Task<bool> EliminarTipoUnidadCustomAsync(Guid tipoId, CancellationToken ct)
    {
        var t = await _db.TiposUnidadCustom.FirstOrDefaultAsync(x => x.Id == tipoId, ct);
        if (t is null) return false;
        _db.TiposUnidadCustom.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Tipos de coeficiente (B2) -----------------------------

    public async Task<IReadOnlyList<TipoCoeficienteDto>> ListTiposCoeficienteAsync(CancellationToken ct)
    {
        // Si no hay ninguno, sembramos el default "Propiedad" (lazy init).
        var existen = await _db.TiposCoeficiente.AnyAsync(ct);
        if (!existen)
        {
            _db.TiposCoeficiente.Add(new TipoCoeficiente
            {
                Nombre = "Propiedad",
                Descripcion = "Coeficiente principal de la PH segun Ley 675",
                EsPrincipal = true,
                Activo = true
            });
            await _db.SaveChangesAsync(ct);
        }

        var tipos = await _db.TiposCoeficiente
            .AsNoTracking()
            .OrderByDescending(t => t.EsPrincipal).ThenBy(t => t.Nombre)
            .ToListAsync(ct);

        // Calcular suma actual por tipo (incluyendo el campo legacy CoeficientePropiedad para el tipo principal).
        var sumas = await _db.UnidadCoeficientes
            .AsNoTracking()
            .GroupBy(uc => uc.TipoCoeficienteId)
            .Select(g => new { TipoId = g.Key, Suma = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.TipoId, x => x.Suma, ct);

        var sumaLegacyPropiedad = await _db.UnidadesPrivadas.SumAsync(u => (decimal?)u.CoeficientePropiedad, ct) ?? 0m;

        return tipos.Select(t =>
        {
            var suma = sumas.GetValueOrDefault(t.Id, 0m);
            if (t.EsPrincipal) suma += sumaLegacyPropiedad - (sumas.GetValueOrDefault(t.Id, 0m) > 0 ? 0m : 0m);
            // Para principal: si hay valores en UnidadCoeficientes los usamos; si no, caemos al legacy.
            // Simplificacion: para el tipo principal sumamos LO QUE HAYA en UnidadCoeficientes;
            // si no hay registros (suma == 0), exponemos el legacy.
            var sumaReal = t.EsPrincipal && sumas.GetValueOrDefault(t.Id, 0m) == 0m
                ? sumaLegacyPropiedad
                : sumas.GetValueOrDefault(t.Id, 0m);
            return new TipoCoeficienteDto(t.Id, t.Nombre, t.Descripcion, t.EsPrincipal, t.Activo, sumaReal);
        }).ToList();
    }

    public async Task<TipoCoeficienteDto> CrearTipoCoeficienteAsync(CrearTipoCoeficienteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del tipo de coeficiente es obligatorio.");
        var nombre = req.Nombre.Trim();
        if (await _db.TiposCoeficiente.AnyAsync(x => x.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un tipo de coeficiente '{nombre}'.");

        var t = new TipoCoeficiente
        {
            Nombre = nombre,
            Descripcion = req.Descripcion,
            EsPrincipal = false,
            Activo = true
        };
        _db.TiposCoeficiente.Add(t);
        await _db.SaveChangesAsync(ct);
        return new TipoCoeficienteDto(t.Id, t.Nombre, t.Descripcion, t.EsPrincipal, t.Activo, 0m);
    }

    public async Task<bool> EliminarTipoCoeficienteAsync(Guid tipoId, CancellationToken ct)
    {
        var t = await _db.TiposCoeficiente.FirstOrDefaultAsync(x => x.Id == tipoId, ct);
        if (t is null) return false;
        if (t.EsPrincipal)
            throw new InvalidOperationException("No se puede eliminar el tipo principal de coeficiente.");

        // Cascade eliminara UnidadCoeficientes asociados via FK
        _db.TiposCoeficiente.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<UnidadCoeficienteDto>> ListCoeficientesUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        return await _db.UnidadCoeficientes
            .AsNoTracking()
            .Include(uc => uc.TipoCoeficiente)
            .Where(uc => uc.UnidadId == unidadId)
            .Select(uc => new UnidadCoeficienteDto(uc.TipoCoeficienteId, uc.TipoCoeficiente!.Nombre, uc.Valor))
            .ToListAsync(ct);
    }

    public async Task<UnidadCoeficienteDto> SetCoeficienteUnidadAsync(Guid unidadId, SetCoeficienteUnidadRequest req, CancellationToken ct)
    {
        if (req.Valor < 0)
            throw new InvalidOperationException("El coeficiente debe ser >= 0.");

        var unidad = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == unidadId, ct)
            ?? throw new InvalidOperationException("Unidad no encontrada.");
        var tipo = await _db.TiposCoeficiente.FirstOrDefaultAsync(t => t.Id == req.TipoCoeficienteId, ct)
            ?? throw new InvalidOperationException("Tipo de coeficiente no encontrado.");

        var existente = await _db.UnidadCoeficientes
            .FirstOrDefaultAsync(uc => uc.UnidadId == unidadId && uc.TipoCoeficienteId == req.TipoCoeficienteId, ct);

        if (existente is null)
        {
            existente = new UnidadCoeficiente
            {
                UnidadId = unidadId,
                TipoCoeficienteId = req.TipoCoeficienteId,
                Valor = req.Valor
            };
            _db.UnidadCoeficientes.Add(existente);
        }
        else
        {
            existente.Valor = req.Valor;
        }

        // Si es el tipo principal, mantenemos el campo legacy de la unidad sincronizado
        if (tipo.EsPrincipal)
            unidad.CoeficientePropiedad = req.Valor;

        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Coeficiente",
            $"Unidad '{unidad.Numero}': coeficiente '{tipo.Nombre}' = {req.Valor:0.####}%.", ct);
        return new UnidadCoeficienteDto(req.TipoCoeficienteId, tipo.Nombre, req.Valor);
    }

    public async Task<GenerarUnidadesResponse> GenerarUnidadesAsync(GenerarUnidadesRequest req, CancellationToken ct)
    {
        if (req.Torres is null || req.Torres.Count == 0)
            throw new InvalidOperationException("Debes definir al menos una torre/agrupacion.");
        if (req.CoeficientePorUnidad < 0)
            throw new InvalidOperationException("Coeficiente por unidad no puede ser negativo.");

        var torresCreadas = new List<Torre>();
        var unidadesCreadas = new List<UnidadPrivada>();
        var torresExistentes = await _db.Torres.CountAsync(ct);
        var unidadesExistentesNumeros = await _db.UnidadesPrivadas.Select(u => u.Numero).ToListAsync(ct);
        var unidadesGeneradas = new HashSet<string>(unidadesExistentesNumeros, StringComparer.OrdinalIgnoreCase);

        // Si hay torres pre-existentes o se piden multiples torres nuevas, usamos
        // prefijo de indice de torre para garantizar unicidad global de identificadores.
        var usarPrefijoIdx = req.Torres.Count > 1 || torresExistentes > 0;
        var corridoSeq = unidadesExistentesNumeros.Count + 1;
        var torreIdx = torresExistentes;

        foreach (var spec in req.Torres)
        {
            torreIdx++;
            if (string.IsNullOrWhiteSpace(spec.Nombre))
                throw new InvalidOperationException("Cada torre debe tener nombre.");
            if (spec.CantidadPisos <= 0 || spec.UnidadesPorPiso <= 0)
                throw new InvalidOperationException($"Torre '{spec.Nombre}': pisos y unidades por piso deben ser > 0.");
            if (await _db.Torres.AnyAsync(t => t.Nombre == spec.Nombre, ct))
                throw new InvalidOperationException($"Ya existe una torre llamada '{spec.Nombre}' en esta copropiedad.");

            var torre = new Torre { Nombre = spec.Nombre.Trim(), CantidadPisos = spec.CantidadPisos };
            _db.Torres.Add(torre);
            torresCreadas.Add(torre);

            for (var piso = 1; piso <= spec.CantidadPisos; piso++)
            {
                for (var n = 1; n <= spec.UnidadesPorPiso; n++)
                {
                    // PisoNumero: si ya hay torres en la copropiedad o se piden multiples,
                    // prefijamos con el indice de torre (Torre 1 piso 1 #1 -> 1101) para
                    // garantizar unicidad global. Con 1 sola torre y tenant vacio: 101, 102 (spec).
                    string numero;
                    if (req.Patron == PatronNumeracion.Corrido)
                    {
                        numero = corridoSeq.ToString();
                        corridoSeq++;
                    }
                    else
                    {
                        numero = usarPrefijoIdx ? $"{torreIdx}{piso}{n:D2}" : $"{piso}{n:D2}";
                    }
                    if (!unidadesGeneradas.Add(numero))
                        throw new InvalidOperationException(
                            $"Colision de identificador '{numero}'. Otra unidad ya tiene ese numero. " +
                            "Renombra la torre o cambia el patron de numeracion.");
                    var unidad = new UnidadPrivada
                    {
                        Numero = numero,
                        Tipo = req.TipoUnidadDefault,
                        Torre = torre,
                        Piso = piso,
                        CoeficientePropiedad = req.CoeficientePorUnidad
                    };
                    _db.UnidadesPrivadas.Add(unidad);
                    unidadesCreadas.Add(unidad);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        var nombresTorre = string.Join(", ", torresCreadas.Select(t => t.Nombre));
        await RegistrarBitacoraAsync("Distribucion",
            $"Generador automatico: {torresCreadas.Count} torre(s) [{nombresTorre}] + {unidadesCreadas.Count} unidades creadas (coef {req.CoeficientePorUnidad}% c/u).", ct);
        return new GenerarUnidadesResponse(
            torresCreadas.Count,
            unidadesCreadas.Count,
            torresCreadas.Select(t => t.Id).ToList(),
            unidadesCreadas.Select(u => u.Id).ToList());
    }

    public async Task<ImportarUnidadesResponse> ImportarUnidadesCsvAsync(ImportarUnidadesRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CsvContent))
            return new ImportarUnidadesResponse(false, 0, 0, 0m, new List<ImportacionFilaError>
            {
                new(0, "CSV", "El archivo viene vacio.")
            });

        // Formato esperado (header en fila 1):
        //   identificador,tipo_unidad,agrupacion,piso,coeficiente,area_m2,paga_administracion
        // Solo identificador, tipo_unidad y coeficiente son obligatorios.

        var lineas = req.CsvContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var errores = new List<ImportacionFilaError>();
        var filas = new List<(int Linea, string Numero, TipoUnidad Tipo, string? Agrupacion, int? Piso, decimal Coef, decimal? Area)>();

        if (lineas.Length < 2)
        {
            errores.Add(new(0, "CSV", "Necesitas al menos un encabezado y una fila de datos."));
            return new ImportarUnidadesResponse(false, 0, 0, 0m, errores);
        }

        var header = lineas[0].Split(',').Select(c => c.Trim().ToLowerInvariant()).ToList();
        int idxId = header.IndexOf("identificador");
        int idxTipo = header.IndexOf("tipo_unidad");
        int idxAgr = header.IndexOf("agrupacion");
        int idxPiso = header.IndexOf("piso");
        int idxCoef = header.IndexOf("coeficiente");
        int idxArea = header.IndexOf("area_m2");

        if (idxId < 0 || idxTipo < 0 || idxCoef < 0)
        {
            errores.Add(new(1, "header", "Faltan columnas obligatorias: identificador, tipo_unidad, coeficiente."));
            return new ImportarUnidadesResponse(false, 0, 0, 0m, errores);
        }

        var numerosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal suma = 0m;

        for (int i = 1; i < lineas.Length; i++)
        {
            var linea = lineas[i];
            if (string.IsNullOrWhiteSpace(linea)) continue;
            var celdas = linea.Split(',');
            var nroFila = i + 1;

            string numero = celdas.Length > idxId ? celdas[idxId].Trim() : "";
            string tipoTxt = celdas.Length > idxTipo ? celdas[idxTipo].Trim() : "";
            string coefTxt = celdas.Length > idxCoef ? celdas[idxCoef].Trim() : "";

            if (string.IsNullOrEmpty(numero))
            {
                errores.Add(new(nroFila, "identificador", "Campo obligatorio."));
                continue;
            }
            if (!numerosVistos.Add(numero))
            {
                errores.Add(new(nroFila, "identificador", $"Identificador '{numero}' aparece duplicado en el archivo."));
            }
            if (!Enum.TryParse<TipoUnidad>(tipoTxt, ignoreCase: true, out var tipo))
            {
                errores.Add(new(nroFila, "tipo_unidad", $"Tipo '{tipoTxt}' no esta en el catalogo base."));
                continue;
            }
            if (!decimal.TryParse(coefTxt, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var coef) || coef < 0)
            {
                errores.Add(new(nroFila, "coeficiente", $"Formato invalido. Usa decimal positivo (ej. 0.023400)."));
                continue;
            }

            string? agrupacion = idxAgr >= 0 && celdas.Length > idxAgr ? celdas[idxAgr].Trim() : null;
            if (string.IsNullOrWhiteSpace(agrupacion)) agrupacion = null;

            int? piso = null;
            if (idxPiso >= 0 && celdas.Length > idxPiso && int.TryParse(celdas[idxPiso].Trim(), out var p)) piso = p;

            decimal? area = null;
            if (idxArea >= 0 && celdas.Length > idxArea && decimal.TryParse(celdas[idxArea].Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var a)) area = a;

            suma += coef;
            filas.Add((nroFila, numero, tipo, agrupacion, piso, coef, area));
        }

        if (errores.Count > 0)
            return new ImportarUnidadesResponse(false, filas.Count, 0, suma, errores);

        // Identificadores duplicados contra BD
        var numerosArchivo = filas.Select(f => f.Numero).ToList();
        var duplicados = await _db.UnidadesPrivadas
            .Where(u => numerosArchivo.Contains(u.Numero))
            .Select(u => u.Numero)
            .ToListAsync(ct);
        foreach (var dup in duplicados)
            errores.Add(new(0, "identificador", $"El identificador '{dup}' ya existe en la copropiedad."));
        if (errores.Count > 0)
            return new ImportarUnidadesResponse(false, filas.Count, 0, suma, errores);

        // Crear torres faltantes
        var nombresTorres = filas.Where(f => f.Agrupacion is not null).Select(f => f.Agrupacion!).Distinct().ToList();
        var torresExistentes = await _db.Torres
            .Where(t => nombresTorres.Contains(t.Nombre))
            .ToDictionaryAsync(t => t.Nombre, t => t, ct);
        foreach (var nombreTorre in nombresTorres.Where(n => !torresExistentes.ContainsKey(n)))
        {
            var nuevaTorre = new Torre { Nombre = nombreTorre };
            _db.Torres.Add(nuevaTorre);
            torresExistentes[nombreTorre] = nuevaTorre;
        }

        foreach (var f in filas)
        {
            var torreId = f.Agrupacion is not null ? torresExistentes[f.Agrupacion].Id : (Guid?)null;
            var torre = f.Agrupacion is not null ? torresExistentes[f.Agrupacion] : null;
            _db.UnidadesPrivadas.Add(new UnidadPrivada
            {
                Numero = f.Numero,
                Tipo = f.Tipo,
                Piso = f.Piso,
                CoeficientePropiedad = f.Coef,
                AreaM2 = f.Area,
                Torre = torre  // EF asigna torreId al guardar la torre nueva
            });
        }
        await _db.SaveChangesAsync(ct);
        return new ImportarUnidadesResponse(true, filas.Count, filas.Count, suma, Array.Empty<ImportacionFilaError>());
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
        var contratos = await _db.ContratosServicio
            .AsNoTracking()
            .OrderBy(c => c.Tipo)
            .ToListAsync(ct);
        return contratos.Select(ToContratoDto).ToList();
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
            Observaciones = req.Observaciones,
            DiasAnticipacionAlerta = req.DiasAnticipacionAlerta <= 0 ? 30 : req.DiasAnticipacionAlerta
        };
        _db.ContratosServicio.Add(c);
        await _db.SaveChangesAsync(ct);
        return ToContratoDto(c);
    }

    public async Task<bool> ActualizarContratoAsync(Guid contratoId, ActualizarContratoRequest req, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        // "Vencido" se deriva por fecha; el admin solo declara Vigente o EnRenovacion.
        c.Estado = req.Estado == EstadoContrato.Vencido ? EstadoContrato.Vigente : req.Estado;
        c.DiasAnticipacionAlerta = req.DiasAnticipacionAlerta <= 0 ? 30 : req.DiasAnticipacionAlerta;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static ContratoServicioDto ToContratoDto(ContratoServicio c)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        int? dias = c.FechaFin.HasValue ? c.FechaFin.Value.DayNumber - hoy.DayNumber : null;
        var estado = (c.FechaFin.HasValue && c.FechaFin.Value < hoy) ? EstadoContrato.Vencido : c.Estado;
        var alerta = dias is >= 0 && dias <= c.DiasAnticipacionAlerta;
        return new ContratoServicioDto(c.Id, c.Tipo, c.Proveedor, c.NitProveedor, c.Contacto,
            c.FechaInicio, c.FechaFin, c.ValorMensual, c.Observaciones,
            estado, c.DiasAnticipacionAlerta, dias, alerta);
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
        e.Marca, e.Modelo, e.NumeroSerie, e.FechaInstalacion, e.GarantiaHasta,
        e.Ubicacion, e.Observaciones,
        e.VidaUtilAnios, e.FechaAdquisicion, e.ValorAdquisicion,
        e.Proveedor, e.NumeroFactura, e.Estado);

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
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Ficha tecnica completa del equipo/activo -----------------------------

    public async Task<EquipoFichaDto?> GetEquipoFichaAsync(Guid equipoId, CancellationToken ct)
    {
        var e = await _db.EquiposActivos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == equipoId, ct);
        if (e is null) return null;

        var fotos = await _db.EquipoFotos.AsNoTracking().Where(f => f.EquipoActivoId == equipoId)
            .Select(f => new EquipoFotoDto(f.Id, f.Url)).ToListAsync(ct);

        var mejoras = await _db.EquipoMejoras.AsNoTracking().Where(m => m.EquipoActivoId == equipoId)
            .OrderByDescending(m => m.Fecha)
            .Select(m => new EquipoMejoraDto(m.Id, m.Descripcion, m.Valor, m.Fecha, m.DocumentoUrl)).ToListAsync(ct);

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
            ToEquipoActivoDto(e), depreciacion, fotos, mejoras,
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
        return new EquipoFotoDto(f.Id, f.Url);
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
        return new EquipoMejoraDto(m.Id, m.Descripcion, m.Valor, m.Fecha, m.DocumentoUrl);
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

    // ----------------------------- Seccion 4 ampliada: Comites + Revisor Fiscal -----------------------------

    public async Task<IReadOnlyList<ComiteDto>> ListComitesAsync(CancellationToken ct)
    {
        return await _db.Comites
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new ComiteDto(c.Id, c.Nombre, c.Descripcion, c.FechaConformacion, c.Activo,
                _db.ComiteMiembros.Count(m => m.ComiteId == c.Id && m.Activo)))
            .ToListAsync(ct);
    }

    public async Task<ComiteDto> CrearComiteAsync(CrearComiteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del comite es obligatorio.");
        var nombre = req.Nombre.Trim();
        if (await _db.Comites.AnyAsync(x => x.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un comite llamado '{nombre}'.");

        var c = new Comite
        {
            Nombre = nombre,
            Descripcion = req.Descripcion,
            FechaConformacion = req.FechaConformacion,
            Activo = true
        };
        _db.Comites.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ComiteDto(c.Id, c.Nombre, c.Descripcion, c.FechaConformacion, c.Activo, 0);
    }

    public async Task<bool> DesactivarComiteAsync(Guid comiteId, CancellationToken ct)
    {
        var c = await _db.Comites.FirstOrDefaultAsync(x => x.Id == comiteId, ct);
        if (c is null) return false;
        c.Activo = false;
        c.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ComiteMiembroDto>> ListMiembrosComiteAsync(Guid comiteId, CancellationToken ct)
    {
        return await _db.ComiteMiembros
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.ComiteId == comiteId)
            .Select(m => new ComiteMiembroDto(m.Id, m.ComiteId, m.PersonaId,
                m.Persona!.Nombres + " " + m.Persona.Apellidos, m.CargoEnComite, m.Activo))
            .ToListAsync(ct);
    }

    public async Task<ComiteMiembroDto> AgregarMiembroComiteAsync(AgregarComiteMiembroRequest req, CancellationToken ct)
    {
        var existe = await _db.Comites.AnyAsync(c => c.Id == req.ComiteId, ct);
        if (!existe) throw new InvalidOperationException("Comite no encontrado.");
        var personaOk = await _db.Personas.AnyAsync(p => p.Id == req.PersonaId, ct);
        if (!personaOk) throw new InvalidOperationException("Persona no encontrada.");
        if (await _db.ComiteMiembros.AnyAsync(m => m.ComiteId == req.ComiteId && m.PersonaId == req.PersonaId, ct))
            throw new InvalidOperationException("Esta persona ya esta en el comite.");

        var m = new ComiteMiembro
        {
            ComiteId = req.ComiteId,
            PersonaId = req.PersonaId,
            CargoEnComite = req.CargoEnComite,
            Activo = true
        };
        _db.ComiteMiembros.Add(m);
        await _db.SaveChangesAsync(ct);
        var persona = await _db.Personas.FirstAsync(p => p.Id == req.PersonaId, ct);
        return new ComiteMiembroDto(m.Id, m.ComiteId, m.PersonaId, $"{persona.Nombres} {persona.Apellidos}", m.CargoEnComite, m.Activo);
    }

    public async Task<bool> RetirarMiembroComiteAsync(Guid miembroId, CancellationToken ct)
    {
        var m = await _db.ComiteMiembros.FirstOrDefaultAsync(x => x.Id == miembroId, ct);
        if (m is null) return false;
        m.Activo = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RevisorFiscalDto?> GetRevisorFiscalActivoAsync(CancellationToken ct)
    {
        return await _db.RevisoresFiscales
            .AsNoTracking()
            .Include(r => r.Persona)
            .Where(r => r.Activo)
            .OrderByDescending(r => r.FechaPosesion)
            .Select(r => new RevisorFiscalDto(r.Id, r.PersonaId,
                r.Persona!.Nombres + " " + r.Persona.Apellidos,
                r.NumeroTarjetaProfesional, r.FechaPosesion, r.FechaFin, r.Activo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RevisorFiscalDto> DesignarRevisorFiscalAsync(DesignarRevisorFiscalRequest req, CancellationToken ct)
    {
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Id == req.PersonaId, ct)
            ?? throw new InvalidOperationException("Persona no encontrada.");

        // Retira cualquier revisor previo activo
        var previos = await _db.RevisoresFiscales.Where(r => r.Activo).ToListAsync(ct);
        foreach (var prev in previos)
        {
            prev.Activo = false;
            prev.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var r = new RevisorFiscal
        {
            PersonaId = req.PersonaId,
            NumeroTarjetaProfesional = req.NumeroTarjetaProfesional,
            FechaPosesion = req.FechaPosesion,
            Activo = true
        };
        _db.RevisoresFiscales.Add(r);
        await _db.SaveChangesAsync(ct);
        return new RevisorFiscalDto(r.Id, r.PersonaId, $"{persona.Nombres} {persona.Apellidos}",
            r.NumeroTarjetaProfesional, r.FechaPosesion, r.FechaFin, r.Activo);
    }

    public async Task<bool> RetirarRevisorFiscalAsync(Guid revisorId, CancellationToken ct)
    {
        var r = await _db.RevisoresFiscales.FirstOrDefaultAsync(x => x.Id == revisorId, ct);
        if (r is null) return false;
        r.Activo = false;
        r.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Seccion 3: Equipo de trabajo -----------------------------

    public async Task<IReadOnlyList<MiembroEquipoDto>> ListEquipoAsync(CancellationToken ct)
    {
        return await _db.MiembrosEquipo
            .AsNoTracking()
            .Include(m => m.Persona)
            .OrderByDescending(m => m.Activo).ThenBy(m => m.Rol).ThenBy(m => m.Persona!.Apellidos)
            .Select(m => new MiembroEquipoDto(m.Id, m.PersonaId,
                m.Persona!.Nombres + " " + m.Persona.Apellidos,
                m.Rol, m.RolPersonalizado, m.Tipo,
                m.FechaVinculacion, m.FechaFin, m.Activo, m.EsUsuarioSistema,
                m.Telefono, m.Email))
            .ToListAsync(ct);
    }

    public async Task<MiembroEquipoDto> AgregarMiembroEquipoAsync(AgregarMiembroEquipoRequest req, CancellationToken ct)
    {
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Id == req.PersonaId, ct)
            ?? throw new InvalidOperationException("Persona no encontrada. Usa /vincular-persona para crearla.");

        var m = new MiembroEquipo
        {
            PersonaId = req.PersonaId,
            Rol = req.Rol,
            RolPersonalizado = req.Rol == RolEquipo.Otro ? req.RolPersonalizado?.Trim() : null,
            Tipo = req.Tipo,
            FechaVinculacion = req.FechaVinculacion,
            Activo = true,
            Telefono = req.Telefono,
            Email = req.Email,
            Observaciones = req.Observaciones
        };
        _db.MiembrosEquipo.Add(m);
        await _db.SaveChangesAsync(ct);
        return new MiembroEquipoDto(m.Id, m.PersonaId, $"{persona.Nombres} {persona.Apellidos}",
            m.Rol, m.RolPersonalizado, m.Tipo,
            m.FechaVinculacion, m.FechaFin, m.Activo, m.EsUsuarioSistema,
            m.Telefono, m.Email);
    }

    public async Task<bool> DesactivarMiembroEquipoAsync(Guid miembroId, CancellationToken ct)
    {
        var m = await _db.MiembrosEquipo.FirstOrDefaultAsync(x => x.Id == miembroId, ct);
        if (m is null) return false;
        m.Activo = false;
        m.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Guid> VincularPersonaPorDocumentoAsync(VincularPersonaPorDocumentoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Documento))
            throw new InvalidOperationException("Documento es obligatorio.");
        var doc = req.Documento.Trim();

        var existente = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Documento == doc, ct);
        if (existente is not null) return existente.Id;

        if (string.IsNullOrWhiteSpace(req.Nombres) || string.IsNullOrWhiteSpace(req.Apellidos))
            throw new InvalidOperationException("Nombres y apellidos son obligatorios al crear una persona nueva.");

        var nueva = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = doc,
            Nombres = req.Nombres.Trim(),
            Apellidos = req.Apellidos.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Telefono = req.Telefono
        };
        _db.Personas.Add(nueva);
        await _db.SaveChangesAsync(ct);
        return nueva.Id;
    }

    // ----------------------------- Seccion 8: Finanzas -----------------------------

    // Maximo legal de la tasa de mora MENSUAL (placeholder configurable). La spec preve
    // actualizarlo desde la Superfinanciera - diferido. Sirve para validar la tasa fija (RN-18).
    private const decimal TasaMoraMaximaLegalMensual = 2.5m;

    // Catalogo de monedas (ISO 4217) estatico - reference data fija.
    private static readonly IReadOnlyList<MonedaDto> _monedas = new List<MonedaDto>
    {
        new("COP", "Peso colombiano", "$"),
        new("USD", "Dolar estadounidense", "US$"),
        new("EUR", "Euro", "EUR"),
        new("MXN", "Peso mexicano", "MX$"),
        new("PEN", "Sol peruano", "S/"),
        new("CLP", "Peso chileno", "CLP$"),
        new("ARS", "Peso argentino", "AR$"),
        new("BRL", "Real brasileno", "R$"),
    };

    public IReadOnlyList<MonedaDto> ListMonedas() => _monedas;

    public async Task<FinanzasParametrosDto> GetFinanzasParametrosAsync(Guid tenantId, CancellationToken ct)
    {
        var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");
        return ToFinanzasParametros(t);
    }

    public async Task<FinanzasParametrosDto> ActualizarFinanzasAsync(Guid tenantId, ActualizarFinanzasRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");

        var moneda = (req.Moneda ?? "").Trim().ToUpperInvariant();
        if (!_monedas.Any(m => m.Codigo == moneda))
            throw new InvalidOperationException("Moneda invalida. Usa un codigo del catalogo (ISO 4217).");

        if (req.DiaCorte < 1 || req.DiaCorte > 28)
            throw new InvalidOperationException("El dia de corte debe estar entre 1 y 28 (RN-17).");

        if (req.PeriodoGraciaDias < 0 || req.PeriodoGraciaDias > 30)
            throw new InvalidOperationException("El periodo de gracia debe estar entre 0 y 30 dias.");

        if (!req.TasaMoraEsLegal)
        {
            if (req.TasaMoraValor is null || req.TasaMoraValor < 0)
                throw new InvalidOperationException("Ingresa una tasa de mora valida.");
            if (req.TasaMoraValor > TasaMoraMaximaLegalMensual)
                throw new InvalidOperationException(
                    $"La tasa fija ({req.TasaMoraValor:0.##}%) supera el maximo legal mensual permitido ({TasaMoraMaximaLegalMensual:0.##}%) (RN-18).");
        }

        // Valores previos para la bitacora (RN-06)
        var monedaPrev = t.Moneda;
        var cortePrev = t.DiaCorte;

        t.Moneda = moneda;
        t.DiaCorte = req.DiaCorte;
        t.TasaMoraEsLegal = req.TasaMoraEsLegal;
        t.TasaMoraValor = req.TasaMoraEsLegal ? null : req.TasaMoraValor;
        t.PeriodoGraciaDias = req.PeriodoGraciaDias;
        t.FinanzasConfiguradas = true;
        await _db.SaveChangesAsync(ct);

        if (monedaPrev != moneda)
            await RegistrarBitacoraAsync("Finanzas", $"Cambio de moneda de {monedaPrev} a {moneda}.", ct);
        if (cortePrev != req.DiaCorte)
            await RegistrarBitacoraAsync("Finanzas", $"Cambio del dia de corte de {cortePrev} a {req.DiaCorte}.", ct);

        return ToFinanzasParametros(t);
    }

    private static FinanzasParametrosDto ToFinanzasParametros(Tenant t) =>
        new(t.Moneda, t.DiaCorte, t.TasaMoraEsLegal, t.TasaMoraValor, t.PeriodoGraciaDias,
            t.FinanzasConfiguradas, TasaMoraMaximaLegalMensual);

    // ----------------------------- Configuracion avanzada de Finanzas -----------------------------

    public async Task<ConfiguracionFinanzasDto> GetConfiguracionFinanzasAsync(Guid tenantId, CancellationToken ct)
    {
        var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");
        return ToConfiguracionFinanzas(t);
    }

    public async Task<ConfiguracionFinanzasDto> ActualizarConfiguracionFinanzasAsync(Guid tenantId, ActualizarConfiguracionFinanzasRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Copropiedad no encontrada.");

        if (req.MinimoSaldoProntoPago < 0 || req.MinimoSaldoCartera < 0)
            throw new InvalidOperationException("Los minimos de saldo no pueden ser negativos.");
        if (req.EstratoFacturacion is < 1 or > 6)
            throw new InvalidOperationException("El estrato debe estar entre 1 y 6.");

        t.MultiploRedondeo = req.MultiploRedondeo;
        t.MultiploRedondeoCuotaExtra = req.MultiploRedondeoCuotaExtra;
        t.MultiploRedondeoProntoPago = req.MultiploRedondeoProntoPago;
        t.ConsecutivoFactura = NormalizarConsec(req.ConsecutivoFactura);
        t.ConsecutivoRC = NormalizarConsec(req.ConsecutivoRC);
        t.ConsecutivoNotaCredito = NormalizarConsec(req.ConsecutivoNotaCredito);
        t.ConsecutivoPazYSalvo = NormalizarConsec(req.ConsecutivoPazYSalvo);
        t.ConsecutivoActaConsejo = NormalizarConsec(req.ConsecutivoActaConsejo);
        t.ConsecutivoActaAsamblea = NormalizarConsec(req.ConsecutivoActaAsamblea);
        t.ConvenioRecaudo = string.IsNullOrWhiteSpace(req.ConvenioRecaudo) ? null : req.ConvenioRecaudo.Trim();
        t.Chartld = string.IsNullOrWhiteSpace(req.Chartld) ? null : req.Chartld.Trim();
        t.ComunicacionFactura = string.IsNullOrWhiteSpace(req.ComunicacionFactura) ? null : req.ComunicacionFactura.Trim();
        t.WenjoyCodigoRecaudo = string.IsNullOrWhiteSpace(req.WenjoyCodigoRecaudo) ? null : req.WenjoyCodigoRecaudo.Trim();
        t.TiposPagoPermitidos = req.TiposPagoPermitidos;
        t.FormasDePago = string.IsNullOrWhiteSpace(req.FormasDePago) ? null : req.FormasDePago.Trim();
        t.MinimoSaldoProntoPago = req.MinimoSaldoProntoPago;
        t.MinimoSaldoCartera = req.MinimoSaldoCartera;
        t.CuentaContable = string.IsNullOrWhiteSpace(req.CuentaContable) ? null : req.CuentaContable.Trim();
        t.ZonaFacturacion = string.IsNullOrWhiteSpace(req.ZonaFacturacion) ? null : req.ZonaFacturacion.Trim();
        t.EstratoFacturacion = req.EstratoFacturacion;
        await _db.SaveChangesAsync(ct);

        await RegistrarBitacoraAsync("Finanzas", "Configuracion avanzada actualizada (mas informacion).", ct);
        return ToConfiguracionFinanzas(t);
    }

    /// <summary>Normaliza un consecutivo (texto libre: admite prefijos tipo "FAC-001", "RC-2026-0042").
    /// Vacio o solo espacios se guarda como null para que el front muestre placeholder.</summary>
    private static string? NormalizarConsec(string? v)
        => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static ConfiguracionFinanzasDto ToConfiguracionFinanzas(Tenant t) => new(
        t.MultiploRedondeo, t.MultiploRedondeoCuotaExtra, t.MultiploRedondeoProntoPago,
        t.ConsecutivoFactura, t.ConsecutivoRC, t.ConsecutivoNotaCredito, t.ConsecutivoPazYSalvo,
        t.ConsecutivoActaConsejo, t.ConsecutivoActaAsamblea,
        t.ConvenioRecaudo, t.Chartld, t.ComunicacionFactura, t.WenjoyCodigoRecaudo,
        t.TiposPagoPermitidos, t.FormasDePago,
        t.MinimoSaldoProntoPago, t.MinimoSaldoCartera,
        t.CuentaContable, t.ZonaFacturacion, t.EstratoFacturacion);

    // ----------------------------- Cuentas bancarias -----------------------------

    public async Task<IReadOnlyList<CuentaBancariaDto>> ListCuentasBancariasAsync(CancellationToken ct)
    {
        return await _db.CuentasBancarias.AsNoTracking()
            .OrderBy(c => c.Cancelada)
            .ThenBy(c => c.Banco)
            .Select(c => new CuentaBancariaDto(c.Id, c.NumeroCuenta, c.TipoCuenta, c.Banco, c.VerEnFactura, c.Cancelada, c.FechaCancelacion))
            .ToListAsync(ct);
    }

    public async Task<CuentaBancariaDto> CrearCuentaBancariaAsync(CrearCuentaBancariaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid)
            throw new InvalidOperationException("Sin tenant activo.");
        if (string.IsNullOrWhiteSpace(req.NumeroCuenta))
            throw new InvalidOperationException("Numero de cuenta requerido.");
        if (string.IsNullOrWhiteSpace(req.Banco))
            throw new InvalidOperationException("Banco requerido.");

        var c = new CuentaBancaria
        {
            TenantId = tid,
            NumeroCuenta = req.NumeroCuenta.Trim(),
            TipoCuenta = req.TipoCuenta,
            Banco = req.Banco.Trim(),
            VerEnFactura = req.VerEnFactura,
            Cancelada = false
        };
        _db.CuentasBancarias.Add(c);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Finanzas", $"Cuenta bancaria agregada: {c.Banco} {c.NumeroCuenta}.", ct);
        return new CuentaBancariaDto(c.Id, c.NumeroCuenta, c.TipoCuenta, c.Banco, c.VerEnFactura, c.Cancelada, c.FechaCancelacion);
    }

    public async Task<CuentaBancariaDto?> ActualizarCuentaBancariaAsync(Guid id, ActualizarCuentaBancariaRequest req, CancellationToken ct)
    {
        var c = await _db.CuentasBancarias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        if (string.IsNullOrWhiteSpace(req.NumeroCuenta) || string.IsNullOrWhiteSpace(req.Banco))
            throw new InvalidOperationException("Numero de cuenta y banco son obligatorios.");
        var seCancela = req.Cancelada && !c.Cancelada;
        c.NumeroCuenta = req.NumeroCuenta.Trim();
        c.TipoCuenta = req.TipoCuenta;
        c.Banco = req.Banco.Trim();
        c.VerEnFactura = req.VerEnFactura;
        c.Cancelada = req.Cancelada;
        c.FechaCancelacion = req.Cancelada ? (c.FechaCancelacion ?? DateTimeOffset.UtcNow) : null;
        await _db.SaveChangesAsync(ct);
        if (seCancela)
            await RegistrarBitacoraAsync("Finanzas", $"Cuenta bancaria cancelada: {c.Banco} {c.NumeroCuenta}.", ct);
        return new CuentaBancariaDto(c.Id, c.NumeroCuenta, c.TipoCuenta, c.Banco, c.VerEnFactura, c.Cancelada, c.FechaCancelacion);
    }

    public async Task<bool> EliminarCuentaBancariaAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.CuentasBancarias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        _db.CuentasBancarias.Remove(c);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Finanzas", $"Cuenta bancaria eliminada: {c.Banco} {c.NumeroCuenta}.", ct);
        return true;
    }

    // ----------------------------- Bitacora de cambios (RN-06) -----------------------------

    public async Task<IReadOnlyList<BitacoraEntradaDto>> ListBitacoraAsync(int limit, CancellationToken ct)
    {
        return await _db.BitacoraMiCopropiedad
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit <= 0 ? 50 : limit)
            .Select(b => new BitacoraEntradaDto(b.Id, b.Categoria, b.Descripcion, b.Autor, b.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Registra una entrada de bitacora (persistencia propia). RN-06.</summary>
    public async Task RegistrarBitacoraAsync(string categoria, string descripcion, CancellationToken ct)
    {
        _db.BitacoraMiCopropiedad.Add(new BitacoraMiCopropiedad
        {
            Categoria = categoria,
            Descripcion = descripcion
        });
        await _db.SaveChangesAsync(ct);
    }
}
