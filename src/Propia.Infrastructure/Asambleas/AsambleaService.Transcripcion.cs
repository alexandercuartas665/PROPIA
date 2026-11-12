using Microsoft.EntityFrameworkCore;
using Propia.Application.Asambleas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Ola 4 del hand-off de Asambleas (PROPUESTAS.md 8.3): bitacora de la sesion. Lineas de VOZ (voz a texto del navegador
/// -"Mi voz a texto"- o de un proveedor detras de ITranscripcionProvider) y eventos del SISTEMA (abrir sala, instalacion,
/// puntos, votaciones con resultado, poderes, cierre), asociadas al punto en curso. Alimenta los subtitulos del residente
/// y el acta redactada por la IA.
/// </summary>
public partial class AsambleaService
{
    /// <summary>Evento del sistema en la bitacora (lo guarda el SaveChanges del metodo que lo llama).</summary>
    private void Evento(Guid sesionId, Guid? puntoId, string texto)
        => _db.SesionTranscripcionLineas.Add(new SesionTranscripcionLinea
        {
            SesionId = sesionId,
            PuntoId = puntoId,
            Hora = DateTimeOffset.UtcNow,
            Tipo = TipoLineaTranscripcion.Sistema,
            Texto = Recortar(texto, 4000),
            Origen = "sistema"
        });

    public async Task<IReadOnlyList<TranscripcionLineaDto>> GetTranscripcionAsync(Guid sesionId, DateTimeOffset? desde, CancellationToken ct)
    {
        var q = _db.SesionTranscripcionLineas.AsNoTracking().Where(x => x.SesionId == sesionId);
        if (desde is { } d) q = q.Where(x => x.Hora > d);
        var lineas = await q.OrderBy(x => x.Hora).ThenBy(x => x.CreatedAt).Take(5000).ToListAsync(ct);
        return await MapearLineasAsync(sesionId, lineas, ct);
    }

    private async Task<IReadOnlyList<TranscripcionLineaDto>> MapearLineasAsync(Guid sesionId, List<SesionTranscripcionLinea> lineas, CancellationToken ct)
    {
        var puntos = await _db.SesionPuntos.AsNoTracking().Where(p => p.SesionId == sesionId).ToDictionaryAsync(p => p.Id, p => p.Numero, ct);
        var unidadIds = lineas.Where(l => l.UnidadPrivadaId != null).Select(l => l.UnidadPrivadaId!.Value).Distinct().ToList();
        var unidades = await _db.UnidadesPrivadas.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Numero, ct);
        return lineas.Select(l => new TranscripcionLineaDto(l.Id, l.Hora, l.Tipo, l.PuntoId,
            l.PuntoId is Guid pid && puntos.TryGetValue(pid, out var n) ? n : null, l.PersonaId, l.AutorNombre,
            l.UnidadPrivadaId is Guid uid && unidades.TryGetValue(uid, out var un) ? un : null, l.Texto, l.Confianza)).ToList();
    }

    public async Task<bool> ActivarTranscripcionAsync(Guid sesionId, ActivarTranscripcionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado != EstadoSesion.EnCurso) throw new InvalidOperationException("La transcripción solo se activa con la sala abierta.");
        if (s.TranscripcionActiva == req.Activa) return true;
        s.TranscripcionActiva = req.Activa;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        Evento(s.Id, s.PuntoActualId, req.Activa ? "Se activa la transcripción de la sesión." : "Se pausa la transcripción de la sesión.");
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Linea de voz. Sin persona en el request = el usuario actual ("Mi voz a texto"); con persona (proveedor de
    /// voz a texto con diarizacion) debe ser participante o apoderado de la sesion.</summary>
    public async Task<TranscripcionLineaDto> AgregarTranscripcionAsync(Guid sesionId, AgregarTranscripcionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("Sesión no encontrada.");
        if (s.Estado != EstadoSesion.EnCurso) throw new InvalidOperationException("La sala no está abierta.");
        if (!s.TranscripcionActiva) throw new InvalidOperationException("La transcripción está pausada: actívala con \"Transcribir\".");
        var texto = (req.Texto ?? "").Trim();
        if (texto.Length == 0) throw new InvalidOperationException("La línea está vacía.");
        if (req.Confianza is < 0 or > 1) throw new InvalidOperationException("La confianza va de 0 a 1.");

        Guid? personaId = req.PersonaId;
        if (personaId is Guid pid)
        {
            var participa = await _db.SesionParticipantes.AnyAsync(p => p.SesionId == sesionId && p.PersonaId == pid, ct)
                || await _db.SesionPoderes.AnyAsync(p => p.SesionId == sesionId && p.ApoderadoPersonaId == pid && p.Estado == EstadoPoder.Aprobado, ct);
            if (!participa) throw new InvalidOperationException("La persona no participa en la sesión.");
        }
        else personaId = await _db.Users.AsNoTracking().Where(u => u.Id == GetUsuarioActualId()).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);

        if (req.UnidadPrivadaId is Guid unidad
            && !await _db.SesionParticipantes.AnyAsync(p => p.SesionId == sesionId && p.UnidadPrivadaId == unidad, ct))
            throw new InvalidOperationException("La unidad no es participante de la sesión.");

        var autor = req.AutorNombre?.Trim();
        if (string.IsNullOrEmpty(autor) && personaId is Guid p2)
            autor = await _db.Personas.AsNoTracking().Where(x => x.Id == p2).Select(x => (x.Nombres + " " + x.Apellidos).Trim()).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(autor)) autor = "Participante";
        autor = string.Join(" ", autor.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var l = new SesionTranscripcionLinea
        {
            SesionId = sesionId,
            PuntoId = s.PuntoActualId,
            Hora = DateTimeOffset.UtcNow,
            Tipo = TipoLineaTranscripcion.Voz,
            PersonaId = personaId,
            UnidadPrivadaId = req.UnidadPrivadaId,
            AutorNombre = Recortar(autor, 200),
            Texto = Recortar(texto, 4000),
            Confianza = req.Confianza,
            Origen = Recortar(req.Origen?.Trim() is { Length: > 0 } o ? o : "navegador", 30)
        };
        _db.SesionTranscripcionLineas.Add(l);
        await _db.SaveChangesAsync(ct);
        return (await MapearLineasAsync(sesionId, new List<SesionTranscripcionLinea> { l }, ct))[0];
    }

    /// <summary>Texto del resultado de una votacion para la bitacora.</summary>
    private static string ResultadoEvento(Votacion v) => v.ResultadoFinal switch
    {
        ResultadoVotacion.Aprobado => $"Aprobado ({v.ResultadoPct:0.00} %)",
        ResultadoVotacion.Rechazado => $"No aprobado ({v.ResultadoOpcion} {v.ResultadoPct:0.00} %)",
        ResultadoVotacion.Elegido => $"Elegida la opción «{v.ResultadoOpcion}» ({v.ResultadoPct:0.00} %)",
        _ => "Sin resultado"
    };
}
