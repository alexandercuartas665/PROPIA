using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Mcp;

/// <summary>
/// Tools MCP del modulo 2.9 PQRSD de cara al RESIDENTE (agente de PORTERIA):
/// - crear_pqr: radica un PQR SOLO a nombre de propietarios/residentes verificados por el numero
///   real (regla de oro). Patron dry-run (propone y confirma) igual que las tools de creacion.
/// - estado_pqr: consulta abierta a nivel de conjunto por radicado, o "mis PQR" del contacto; de
///   PQR ajenos solo devuelve estado/tipo/fecha/plazo/ultima actualizacion (nunca la narrativa).
///
/// El tenant/RLS lo fija el TenantMiddleware; el telefono real viene de IAgentCallContext (NO es
/// argumento del LLM). La resolucion telefono -> persona la comparte con verificar_residencia via
/// AgentContactoHelper. Estas tools se asignan al agente de PORTERIA por la API de administracion.
/// </summary>
[McpServerToolType]
public sealed class PqrsdAgenteTools
{
    [McpServerTool(Name = "crear_pqr")]
    [Description("Radica un PQRSD (peticion, queja, reclamo, sugerencia, denuncia, etc.) a nombre del residente que escribe. REGLA DE ORO: solo radica si el numero REAL de la conversacion pertenece a un PROPIETARIO o RESIDENTE registrado (no recibe el telefono como argumento; usa el de la conversacion). Resuelve el tipo y la categoria contra el catalogo de la copropiedad (pregunta si no los reconoce). Es dry-run por defecto: propone (tipo, unidad, descripcion) para confirmar con el residente; vuelve a llamar con dryRun=false para radicar en firme y devolver el numero de radicado y el plazo.")]
    public static async Task<ResultadoCreacionMcp> CrearPqr(
        IPqrsdService pqrsd, PropiaDbContext db, ITenantContext tenant, IAgentCallContext call,
        [Description("Tipo de PQRSD en texto (Peticion, Queja, Reclamo, Sugerencia, Denuncia, Consulta, Felicitacion, ...). Se resuelve contra el catalogo de tipos de la copropiedad.")] string tipo,
        [Description("Descripcion del caso, minimo 20 caracteres. Es la narrativa del residente.")] string descripcion,
        CancellationToken ct,
        [Description("Categoria en texto (opcional). Si se omite se usa la categoria predeterminada de la copropiedad.")] string? categoria = null,
        [Description("Unidad del residente en texto libre (opcional): torre + apto/casa o su codigo. Si se omite se usa la unidad del residente resuelta por su numero.")] string? unidad = null,
        [Description("Si true (por defecto) solo propone sin radicar. Pasa false para radicar en firme.")] bool dryRun = true)
    {
        if (tenant.CurrentTenantId is null) { return Fail(dryRun, "No hay copropiedad activa en el contexto."); }

        // 1) Resolver el numero real -> persona propietaria/residente (regla de oro).
        var unidades = await AgentContactoHelper.CargarUnidadesAsync(db, ct);
        var resol = await AgentContactoHelper.ResolverContactoAsync(db, call.ContactPhone, unidades, ct);
        if (!resol.TelefonoPresente) { return Fail(dryRun, "No hay un telefono de contacto en la conversacion; no puedo radicar."); }

        var persona = resol.MejorPersonaResidente;
        if (persona is null)
        {
            return Fail(dryRun,
                "Solo puedo radicar PQR a nombre de propietarios o residentes registrados. El numero desde el que escribes no figura como propietario/residente de ninguna unidad; por favor comunicate con la administracion para radicar tu solicitud.");
        }

        // 2) Descripcion minima (RadicarAsync exige >= 20).
        var desc = (descripcion ?? "").Trim();
        if (desc.Length < 20) { return Fail(dryRun, "La descripcion debe tener al menos 20 caracteres. Pide al residente que amplie el detalle del caso."); }

        // 3) Resolver el tipo contra el catalogo configurable.
        var tipos = await pqrsd.ListarTiposAsync(false, ct);
        var tipoDto = AgentContactoHelper.MatchByName(tipos, t => t.Nombre, tipo);
        if (tipoDto is null)
        {
            var nombres = string.Join(", ", tipos.Select(t => t.Nombre));
            return Fail(dryRun, $"No reconoci el tipo '{tipo}'. Tipos disponibles: {nombres}. Pregunta al residente cual aplica.");
        }

        // 4) Resolver la categoria (o usar la predeterminada).
        var categorias = (await pqrsd.ListarCategoriasAsync(ct)).Where(c => c.Activa).ToList();
        PqrsdCategoriaDto? catDto;
        if (!string.IsNullOrWhiteSpace(categoria))
        {
            catDto = AgentContactoHelper.MatchByName(categorias, c => c.Nombre, categoria);
            if (catDto is null)
            {
                var nombres = string.Join(", ", categorias.Select(c => c.Nombre));
                return Fail(dryRun, $"No reconoci la categoria '{categoria}'. Categorias disponibles: {nombres}.");
            }
        }
        else
        {
            catDto = categorias.FirstOrDefault(c => c.EsPredeterminada) ?? categorias.FirstOrDefault();
            if (catDto is null) { return Fail(dryRun, "La copropiedad no tiene categorias de PQRSD configuradas."); }
        }

        // 5) Resolver la unidad (texto libre, o la unidad del residente resuelta por su numero).
        Guid? unidadId;
        string? unidadLabel;
        if (!string.IsNullOrWhiteSpace(unidad))
        {
            var u = AgentContactoHelper.ResolverUnidad(unidades, unidad);
            if (u is null) { return Fail(dryRun, $"No pude resolver la unidad '{unidad}'. Confirma la torre y el numero de la unidad."); }
            unidadId = u.Id; unidadLabel = u.Label;
        }
        else
        {
            unidadId = persona.UnidadId; unidadLabel = persona.UnidadLabel;
        }

        // 6) Radicar dentro de una transaccion (dry-run = rollback; confirmado = commit).
        var req = new RadicarPqrsdRequest(
            Tipo: tipoDto.Legal, CategoriaId: catDto.Id, Descripcion: desc,
            IdentidadReservada: false, Adjuntos: null, UnidadPrivadaId: unidadId,
            RadicadorPersonaId: persona.EntidadId, Campos: null, TipoId: tipoDto.Id);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var detalle = await pqrsd.RadicarAsync(req, ct);
            if (dryRun)
            {
                await tx.RollbackAsync(ct);
                return new ResultadoCreacionMcp(
                    DryRun: true, Exito: true,
                    Mensaje: $"Propuesta de radicacion (aun no radicado): tipo {tipoDto.Nombre}, categoria {catDto.Nombre}, unidad {unidadLabel ?? "sin unidad"}, a nombre de {persona.Nombre}. Confirma con el residente y vuelve a llamar con dryRun=false para radicar.",
                    Recurso: new { tipo = tipoDto.Nombre, categoria = catDto.Nombre, unidad = unidadLabel, solicitante = persona.Nombre, descripcion = desc });
            }
            await tx.CommitAsync(ct);
            return new ResultadoCreacionMcp(
                DryRun: false, Exito: true,
                Mensaje: $"PQR radicado: {detalle.NumeroRadicado} ({tipoDto.Nombre}), a nombre de {persona.Nombre}. Vence el {detalle.FechaVencimiento:yyyy-MM-dd} ({detalle.DiasHastaVencimiento} dias habiles).",
                Recurso: new { radicado = detalle.NumeroRadicado, tipo = tipoDto.Nombre, estado = detalle.Estado.ToString(), unidad = unidadLabel, vence = detalle.FechaVencimiento.ToString("yyyy-MM-dd"), diasHabiles = detalle.DiasHastaVencimiento });
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return Fail(dryRun, $"No se pudo radicar: {ex.Message}");
        }
    }

    [McpServerTool(Name = "estado_pqr")]
    [Description("Consulta el estado de un PQRSD por su numero de radicado, o lista los PQR del residente que escribe (por su numero real) si no se da radicado. Consulta abierta a nivel de conjunto: cualquiera puede consultar el estado por radicado, PERO de PQR que no son del contacto solo se devuelve estado/tipo/fecha/plazo/ultima actualizacion (nunca la narrativa ni datos personales del expediente ajeno).")]
    public static async Task<EstadoPqrResultado> EstadoPqr(
        IPqrsdService pqrsd, PropiaDbContext db, ITenantContext tenant, IAgentCallContext call,
        CancellationToken ct,
        [Description("Numero de radicado (ej. PQRSD-2026-0001) o el Guid del expediente. Si se omite, se listan los PQR del residente que escribe.")] string? radicado_o_id = null)
    {
        if (tenant.CurrentTenantId is null) { return new EstadoPqrResultado(false, "No hay copropiedad activa en el contexto.", false, null, null); }

        var unidades = await AgentContactoHelper.CargarUnidadesAsync(db, ct);
        var resol = await AgentContactoHelper.ResolverContactoAsync(db, call.ContactPhone, unidades, ct);
        var contactoPersonaIds = resol.Matches
            .Where(m => m.EntidadTipo == EntidadDirectorio.Persona)
            .Select(m => m.EntidadId).ToHashSet();

        if (!string.IsNullOrWhiteSpace(radicado_o_id))
        {
            var clave = radicado_o_id.Trim();
            PqrsdExpedienteDetalleDto? det = null;
            if (Guid.TryParse(clave, out var gid))
            {
                det = await pqrsd.GetExpedienteAsync(gid, ct);
            }
            else
            {
                // Busqueda directa por radicado (tenant-scoped): encuentra activos Y archivados.
                var id = await db.PqrsdExpedientes.AsNoTracking()
                    .Where(x => x.NumeroRadicado.ToLower() == clave.ToLower())
                    .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
                if (id is { } eid) { det = await pqrsd.GetExpedienteAsync(eid, ct); }
            }

            if (det is null) { return new EstadoPqrResultado(false, $"No encontre ningun PQR con radicado '{clave}'.", false, null, null); }

            var propio = det.RadicadorPersonaId is { } rp && contactoPersonaIds.Contains(rp);
            var ultima = det.Historial is { Count: > 0 }
                ? det.Historial.Max(h => h.CreatedAt).ToString("yyyy-MM-dd")
                : det.CreatedAt.ToString("yyyy-MM-dd");
            var vence = det.FechaVencimiento.ToString("yyyy-MM-dd");
            var tipoN = det.TipoNombre ?? det.Tipo.ToString();

            if (propio)
            {
                var detalle = new EstadoPqrDetalle(det.NumeroRadicado, tipoN, det.Estado.ToString(), vence, det.DiasHastaVencimiento, ultima, det.Descripcion, det.RespuestaAdmin);
                return new EstadoPqrResultado(true, $"PQR {det.NumeroRadicado}: {det.Estado} (vence {vence}, {det.DiasHastaVencimiento} dias habiles).", true, detalle, null);
            }
            else
            {
                // PQR ajeno: SOLO estado/tipo/fecha/plazo/ultima actualizacion (sin narrativa ni datos personales).
                var detalle = new EstadoPqrDetalle(det.NumeroRadicado, tipoN, det.Estado.ToString(), vence, det.DiasHastaVencimiento, ultima, null, null);
                return new EstadoPqrResultado(true, $"PQR {det.NumeroRadicado}: {det.Estado} (vence {vence}). Es un PQR de otra persona: por privacidad solo puedo darte el estado, el tipo y el plazo.", false, detalle, null);
            }
        }

        // "Mis PQR": requiere identificar al contacto por su numero.
        if (contactoPersonaIds.Count == 0)
        {
            return new EstadoPqrResultado(false, "No pude identificar tus PQR desde este numero. Dame el numero de radicado (ej. PQRSD-2026-0001) para consultarlo.", false, null, null);
        }

        // La bandeja separa activos/archivados; consulto ambos para "mis PQR".
        var activos = await pqrsd.GetBandejaAsync(null, null, null, null, false, ct);
        var archivados = await pqrsd.GetBandejaAsync(null, null, null, null, true, ct);
        var mios = activos.Items.Concat(archivados.Items)
            .Where(x => x.RadicadorPersonaId is { } rp && contactoPersonaIds.Contains(rp))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new EstadoPqrItem(
                x.NumeroRadicado, x.TipoNombre ?? x.Tipo.ToString(), x.Estado.ToString(),
                x.FechaVencimiento.ToString("yyyy-MM-dd"), x.DiasHastaVencimiento, x.Semaforo.ToString()))
            .ToList();

        if (mios.Count == 0) { return new EstadoPqrResultado(true, "No tienes PQR radicados a tu nombre.", true, null, mios); }
        return new EstadoPqrResultado(true, $"Tienes {mios.Count} PQR. El mas reciente: {mios[0].Radicado} ({mios[0].Estado}, vence {mios[0].Vence}).", true, null, mios);
    }

    private static ResultadoCreacionMcp Fail(bool dryRun, string mensaje) => new(dryRun, false, mensaje, null);

    /// <summary>Estado de un PQR (propio: con narrativa; ajeno: solo campos publicos).</summary>
    public sealed record EstadoPqrDetalle(
        [property: Description("Numero de radicado.")] string Radicado,
        [property: Description("Tipo de PQRSD.")] string Tipo,
        [property: Description("Estado actual (Recibida, EnGestion, Respondida, Cerrada, ViaInternaAgotada).")] string Estado,
        [property: Description("Fecha de vencimiento (yyyy-MM-dd).")] string Vence,
        [property: Description("Dias habiles hasta el vencimiento (negativo si vencido).")] int DiasHabiles,
        [property: Description("Fecha de la ultima actualizacion (yyyy-MM-dd).")] string? UltimaActualizacion,
        [property: Description("Narrativa del caso. SOLO se llena si el PQR es del contacto que consulta.")] string? Descripcion,
        [property: Description("Respuesta de la administracion. SOLO si el PQR es del contacto.")] string? RespuestaAdmin);

    /// <summary>Item de la lista "mis PQR".</summary>
    public sealed record EstadoPqrItem(string Radicado, string Tipo, string Estado, string Vence, int DiasHabiles, string Semaforo);

    /// <summary>Resultado de estado_pqr: un PQR puntual o la lista del contacto.</summary>
    public sealed record EstadoPqrResultado(
        [property: Description("True si se pudo resolver la consulta.")] bool Encontrado,
        [property: Description("Mensaje legible para responder al residente.")] string Mensaje,
        [property: Description("True si el PQR consultado es del propio contacto (se puede dar detalle completo).")] bool Propio,
        [property: Description("Detalle de un PQR puntual (por radicado/id). Null en modo 'mis PQR'.")] EstadoPqrDetalle? Pqr,
        [property: Description("Lista de los PQR del contacto. Null cuando se consulto un radicado puntual.")] IReadOnlyList<EstadoPqrItem>? MisPqr);
}
