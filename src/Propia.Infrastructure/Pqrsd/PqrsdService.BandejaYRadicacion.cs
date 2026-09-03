using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Pqrsd;

// Particion de PqrsdService por area (clase parcial: comparte _db/_tenantContext/_http/_noti/_tareas/_membrete
// y los helpers transversales del archivo principal). Mismo comportamiento.
public partial class PqrsdService
{
    // Bandeja + ficha del expediente y radicacion (interna).
    // ===================== Bandeja + ficha =====================

    public async Task<PqrsdBandejaDto> GetBandejaAsync(EstadoPqrsd? estado, TipoPqrsd? tipo, Guid? categoriaId, string? query, bool incluirArchivados, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        IQueryable<PqrsdExpediente> q = _db.PqrsdExpedientes.AsNoTracking().Include(x => x.Categoria);
        // incluirArchivados=false => solo activos (tablero/tabla); true => solo archivados (tab Archivados).
        q = q.Where(x => x.Archivado == incluirArchivados);
        if (estado.HasValue) q = q.Where(x => x.Estado == estado.Value);
        if (tipo.HasValue) q = q.Where(x => x.Tipo == tipo.Value);
        if (categoriaId.HasValue) q = q.Where(x => x.CategoriaId == categoriaId.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var qn = query.Trim().ToLower();
            q = q.Where(x => x.NumeroRadicado.ToLower().Contains(qn) || x.Descripcion.ToLower().Contains(qn));
        }

        var rows = await (
            from x in q
            join p in _db.Personas.AsNoTracking() on x.RadicadorPersonaId equals p.Id into pj
            from p in pj.DefaultIfEmpty()
            orderby x.CreatedAt descending
            select new { x, RadicadorNombre = p == null ? null : (p.Nombres + " " + p.Apellidos).Trim() }
        ).Take(500).ToListAsync(ct);

        var ids = rows.Select(r => r.x.Id).ToList();

        var sesionesActivas = await _db.PqrsdComiteSesiones.AsNoTracking()
            .Where(s => s.Resultado == null)
            .Select(s => s.ExpedienteId).ToHashSetAsync(ct);

        // Valores de campos dinamicos por expediente (para la vista tabla).
        var valores = await _db.PqrsdCampoValores.AsNoTracking()
            .Where(v => ids.Contains(v.ExpedienteId))
            .Select(v => new { v.ExpedienteId, v.PqrsdCampoId, v.Valor })
            .ToListAsync(ct);
        var valoresPorExp = valores.GroupBy(v => v.ExpedienteId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PqrsdCampoValorDto>)g
                .Select(v => new PqrsdCampoValorDto(v.PqrsdCampoId, v.Valor)).ToList());

        // Numero de unidad relacionada (si el expediente la tiene fijada).
        var unidadIds = rows.Where(r => r.x.UnidadPrivadaId.HasValue).Select(r => r.x.UnidadPrivadaId!.Value).Distinct().ToList();
        var unidadNumeros = unidadIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.UnidadesPrivadas.AsNoTracking().Where(u => unidadIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Numero, ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var plazos = await _db.PqrsdConfiguracionPlazos.AsNoTracking().ToDictionaryAsync(p => p.Tipo, ct);
        var tiposNombres = await _db.PqrsdTipos.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        var items = rows.Select(r =>
        {
            var fechaCreacion = DateOnly.FromDateTime(r.x.CreatedAt.UtcDateTime);
            var semaforo = CalcularSemaforo(r.x.Estado, r.x.TutelaActiva, fechaCreacion, r.x.FechaVencimiento, hoy);
            var diasHasta = r.x.FechaVencimiento.DayNumber - hoy.DayNumber;
            var urgencia = plazos.TryGetValue(r.x.Tipo, out var pl) ? pl.NivelUrgencia : NivelUrgenciaPqrsd.Media;
            var nombre = r.x.IdentidadReservada ? null : r.RadicadorNombre;
            var resumen = r.x.Descripcion.Length > 100 ? r.x.Descripcion[..100] + "..." : r.x.Descripcion;
            var unidadNumero = r.x.UnidadPrivadaId.HasValue ? unidadNumeros.GetValueOrDefault(r.x.UnidadPrivadaId.Value) : null;
            var radId = r.x.IdentidadReservada ? (Guid?)null : r.x.RadicadorPersonaId;
            var tipoNombre = (r.x.TipoId.HasValue && tiposNombres.TryGetValue(r.x.TipoId.Value, out var tn)) ? tn : TipoNombreBase(r.x.Tipo);
            return new PqrsdBandejaItemDto(
                r.x.Id, r.x.NumeroRadicado, r.x.Tipo, r.x.Categoria!.Nombre, resumen, r.x.Estado,
                semaforo, nombre, unidadNumero, r.x.IdentidadReservada, r.x.TutelaActiva,
                r.x.FechaVencimiento, diasHasta, urgencia,
                sesionesActivas.Contains(r.x.Id), r.x.CreatedAt,
                r.x.EstadoId, r.x.Archivado, r.x.UnidadPrivadaId, radId,
                valoresPorExp.GetValueOrDefault(r.x.Id), tipoNombre);
        }).ToList();

        var kpis = new PqrsdKpisDto(
            items.Count,
            items.Count(i => i.Estado == EstadoPqrsd.Recibida),
            items.Count(i => i.Estado == EstadoPqrsd.EnGestion),
            items.Count(i => i.Estado == EstadoPqrsd.Respondida),
            items.Count(i => i.Estado == EstadoPqrsd.Cerrada || i.Estado == EstadoPqrsd.ViaInternaAgotada),
            items.Count(i => i.Semaforo == SemaforoPqrsd.Rojo && i.Estado != EstadoPqrsd.Cerrada && i.Estado != EstadoPqrsd.ViaInternaAgotada),
            items.Count(i => i.Semaforo == SemaforoPqrsd.Negro && i.Estado != EstadoPqrsd.Cerrada && i.Estado != EstadoPqrsd.ViaInternaAgotada),
            items.Count(i => i.TutelaActiva),
            items.Count(i => i.TieneComiteActivo));

        return new PqrsdBandejaDto(kpis, items);
    }

    public async Task<PqrsdExpedienteDetalleDto?> GetExpedienteAsync(Guid id, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        var x = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.Categoria)
            .Include(e => e.Adjuntos)
            .Include(e => e.Historial)
            .Include(e => e.CamposValores)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return null;

        string? unidadNumero = x.UnidadPrivadaId.HasValue
            ? await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == x.UnidadPrivadaId).Select(u => u.Numero).FirstOrDefaultAsync(ct)
            : null;
        var camposValores = x.CamposValores
            .Select(v => new PqrsdCampoValorDto(v.PqrsdCampoId, v.Valor)).ToList();

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaCreacion = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
        var semaforo = CalcularSemaforo(x.Estado, x.TutelaActiva, fechaCreacion, x.FechaVencimiento, hoy);
        var diasHasta = x.FechaVencimiento.DayNumber - hoy.DayNumber;

        var plazo = await _db.PqrsdConfiguracionPlazos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Tipo == x.Tipo, ct);
        var urgencia = plazo?.NivelUrgencia ?? NivelUrgenciaPqrsd.Media;

        string? tipoNombre = x.TipoId.HasValue
            ? await _db.PqrsdTipos.AsNoTracking().Where(t => t.Id == x.TipoId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
            : null;
        tipoNombre ??= TipoNombreBase(x.Tipo);

        // Radicador (filtrar nombre si hay reserva)
        var rad = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == x.RadicadorPersonaId, ct);
        string? radNombre = x.IdentidadReservada ? null : (rad is null ? null : $"{rad.Nombres} {rad.Apellidos}".Trim());
        Guid? radId = x.IdentidadReservada ? null : x.RadicadorPersonaId;

        // Nombre de quien subio cada adjunto (Users -> PersonaId -> Personas), para pintar la burbuja.
        var subidoIds = x.Adjuntos.Select(a => a.SubidoPorUsuarioId).Where(g => g != Guid.Empty).Distinct().ToList();
        var nombresSubido = new Dictionary<Guid, string>();
        if (subidoIds.Count > 0)
        {
            var users = await _db.Users.AsNoTracking().Where(u => subidoIds.Contains(u.Id))
                .Select(u => new { u.Id, u.PersonaId }).ToListAsync(ct);
            var personaIds = users.Where(u => u.PersonaId != null).Select(u => u.PersonaId!.Value).Distinct().ToList();
            var personasN = personaIds.Count == 0 ? new() : await _db.Personas.AsNoTracking()
                .Where(p => personaIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
            foreach (var u in users)
                if (u.PersonaId is { } pid && personasN.TryGetValue(pid, out var nm)) nombresSubido[u.Id] = nm;
        }

        var adjuntos = x.Adjuntos.OrderBy(a => a.CreatedAt)
            .Select(a => new PqrsdAdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt,
                a.SubidoPorUsuarioId != Guid.Empty && nombresSubido.TryGetValue(a.SubidoPorUsuarioId, out var sn) ? sn : null,
                a.SubidoPorUsuarioId == Guid.Empty ? null : a.SubidoPorUsuarioId,
                a.Texto, a.Compartido))
            .ToList();

        var historial = x.Historial.OrderByDescending(h => h.CreatedAt)
            .Select(h => new PqrsdHistorialDto(h.EstadoAnterior, h.EstadoNuevo, h.ActorUsuarioId, h.Origen, h.Nota, h.CreatedAt))
            .ToList();

        // Comite
        var sesion = await _db.PqrsdComiteSesiones.AsNoTracking()
            .Include(s => s.Miembros)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(s => s.ExpedienteId == id, ct);
        PqrsdComiteSesionDto? comiteDto = null;
        if (sesion is not null)
        {
            var personaIds = sesion.Miembros.Select(m => m.PersonaId).ToList();
            var personas = await _db.Personas.AsNoTracking()
                .Where(p => personaIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
            var miembros = sesion.Miembros.Select(m => new PqrsdComiteMiembroDto(
                m.Id, m.PersonaId, personas.GetValueOrDefault(m.PersonaId, ""))).ToList();
            comiteDto = new PqrsdComiteSesionDto(
                sesion.Id, sesion.FechaSesion, sesion.Modalidad, sesion.EnlaceReunion,
                sesion.Resultado, sesion.BorradorActa, sesion.ActaFinal,
                sesion.ActivadaPorUsuarioId, sesion.CreatedAt, miembros);
        }

        // Asignado (persona responsable) - nombre por join (sin FK dura)
        string? asignadoNombre = null;
        if (x.AsignadoPersonaId is { } apid)
        {
            var asig = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == apid, ct);
            asignadoNombre = asig is null ? null : $"{asig.Nombres} {asig.Apellidos}".Trim();
        }

        // Reportes de actividad (comentarios libres), mas recientes primero
        var comentarios = await _db.PqrsdComentarios.AsNoTracking()
            .Where(c => c.PqrsdExpedienteId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PqrsdComentarioDto(c.Id, c.Texto, c.AutorNombre, c.CreatedAt, c.AutorUsuarioId))
            .ToListAsync(ct);

        return new PqrsdExpedienteDetalleDto(
            x.Id, x.NumeroRadicado, x.Tipo, x.CategoriaId, x.Categoria!.Nombre, x.Descripcion,
            x.Estado, semaforo, radNombre, radId, unidadNumero, x.IdentidadReservada, x.TutelaActiva,
            x.TutelaActivadaAt, x.FechaVencimiento, diasHasta, urgencia,
            x.RespuestaAdmin, x.RespuestaAdminAt, x.InconformidadTexto, x.InconformidadAt,
            x.RespuestaDefinitiva, x.RespuestaDefinitivaAt, x.FechaCierre, x.TareaId,
            x.CreatedAt, adjuntos, historial, comiteDto,
            x.EstadoId, x.UnidadPrivadaId, x.Archivado, camposValores, x.TipoId, tipoNombre,
            x.AsignadoPersonaId, asignadoNombre, x.Progreso, comentarios, x.ProrrogaDias,
            x.IdentidadReservada ? null : rad?.Email,
            x.IdentidadReservada ? null : rad?.Telefono,
            x.MedioRecepcion, x.Seccional, x.Administrador, x.FechaRecibido);
    }

    // ===================== Radicacion =====================

    public async Task<PqrsdExpedienteDetalleDto> RadicarAsync(RadicarPqrsdRequest req, CancellationToken ct)
    {
        await AsegurarCatalogoBaseAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Descripcion) || req.Descripcion.Trim().Length < 20)
            throw new InvalidOperationException("Descripcion obligatoria, minimo 20 caracteres.");
        if (req.Descripcion.Length > 2000)
            throw new InvalidOperationException("Descripcion maxima 2000 caracteres.");

        var categoria = await _db.PqrsdCategorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CategoriaId, ct)
            ?? throw new InvalidOperationException("Categoria invalida.");
        if (!categoria.Activa) throw new InvalidOperationException("La categoria no esta activa.");

        // Tipo: si viene TipoId se usa el tipo configurable (nombre + plazo + conducta legal Legal);
        // si no, se resuelve el tipo base del enum recibido (compatibilidad con el flujo viejo).
        PqrsdTipo? tipoConfig = req.TipoId is { } tid
            ? (await _db.PqrsdTipos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid && t.Activo, ct)
                ?? throw new InvalidOperationException("El tipo seleccionado no existe o esta inactivo."))
            : await _db.PqrsdTipos.AsNoTracking().FirstOrDefaultAsync(t => t.EsBase && t.Legal == req.Tipo, ct);
        var tipoLegal = tipoConfig?.Legal ?? req.Tipo;

        if (req.IdentidadReservada && tipoLegal != TipoPqrsd.Denuncia)
            throw new InvalidOperationException("La reserva de identidad solo aplica al tipo Denuncia (RN-02).");

        // Radicador: si el admin selecciona una persona del directorio, se usa esa; si no, la del usuario actual.
        Guid personaId;
        if (req.RadicadorPersonaId is { } radPid)
        {
            var existe = await _db.Personas.AsNoTracking().AnyAsync(p => p.Id == radPid, ct);
            if (!existe) throw new InvalidOperationException("La persona seleccionada como radicador no existe.");
            personaId = radPid;
        }
        else
        {
            personaId = await GetPersonaActualIdAsync(ct)
                ?? throw new InvalidOperationException("No se pudo resolver el radicador (persona del usuario autenticado).");
        }

        // Plazo: del tipo configurable si existe; si no, del plazo legacy por enum legal.
        int diasHabiles;
        if (tipoConfig is not null) { diasHabiles = tipoConfig.DiasHabiles; }
        else
        {
            var plazo = await _db.PqrsdConfiguracionPlazos.AsNoTracking().FirstOrDefaultAsync(p => p.Tipo == tipoLegal, ct)
                ?? throw new InvalidOperationException("No hay plazo configurado para este tipo.");
            diasHabiles = plazo.DiasHabiles;
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // El plazo legal cuenta desde la FECHA DE RECIBIDO real si se informa (bitacora legal); si no, desde hoy.
        var fechaBasePlazo = req.FechaRecibido ?? hoy;
        var fechaVencimiento = SumarDiasHabiles(fechaBasePlazo, diasHabiles);
        var numero = await GenerarNumeroRadicadoAsync(ct);

        var columnaRecibida = await _db.PqrsdEstados.AsNoTracking()
            .Where(e => e.SemanticaLegal == EstadoPqrsd.Recibida).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);

        var exp = new PqrsdExpediente
        {
            NumeroRadicado = numero,
            Tipo = tipoLegal,
            TipoId = tipoConfig?.Id,
            CategoriaId = req.CategoriaId,
            Descripcion = req.Descripcion.Trim(),
            Estado = EstadoPqrsd.Recibida,
            EstadoId = columnaRecibida,
            RadicadorPersonaId = personaId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            IdentidadReservada = req.IdentidadReservada,
            FechaVencimiento = fechaVencimiento,
            // Datos de recepcion (bitacora legal).
            MedioRecepcion = req.MedioRecepcion,
            Seccional = string.IsNullOrWhiteSpace(req.Seccional) ? null : req.Seccional.Trim(),
            Administrador = string.IsNullOrWhiteSpace(req.Administrador) ? null : req.Administrador.Trim(),
            FechaRecibido = req.FechaRecibido
        };
        _db.PqrsdExpedientes.Add(exp);

        // Valores de campos dinamicos capturados al radicar.
        if (req.Campos is { Count: > 0 })
        {
            var camposActivos = await _db.PqrsdCampos.AsNoTracking().Where(c => c.Activo).Select(c => c.Id).ToHashSetAsync(ct);
            foreach (var cv in req.Campos)
            {
                if (!camposActivos.Contains(cv.CampoId)) continue;
                if (string.IsNullOrWhiteSpace(cv.Valor)) continue;
                _db.PqrsdCampoValores.Add(new PqrsdCampoValor { Expediente = exp, PqrsdCampoId = cv.CampoId, Valor = cv.Valor });
            }
        }

        // Adjuntos iniciales
        if (req.Adjuntos is { Count: > 0 })
        {
            foreach (var a in req.Adjuntos)
            {
                _db.PqrsdAdjuntos.Add(new PqrsdAdjunto
                {
                    Expediente = exp,
                    NombreArchivo = a.NombreArchivo,
                    TipoMime = a.TipoMime,
                    TamanioBytes = a.TamanioBytes,
                    UrlStorage = a.UrlStorage,
                    SubidoPorUsuarioId = GetUsuarioActualId()
                });
            }
        }

        // Historial inicial
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            Expediente = exp,
            EstadoAnterior = null,
            EstadoNuevo = EstadoPqrsd.Recibida,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = $"Expediente radicado: {numero}"
        });

        await _db.SaveChangesAsync(ct);

        await NotificarAdminsTenantAsync("2.9", exp.Id,
            $"PQRSD radicado: {numero}",
            $"Se radico un expediente {exp.Tipo} con plazo legal. Asignar y responder dentro del SLA.",
            exp.Tipo == TipoPqrsd.Denuncia ? Domain.Enums.PrioridadNotificacion.Alta : Domain.Enums.PrioridadNotificacion.Normal,
            ct);

        return (await GetExpedienteAsync(exp.Id, ct))!;
    }

}
