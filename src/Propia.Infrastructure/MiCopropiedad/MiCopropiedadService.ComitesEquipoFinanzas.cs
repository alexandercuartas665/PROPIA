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
    // Comites + Revisor Fiscal, Seccion 3 Equipo de trabajo, Seccion 8 Finanzas (config avanzada, cuentas bancarias), bitacora y ficha completa de zona comun.
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

    public async Task<GobiernoPersonaDto> GetGobiernoPersonaAsync(Guid personaId, CancellationToken ct)
    {
        var consejo = await _db.MiembrosConsejo.AsNoTracking()
            .Where(m => m.PersonaId == personaId && m.Activo)
            .Select(m => new GobiernoConsejoTag(m.Id, m.Cargo))
            .FirstOrDefaultAsync(ct);

        var comites = await _db.ComiteMiembros.AsNoTracking()
            .Where(cm => cm.PersonaId == personaId && cm.Activo)
            .Join(_db.Comites, cm => cm.ComiteId, c => c.Id,
                  (cm, c) => new GobiernoComiteTag(cm.Id, c.Id, c.Nombre, cm.CargoEnComite))
            .ToListAsync(ct);

        var revisor = await _db.RevisoresFiscales.AsNoTracking()
            .Where(r => r.PersonaId == personaId && r.Activo)
            .Select(r => new GobiernoRevisorTag(r.Id, r.NumeroTarjetaProfesional))
            .FirstOrDefaultAsync(ct);

        var equipo = await _db.MiembrosEquipo.AsNoTracking()
            .Where(e => e.PersonaId == personaId && e.Activo)
            .Select(e => new GobiernoEquipoTag(e.Id, e.Rol, e.RolPersonalizado))
            .FirstOrDefaultAsync(ct);

        return new GobiernoPersonaDto(consejo, comites, revisor, equipo);
    }

    public async Task<Guid> VincularPersonaPorDocumentoAsync(VincularPersonaPorDocumentoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Documento))
            throw new InvalidOperationException("Documento es obligatorio.");
        var doc = req.Documento.Trim();

        var existente = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Documento == doc, ct);
        if (existente is not null)
        {
            // Ya existia en la plataforma (quiza creada en otra copropiedad). Se vincula a
            // esta para que aparezca en el Directorio y en todos los selectores de persona.
            await Directorio.VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, existente.Id, ct);
            return existente.Id;
        }

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
        await Directorio.VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, nueva.Id, ct);
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

    /// <summary>Bitacora filtrada por una entidad concreta (ej. una unidad), para su ficha.</summary>
    public async Task<IReadOnlyList<BitacoraEntradaDto>> ListBitacoraEntidadAsync(Guid entidadId, int limit, CancellationToken ct)
    {
        return await _db.BitacoraMiCopropiedad
            .AsNoTracking()
            .Where(b => b.EntidadId == entidadId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit <= 0 ? 100 : limit)
            .Select(b => new BitacoraEntradaDto(b.Id, b.Categoria, b.Descripcion, b.Autor, b.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Registra una entrada de bitacora (persistencia propia). RN-06.
    /// entidadId (opcional) enlaza el evento a una entidad concreta para su ficha.</summary>
    public async Task RegistrarBitacoraAsync(string categoria, string descripcion, CancellationToken ct, Guid? entidadId = null)
    {
        _db.BitacoraMiCopropiedad.Add(new BitacoraMiCopropiedad
        {
            Categoria = categoria,
            Descripcion = descripcion,
            EntidadId = entidadId
        });
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------- Ficha completa de zona comun (seccion 4) -----------------------------

    public async Task<ZonaFichaDto?> GetZonaFichaAsync(Guid zonaId, Guid? personaId, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return null;

        var zonaDto = new ZonaComunDto(z.Id, z.Nombre, z.Categoria, z.Descripcion, z.EsReservable,
            z.TarifaReserva, z.CapacidadPersonas, z.HorariosUso, z.ReglasUso, z.Estado);

        var facturas = await _db.ZonaFacturas.AsNoTracking().Where(f => f.ZonaComunId == zonaId)
            .OrderByDescending(f => f.Fecha)
            .Select(f => new ZonaFacturaDto(f.Id, f.Concepto, f.Valor, f.Fecha)).ToListAsync(ct);

        var docs = (await _db.ZonaDocumentos.AsNoTracking().Where(d => d.ZonaComunId == zonaId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Nombre, d.Url }).ToListAsync(ct))
            .Select(d => new ZonaDocumentoDto(d.Id, d.Nombre, _blob.ResolveUrl(d.Url) ?? d.Url)).ToList();

        var campos = await _db.ZonaCamposPersonalizados.AsNoTracking().Where(c => c.ZonaComunId == zonaId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ZonaCampoDto(c.Id, c.Label, c.Valor)).ToListAsync(ct);

        var horarios = await _db.VentanasDisponibilidad.AsNoTracking()
            .Where(v => v.TipoEntidad == TipoEntidadDisponibilidad.ZonaComun && v.EntidadId == zonaId)
            .OrderBy(v => v.DiaSemana).ThenBy(v => v.HoraInicio)
            .Select(v => new VentanaDisponibilidadDto(v.Id, v.TipoEntidad, v.EntidadId, v.DiaSemana, v.HoraInicio, v.HoraFin, v.Activa))
            .ToListAsync(ct);

        var contratos = await _db.ContratosServicio.AsNoTracking()
            .Select(c => new ZonaContratoRefDto(c.Id, c.Tipo + " - " + c.Proveedor)).ToListAsync(ct);

        return new ZonaFichaDto(zonaDto, _blob.ResolveUrl(z.ImagenUrl), z.MantenimientoTipo, z.MantenimientoContrato,
            z.MantenimientoFrecuencia, z.MantenimientoDiaMes, facturas, docs, campos, horarios, contratos);
    }

    public async Task<bool> GuardarZonaFichaAsync(Guid zonaId, GuardarZonaFichaRequest req, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return false;
        z.ImagenUrl = string.IsNullOrWhiteSpace(req.ImagenUrl) ? null : req.ImagenUrl.Trim();
        z.MantenimientoTipo = string.IsNullOrWhiteSpace(req.MantenimientoTipo) ? "Interno" : req.MantenimientoTipo.Trim();
        z.MantenimientoContrato = string.IsNullOrWhiteSpace(req.MantenimientoContrato) ? null : req.MantenimientoContrato.Trim();
        z.MantenimientoFrecuencia = string.IsNullOrWhiteSpace(req.MantenimientoFrecuencia) ? "Mensual" : req.MantenimientoFrecuencia.Trim();
        z.MantenimientoDiaMes = req.MantenimientoDiaMes;
        z.EsReservable = req.EsReservable;
        z.CapacidadPersonas = req.CapacidadPersonas;
        z.ReglasUso = string.IsNullOrWhiteSpace(req.ReglasUso) ? null : req.ReglasUso.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string?> SetZonaImagenAsync(Guid zonaId, string url, CancellationToken ct)
    {
        var z = await _db.ZonasComunes.FirstOrDefaultAsync(x => x.Id == zonaId, ct);
        if (z is null) return null;
        z.ImagenUrl = url;
        await _db.SaveChangesAsync(ct);
        return url;
    }

    public async Task<ZonaFacturaDto?> AgregarZonaFacturaAsync(Guid zonaId, AgregarZonaFacturaRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.ZonasComunes.AnyAsync(z => z.Id == zonaId, ct)) return null;
        var f = new ZonaFactura { TenantId = tid, ZonaComunId = zonaId, Concepto = req.Concepto.Trim(), Valor = req.Valor, Fecha = req.Fecha };
        _db.ZonaFacturas.Add(f);
        await _db.SaveChangesAsync(ct);
        return new ZonaFacturaDto(f.Id, f.Concepto, f.Valor, f.Fecha);
    }

    public async Task<bool> EliminarZonaFacturaAsync(Guid facturaId, CancellationToken ct)
    {
        var f = await _db.ZonaFacturas.FirstOrDefaultAsync(x => x.Id == facturaId, ct);
        if (f is null) return false;
        _db.ZonaFacturas.Remove(f);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ZonaDocumentoDto?> AgregarZonaDocumentoAsync(Guid zonaId, string nombre, string url, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (!await _db.ZonasComunes.AnyAsync(z => z.Id == zonaId, ct)) return null;
        var d = new ZonaDocumento { TenantId = tid, ZonaComunId = zonaId, Nombre = nombre.Trim(), Url = url };
        _db.ZonaDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);
        return new ZonaDocumentoDto(d.Id, d.Nombre, _blob.ResolveUrl(d.Url) ?? d.Url);
    }

    public async Task<bool> EliminarZonaDocumentoAsync(Guid docId, CancellationToken ct)
    {
        var d = await _db.ZonaDocumentos.FirstOrDefaultAsync(x => x.Id == docId, ct);
        if (d is null) return false;
        _db.ZonaDocumentos.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ZonaCampoDto?> AgregarZonaCampoAsync(Guid zonaId, AgregarZonaCampoRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        if (string.IsNullOrWhiteSpace(req.Label)) return null;
        if (!await _db.ZonasComunes.AnyAsync(z => z.Id == zonaId, ct)) return null;
        var c = new ZonaCampoPersonalizado { TenantId = tid, ZonaComunId = zonaId, Label = req.Label.Trim(), Valor = req.Valor?.Trim() };
        _db.ZonaCamposPersonalizados.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ZonaCampoDto(c.Id, c.Label, c.Valor);
    }

    public async Task<bool> EliminarZonaCampoAsync(Guid campoId, CancellationToken ct)
    {
        var c = await _db.ZonaCamposPersonalizados.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        _db.ZonaCamposPersonalizados.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> ResolverNombrePersonaAsync(Guid? personaId, string fallback, CancellationToken ct)
    {
        if (personaId is not Guid pid) return fallback;
        var n = await _db.Personas.AsNoTracking().Where(p => p.Id == pid)
            .Select(p => p.Nombres + " " + p.Apellidos).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(n) ? fallback : n.Trim();
    }

    private static string Iniciales(string? nombre)
    {
        var parts = (nombre ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        return ("" + parts[0][0] + parts[1][0]).ToUpperInvariant();
    }

    private static string FechaRel(DateTimeOffset dt)
    {
        var d = DateTimeOffset.UtcNow - dt;
        if (d.TotalMinutes < 1) return "ahora";
        if (d.TotalMinutes < 60) return "hace " + (int)d.TotalMinutes + " min";
        if (d.TotalHours < 24) return "hace " + (int)d.TotalHours + " h";
        if (d.TotalDays < 30) return "hace " + (int)d.TotalDays + " d";
        return dt.ToString("yyyy-MM-dd");
    }
}
