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

    public async Task<ContratoServicioDto> CrearContratoAsync(CrearContratoServicioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Proveedor))
            throw new InvalidOperationException("Proveedor obligatorio.");
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
        await RegistrarBitacoraAsync("Contrato", $"Contrato con '{c.Proveedor}' creado.", ct, c.Id);
        return ToContratoDto(c);
    }

    public async Task<bool> ActualizarContratoAsync(Guid contratoId, ActualizarContratoRequest req, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
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
        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Contrato", $"Contrato con '{c.Proveedor}' actualizado.", ct, c.Id);
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
        _db.ContratosServicio.Remove(c);
        await _db.SaveChangesAsync(ct);
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
        c.EtapaId = req.EtapaId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

}
