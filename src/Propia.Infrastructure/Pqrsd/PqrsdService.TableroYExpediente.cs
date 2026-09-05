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
    // Tablero: columnas (estados) y campos dinamicos; archivar/actualizar expediente; tareas enlazadas al PQR.
    // ===================== Tablero: columnas (estados) configurables =====================

    private static PqrsdEstadoDto MapEstado(PqrsdEstado e) => new(
        e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal, e.EsBase, e.Activo, e.SemanticaLegal);

    public async Task<IReadOnlyList<PqrsdEstadoDto>> ListarEstadosAsync(CancellationToken ct)
    {
        await AsegurarTableroBaseAsync(ct);
        return await _db.PqrsdEstados.AsNoTracking()
            .OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new PqrsdEstadoDto(e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal, e.EsBase, e.Activo, e.SemanticaLegal))
            .ToListAsync(ct);
    }

    public async Task<PqrsdEstadoDto> CrearEstadoAsync(CrearEstadoPqrsdRequest req, CancellationToken ct)
    {
        await AsegurarTableroBaseAsync(ct);
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var nom = req.Nombre.Trim();
        if (await _db.PqrsdEstados.AnyAsync(e => e.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe una columna con este nombre.");
        var maxOrden = await _db.PqrsdEstados.AnyAsync(ct) ? await _db.PqrsdEstados.MaxAsync(e => e.Orden, ct) : 0;
        var estado = new PqrsdEstado
        {
            Nombre = nom,
            Color = string.IsNullOrWhiteSpace(req.Color) ? "#6D4FE3" : req.Color!.Trim(),
            Orden = maxOrden + 1,
            EsTerminal = false,
            EsBase = false,
            Activo = true,
            SemanticaLegal = null
        };
        _db.PqrsdEstados.Add(estado);
        await _db.SaveChangesAsync(ct);
        return MapEstado(estado);
    }

    public async Task<bool> ActualizarEstadoAsync(Guid id, ActualizarEstadoPqrsdRequest req, CancellationToken ct)
    {
        var e = await _db.PqrsdEstados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var nom = req.Nombre.Trim();
        if (await _db.PqrsdEstados.AnyAsync(x => x.Id != id && x.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe una columna con este nombre.");
        e.Nombre = nom;
        if (!string.IsNullOrWhiteSpace(req.Color)) e.Color = req.Color!.Trim();
        e.Orden = req.Orden;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarEstadoAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.PqrsdEstados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        var total = await _db.PqrsdEstados.CountAsync(ct);
        if (total <= 1) throw new InvalidOperationException("El tablero debe tener al menos una columna.");
        var enUso = await _db.PqrsdExpedientes.AnyAsync(x => x.EstadoId == id, ct);
        if (enUso) throw new InvalidOperationException("Hay PQR en esta columna. Muevelos a otra columna antes de eliminarla.");
        _db.PqrsdEstados.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReordenarEstadoAsync(Guid id, string direccion, CancellationToken ct)
    {
        var lista = await _db.PqrsdEstados.OrderBy(e => e.Orden).ThenBy(e => e.Nombre).ToListAsync(ct);
        var idx = lista.FindIndex(e => e.Id == id);
        if (idx < 0) return false;
        var dir = (direccion ?? "").ToLowerInvariant();
        var j = dir is "arriba" or "up" or "-1" ? idx - 1 : idx + 1;
        if (j < 0 || j >= lista.Count) return true;
        (lista[idx].Orden, lista[j].Orden) = (lista[j].Orden, lista[idx].Orden);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MoverAEstadoAsync(Guid expedienteId, Guid estadoId, CancellationToken ct)
    {
        await AsegurarTableroBaseAsync(ct);
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (x is null) return false;
        var col = await _db.PqrsdEstados.FirstOrDefaultAsync(e => e.Id == estadoId, ct)
            ?? throw new InvalidOperationException("Columna no encontrada.");
        x.EstadoId = col.Id;
        // Si la columna arrastrada tiene semantica legal, sincronizar el enum legal (plazos/semaforo).
        if (col.SemanticaLegal is { } sem && sem != x.Estado)
        {
            var anterior = x.Estado;
            x.Estado = sem;
            if (sem == EstadoPqrsd.Cerrada || sem == EstadoPqrsd.ViaInternaAgotada)
            {
                x.FechaCierre ??= DateTimeOffset.UtcNow;
                x.CerradoPorUsuarioId ??= GetUsuarioActualId();
            }
            _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
            {
                ExpedienteId = x.Id,
                EstadoAnterior = anterior,
                EstadoNuevo = sem,
                ActorUsuarioId = GetUsuarioActualId(),
                Origen = OrigenCambioEstado.Manual,
                Nota = $"Movido a columna '{col.Nombre}'"
            });
        }
        x.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Tablero: campos dinamicos =====================

    private static PqrsdCampoDto MapCampo(PqrsdCampo c) => new(
        c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna,
        c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo);

    public async Task<IReadOnlyList<PqrsdCampoDto>> ListarCamposAsync(CancellationToken ct)
    {
        return await _db.PqrsdCampos.AsNoTracking().Where(c => c.Activo)
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new PqrsdCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna,
                c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo, c.MostrarEnPublico))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PqrsdCampoDto>> ListarCamposArchivadosAsync(CancellationToken ct)
    {
        return await _db.PqrsdCampos.AsNoTracking().Where(c => !c.Activo)
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new PqrsdCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna,
                c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo, c.MostrarEnPublico))
            .ToListAsync(ct);
    }

    public async Task<PqrsdCampoDto> CrearCampoAsync(GuardarCampoPqrsdRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("La etiqueta del campo es obligatoria.");
        var label = req.Label.Trim();
        if (await _db.PqrsdCampos.AnyAsync(c => c.Activo && c.Label == label, ct))
            throw new InvalidOperationException("Ya existe un campo activo con esta etiqueta.");
        var maxOrden = await _db.PqrsdCampos.AnyAsync(ct) ? await _db.PqrsdCampos.MaxAsync(c => c.Orden, ct) : 0;
        var c = new PqrsdCampo
        {
            Label = label,
            Orden = maxOrden + 1,
            Tipo = req.Tipo,
            Opciones = req.Opciones,
            MostrarEnFiltro = req.MostrarEnFiltro,
            Columna = Math.Clamp(req.Columna, 1, 3),
            Descripcion = req.Descripcion,
            Requerido = req.Requerido,
            ValorPorDefecto = req.ValorPorDefecto,
            PermiteVarios = req.PermiteVarios,
            CamposSuma = req.CamposSuma,
            Activo = true
        };
        _db.PqrsdCampos.Add(c);
        await _db.SaveChangesAsync(ct);
        return MapCampo(c);
    }

    public async Task<bool> ActualizarCampoAsync(Guid id, GuardarCampoPqrsdRequest req, CancellationToken ct)
    {
        var c = await _db.PqrsdCampos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("La etiqueta del campo es obligatoria.");
        var label = req.Label.Trim();
        if (await _db.PqrsdCampos.AnyAsync(x => x.Id != id && x.Activo && x.Label == label, ct))
            throw new InvalidOperationException("Ya existe un campo activo con esta etiqueta.");
        c.Label = label;
        c.Tipo = req.Tipo;
        c.Opciones = req.Opciones;
        c.MostrarEnFiltro = req.MostrarEnFiltro;
        c.Columna = Math.Clamp(req.Columna, 1, 3);
        c.Descripcion = req.Descripcion;
        c.Requerido = req.Requerido;
        c.ValorPorDefecto = req.ValorPorDefecto;
        c.PermiteVarios = req.PermiteVarios;
        c.CamposSuma = req.CamposSuma;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.PqrsdCampos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        await _db.PqrsdCampoValores.Where(v => v.PqrsdCampoId == id).ExecuteDeleteAsync(ct);
        _db.PqrsdCampos.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetCampoActivoAsync(Guid id, bool activo, CancellationToken ct)
    {
        var c = await _db.PqrsdCampos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (activo && await _db.PqrsdCampos.AnyAsync(x => x.Id != id && x.Activo && x.Label == c.Label, ct))
            throw new InvalidOperationException("Ya existe un campo activo con esta etiqueta. Renombra antes de restaurar.");
        c.Activo = activo;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReordenarCampoAsync(Guid id, string direccion, CancellationToken ct)
    {
        var lista = await _db.PqrsdCampos.Where(c => c.Activo).OrderBy(c => c.Orden).ThenBy(c => c.Label).ToListAsync(ct);
        var idx = lista.FindIndex(c => c.Id == id);
        if (idx < 0) return false;
        var dir = (direccion ?? "").ToLowerInvariant();
        var j = dir is "arriba" or "up" or "-1" ? idx - 1 : idx + 1;
        if (j < 0 || j >= lista.Count) return true;
        (lista[idx].Orden, lista[j].Orden) = (lista[j].Orden, lista[idx].Orden);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Expediente: archivar + actualizar =====================

    public async Task<bool> ArchivarExpedienteAsync(Guid id, bool archivar, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        x.Archivado = archivar;
        x.ArchivadoAt = archivar ? DateTimeOffset.UtcNow : null;
        x.ArchivadoPorUsuarioId = archivar ? GetUsuarioActualId() : null;
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = x.Estado,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = archivar ? "Expediente archivado" : "Expediente restaurado desde archivados"
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Reportar actividad: agrega un comentario libre al expediente (chat estilo Tareas).
    // Devuelve el comentario creado para que la UI lo agregue sin recargar, y notifica @menciones.
    public async Task<PqrsdComentarioDto?> ReportarActividadAsync(Guid id, ReportarActividadPqrsdRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Texto)) throw new InvalidOperationException("El texto de la actividad es obligatorio.");
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (exp is null) return null;
        var (uid, nombre) = ActorActual();
        var c = new PqrsdComentario
        {
            PqrsdExpedienteId = id,
            Texto = req.Texto.Trim(),
            AutorUsuarioId = uid,
            AutorNombre = nombre
        };
        _db.PqrsdComentarios.Add(c);
        await _db.SaveChangesAsync(ct);
        await NotificarMencionesAsync(exp, c.Texto, ct);
        return new PqrsdComentarioDto(c.Id, c.Texto, c.AutorNombre, c.CreatedAt, c.AutorUsuarioId);
    }

    // Notifica @menciones escritas en un comentario/caption (canal InApp). Port del feed de Tareas.
    public async Task NotificarMencionComentarioAsync(Guid expedienteId, string? texto, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is not null) await NotificarMencionesAsync(exp, texto, ct);
    }

    // Detecta tokens @nombre.apellido y avisa a las personas del directorio que coincidan.
    private async Task NotificarMencionesAsync(PqrsdExpediente exp, string? texto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return;
        var matches = System.Text.RegularExpressions.Regex.Matches(texto, @"@([A-Za-z0-9._-]{2,40})");
        if (matches.Count == 0) return;
        var tokens = matches.Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToHashSet();

        var personas = await _db.Personas.AsNoTracking()
            .Select(p => new { p.Id, Nombre = (p.Nombres + " " + p.Apellidos).Trim() })
            .ToListAsync(ct);
        var destinatarios = personas
            .Where(p => tokens.Contains(p.Nombre.ToLowerInvariant().Replace(" ", ".")))
            .Select(p => p.Id).Distinct().Take(20).ToList();
        if (destinatarios.Count == 0) return;

        var resumen = texto.Length > 120 ? texto[..120] + "..." : texto;
        foreach (var pid in destinatarios)
            await _noti.EnviarEventoUsuarioAsync(pid,
                $"Mencion en PQR {exp.NumeroRadicado}",
                $"Te mencionaron en el PQR {exp.NumeroRadicado}: {resumen}",
                "2.9", exp.Id, tenantId, Domain.Enums.PrioridadNotificacion.Normal, ct);
    }

    // Genera una tarea interna (modulo 2.10) a partir del PQR y la vincula (TareaId). Idempotente.
    public async Task<Guid?> GenerarTareaAsync(Guid id, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return null;
        if (x.TareaId is { } yaExiste) return yaExiste;

        var resumen = x.Descripcion.Length > 60 ? x.Descripcion[..60] + "..." : x.Descripcion;
        var tarea = await _tareas.CrearTareaAsync(new Propia.Application.Tareas.CrearTareaRequest(
            Titulo: $"PQR {x.NumeroRadicado}: {resumen}",
            Descripcion: x.Descripcion,
            Prioridad: PrioridadTarea.Alta,
            EstadoId: null,
            AsignadoPersonaId: x.AsignadoPersonaId,
            FechaInicio: null,
            FechaVencimiento: x.FechaVencimiento,
            PadreId: null,
            EtiquetaIds: null), ct);

        x.TareaId = tarea.Id;
        x.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return tarea.Id;
    }

    public async Task<bool> ActualizarExpedienteAsync(Guid id, ActualizarExpedienteRequest req, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;

        x.UnidadPrivadaId = req.UnidadPrivadaId;

        // Persona asignada (Guid.Empty = quitar; null = no tocar; otro = asignar validando)
        var prevAsignado = x.AsignadoPersonaId;
        Guid? asignadoNuevo = null;
        if (req.AsignadoPersonaId is { } aPid)
        {
            if (aPid == Guid.Empty) x.AsignadoPersonaId = null;
            else if (await _db.Personas.AsNoTracking().AnyAsync(p => p.Id == aPid, ct))
            {
                x.AsignadoPersonaId = aPid;
                if (aPid != prevAsignado) asignadoNuevo = aPid;
            }
            else throw new InvalidOperationException("La persona asignada no existe.");
        }
        if (req.Progreso is { } prog) x.Progreso = Math.Clamp(prog, 0, 100);

        if (req.RadicadorPersonaId is { } radPid && radPid != x.RadicadorPersonaId)
        {
            if (!await _db.Personas.AsNoTracking().AnyAsync(p => p.Id == radPid, ct))
                throw new InvalidOperationException("La persona seleccionada no existe.");
            x.RadicadorPersonaId = radPid;
        }

        if (!string.IsNullOrWhiteSpace(req.Descripcion))
        {
            var desc = req.Descripcion.Trim();
            if (desc.Length > 2000) throw new InvalidOperationException("Descripcion maxima 2000 caracteres.");
            x.Descripcion = desc;
        }

        // Datos de recepcion (bitacora legal). Solo si el modal lo pide explicitamente.
        if (req.ActualizarRecepcion)
        {
            x.MedioRecepcion = req.MedioRecepcion;
            x.Seccional = string.IsNullOrWhiteSpace(req.Seccional) ? null : req.Seccional.Trim();
            x.Administrador = string.IsNullOrWhiteSpace(req.Administrador) ? null : req.Administrador.Trim();
            if (req.FechaRecibido != x.FechaRecibido)
            {
                x.FechaRecibido = req.FechaRecibido;
                // Recalcular el plazo legal desde la nueva fecha de recibido (o desde la radicacion si se limpia).
                int diasHabiles = 0;
                if (x.TipoId is { } tid)
                    diasHabiles = await _db.PqrsdTipos.AsNoTracking().Where(t => t.Id == tid).Select(t => t.DiasHabiles).FirstOrDefaultAsync(ct);
                if (diasHabiles == 0)
                    diasHabiles = (await _db.PqrsdConfiguracionPlazos.AsNoTracking().FirstOrDefaultAsync(p => p.Tipo == x.Tipo, ct))?.DiasHabiles ?? 15;
                var baseDate = req.FechaRecibido ?? DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
                x.FechaVencimiento = SumarDiasHabiles(baseDate, diasHabiles + x.ProrrogaDias);
            }
        }

        // Upsert de campos dinamicos.
        if (req.Campos is not null)
        {
            var existentes = await _db.PqrsdCampoValores.Where(v => v.ExpedienteId == id).ToListAsync(ct);
            var camposActivos = await _db.PqrsdCampos.AsNoTracking().Where(c => c.Activo).Select(c => c.Id).ToHashSetAsync(ct);
            foreach (var cv in req.Campos)
            {
                if (!camposActivos.Contains(cv.CampoId)) continue;
                var actual = existentes.FirstOrDefault(v => v.PqrsdCampoId == cv.CampoId);
                if (string.IsNullOrWhiteSpace(cv.Valor))
                {
                    if (actual is not null) _db.PqrsdCampoValores.Remove(actual);
                }
                else if (actual is null)
                {
                    _db.PqrsdCampoValores.Add(new PqrsdCampoValor { ExpedienteId = id, PqrsdCampoId = cv.CampoId, Valor = cv.Valor });
                }
                else
                {
                    actual.Valor = cv.Valor;
                    actual.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        x.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notificar al nuevo responsable del expediente (todos sus canales configurados).
        if (asignadoNuevo is { } respPid)
            await _noti.EnviarEventoUsuarioAsync(respPid, $"PQR asignado: {x.NumeroRadicado}",
                $"Te asignaron como responsable del PQR {x.NumeroRadicado}", "2.9", x.Id,
                _tenantContext.CurrentTenantId, Domain.Enums.PrioridadNotificacion.Normal, ct);

        return true;
    }

    // ===================== Tareas enlazadas al PQR (tablero configurable) =====================
    private const string TableroPqrsdNombre = "PQRSD";

    private async Task<Guid> AsegurarTableroPqrsdAsync(CancellationToken ct)
    {
        // Si el administrador configuro un tablero destino (y sigue existiendo), se respeta.
        var cfg = await _db.PqrsdTareasConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg?.TableroId is Guid elegido && await _db.Tableros.AnyAsync(t => t.Id == elegido, ct))
            return elegido;
        // Fallback: tablero "PQRSD" por defecto (se crea si no existe).
        var board = await _db.Tableros.FirstOrDefaultAsync(t => t.Nombre == TableroPqrsdNombre, ct);
        if (board is not null) return board.Id;
        var dto = await _tareas.CrearTableroAsync(
            new Propia.Application.Tareas.GuardarTableroRequest(TableroPqrsdNombre, "Tareas generadas desde PQRSD", "#7C5CFA", new List<Guid>()), ct);
        return dto.Id;
    }

    public async Task<Guid?> ObtenerTableroTareasConfigAsync(CancellationToken ct)
        => (await _db.PqrsdTareasConfigs.AsNoTracking().FirstOrDefaultAsync(ct))?.TableroId;

    public async Task GuardarTableroTareasConfigAsync(Guid? tableroId, CancellationToken ct)
    {
        if (tableroId is Guid tid && !await _db.Tableros.AnyAsync(t => t.Id == tid, ct))
            throw new InvalidOperationException("El tablero elegido no existe.");
        var cfg = await _db.PqrsdTareasConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null) { cfg = new PqrsdTareasConfig { TableroId = tableroId }; _db.PqrsdTareasConfigs.Add(cfg); }
        else cfg.TableroId = tableroId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Guid?> CrearTareaDePqrAsync(Guid pqrId, CrearPqrTareaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo)) return null;
        if (!await _db.PqrsdExpedientes.AnyAsync(x => x.Id == pqrId, ct)) return null;
        var boardId = await AsegurarTableroPqrsdAsync(ct);
        var det = await _tareas.CrearTareaAsync(new Propia.Application.Tareas.CrearTareaRequest(
            req.Titulo.Trim(), null, PrioridadTarea.Normal, null, req.AsignadoPersonaId,
            null, null, null, null, TableroId: boardId), ct);
        // Enlazar la tarea al PQR (Origen = modulo externo). CrearTareaRequest no lleva estos campos.
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == det.Id, ct);
        if (t is not null)
        {
            t.Origen = OrigenTarea.ModuloExterno;
            t.ModuloOrigenCodigo = TableroPqrsdNombre;
            t.ModuloOrigenEntidadId = pqrId;
            await _db.SaveChangesAsync(ct);
        }

        // Avisar al responsable del PQR que se creo una tarea en su expediente (todos sus canales).
        var exp = await _db.PqrsdExpedientes.AsNoTracking()
            .Where(x => x.Id == pqrId)
            .Select(x => new { x.AsignadoPersonaId, x.NumeroRadicado })
            .FirstOrDefaultAsync(ct);
        if (exp?.AsignadoPersonaId is { } respPid)
            await _noti.EnviarEventoUsuarioAsync(respPid, $"Nueva tarea en PQR {exp.NumeroRadicado}",
                $"Se creo la tarea \"{req.Titulo.Trim()}\" en el PQR {exp.NumeroRadicado}", "2.9", det.Id,
                _tenantContext.CurrentTenantId, Domain.Enums.PrioridadNotificacion.Normal, ct);

        return det.Id;
    }

    public async Task<PqrTareasDto> ListTareasDePqrAsync(Guid pqrId, CancellationToken ct)
    {
        var boardId = await AsegurarTableroPqrsdAsync(ct);
        var etapas = await _db.TareasEstados.AsNoTracking()
            .Where(e => e.TableroId == boardId).OrderBy(e => e.Orden)
            .Select(e => new PqrEtapaDto(e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal))
            .ToListAsync(ct);
        var tareas = await _db.Tareas.AsNoTracking()
            .Where(t => t.ModuloOrigenCodigo == TableroPqrsdNombre && t.ModuloOrigenEntidadId == pqrId && !t.Eliminada)
            .Include(t => t.Estado).Include(t => t.AsignadoPersona)
            .OrderBy(t => t.NumeroTarea)
            .Select(t => new PqrTareaDto(t.Id, t.NumeroTarea, t.Titulo, t.EstadoId,
                t.Estado!.Nombre, t.Estado.Color, t.Estado.EsTerminal,
                t.AsignadoPersona != null ? (t.AsignadoPersona.Nombres + " " + t.AsignadoPersona.Apellidos).Trim() : null,
                t.Prioridad.ToString(), t.FechaVencimiento, t.Progreso))
            .ToListAsync(ct);
        var pct = tareas.Count == 0 ? 0 : (int)Math.Round(100.0 * tareas.Count(x => x.EstadoEsTerminal) / tareas.Count);
        return new PqrTareasDto(etapas, tareas, pct);
    }
}
