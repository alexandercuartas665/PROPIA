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
    // Seccion 4 Gobierno + Seccion 5 Servicios/Contratos (expedientes, campos EAV, etapas de flujo).
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
        await AsegurarEtapasBaseAsync(ct);
        var contratos = await _db.ContratosServicio
            .AsNoTracking()
            .Include(x => x.Adjuntos)
            .OrderBy(c => c.Tipo)
            .ToListAsync(ct);
        var ids = contratos.Select(c => c.Id).ToList();
        var valores = await _db.ContratoCampoValores.AsNoTracking()
            .Where(v => ids.Contains(v.ContratoId))
            .ToListAsync(ct);
        var porContrato = valores.GroupBy(v => v.ContratoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ContratoCampoValorDto>)g
                .Select(v => new ContratoCampoValorDto(v.ContratoCampoId, v.Valor)).ToList());

        // "Asociado a": resolver nombres de equipos/zonas referenciados, en batch.
        var equipoIds = contratos.Where(c => c.AsociadoTipo == TipoActivoMantenimiento.Equipo && c.AsociadoId.HasValue).Select(c => c.AsociadoId!.Value).Distinct().ToList();
        var zonaIds = contratos.Where(c => c.AsociadoTipo == TipoActivoMantenimiento.ZonaComun && c.AsociadoId.HasValue).Select(c => c.AsociadoId!.Value).Distinct().ToList();
        var equipoNombres = equipoIds.Count == 0 ? new() : await _db.EquiposActivos.AsNoTracking().Where(e => equipoIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nombre, ct);
        var zonaNombres = zonaIds.Count == 0 ? new() : await _db.ZonasComunes.AsNoTracking().Where(z => zonaIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Nombre, ct);
        string? AsocNombre(ContratoServicio c) => c.AsociadoId is not { } id ? null
            : c.AsociadoTipo == TipoActivoMantenimiento.Equipo ? equipoNombres.GetValueOrDefault(id)
            : c.AsociadoTipo == TipoActivoMantenimiento.ZonaComun ? zonaNombres.GetValueOrDefault(id)
            : null;

        return contratos.Select(c => ToContratoDto(c, porContrato.GetValueOrDefault(c.Id), AsocNombre(c))).ToList();
    }

    // ----------------------------- K-01: validacion de contrato -----------------------------

    /// <summary>
    /// K-01: reglas de negocio de un contrato, compartidas por crear y actualizar. Antes la unica
    /// validacion era "proveedor obligatorio", asi que el API aceptaba y guardaba contratos
    /// imposibles: fecha fin anterior al inicio, valores negativos, NIT con letras, cuotas en cero.
    ///
    /// Se valida el ESTADO RESULTANTE (no el request suelto): al actualizar, el PUT es un MERGE, asi
    /// que una fecha fin que llega sola tiene que compararse contra la fecha inicio YA guardada.
    /// Lanza InvalidOperationException con mensaje en espanol; el controller lo mapea a 400.
    /// </summary>
    private async Task ValidarContratoAsync(
        string? proveedor, DateOnly fechaInicio, DateOnly? fechaFin,
        decimal? valorMensual, decimal? valorTotal, int? formaPagoCuotas, bool pagoMensual,
        int diasAnticipacionAlerta, string? nitProveedor, string? numeroContrato,
        Guid? contratoIdActual, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(proveedor))
            throw new InvalidOperationException("El proveedor es obligatorio.");

        if (fechaFin is { } fin && fin < fechaInicio)
            throw new InvalidOperationException(
                $"La fecha de fin ({fin:dd/MM/yyyy}) no puede ser anterior a la de inicio ({fechaInicio:dd/MM/yyyy}).");

        if (valorMensual is { } vm && vm < 0)
            throw new InvalidOperationException("El valor mensual no puede ser negativo.");

        if (valorTotal is { } vt && vt < 0)
            throw new InvalidOperationException("El valor total no puede ser negativo.");

        // Si no se paga mes a mes, el contrato se reparte en cuotas: al menos una.
        if (!pagoMensual && formaPagoCuotas is { } cuotas && cuotas < 1)
            throw new InvalidOperationException("El numero de cuotas debe ser al menos 1.");

        if (diasAnticipacionAlerta < 1 || diasAnticipacionAlerta > 365)
            throw new InvalidOperationException("Los dias de anticipacion de la alerta deben estar entre 1 y 365.");

        // Misma regla que el Directorio para el NIT de una empresa: se admiten puntos y guiones
        // como separadores, pero el resto tienen que ser digitos. Aqui es opcional.
        if (!string.IsNullOrWhiteSpace(nitProveedor))
        {
            var nitN = nitProveedor.Trim().Replace(".", "").Replace("-", "").Replace(" ", "");
            if (nitN.Length == 0 || !nitN.All(char.IsDigit))
                throw new InvalidOperationException("El NIT del proveedor debe contener solo digitos.");
        }

        // El numero de contrato, si se usa, identifica al contrato dentro de la copropiedad.
        // El filtro de tenant de EF ya acota la consulta a la copropiedad activa.
        var numero = string.IsNullOrWhiteSpace(numeroContrato) ? null : numeroContrato.Trim();
        if (numero is not null)
        {
            var repetido = await _db.ContratosServicio
                .AnyAsync(x => x.NumeroContrato == numero
                               && (contratoIdActual == null || x.Id != contratoIdActual), ct);
            if (repetido)
                throw new InvalidOperationException($"Ya existe un contrato con el numero '{numero}' en esta copropiedad.");
        }
    }

    public async Task<ContratoServicioDto> CrearContratoAsync(CrearContratoServicioRequest req, CancellationToken ct)
    {
        await ValidarContratoAsync(
            req.Proveedor, req.FechaInicio, req.FechaFin, req.ValorMensual, req.ValorTotal,
            req.FormaPagoCuotas, req.PagoMensual, req.DiasAnticipacionAlerta <= 0 ? 30 : req.DiasAnticipacionAlerta,
            req.NitProveedor, req.NumeroContrato, null, ct);
        var c = new ContratoServicio
        {
            Tipo = req.Tipo,
            ProveedorPersonaId = req.ProveedorPersonaId,
            ProveedorEmpresaId = req.ProveedorEmpresaId,
            Proveedor = req.Proveedor,
            NitProveedor = req.NitProveedor,
            ContactoPersonaId = req.ContactoPersonaId,
            Contacto = req.Contacto,
            FechaInicio = req.FechaInicio,
            FechaFin = req.FechaFin,
            ValorMensual = req.ValorMensual,
            Observaciones = req.Observaciones,
            DiasAnticipacionAlerta = req.DiasAnticipacionAlerta <= 0 ? 30 : req.DiasAnticipacionAlerta,
            RenovacionAutomatica = req.RenovacionAutomatica,
            ServicioId = req.ServicioId,
            ExpedienteId = req.ExpedienteId,
            ProyectoTareaId = req.ProyectoTareaId,
            // ----- Campos del pedido de Contratos (Ola 1) -----
            NumeroContrato = string.IsNullOrWhiteSpace(req.NumeroContrato) ? null : req.NumeroContrato.Trim(),
            TipoContrato = req.TipoContrato,
            Categoria = req.Categoria,
            ValorTotal = req.ValorTotal,
            FormaPagoCuotas = req.FormaPagoCuotas,
            PagoMensual = req.PagoMensual,
            AsociadoTipo = req.AsociadoTipo,
            AsociadoId = req.AsociadoId
        };
        _db.ContratosServicio.Add(c);
        await _db.SaveChangesAsync(ct);
        // K-04: bitacora con los datos clave del alta, no solo "creado".
        var altaNum = string.IsNullOrWhiteSpace(c.NumeroContrato) ? "" : $" (numero {c.NumeroContrato})";
        var altaFin = c.FechaFin.HasValue ? $" a {FfFecha(c.FechaFin)}" : "";
        await RegistrarBitacoraAsync("Contrato",
            $"Contrato con '{c.Proveedor}'{altaNum} creado, vigencia {FfFecha(c.FechaInicio)}{altaFin}.", ct, c.Id);
        return ToContratoDto(c);
    }

    public async Task<bool> ActualizarContratoAsync(Guid contratoId, ActualizarContratoRequest req, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;

        // K-08/K-04: snapshot antes del MERGE, para detectar una prorroga (K-08) y registrar el
        // diff campo -> antes -> despues en la bitacora (K-04).
        var fechaInicioAntes = c.FechaInicio;
        var fechaFinAntes = c.FechaFin;
        var provAntes = c.Proveedor;
        var numAntes = c.NumeroContrato;
        var nitAntes = c.NitProveedor;
        var vmAntes = c.ValorMensual;
        var vtAntes = c.ValorTotal;
        var cuotasAntes = c.FormaPagoCuotas;
        var pagoAntes = c.PagoMensual;
        var estadoAntes = c.Estado;
        var catAntes = c.Categoria;
        var tcAntes = c.TipoContrato;
        var tipoAntes = c.Tipo;
        var renovAntes = c.RenovacionAutomatica;
        var obsAntes = c.Observaciones;
        var asocTipoAntes = c.AsociadoTipo;
        var asocIdAntes = c.AsociadoId;

        // K-01: el PUT es un MERGE, asi que se valida el ESTADO RESULTANTE (lo que llega o, si no
        // llega, lo que ya estaba guardado). Validar solo el request dejaria pasar, por ejemplo,
        // una fecha fin suelta anterior a la fecha inicio que ya tiene el contrato.
        var diasResultante = req.DiasAnticipacionAlerta <= 0 ? 30 : req.DiasAnticipacionAlerta;
        await ValidarContratoAsync(
            string.IsNullOrWhiteSpace(req.Proveedor) ? c.Proveedor : req.Proveedor,
            req.FechaInicio ?? c.FechaInicio,
            req.FechaFin ?? c.FechaFin,
            req.ValorMensual ?? c.ValorMensual,
            req.ValorTotal ?? c.ValorTotal,
            req.FormaPagoCuotas ?? c.FormaPagoCuotas,
            req.PagoMensual ?? c.PagoMensual,
            diasResultante,
            req.NitProveedor is null ? c.NitProveedor : req.NitProveedor,
            req.NumeroContrato is null ? c.NumeroContrato : req.NumeroContrato,
            c.Id, ct);

        // "Vencido" se deriva por fecha; el admin solo declara Vigente o EnRenovacion.
        c.Estado = req.Estado == EstadoContrato.Vencido ? EstadoContrato.Vigente : req.Estado;
        c.DiasAnticipacionAlerta = req.DiasAnticipacionAlerta <= 0 ? 30 : req.DiasAnticipacionAlerta;
        // MERGE de datos del contrato (solo lo provisto; conserva el resto). La tool MCP no manda estos.
        if (req.Tipo.HasValue) c.Tipo = req.Tipo.Value;
        if (!string.IsNullOrWhiteSpace(req.Proveedor)) c.Proveedor = req.Proveedor.Trim();
        if (req.NitProveedor is not null) c.NitProveedor = string.IsNullOrWhiteSpace(req.NitProveedor) ? null : req.NitProveedor.Trim();
        if (req.Contacto is not null) c.Contacto = string.IsNullOrWhiteSpace(req.Contacto) ? null : req.Contacto.Trim();
        if (req.FechaInicio.HasValue) c.FechaInicio = req.FechaInicio.Value;
        if (req.FechaFin.HasValue) c.FechaFin = req.FechaFin.Value;
        if (req.ValorMensual.HasValue) c.ValorMensual = req.ValorMensual.Value;
        if (req.Observaciones is not null) c.Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones.Trim();
        // ----- Campos del pedido de Contratos (Ola 1). MERGE: se aplican si vienen. -----
        if (req.NumeroContrato is not null) c.NumeroContrato = string.IsNullOrWhiteSpace(req.NumeroContrato) ? null : req.NumeroContrato.Trim();
        if (req.TipoContrato.HasValue) c.TipoContrato = req.TipoContrato.Value;
        if (req.Categoria.HasValue) c.Categoria = req.Categoria.Value;
        if (req.ValorTotal.HasValue) c.ValorTotal = req.ValorTotal.Value;
        if (req.FormaPagoCuotas.HasValue) c.FormaPagoCuotas = req.FormaPagoCuotas.Value;
        if (req.PagoMensual.HasValue) c.PagoMensual = req.PagoMensual.Value;
        if (req.LimpiarAsociado) { c.AsociadoTipo = null; c.AsociadoId = null; }
        else if (req.AsociadoTipo.HasValue && req.AsociadoId.HasValue) { c.AsociadoTipo = req.AsociadoTipo.Value; c.AsociadoId = req.AsociadoId.Value; }
        // Vinculos: solo el editor de la pagina los toca (ActualizarVinculos=true). La tool MCP no.
        if (req.ActualizarVinculos)
        {
            c.RenovacionAutomatica = req.RenovacionAutomatica;
            c.ServicioId = req.ServicioId;
            c.ExpedienteId = req.ExpedienteId;
            c.ProyectoTareaId = req.ProyectoTareaId;
            // Tercero del Directorio (contratista): se persisten los FK del selector.
            c.ProveedorPersonaId = req.ProveedorPersonaId;
            c.ProveedorEmpresaId = req.ProveedorEmpresaId;
            c.ContactoPersonaId = req.ContactoPersonaId;
        }
        // K-08: si cambia la vigencia, reinicia el control de alerta para que el job vuelva a evaluar.
        // Sin esto un contrato en rojo que se prorroga a amarillo nunca vuelve a avisar (el job solo
        // resetea el contador al pasar a verde). Las polizas ya lo hacian (SegurosService.cs).
        if (c.FechaInicio != fechaInicioAntes || c.FechaFin != fechaFinAntes)
            c.AlertaVencimientoPctNotificado = null;

        // K-04: diff campo -> antes -> despues comparando el estado post-MERGE con el snapshot.
        var cambios = new List<string>();
        if (c.Proveedor != provAntes) cambios.Add($"proveedor: '{provAntes}' -> '{c.Proveedor}'");
        if (c.NumeroContrato != numAntes) cambios.Add($"numero: {FfTxt(numAntes)} -> {FfTxt(c.NumeroContrato)}");
        if (c.NitProveedor != nitAntes) cambios.Add($"NIT: {FfTxt(nitAntes)} -> {FfTxt(c.NitProveedor)}");
        if (c.FechaInicio != fechaInicioAntes) cambios.Add($"inicio: {FfFecha(fechaInicioAntes)} -> {FfFecha(c.FechaInicio)}");
        if (c.FechaFin != fechaFinAntes) cambios.Add($"fin: {FfFecha(fechaFinAntes)} -> {FfFecha(c.FechaFin)}");
        if (c.ValorMensual != vmAntes) cambios.Add($"valor mensual: {FfVal(vmAntes)} -> {FfVal(c.ValorMensual)}");
        if (c.ValorTotal != vtAntes) cambios.Add($"valor total: {FfVal(vtAntes)} -> {FfVal(c.ValorTotal)}");
        if (c.FormaPagoCuotas != cuotasAntes) cambios.Add($"cuotas: {FfEnum(cuotasAntes)} -> {FfEnum(c.FormaPagoCuotas)}");
        if (c.PagoMensual != pagoAntes) cambios.Add($"pago mensual: {FfBool(pagoAntes)} -> {FfBool(c.PagoMensual)}");
        if (c.Estado != estadoAntes) cambios.Add($"estado: {estadoAntes} -> {c.Estado}");
        if (!Equals(c.Categoria, catAntes)) cambios.Add($"categoria: {FfEnum(catAntes)} -> {FfEnum(c.Categoria)}");
        if (!Equals(c.TipoContrato, tcAntes)) cambios.Add($"tipo de contrato: {FfEnum(tcAntes)} -> {FfEnum(c.TipoContrato)}");
        if (c.Tipo != tipoAntes) cambios.Add($"tipo de servicio: {tipoAntes} -> {c.Tipo}");
        if (c.RenovacionAutomatica != renovAntes) cambios.Add($"renovacion automatica: {FfBool(renovAntes)} -> {FfBool(c.RenovacionAutomatica)}");
        if (c.AsociadoTipo != asocTipoAntes || c.AsociadoId != asocIdAntes) cambios.Add("asociado actualizado");
        if (c.Observaciones != obsAntes) cambios.Add("observaciones actualizadas");

        await _db.SaveChangesAsync(ct);
        var detalle = cambios.Count == 0
            ? $"Contrato con '{c.Proveedor}' actualizado (sin cambios de datos)."
            : $"Contrato con '{c.Proveedor}' actualizado: {string.Join("; ", cambios)}.";
        await RegistrarBitacoraAsync("Contrato", detalle, ct, c.Id);
        return true;
    }

    /// <summary>Semaforo de vencimiento por % de dias totales (Ola 3): sin fecha fin = Ninguno;
    /// vencido = Rojo; &lt;=10% restante = Rojo; &lt;=20% = Amarillo; resto = Verde.</summary>
    public static SemaforoContrato CalcularSemaforoContrato(DateOnly inicio, DateOnly? fin, DateOnly hoy)
    {
        if (fin is not { } f) return SemaforoContrato.Ninguno;
        var restante = f.DayNumber - hoy.DayNumber;
        if (restante < 0) return SemaforoContrato.Rojo;                 // vencido
        var total = f.DayNumber - inicio.DayNumber;
        if (total <= 0) return SemaforoContrato.Rojo;                   // fin <= inicio: critico
        var pct = (double)restante / total;
        return pct <= 0.10 ? SemaforoContrato.Rojo
             : pct <= 0.20 ? SemaforoContrato.Amarillo
             : SemaforoContrato.Verde;
    }

    // K-04: helpers de formato para las lineas de bitacora (ASCII, cultura invariante).
    private static string FfFecha(DateOnly? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy") : "(sin fecha)";
    private static string FfVal(decimal? v) => v.HasValue ? v.Value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture) : "(vacio)";
    private static string FfTxt(string? t) => string.IsNullOrWhiteSpace(t) ? "(vacio)" : t;
    private static string FfBool(bool b) => b ? "si" : "no";
    private static string FfEnum(object? e) => e?.ToString() ?? "(vacio)";

    private static ContratoServicioDto ToContratoDto(ContratoServicio c, IReadOnlyList<ContratoCampoValorDto>? valores = null, string? asociadoNombre = null)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        int? dias = c.FechaFin.HasValue ? c.FechaFin.Value.DayNumber - hoy.DayNumber : null;
        var estado = (c.FechaFin.HasValue && c.FechaFin.Value < hoy) ? EstadoContrato.Vencido : c.Estado;
        // Semaforo por % de dias totales del contrato (Ola 3): 20% -> amarillo, 10% o vencido -> rojo.
        var semaforo = CalcularSemaforoContrato(c.FechaInicio, c.FechaFin, hoy);
        var alerta = semaforo is SemaforoContrato.Amarillo or SemaforoContrato.Rojo;
        return new ContratoServicioDto(c.Id, c.Tipo, c.Proveedor, c.NitProveedor, c.Contacto,
            c.FechaInicio, c.FechaFin, c.ValorMensual, c.Observaciones,
            estado, c.DiasAnticipacionAlerta, dias, alerta,
            c.RenovacionAutomatica, c.ServicioId, c.ExpedienteId, c.ProyectoTareaId,
            c.Adjuntos?.Count ?? 0, valores, c.EtapaId,
            c.NumeroContrato, c.TipoContrato, c.Categoria, c.ValorTotal, c.FormaPagoCuotas, c.PagoMensual,
            c.AsociadoTipo, c.AsociadoId, asociadoNombre,
            c.ProveedorPersonaId, c.ProveedorEmpresaId, c.ContactoPersonaId, semaforo);
    }

    public async Task<bool> EliminarContratoAsync(Guid contratoId, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        // Limpiar los valores EAV del contrato (no hay cascade configurado).
        var valores = await _db.ContratoCampoValores.Where(v => v.ContratoId == contratoId).ToListAsync(ct);
        if (valores.Count > 0) _db.ContratoCampoValores.RemoveRange(valores);
        var vincs = await _db.ContratoExpedientes.Where(v => v.ContratoId == contratoId).ToListAsync(ct);
        if (vincs.Count > 0) _db.ContratoExpedientes.RemoveRange(vincs);
        var provElim = c.Proveedor;
        var numElim = c.NumeroContrato;
        var idElim = c.Id;
        _db.ContratosServicio.Remove(c);
        await _db.SaveChangesAsync(ct);
        // K-04: dejar rastro del borrado (antes no registraba nada).
        var elimNum = string.IsNullOrWhiteSpace(numElim) ? "" : $" (numero {numElim})";
        await RegistrarBitacoraAsync("Contrato", $"Contrato con '{provElim}'{elimNum} eliminado.", ct, idElim);
        return true;
    }

    // ---- Expedientes vinculados a un contrato (Ola 2: pestana Documentos) ----
    public async Task<IReadOnlyList<ContratoExpedienteDto>> ListExpedientesContratoAsync(Guid contratoId, CancellationToken ct)
    {
        // Dos queries (evita el Join entre DbSets con HasQueryFilter, que EF no traduce).
        var ids = await _db.ContratoExpedientes.AsNoTracking()
            .Where(v => v.ContratoId == contratoId)
            .Select(v => v.ExpedienteId)
            .ToListAsync(ct);
        if (ids.Count == 0) return Array.Empty<ContratoExpedienteDto>();
        return await _db.Expedientes.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .OrderBy(e => e.Codigo)
            .Select(e => new ContratoExpedienteDto(e.Id, e.Codigo, e.Nombre))
            .ToListAsync(ct);
    }

    public async Task<bool> VincularExpedienteContratoAsync(Guid contratoId, Guid expedienteId, CancellationToken ct)
    {
        if (!await _db.ContratosServicio.AnyAsync(c => c.Id == contratoId, ct)) return false;
        if (!await _db.Expedientes.AnyAsync(e => e.Id == expedienteId, ct)) return false;
        if (await _db.ContratoExpedientes.AnyAsync(v => v.ContratoId == contratoId && v.ExpedienteId == expedienteId, ct))
            return true;   // ya vinculado, idempotente
        _db.ContratoExpedientes.Add(new ContratoExpediente { ContratoId = contratoId, ExpedienteId = expedienteId });
        await _db.SaveChangesAsync(ct);
        var cod = await _db.Expedientes.Where(e => e.Id == expedienteId).Select(e => e.Codigo).FirstOrDefaultAsync(ct);
        await RegistrarBitacoraAsync("Contrato", $"Expediente '{cod}' conectado al contrato.", ct, contratoId);
        return true;
    }

    public async Task<bool> DesvincularExpedienteContratoAsync(Guid contratoId, Guid expedienteId, CancellationToken ct)
    {
        var v = await _db.ContratoExpedientes.FirstOrDefaultAsync(x => x.ContratoId == contratoId && x.ExpedienteId == expedienteId, ct);
        if (v is null) return false;
        _db.ContratoExpedientes.Remove(v);
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Contrato", "Expediente desconectado del contrato.", ct, contratoId);
        return true;
    }

    // ---- Campos personalizados (EAV) de contratos ----
    public async Task<IReadOnlyList<ContratoCampoDto>> ListContratoCamposAsync(CancellationToken ct)
    {
        return await _db.ContratoCampos.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new ContratoCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.Descripcion, c.Activo))
            .ToListAsync(ct);
    }

    public async Task<ContratoCampoDto> CrearContratoCampoAsync(CrearContratoCampoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new InvalidOperationException("El nombre del campo es obligatorio.");
        var maxOrden = await _db.ContratoCampos.AnyAsync(ct) ? await _db.ContratoCampos.MaxAsync(c => (int?)c.Orden, ct) ?? 0 : 0;
        var campo = new ContratoCampo
        {
            Label = req.Label.Trim(),
            Tipo = req.Tipo,
            Opciones = string.IsNullOrWhiteSpace(req.Opciones) ? null : req.Opciones.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Orden = maxOrden + 1,
            Activo = true
        };
        _db.ContratoCampos.Add(campo);
        await _db.SaveChangesAsync(ct);
        return new ContratoCampoDto(campo.Id, campo.Label, campo.Orden, campo.Tipo, campo.Opciones, campo.Descripcion, campo.Activo);
    }

    public async Task<bool> ActualizarContratoCampoAsync(Guid campoId, ActualizarContratoCampoRequest req, CancellationToken ct)
    {
        var campo = await _db.ContratoCampos.FirstOrDefaultAsync(c => c.Id == campoId, ct);
        if (campo is null) return false;
        if (!string.IsNullOrWhiteSpace(req.Label)) campo.Label = req.Label.Trim();
        campo.Tipo = req.Tipo;
        campo.Opciones = string.IsNullOrWhiteSpace(req.Opciones) ? null : req.Opciones.Trim();
        campo.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        campo.Orden = req.Orden;
        campo.Activo = req.Activo;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarContratoCampoAsync(Guid campoId, CancellationToken ct)
    {
        var campo = await _db.ContratoCampos.FirstOrDefaultAsync(c => c.Id == campoId, ct);
        if (campo is null) return false;
        var valores = await _db.ContratoCampoValores.Where(v => v.ContratoCampoId == campoId).ToListAsync(ct);
        if (valores.Count > 0) _db.ContratoCampoValores.RemoveRange(valores);
        _db.ContratoCampos.Remove(campo);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> GuardarContratoCampoValorAsync(Guid contratoId, Guid campoId, GuardarContratoCampoValorRequest req, CancellationToken ct)
    {
        var contrato = await _db.ContratosServicio.AnyAsync(c => c.Id == contratoId, ct);
        if (!contrato) return false;
        var campo = await _db.ContratoCampos.AnyAsync(c => c.Id == campoId, ct);
        if (!campo) return false;
        var val = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var existente = await _db.ContratoCampoValores
            .FirstOrDefaultAsync(v => v.ContratoId == contratoId && v.ContratoCampoId == campoId, ct);
        if (existente is null)
        {
            if (val is null) return true;   // nada que guardar
            _db.ContratoCampoValores.Add(new ContratoCampoValor { ContratoId = contratoId, ContratoCampoId = campoId, Valor = val });
        }
        else if (val is null)
        {
            _db.ContratoCampoValores.Remove(existente);
        }
        else
        {
            existente.Valor = val;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Etapas de flujo (Kanban) de contratos ----
    // Siembra las 4 etapas base por copropiedad si no existen y ancla los contratos sin etapa a "Activo".
    private async Task AsegurarEtapasBaseAsync(CancellationToken ct)
    {
        if (await _db.ContratoEtapas.AnyAsync(ct)) return;
        var baseEtapas = new (string Nombre, string Color)[]
        {
            ("En tramite", "#3B82F6"),
            ("Pendiente aprobacion asamblea", "#F59E0B"),
            ("Activo", "#22C55E"),
            ("Terminado", "#6B7280"),
        };
        var creadas = new List<ContratoEtapa>();
        for (int i = 0; i < baseEtapas.Length; i++)
        {
            var e = new ContratoEtapa { Nombre = baseEtapas[i].Nombre, Color = baseEtapas[i].Color, Orden = i + 1 };
            _db.ContratoEtapas.Add(e);
            creadas.Add(e);
        }
        await _db.SaveChangesAsync(ct);
        // Contratos existentes sin etapa -> "Activo" (la tercera).
        var activo = creadas[2];
        await _db.ContratosServicio.Where(c => c.EtapaId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EtapaId, activo.Id), ct);
    }

    public async Task<IReadOnlyList<ContratoEtapaDto>> ListContratoEtapasAsync(CancellationToken ct)
    {
        await AsegurarEtapasBaseAsync(ct);
        return await _db.ContratoEtapas.AsNoTracking()
            .OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new ContratoEtapaDto(e.Id, e.Nombre, e.Orden, e.Color))
            .ToListAsync(ct);
    }

    public async Task<ContratoEtapaDto> CrearContratoEtapaAsync(CrearContratoEtapaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la etapa es obligatorio.");
        await AsegurarEtapasBaseAsync(ct);
        var maxOrden = await _db.ContratoEtapas.AnyAsync(ct) ? await _db.ContratoEtapas.MaxAsync(e => (int?)e.Orden, ct) ?? 0 : 0;
        var etapa = new ContratoEtapa { Nombre = req.Nombre.Trim(), Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim(), Orden = maxOrden + 1 };
        _db.ContratoEtapas.Add(etapa);
        await _db.SaveChangesAsync(ct);
        return new ContratoEtapaDto(etapa.Id, etapa.Nombre, etapa.Orden, etapa.Color);
    }

    public async Task<bool> ActualizarContratoEtapaAsync(Guid etapaId, ActualizarContratoEtapaRequest req, CancellationToken ct)
    {
        var etapa = await _db.ContratoEtapas.FirstOrDefaultAsync(e => e.Id == etapaId, ct);
        if (etapa is null) return false;
        if (!string.IsNullOrWhiteSpace(req.Nombre)) etapa.Nombre = req.Nombre.Trim();
        etapa.Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarContratoEtapaAsync(Guid etapaId, CancellationToken ct)
    {
        var etapa = await _db.ContratoEtapas.FirstOrDefaultAsync(e => e.Id == etapaId, ct);
        if (etapa is null) return false;
        // No permitir borrar la ultima etapa; reasignar los contratos a otra etapa antes de borrar.
        var otras = await _db.ContratoEtapas.Where(e => e.Id != etapaId).OrderBy(e => e.Orden).ToListAsync(ct);
        if (otras.Count == 0) return false;
        var destino = otras.First().Id;
        await _db.ContratosServicio.Where(c => c.EtapaId == etapaId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EtapaId, destino), ct);
        _db.ContratoEtapas.Remove(etapa);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReordenarContratoEtapasAsync(ReordenarContratoEtapasRequest req, CancellationToken ct)
    {
        if (req.Orden is null || req.Orden.Count == 0) return;
        var etapas = await _db.ContratoEtapas.ToListAsync(ct);
        for (int i = 0; i < req.Orden.Count; i++)
        {
            var e = etapas.FirstOrDefault(x => x.Id == req.Orden[i]);
            if (e is not null) e.Orden = i + 1;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> CambiarEtapaContratoAsync(Guid contratoId, CambiarEtapaContratoRequest req, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        if (req.EtapaId is { } eid && !await _db.ContratoEtapas.AnyAsync(e => e.Id == eid, ct)) return false;
        var etapaAntesId = c.EtapaId;
        c.EtapaId = req.EtapaId;
        await _db.SaveChangesAsync(ct);
        // K-04: el cambio de etapa (drag & drop del kanban) antes no dejaba rastro.
        if (etapaAntesId != c.EtapaId)
            await RegistrarBitacoraAsync("Contrato",
                $"Contrato con '{c.Proveedor}' movido de etapa: {await NombreEtapaAsync(etapaAntesId, ct)} -> {await NombreEtapaAsync(c.EtapaId, ct)}.",
                ct, c.Id);
        return true;
    }

    private async Task<string> NombreEtapaAsync(Guid? etapaId, CancellationToken ct)
        => etapaId is { } id
            ? (await _db.ContratoEtapas.AsNoTracking().Where(e => e.Id == id).Select(e => e.Nombre).FirstOrDefaultAsync(ct)) ?? "(desconocida)"
            : "(ninguna)";

}
