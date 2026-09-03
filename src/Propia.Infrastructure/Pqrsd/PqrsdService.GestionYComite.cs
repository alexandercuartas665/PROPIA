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
    // Vista residente, ciclo de gestion, tutela, prorroga y comite.
    // ===================== Vista residente =====================

    public async Task<IReadOnlyList<PqrsdBandejaItemDto>> ListarMisPqrsdAsync(CancellationToken ct)
    {
        var personaId = await GetPersonaActualIdAsync(ct);
        if (personaId is null) return new List<PqrsdBandejaItemDto>();

        var rows = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(x => x.Categoria)
            .Where(x => x.RadicadorPersonaId == personaId.Value)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var sesionesActivas = await _db.PqrsdComiteSesiones.AsNoTracking()
            .Where(s => s.Resultado == null && rows.Select(r => r.Id).Contains(s.ExpedienteId))
            .Select(s => s.ExpedienteId).ToHashSetAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var plazos = await _db.PqrsdConfiguracionPlazos.AsNoTracking().ToDictionaryAsync(p => p.Tipo, ct);

        return rows.Select(x =>
        {
            var fechaCreacion = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
            var semaforo = CalcularSemaforo(x.Estado, x.TutelaActiva, fechaCreacion, x.FechaVencimiento, hoy);
            var diasHasta = x.FechaVencimiento.DayNumber - hoy.DayNumber;
            var urgencia = plazos.TryGetValue(x.Tipo, out var pl) ? pl.NivelUrgencia : NivelUrgenciaPqrsd.Media;
            var resumen = x.Descripcion.Length > 100 ? x.Descripcion[..100] + "..." : x.Descripcion;
            return new PqrsdBandejaItemDto(
                x.Id, x.NumeroRadicado, x.Tipo, x.Categoria!.Nombre, resumen, x.Estado,
                semaforo, null, null, x.IdentidadReservada, x.TutelaActiva,
                x.FechaVencimiento, diasHasta, urgencia,
                sesionesActivas.Contains(x.Id), x.CreatedAt);
        }).ToList();
    }

    // ===================== Ciclo de gestion =====================

    public async Task<bool> TomarExpedienteAsync(Guid id, TomarExpedienteRequest req, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado != EstadoPqrsd.Recibida) return true;
        var anterior = x.Estado;
        x.Estado = EstadoPqrsd.EnGestion;
        await SincronizarColumnaLegalAsync(x, EstadoPqrsd.EnGestion, ct);
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = EstadoPqrsd.EnGestion,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = string.IsNullOrWhiteSpace(req.Nota) ? "Admin tomo el expediente" : req.Nota
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ResponderAsync(Guid id, ResponderExpedienteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Texto) || req.Texto.Trim().Length < 20)
            throw new InvalidOperationException("Respuesta minima 20 caracteres.");
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado == EstadoPqrsd.Cerrada || x.Estado == EstadoPqrsd.ViaInternaAgotada)
            throw new InvalidOperationException("No se puede responder un expediente cerrado.");

        var anterior = x.Estado;
        var esRespuestaDefinitiva = x.InconformidadTexto != null;

        if (esRespuestaDefinitiva)
        {
            // Segunda respuesta tras inconformidad -> cierra definitivamente (y archiva: desaparece del tablero)
            x.RespuestaDefinitiva = req.Texto.Trim();
            x.RespuestaDefinitivaAt = DateTimeOffset.UtcNow;
            x.Estado = EstadoPqrsd.Cerrada;
            x.FechaCierre = DateTimeOffset.UtcNow;
            x.CerradoPorUsuarioId = GetUsuarioActualId();
            x.Archivado = true;
            x.ArchivadoAt = DateTimeOffset.UtcNow;
            x.ArchivadoPorUsuarioId = GetUsuarioActualId();
        }
        else
        {
            x.RespuestaAdmin = req.Texto.Trim();
            x.RespuestaAdminAt = DateTimeOffset.UtcNow;
            x.RespuestaAdminPorUsuarioId = GetUsuarioActualId();
            x.Estado = EstadoPqrsd.Respondida;
        }
        await SincronizarColumnaLegalAsync(x, x.Estado, ct);
        x.UpdatedAt = DateTimeOffset.UtcNow;

        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = esRespuestaDefinitiva ? "Respuesta definitiva - cierre" : "Respuesta del admin"
        });
        await _db.SaveChangesAsync(ct);

        var asunto = esRespuestaDefinitiva
            ? $"PQRSD cerrado: {x.NumeroRadicado}"
            : $"PQRSD respondido: {x.NumeroRadicado}";
        await NotificarAdminsTenantAsync("2.9", id, asunto,
            esRespuestaDefinitiva
                ? "El expediente quedo cerrado tras la respuesta definitiva."
                : "El admin respondio el expediente. Si el ciudadano queda inconforme tiene una oportunidad de inconformidad (RN-06).",
            Domain.Enums.PrioridadNotificacion.Normal, ct);

        // Enviar la respuesta al radicador por los canales elegidos (correo / celular).
        var canales = new List<Domain.Enums.CanalNotificacion>();
        if (req.Correo) canales.Add(Domain.Enums.CanalNotificacion.Email);
        if (req.Celular) canales.Add(Domain.Enums.CanalNotificacion.WhatsApp);
        var tenantIdResp = _tenantContext.CurrentTenantId;
        if (canales.Count > 0 && tenantIdResp is not null && x.RadicadorPersonaId != Guid.Empty)
        {
            var cuerpo = $"Respuesta a tu PQR {x.NumeroRadicado}:\n\n{req.Texto.Trim()}";
            var lote = canales.Select(canal => new Propia.Application.Notificaciones.EnviarNotificacionRequest(
                Canal: canal,
                Cuerpo: cuerpo,
                TenantId: tenantIdResp,
                PersonaDestinatariaId: x.RadicadorPersonaId,
                Asunto: $"Respuesta a tu PQR {x.NumeroRadicado}",
                Prioridad: Domain.Enums.PrioridadNotificacion.Normal,
                ModuloOrigenCodigo: "2.9",
                EntidadOrigenId: x.Id));
            await _noti.EnviarLoteAsync(lote, ct);
        }

        return true;
    }

    public async Task<bool> ManifestarInconformidadAsync(Guid id, ManifestarInconformidadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Texto))
            throw new InvalidOperationException("Texto de inconformidad obligatorio.");
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado != EstadoPqrsd.Respondida)
            throw new InvalidOperationException("Solo se puede manifestar inconformidad sobre expedientes Respondidos.");
        if (x.InconformidadTexto != null)
            throw new InvalidOperationException("RN-06: solo se permite una inconformidad por expediente.");

        var anterior = x.Estado;
        x.InconformidadTexto = req.Texto.Trim();
        x.InconformidadAt = DateTimeOffset.UtcNow;
        x.Estado = EstadoPqrsd.EnGestion;
        await SincronizarColumnaLegalAsync(x, EstadoPqrsd.EnGestion, ct);
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = EstadoPqrsd.EnGestion,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = "Radicador manifesto inconformidad"
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CerrarDefinitivoAsync(Guid id, CerrarDefinitivoRequest req, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado == EstadoPqrsd.Cerrada || x.Estado == EstadoPqrsd.ViaInternaAgotada) return true;
        if (string.IsNullOrWhiteSpace(req.RespuestaDefinitiva))
            throw new InvalidOperationException("Respuesta definitiva obligatoria al cerrar.");
        if (req.MotivoCierreId is not Guid motivoId)
            throw new InvalidOperationException("Debes elegir un motivo de cierre.");
        var motivo = await _db.MotivosCierre.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == motivoId && m.Modulo == "pqrsd", ct)
            ?? throw new InvalidOperationException("Motivo de cierre invalido.");

        // La clasificacion del motivo define el estado legal terminal.
        var estadoDestino = motivo.Clasificacion == ClasificacionCierre.ViaInternaAgotada
            ? EstadoPqrsd.ViaInternaAgotada : EstadoPqrsd.Cerrada;

        var anterior = x.Estado;
        x.RespuestaDefinitiva = req.RespuestaDefinitiva.Trim();
        x.RespuestaDefinitivaAt = DateTimeOffset.UtcNow;
        x.Estado = estadoDestino;
        x.MotivoCierreId = motivoId;
        await SincronizarColumnaLegalAsync(x, estadoDestino, ct);
        x.FechaCierre = DateTimeOffset.UtcNow;
        x.CerradoPorUsuarioId = GetUsuarioActualId();
        // Cerrar = archivar: la tarjeta desaparece del tablero activo y queda en "Cerrados".
        x.Archivado = true;
        x.ArchivadoAt = DateTimeOffset.UtcNow;
        x.ArchivadoPorUsuarioId = GetUsuarioActualId();
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = estadoDestino,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = $"Cierre por admin - motivo: {motivo.Nombre}"
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Tutela =====================

    public async Task<bool> ActivarTutelaAsync(Guid id, ActivarTutelaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Justificacion))
            throw new InvalidOperationException("Justificacion obligatoria al activar Tutela (RN-11).");
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.TutelaActiva) return true;
        x.TutelaActiva = true;
        x.TutelaActivadaAt = DateTimeOffset.UtcNow;
        x.TutelaActivadaPorUsuarioId = GetUsuarioActualId();
        x.UpdatedAt = DateTimeOffset.UtcNow;
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = x.Estado,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = $"Tutela activada - {req.Justificacion}"
        });
        await _db.SaveChangesAsync(ct);

        await NotificarAdminsTenantAsync("2.9", id,
            $"TUTELA marcada: {x.NumeroRadicado}",
            $"Se marco tutela activa sobre el expediente. Atender con prioridad maxima. Justificacion: {req.Justificacion}",
            Domain.Enums.PrioridadNotificacion.Critica, ct);

        return true;
    }

    // ===================== Prorroga (ampliacion de plazo) =====================

    public async Task<bool> AmpliarPlazoAsync(Guid id, AmpliarPlazoRequest req, CancellationToken ct)
    {
        if (req.Dias < 1) throw new InvalidOperationException("La prorroga debe ser de al menos 1 dia habil.");
        if (req.Dias > 60) throw new InvalidOperationException("La prorroga no puede superar 60 dias habiles.");
        if (string.IsNullOrWhiteSpace(req.Motivo))
            throw new InvalidOperationException("Debes indicar el motivo de la prorroga (queda registrado en la traza).");

        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null) return false;
        if (x.Estado is EstadoPqrsd.Cerrada or EstadoPqrsd.ViaInternaAgotada)
            throw new InvalidOperationException("No se puede prorrogar un expediente cerrado.");

        var anterior = x.FechaVencimiento;
        // La prorroga se suma en dias habiles a la fecha de vencimiento vigente (aumenta el tiempo de entrega).
        x.FechaVencimiento = SumarDiasHabiles(x.FechaVencimiento, req.Dias);
        x.ProrrogaDias += req.Dias;
        x.UpdatedAt = DateTimeOffset.UtcNow;

        var motivo = req.Motivo.Trim();
        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = id,
            EstadoAnterior = x.Estado,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Prorroga,
            Nota = $"Prorroga de {req.Dias} dia(s) habil(es). Vencimiento {anterior:yyyy-MM-dd} -> {x.FechaVencimiento:yyyy-MM-dd}. Motivo: {motivo}"
        });
        await _db.SaveChangesAsync(ct);

        await NotificarAdminsTenantAsync("2.9", id,
            $"Prorroga PQRSD: {x.NumeroRadicado}",
            $"Se amplio el plazo en {req.Dias} dia(s) habil(es). Nueva fecha de vencimiento: {x.FechaVencimiento:yyyy-MM-dd}. Motivo: {motivo}",
            Domain.Enums.PrioridadNotificacion.Normal, ct);

        return true;
    }

    // ===================== Comite =====================

    public async Task<PqrsdComiteSesionDto> EscalarAComiteAsync(Guid expedienteId, EscalarAComiteRequest req, CancellationToken ct)
    {
        var x = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct)
            ?? throw new InvalidOperationException("Expediente no encontrado.");
        if (x.Tipo != TipoPqrsd.Denuncia)
            throw new InvalidOperationException("Solo se puede escalar al Comite de Convivencia un expediente de tipo Denuncia (Ley 675 Art. 58).");
        if (req.PersonaIds is null || req.PersonaIds.Count == 0)
            throw new InvalidOperationException("Debes seleccionar al menos un miembro del Comite.");

        // Validar que las personas existan
        var personasValidas = await _db.Personas.AsNoTracking()
            .Where(p => req.PersonaIds.Contains(p.Id))
            .Select(p => p.Id).ToListAsync(ct);
        if (personasValidas.Count != req.PersonaIds.Count)
            throw new InvalidOperationException("Una o mas personas seleccionadas no existen.");

        var sesion = new PqrsdComiteSesion
        {
            ExpedienteId = expedienteId,
            FechaSesion = req.FechaPropuestaSesion,
            Modalidad = req.Modalidad,
            EnlaceReunion = req.EnlaceReunion,
            ActivadaPorUsuarioId = GetUsuarioActualId()
        };
        _db.PqrsdComiteSesiones.Add(sesion);

        foreach (var pid in personasValidas.Distinct())
        {
            _db.PqrsdComiteMiembros.Add(new PqrsdComiteMiembroSesion
            {
                Sesion = sesion,
                PersonaId = pid
            });
        }

        _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            ExpedienteId = expedienteId,
            EstadoAnterior = x.Estado,
            EstadoNuevo = x.Estado,
            ActorUsuarioId = GetUsuarioActualId(),
            Origen = OrigenCambioEstado.Manual,
            Nota = $"Escalado al Comite de Convivencia ({req.Modalidad}, {personasValidas.Count} miembros)"
        });

        await _db.SaveChangesAsync(ct);

        var personas = await _db.Personas.AsNoTracking()
            .Where(p => personasValidas.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
        var miembrosDto = sesion.Miembros.Select(m => new PqrsdComiteMiembroDto(
            m.Id, m.PersonaId, personas.GetValueOrDefault(m.PersonaId, ""))).ToList();
        return new PqrsdComiteSesionDto(
            sesion.Id, sesion.FechaSesion, sesion.Modalidad, sesion.EnlaceReunion,
            sesion.Resultado, sesion.BorradorActa, sesion.ActaFinal,
            sesion.ActivadaPorUsuarioId, sesion.CreatedAt, miembrosDto);
    }

    public async Task<bool> RegistrarSesionComiteAsync(Guid sesionId, RegistrarSesionComiteRequest req, CancellationToken ct)
    {
        var s = await _db.PqrsdComiteSesiones.Include(x => x.Expediente).FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Resultado != null)
            throw new InvalidOperationException("La sesion ya fue cerrada con resultado registrado.");

        s.FechaSesion = req.FechaSesion;
        s.ActaFinal = req.Acta;
        s.Resultado = req.Resultado;
        s.UpdatedAt = DateTimeOffset.UtcNow;

        // Si el resultado es SinAcuerdo, marcar el expediente como ViaInternaAgotada
        if (req.Resultado == ResultadoComite.SinAcuerdo)
        {
            var x = s.Expediente!;
            var anterior = x.Estado;
            x.Estado = EstadoPqrsd.ViaInternaAgotada;
            await SincronizarColumnaLegalAsync(x, EstadoPqrsd.ViaInternaAgotada, ct);
            x.FechaCierre = DateTimeOffset.UtcNow;
            x.CerradoPorUsuarioId = GetUsuarioActualId();
            x.UpdatedAt = DateTimeOffset.UtcNow;
            _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
            {
                ExpedienteId = x.Id,
                EstadoAnterior = anterior,
                EstadoNuevo = EstadoPqrsd.ViaInternaAgotada,
                ActorUsuarioId = GetUsuarioActualId(),
                Origen = OrigenCambioEstado.Sistema,
                Nota = "Comite: sin acuerdo - via interna agotada (Ley 675 Art. 58)"
            });
        }
        else
        {
            // Acuerdo: el admin debe cerrar manualmente con respuesta definitiva
            _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
            {
                ExpedienteId = s.ExpedienteId,
                EstadoAnterior = s.Expediente!.Estado,
                EstadoNuevo = s.Expediente.Estado,
                ActorUsuarioId = GetUsuarioActualId(),
                Origen = OrigenCambioEstado.Sistema,
                Nota = "Comite: acuerdo alcanzado - admin debe cerrar con respuesta definitiva"
            });
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

}
