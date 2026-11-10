using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Asambleas;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Self-service del residente (Ola 1 de la capa en vivo): el copropietario consulta la sesion,
/// registra su asistencia y vota sobre SUS unidades. Envuelve CheckIn/EmitirVoto del flujo admin sin
/// alterar sus reglas (sala abierta, presente, RN-09, opcion valida); solo agrega el gate de propiedad
/// y registra a la persona que actua. El tenant ya debe estar fijado por quien llama (request
/// autenticado o controller publico): todas las consultas corren bajo el filtro de tenant + RLS.
/// </summary>
public partial class AsambleaService
{
    public async Task<SesionResidenteDto?> GetSesionParaResidenteAsync(Guid sesionId, Guid personaId, CancellationToken ct)
    {
        var s = await _db.Sesiones.AsNoTracking()
            .Include(x => x.Puntos)
            .FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        // Un borrador aun no existe para los copropietarios.
        if (s is null || s.Estado == EstadoSesion.Borrador) return null;

        var participantes = await _db.SesionParticipantes.AsNoTracking()
            .Where(p => p.SesionId == sesionId)
            .Select(p => new { p.UnidadPrivadaId, p.Coeficiente, p.Presente })
            .ToListAsync(ct);

        // Quorum agregado (mismo calculo que la ficha del admin); no expone quien esta presente.
        var coefRepresentado = participantes.Where(p => p.Presente).Sum(p => p.Coeficiente);
        var coefTotal = participantes.Sum(p => p.Coeficiente);
        var pctRepresentado = coefTotal > 0 ? Math.Round(coefRepresentado / coefTotal * 100m, 2) : 0m;
        var quorum = new QuorumDto(coefRepresentado, coefTotal, pctRepresentado, s.QuorumRequeridoPct,
            !s.QuorumRequeridoPct.HasValue || pctRepresentado >= s.QuorumRequeridoPct.Value,
            participantes.Count(p => p.Presente), participantes.Count);

        var habilitadas = await UnidadesHabilitadasAsync(sesionId, personaId, ct);
        var mias = participantes.Where(p => habilitadas.ContainsKey(p.UnidadPrivadaId)).ToList();
        var miasIds = mias.Select(p => p.UnidadPrivadaId).ToList();
        var numeros = await _db.UnidadesPrivadas.AsNoTracking()
            .Where(u => miasIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Numero, ct);

        var votaciones = await _db.Votaciones.AsNoTracking()
            .Where(v => v.SesionId == sesionId)
            .Select(v => new { v.Id, v.PuntoId, v.Estado, v.HoraApertura, v.ResultadoFinal, v.ResultadoOpcion, v.ResultadoPct })
            .ToListAsync(ct);
        var ultimaPorPunto = votaciones.GroupBy(v => v.PuntoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.HoraApertura).First());
        var puntoDeVotacion = votaciones.ToDictionary(v => v.Id, v => v.PuntoId);

        var votacionIds = votaciones.Select(v => v.Id).ToList();
        var misVotos = await _db.Votos.AsNoTracking()
            .Where(v => votacionIds.Contains(v.VotacionId) && miasIds.Contains(v.UnidadPrivadaId))
            .Select(v => new { v.VotacionId, v.UnidadPrivadaId, v.Opcion, v.EsSecreto })
            .ToListAsync(ct);

        var puntos = s.Puntos.OrderBy(p => p.Numero).Select(p =>
        {
            ultimaPorPunto.TryGetValue(p.Id, out var v);
            var cerrada = v?.Estado == EstadoVotacion.Cerrada;
            var opciones = string.IsNullOrEmpty(p.OpcionesVoto)
                ? OpcionesVotoBase.Default
                : JsonSerializer.Deserialize<string[]>(p.OpcionesVoto) ?? OpcionesVotoBase.Default;
            return new SesionPuntoResidenteDto(p.Id, p.Numero, p.Titulo, p.Descripcion, p.RequiereVotacion,
                p.Estado, p.ModalidadVoto, opciones, v?.Id, v?.Estado,
                cerrada ? v!.ResultadoFinal : null, cerrada ? v!.ResultadoOpcion : null, cerrada ? v!.ResultadoPct : null);
        }).ToList();

        var misUnidades = mias
            .OrderBy(p => numeros.GetValueOrDefault(p.UnidadPrivadaId, ""))
            .Select(p => new MiUnidadSesionDto(p.UnidadPrivadaId, numeros.GetValueOrDefault(p.UnidadPrivadaId, ""),
                p.Coeficiente, p.Presente, habilitadas[p.UnidadPrivadaId],
                misVotos.Where(v => v.UnidadPrivadaId == p.UnidadPrivadaId)
                    .Select(v => new MiVotoDto(v.VotacionId, puntoDeVotacion[v.VotacionId], v.EsSecreto ? null : v.Opcion))
                    .ToList()))
            .ToList();

        // Personas es global: se lee por id (el id viene del token/JWT, no del cliente).
        var nombre = await _db.Personas.AsNoTracking()
            .Where(x => x.Id == personaId)
            .Select(x => (x.Nombres + " " + x.Apellidos).Trim())
            .FirstOrDefaultAsync(ct) ?? "";

        return new SesionResidenteDto(s.Id, s.Titulo, s.Tipo, s.Modalidad, s.Estado, s.FechaSesion,
            s.LugarFisico, s.EnlaceVideo, nombre, quorum, puntos, misUnidades);
    }

    public async Task<bool> MiCheckInAsync(Guid sesionId, Guid personaId, Guid unidadId, bool presente, CancellationToken ct)
    {
        await ExigirUnidadHabilitadaAsync(sesionId, personaId, unidadId, ct);

        // Idempotente: repetir "Presente" desde el celular no agrega eventos duplicados al quorum log.
        var actual = await _db.SesionParticipantes.AsNoTracking()
            .Where(p => p.SesionId == sesionId && p.UnidadPrivadaId == unidadId)
            .Select(p => (bool?)p.Presente)
            .FirstOrDefaultAsync(ct);
        if (actual is null) return false;
        if (actual == presente) return true;

        return await CheckInParticipanteCoreAsync(sesionId, new CheckInParticipanteRequest(unidadId, presente), personaId, ct);
    }

    public async Task<bool> MiVotoAsync(Guid sesionId, Guid votacionId, Guid personaId, Guid unidadId, string opcion, CancellationToken ct)
    {
        // La votacion debe pertenecer a la sesion habilitada (el enlace es de UNA sesion).
        var deLaSesion = await _db.Votaciones.AsNoTracking()
            .AnyAsync(v => v.Id == votacionId && v.SesionId == sesionId, ct);
        if (!deLaSesion) return false;

        await ExigirUnidadHabilitadaAsync(sesionId, personaId, unidadId, ct);
        return await EmitirVotoCoreAsync(votacionId, new EmitirVotoRequest(unidadId, opcion), personaId, ct);
    }

    // ===================== Gate de propiedad =====================

    /// <summary>
    /// Unidades sobre las que la persona puede actuar en la sesion -> true si es por poder.
    /// Ley 675: vota el Propietario (vinculo activo), o el apoderado con poder Aprobado para esa unidad
    /// en esa sesion. Residente/Familiar/Arrendatario no votan. Si es duena y ademas apoderada, prima
    /// "propia". Un dueno Empresa (PersonaId null) no entra por aqui: vota su apoderado.
    /// </summary>
    private async Task<Dictionary<Guid, bool>> UnidadesHabilitadasAsync(Guid sesionId, Guid personaId, CancellationToken ct)
    {
        var propias = await _db.UnidadPersonas.AsNoTracking()
            .Where(x => x.PersonaId == personaId && x.Activo && x.Rol == RolUnidadPersona.Propietario)
            .Select(x => x.UnidadId)
            .ToListAsync(ct);
        var porPoder = await _db.SesionPoderes.AsNoTracking()
            .Where(x => x.SesionId == sesionId && x.ApoderadoPersonaId == personaId && x.Estado == EstadoPoder.Aprobado)
            .Select(x => x.OtorganteUnidadId)
            .ToListAsync(ct);

        var r = porPoder.Distinct().ToDictionary(id => id, _ => true);
        foreach (var id in propias) r[id] = false;
        return r;
    }

    private async Task ExigirUnidadHabilitadaAsync(Guid sesionId, Guid personaId, Guid unidadId, CancellationToken ct)
    {
        var habilitadas = await UnidadesHabilitadasAsync(sesionId, personaId, ct);
        if (!habilitadas.ContainsKey(unidadId))
            throw new UnauthorizedAccessException("No puedes actuar sobre esta unidad en esta asamblea.");
    }
}
