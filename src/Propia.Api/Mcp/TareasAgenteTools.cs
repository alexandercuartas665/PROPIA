using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Tareas;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Mcp;

/// <summary>
/// Tools MCP del modulo 2.10 Tareas y Proyectos para el agente INTERNO/ADMIN (NO porteria):
/// - crear_tarea: crea una tarea (dry-run: propone y confirma). Resuelve el tablero por nombre (o el
///   por defecto) y deja el estado inicial en la primera columna del tablero.
/// - estado_tarea: consulta el estado/progreso/asignado de una tarea por id, numero o titulo.
///
/// El tenant/RLS lo fija el TenantMiddleware. Estas tools se asignan al agente INTERNO (Asistente)
/// por la API de administracion; NO al agente de porteria.
/// </summary>
[McpServerToolType]
public sealed class TareasAgenteTools
{
    [McpServerTool(Name = "crear_tarea")]
    [Description("Crea una tarea del modulo de Tareas y Proyectos. Resuelve el tablero destino por su nombre (o usa el tablero por defecto si no se indica) y deja la tarea en la primera columna (estado inicial). La prioridad por defecto es Normal. El asignado (opcional) se resuelve por nombre contra el directorio de la copropiedad; si no se resuelve, la tarea queda sin asignar y se avisa. Algunos agentes solo pueden crear tareas en ciertos tableros y/o reparten automaticamente el responsable; en ese caso la herramienta lo aplica sola. Es dry-run por defecto: propone la tarea para confirmar; vuelve a llamar con dryRun=false para crearla y devolver su numero.")]
    public static async Task<ResultadoCreacionMcp> CrearTarea(
        ITareasService tareas, PropiaDbContext db, ITenantContext tenant, IAgentCallContext call,
        [Description("Titulo de la tarea (obligatorio).")] string titulo,
        CancellationToken ct,
        [Description("Descripcion / detalle de la tarea (opcional).")] string? descripcion = null,
        [Description("Nombre del tablero destino (opcional). Si se omite se usa el tablero por defecto.")] string? tablero = null,
        [Description("Prioridad: Urgente, Alta, Normal o Baja. Por defecto Normal.")] string? prioridad = null,
        [Description("Nombre de la persona a asignar (opcional). Se resuelve contra el directorio de la copropiedad.")] string? asignado = null,
        [Description("Si true (por defecto) solo propone sin crear. Pasa false para crear la tarea.")] bool dryRun = true)
    {
        if (tenant.CurrentTenantId is null) { return Fail(dryRun, "No hay copropiedad activa en el contexto."); }

        var tit = (titulo ?? "").Trim();
        if (tit.Length < 3) { return Fail(dryRun, "El titulo de la tarea es obligatorio (minimo 3 caracteres)."); }

        // Ola H: config de enrutamiento del agente (whitelist de tableros + reparto round-robin). Se carga
        // TRACKED porque el reparto avanza el cursor. Si no hay agente en contexto (llamada directa), null
        // -> sin whitelist ni reparto (comportamiento historico).
        Domain.Entities.AiAgent? agente = call.AgentId is Guid aid
            ? await db.AiAgents.FirstOrDefaultAsync(a => a.Id == aid, ct)
            : null;

        // Prioridad (default Normal).
        var prio = PrioridadTarea.Normal;
        if (!string.IsNullOrWhiteSpace(prioridad) && !TryPrioridad(prioridad, out prio))
        {
            return Fail(dryRun, $"Prioridad '{prioridad}' no valida. Usa Urgente, Alta, Normal o Baja.");
        }

        // Tablero: por nombre, o el por defecto, acotado por la WHITELIST del agente (Ola H).
        // allowed == null => sin restriccion (todos). allowed con 1+ ids => solo esos. Semantica ECOREX:
        //  - con 1 permitido, un nombre ausente o que no calza cae al unico permitido (jamas se escapa);
        //  - con 2+ permitidos, no hay default: si no calza un permitido, se rechaza listando solo los permitidos.
        var allowedBoards = agente is not null ? AgentEnrutamientoTareas.ParseIds(agente.AllowedBoardIdsJson) : Array.Empty<Guid>();
        var allowed = allowedBoards.Count > 0 ? allowedBoards : null;

        var tableros = await tareas.ListarTablerosAsync(ct);
        var universo = allowed is null ? tableros : tableros.Where(b => allowed.Contains(b.Id)).ToList();

        Guid? tableroId = null;
        var tableroNombre = "(por defecto)";
        TableroDto? elegido = null;
        if (!string.IsNullOrWhiteSpace(tablero))
        {
            elegido = AgentContactoHelper.MatchByName(universo, b => b.Nombre, tablero);
        }
        if (elegido is null && allowed is { Count: 1 })
        {
            elegido = universo.FirstOrDefault(b => b.Id == allowed[0]);  // comodidad: 1 permitido = por defecto
        }
        if (elegido is null)
        {
            if (allowed is null)
            {
                // Sin whitelist: nombre dado que no calza -> error; sin nombre -> tablero por defecto del servicio.
                if (!string.IsNullOrWhiteSpace(tablero))
                {
                    var nombres = string.Join(", ", universo.Select(b => b.Nombre));
                    return Fail(dryRun, $"No encontre el tablero '{tablero}'. Tableros disponibles: {nombres}.");
                }
            }
            else
            {
                // Con whitelist (2+ aqui): hay que elegir uno de los permitidos. No se escapa ni cae a default.
                var nombres = string.Join(", ", universo.Select(b => b.Nombre));
                var pedido = string.IsNullOrWhiteSpace(tablero) ? "" : $" (el tablero '{tablero}' no esta permitido para este agente)";
                return Fail(dryRun, $"Indica en cual de estos tableros crear la tarea{pedido}. Tableros disponibles: {nombres}.");
            }
        }
        else
        {
            tableroId = elegido.Id; tableroNombre = elegido.Nombre;
        }

        // Asignado: (1) si el modelo nombra a alguien y resuelve, gana el explicito; (2) si no, y el agente
        // tiene lista de reparto round-robin (Ola H), se toma el SIGUIENTE de la rotacion; (3) si no, sin asignar.
        Guid? asignadoId = null;
        string? asignadoNombre = null;
        string? asignadoNota = null;
        var porRoundRobin = false;
        int? rrNextCursor = null;
        if (!string.IsNullOrWhiteSpace(asignado))
        {
            var (pid, pnom, err) = await ResolverPersonaDirectorioAsync(db, asignado, ct);
            if (pid is { } id) { asignadoId = id; asignadoNombre = pnom; }
            else { asignadoNota = err; }
        }
        if (asignadoId is null && agente is not null)
        {
            var pool = AgentEnrutamientoTareas.ParseIds(agente.AssigneeUserIdsJson);
            if (pool.Count > 0)
            {
                var start = ((agente.AssigneeCursor % pool.Count) + pool.Count) % pool.Count;
                for (var i = 0; i < pool.Count; i++)
                {
                    var idx = (start + i) % pool.Count;
                    var cand = pool[idx];
                    // Solo personas VINCULADAS a esta copropiedad (mismo criterio que valida CrearTareaAsync):
                    // una persona que ya no esta en el directorio se salta y la rotacion sigue a la siguiente.
                    var vinculada = await db.DirectorioVinculos.AsNoTracking()
                        .AnyAsync(v => v.EntidadTipo == EntidadDirectorio.Persona && v.EntidadId == cand, ct);
                    if (!vinculada) { continue; }
                    var nom = await db.Personas.AsNoTracking()
                        .Where(p => p.Id == cand).Select(p => (p.Nombres + " " + p.Apellidos).Trim()).FirstOrDefaultAsync(ct);
                    if (!string.IsNullOrWhiteSpace(nom))
                    {
                        asignadoId = cand; asignadoNombre = nom; porRoundRobin = true;
                        rrNextCursor = (idx + 1) % pool.Count;   // el proximo arranca despues del elegido
                        break;
                    }
                }
                if (asignadoId is null) { asignadoNota = "La lista de reparto del agente no tiene personas validas; la tarea queda sin asignar."; }
            }
        }

        var comoAsig = porRoundRobin ? "asignada (reparto) a" : "asignada a";
        var asigTxt = asignadoNombre is not null ? $"{comoAsig} {asignadoNombre}" : (asignadoNota ?? "sin asignar");

        // Dry-run: NO crea nada. CrearTareaAsync abre y commitea su PROPIA transaccion, asi que no se puede
        // "crear y revertir"; el dry-run solo propone (y por eso tampoco consume la rotacion round-robin).
        if (dryRun)
        {
            return new ResultadoCreacionMcp(
                DryRun: true, Exito: true,
                Mensaje: $"Propuesta de tarea (aun no creada): '{tit}' en el tablero {tableroNombre}, prioridad {prio}, {asigTxt}. Vuelve a llamar con dryRun=false para crearla.",
                Recurso: new { titulo = tit, tablero = tableroNombre, prioridad = prio.ToString(), asignado = asignadoNombre, nota = asignadoNota });
        }

        var req = new CrearTareaRequest(
            Titulo: tit,
            Descripcion: string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
            Prioridad: prio,
            EstadoId: null,                 // el servicio asigna la primera columna del tablero
            AsignadoPersonaId: asignadoId,
            FechaInicio: null,
            FechaVencimiento: null,
            PadreId: null,
            EtiquetaIds: null,
            TableroId: tableroId);

        TareaDetalleDto det;
        try { det = await tareas.CrearTareaAsync(req, ct); }
        catch (InvalidOperationException ex) { return Fail(dryRun, $"No se pudo crear la tarea: {ex.Message}"); }

        // Creada OK -> avanza el cursor del reparto round-robin (solo si se repartio en esta llamada).
        if (porRoundRobin && rrNextCursor is int nc && agente is not null)
        {
            agente.AssigneeCursor = nc;
            await db.SaveChangesAsync(ct);
        }
        var nota = asignadoNota is not null ? $" Nota: {asignadoNota}" : "";
        return new ResultadoCreacionMcp(
            DryRun: false, Exito: true,
            Mensaje: $"Tarea creada: {det.NumeroTarea} '{det.Titulo}' ({det.Estado.Nombre}), prioridad {det.Prioridad}, {asigTxt}.{nota}",
            Recurso: new { numero = det.NumeroTarea, id = det.Id, titulo = det.Titulo, tablero = tableroNombre, estado = det.Estado.Nombre, prioridad = det.Prioridad.ToString(), asignado = det.AsignadoNombre });
    }

    [McpServerTool(Name = "estado_tarea")]
    [Description("Consulta el estado de una tarea por su Guid, su numero (ej. T-0001) o por texto del titulo. Devuelve estado (columna), progreso, prioridad, asignado y fechas. Si el texto coincide con varias tareas, devuelve la lista de coincidencias para precisar. Consulta abierta a nivel de conjunto.")]
    public static async Task<EstadoTareaResultado> EstadoTarea(
        ITareasService tareas, PropiaDbContext db, ITenantContext tenant,
        [Description("Guid de la tarea, su numero (ej. T-2026-0001) o texto del titulo para buscar.")] string id_o_texto,
        CancellationToken ct)
    {
        if (tenant.CurrentTenantId is null) { return new EstadoTareaResultado(false, "No hay copropiedad activa en el contexto.", null, null); }
        var q = (id_o_texto ?? "").Trim();
        if (q.Length == 0) { return new EstadoTareaResultado(false, "Indica el numero, id o titulo de la tarea a consultar.", null, null); }

        // Por Guid.
        if (Guid.TryParse(q, out var gid))
        {
            var d = await tareas.GetTareaAsync(gid, ct);
            return d is null
                ? new EstadoTareaResultado(false, $"No encontre ninguna tarea con id {q}.", null, null)
                : new EstadoTareaResultado(true, Resumen(d), Map(d), null);
        }

        // Por numero exacto (tenant-scoped): ListarTareasAsync busca por titulo, no por numero.
        var porNumero = await db.Tareas.AsNoTracking()
            .Where(t => t.NumeroTarea.ToLower() == q.ToLower())
            .Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (porNumero is { } tnum)
        {
            var d = await tareas.GetTareaAsync(tnum, ct);
            if (d is not null) { return new EstadoTareaResultado(true, Resumen(d), Map(d), null); }
        }

        // Por texto del titulo. verCerradas es EXCLUYENTE (true=solo cerradas), asi que consulto ambas.
        var abiertas = await tareas.ListarTareasAsync(null, null, null, null, null, q, ct, verCerradas: false);
        var cerradas = await tareas.ListarTareasAsync(null, null, null, null, null, q, ct, verCerradas: true);
        var lista = abiertas.Concat(cerradas).ToList();
        if (lista.Count == 0) { return new EstadoTareaResultado(false, $"No encontre tareas que coincidan con '{q}'.", null, null); }

        var exacta = lista.FirstOrDefault(t => string.Equals(t.NumeroTarea, q, StringComparison.OrdinalIgnoreCase));
        var elegida = exacta ?? (lista.Count == 1 ? lista[0] : null);
        if (elegida is not null)
        {
            var d = await tareas.GetTareaAsync(elegida.Id, ct);
            if (d is not null) { return new EstadoTareaResultado(true, Resumen(d), Map(d), null); }
        }

        var items = lista.Take(10)
            .Select(t => new EstadoTareaItem(t.NumeroTarea, t.Titulo, t.EstadoNombre, t.Prioridad.ToString(), t.AsignadoNombre, t.Progreso))
            .ToList();
        return new EstadoTareaResultado(true, $"Encontre {lista.Count} tareas para '{q}'. Precisa cual por su numero de tarea.", null, items);
    }

    // ---------- helpers ----------

    private static ResultadoCreacionMcp Fail(bool dryRun, string mensaje) => new(dryRun, false, mensaje, null);

    private static string Resumen(TareaDetalleDto d)
    {
        var asig = d.AsignadoNombre is not null ? $", asignada a {d.AsignadoNombre}" : ", sin asignar";
        var vence = d.FechaVencimiento is { } fv ? $", vence {fv:yyyy-MM-dd}" : "";
        return $"{d.NumeroTarea} '{d.Titulo}': {d.Estado.Nombre}, prioridad {d.Prioridad}, progreso {d.Progreso}%{asig}{vence}.";
    }

    private static EstadoTareaDetalle Map(TareaDetalleDto d) => new(
        d.NumeroTarea, d.Titulo, d.Estado.Nombre, d.Prioridad.ToString(), d.Progreso,
        d.AsignadoNombre, d.FechaVencimiento?.ToString("yyyy-MM-dd"), d.Descripcion);

    private static bool TryPrioridad(string s, out PrioridadTarea p)
    {
        var n = new string((s ?? "").ToLowerInvariant().Where(char.IsLetter).ToArray());
        switch (n)
        {
            case "urgente": p = PrioridadTarea.Urgente; return true;
            case "alta": p = PrioridadTarea.Alta; return true;
            case "normal":
            case "media": p = PrioridadTarea.Normal; return true;
            case "baja": p = PrioridadTarea.Baja; return true;
            default: p = PrioridadTarea.Normal; return false;
        }
    }

    /// <summary>Resuelve un nombre libre a una persona del directorio de la copropiedad (tenant-scoped). Best-effort.</summary>
    private static async Task<(Guid? Id, string? Nombre, string? Error)> ResolverPersonaDirectorioAsync(
        PropiaDbContext db, string nombre, CancellationToken ct)
    {
        static string N(string? s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var qn = N(nombre);
        if (qn.Length == 0) { return (null, null, null); }

        var personaIds = await db.DirectorioVinculos.AsNoTracking()
            .Where(v => v.EntidadTipo == EntidadDirectorio.Persona && v.Estado == EstadoVinculo.Activo)
            .Select(v => v.EntidadId).Distinct().ToListAsync(ct);
        if (personaIds.Count == 0) { return (null, null, $"No pude resolver el asignado '{nombre}': el directorio esta vacio."); }

        var personas = await db.Personas.AsNoTracking()
            .Where(p => personaIds.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = (p.Nombres + " " + p.Apellidos).Trim() })
            .ToListAsync(ct);

        var matches = personas.Where(p => { var n = N(p.Nombre); return n.Length > 0 && (n.Contains(qn) || qn.Contains(n)); }).ToList();
        if (matches.Count == 1) { return (matches[0].Id, matches[0].Nombre, null); }
        if (matches.Count == 0) { return (null, null, $"No encontre a '{nombre}' en el directorio; la tarea queda sin asignar."); }
        return (null, null, $"'{nombre}' es ambiguo ({matches.Count} coincidencias); la tarea queda sin asignar.");
    }

    /// <summary>Detalle de una tarea consultada.</summary>
    public sealed record EstadoTareaDetalle(
        [property: Description("Numero de la tarea (ej. T-0001).")] string Numero,
        [property: Description("Titulo.")] string Titulo,
        [property: Description("Estado / columna del tablero.")] string Estado,
        [property: Description("Prioridad (Urgente, Alta, Normal, Baja).")] string Prioridad,
        [property: Description("Progreso 0-100.")] int Progreso,
        [property: Description("Persona asignada (null si no tiene).")] string? Asignado,
        [property: Description("Fecha de vencimiento (yyyy-MM-dd) o null.")] string? Vence,
        [property: Description("Descripcion / detalle.")] string? Descripcion);

    /// <summary>Item de la lista de coincidencias cuando el texto matchea varias tareas.</summary>
    public sealed record EstadoTareaItem(string Numero, string Titulo, string Estado, string Prioridad, string? Asignado, int Progreso);

    /// <summary>Resultado de estado_tarea: una tarea puntual o la lista de coincidencias.</summary>
    public sealed record EstadoTareaResultado(
        [property: Description("True si se resolvio la consulta.")] bool Encontrada,
        [property: Description("Mensaje legible.")] string Mensaje,
        [property: Description("Detalle de la tarea si se resolvio una unica. Null si hay varias coincidencias.")] EstadoTareaDetalle? Tarea,
        [property: Description("Lista de coincidencias cuando el texto matchea varias tareas. Null si se resolvio una unica.")] IReadOnlyList<EstadoTareaItem>? Coincidencias);
}
