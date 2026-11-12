using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Asambleas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Ola 2 del hand-off de Asambleas (replica mod-asambleas.js, PROPUESTAS.md 7.3 y 8): las reglas de la Ley 675 que la
/// interfaz no exponia. Instalacion con presidente y secretario (art. 47), quorum "mas de la mitad" o el criterio de
/// Configuracion y segunda convocatoria con numero plural (art. 41), punto en curso, edicion del orden del dia en
/// Borrador, cancelacion con motivo, votos de la sala en bloque y elecciones por planchas.
/// </summary>
public partial class AsambleaService
{
    // ===================== Quorum (helper compartido: ficha, residente, instalacion, votaciones) =====================

    private async Task<CriterioMayoria> CriterioAsync(CancellationToken ct)
        => await _db.AsambleaConfigs.AsNoTracking().Select(c => (CriterioMayoria?)c.CriterioMayoria).FirstOrDefaultAsync(ct)
           ?? CriterioMayoria.MasDe;

    /// <summary>Quorum: % de coeficientes presentes sobre los citados. Primera convocatoria de asamblea: MAS del
    /// requerido (o "al menos" si la copropiedad lo configuro). Segunda convocatoria: numero plural (2+ unidades),
    /// cualquiera sea el coeficiente. Consejo/comite (sin % requerido): al menos un presente.</summary>
    internal static QuorumDto CalcularQuorum(Sesion s, IReadOnlyCollection<(decimal Coef, bool Presente)> parts, CriterioMayoria criterio)
    {
        var rep = parts.Where(p => p.Presente).Sum(p => p.Coef);
        var total = parts.Sum(p => p.Coef);
        var presentes = parts.Count(p => p.Presente);
        var pctExacto = total > 0 ? rep / total * 100m : 0m;
        bool alcanzado;
        if (s.SegundaConvocatoria) alcanzado = presentes >= 2;
        else if (s.QuorumRequeridoPct is decimal req) alcanzado = criterio == CriterioMayoria.AlMenos ? pctExacto >= req : pctExacto > req;
        else alcanzado = presentes >= 1;
        return new QuorumDto(rep, total, Math.Round(pctExacto, 2), s.SegundaConvocatoria ? null : s.QuorumRequeridoPct,
            alcanzado, presentes, parts.Count, s.SegundaConvocatoria, criterio);
    }

    private async Task<QuorumDto> QuorumActualAsync(Sesion s, CancellationToken ct)
    {
        var parts = await _db.SesionParticipantes.AsNoTracking().Where(x => x.SesionId == s.Id)
            .Select(x => new { x.Coeficiente, x.Presente }).ToListAsync(ct);
        return CalcularQuorum(s, parts.Select(x => (x.Coeficiente, x.Presente)).ToList(), await CriterioAsync(ct));
    }

    private static string[] OpcionesDe(SesionPunto p)
        => string.IsNullOrEmpty(p.OpcionesVoto) ? OpcionesVotoBase.Default
           : JsonSerializer.Deserialize<string[]>(p.OpcionesVoto) ?? OpcionesVotoBase.Default;

    /// <summary>Normaliza las opciones propias de un punto (planchas/candidatos): sin vacios ni repetidas, 2..20.
    /// Null/vacio = Si / No / Abstencion.</summary>
    private static string SerializarOpciones(IReadOnlyList<string>? opciones)
    {
        var l = (opciones ?? Array.Empty<string>()).Select(o => (o ?? "").Trim()).Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (l.Count == 0) return JsonSerializer.Serialize(OpcionesVotoBase.Default);
        if (l.Count < 2) throw new InvalidOperationException("Una eleccion necesita al menos dos opciones (planchas o candidatos).");
        if (l.Count > 20) throw new InvalidOperationException("Maximo 20 opciones por punto.");
        if (l.Any(o => o.Length > 120)) throw new InvalidOperationException("Cada opcion admite maximo 120 caracteres.");
        return JsonSerializer.Serialize(l);
    }

    // ===================== Sesion: editar en Borrador (MERGE) =====================

    public async Task<bool> ActualizarSesionAsync(Guid sesionId, ActualizarSesionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        var datos = req.Titulo is not null || req.FechaSesion is not null || req.LugarFisico is not null || req.Modalidad is not null;
        if (datos && s.Estado != EstadoSesion.Borrador)
            throw new InvalidOperationException("Titulo, fecha, lugar y modalidad solo se cambian en Borrador: la citacion ya salio con esos datos.");
        if ((req.SalaPropia is not null || req.EnlaceVideo is not null) && s.Estado is not (EstadoSesion.Borrador or EstadoSesion.Citada))
            throw new InvalidOperationException("La videollamada solo se cambia antes de abrir la sala.");

        if (req.Titulo is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo obligatorio.");
            s.Titulo = req.Titulo.Trim();
        }
        if (req.FechaSesion is { } f) s.FechaSesion = f.ToUniversalTime();
        if (req.LugarFisico is not null) s.LugarFisico = string.IsNullOrWhiteSpace(req.LugarFisico) ? null : req.LugarFisico.Trim();
        if (req.Modalidad is { } m) s.Modalidad = m;
        if (req.EnlaceVideo is not null) s.EnlaceVideo = string.IsNullOrWhiteSpace(req.EnlaceVideo) ? null : req.EnlaceVideo.Trim();
        if (req.SalaPropia is bool sp) s.SalaPropia = sp;
        if (s.Modalidad == ModalidadSesion.Presencial) s.SalaPropia = false;
        if (s.SalaPropia) s.EnlaceVideo = null;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Orden del dia: agregar / quitar en Borrador =====================

    public async Task<bool> AgregarPuntoAsync(Guid sesionId, CrearPuntoRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.Include(x => x.Puntos).FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado != EstadoSesion.Borrador)
            throw new InvalidOperationException("El orden del dia solo se edita en Borrador: la citacion ya salio con este orden.");
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo del punto obligatorio.");
        _db.SesionPuntos.Add(new SesionPunto
        {
            SesionId = s.Id,
            Numero = s.Puntos.Count == 0 ? 1 : s.Puntos.Max(p => p.Numero) + 1,
            Titulo = req.Titulo.Trim(),
            Descripcion = req.Descripcion,
            RequiereVotacion = req.RequiereVotacion,
            TipoMayoria = req.TipoMayoria,
            MayoriaPct = req.MayoriaPct,
            ModalidadVoto = req.ModalidadVoto,
            PresupuestoId = req.PresupuestoId,
            OpcionesVoto = SerializarOpciones(req.OpcionesVoto)
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarPuntoAsync(Guid puntoId, CancellationToken ct)
    {
        var p = await _db.SesionPuntos.Include(x => x.Sesion).ThenInclude(s => s!.Puntos).FirstOrDefaultAsync(x => x.Id == puntoId, ct);
        if (p is null) return false;
        if (p.Sesion!.Estado != EstadoSesion.Borrador)
            throw new InvalidOperationException("El orden del dia solo se edita en Borrador: la citacion ya salio con este orden.");
        if (p.Sesion.Puntos.Count <= 1) throw new InvalidOperationException("La sesion debe tener al menos un punto del orden del dia.");
        _db.SesionPuntos.Remove(p);
        // Renumerar 1..n en el orden actual
        var n = 1;
        foreach (var x in p.Sesion.Puntos.Where(x => x.Id != p.Id).OrderBy(x => x.Numero)) { x.Numero = n++; x.UpdatedAt = DateTimeOffset.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Cancelar (Borrador o Citada, con motivo) =====================

    public async Task<bool> CancelarSesionAsync(Guid sesionId, CancelarSesionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado is not (EstadoSesion.Borrador or EstadoSesion.Citada))
            throw new InvalidOperationException("Solo se cancela una sesion en Borrador o Citada.");
        if (string.IsNullOrWhiteSpace(req.Motivo)) throw new InvalidOperationException("Indica el motivo de la cancelacion.");
        if (req.Motivo.Trim().Length > 1000) throw new InvalidOperationException("El motivo admite maximo 1000 caracteres.");
        s.Estado = EstadoSesion.Cancelada;
        s.MotivoCancelacion = req.Motivo.Trim();
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Segunda convocatoria (Ley 675 art. 41) =====================

    /// <summary>Desde una sesion con Quorum fallido crea la segunda convocatoria para el TERCER dia habil siguiente a
    /// las 8:00 p. m. (hora de Colombia), con el mismo orden del dia (sin votaciones). Queda en Borrador para citarla
    /// (sin el plazo de la ordinaria) y sesiona con un numero plural de propietarios.</summary>
    public async Task<Guid> ConvocarSegundaAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones.Include(x => x.Puntos).FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("Sesion no encontrada.");
        if (s.Estado != EstadoSesion.QuorumFallido)
            throw new InvalidOperationException("Solo se convoca segunda convocatoria desde una sesion sin quorum.");
        if (s.SegundaConvocatoria) throw new InvalidOperationException("Esta sesion ya es una segunda convocatoria.");
        if (await _db.Sesiones.AnyAsync(x => x.SesionPadreId == s.Id, ct))
            throw new InvalidOperationException("Ya existe la segunda convocatoria de esta sesion.");

        var co = TimeSpan.FromHours(-5);   // Colombia no tiene horario de verano
        var dia = DateOnly.FromDateTime((s.HoraCierre ?? s.FechaSesion).ToOffset(co).DateTime);
        var hasta = dia.AddDays(30);
        var festivos = (await _db.FestivosColombianos.AsNoTracking()
            .Where(x => x.Fecha > dia && x.Fecha <= hasta).Select(x => x.Fecha).ToListAsync(ct)).ToHashSet();
        var habiles = 0;
        while (habiles < 3)
        {
            dia = dia.AddDays(1);
            if (dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !festivos.Contains(dia)) habiles++;
        }
        var fecha = new DateTimeOffset(dia.ToDateTime(new TimeOnly(20, 0)), co).ToUniversalTime();

        var cfg = await _db.AsambleaConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        var n = new Sesion
        {
            Tipo = s.Tipo,
            Modalidad = s.Modalidad,
            Titulo = s.Titulo,
            FechaSesion = fecha,
            LugarFisico = s.LugarFisico,
            EnlaceVideo = s.EnlaceVideo,
            SalaPropia = s.SalaPropia,
            PlazoCitacionDias = cfg?.PlazoCitacionDias ?? s.PlazoCitacionDias,
            SegundaConvocatoria = true,
            SesionPadreId = s.Id,
            QuorumRequeridoPct = null,
            Estado = EstadoSesion.Borrador,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.Sesiones.Add(n);
        foreach (var p in s.Puntos.OrderBy(p => p.Numero))
        {
            _db.SesionPuntos.Add(new SesionPunto
            {
                Sesion = n,
                Numero = p.Numero,
                Titulo = p.Titulo,
                Descripcion = p.Descripcion,
                RequiereVotacion = p.RequiereVotacion,
                TipoMayoria = p.TipoMayoria,
                MayoriaPct = p.MayoriaPct,
                ModalidadVoto = p.ModalidadVoto,
                PresupuestoId = p.PresupuestoId,
                OpcionesVoto = p.OpcionesVoto
            });
        }
        await _db.SaveChangesAsync(ct);
        return n.Id;
    }

    /// <summary>Suma N dias habiles (sin sabados, domingos ni festivos de festivos_colombianos) a una fecha.</summary>
    private async Task<DateOnly> SumarDiasHabilesAsync(DateOnly desde, int dias, CancellationToken ct)
    {
        var hasta = desde.AddDays(dias * 2 + 20);
        var festivos = (await _db.FestivosColombianos.AsNoTracking().Where(x => x.Fecha > desde && x.Fecha <= hasta)
            .Select(x => x.Fecha).ToListAsync(ct)).ToHashSet();
        var d = desde;
        for (var n = 0; n < dias;)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !festivos.Contains(d)) n++;
        }
        return d;
    }

    // ===================== Instalacion (Ley 675 art. 47) =====================

    public async Task<bool> InstalarAsync(Guid sesionId, InstalarSesionRequest req, CancellationToken ct)
    {
        var s = await _db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return false;
        if (s.Estado != EstadoSesion.EnCurso) throw new InvalidOperationException("La sala debe estar abierta para instalar la asamblea.");
        if (s.InstaladaEn is not null) throw new InvalidOperationException("La asamblea ya esta instalada.");
        var q = await QuorumActualAsync(s, ct);
        if (!q.Alcanzado)
            throw new InvalidOperationException(q.SegundaConvocatoria
                ? "No hay quorum para instalar: en segunda convocatoria se necesitan al menos 2 unidades presentes."
                : $"No hay quorum para instalar la asamblea: {q.PctRepresentado:0.00}% representado" +
                  (q.PctRequerido is decimal r ? $" (se requiere {(q.Criterio == CriterioMayoria.AlMenos ? "al menos el" : "mas del")} {r:0.00}%)." : "."));

        var pres = await ParticipantePresenteAsync(sesionId, req.PresidenteUnidadId, "presidente", ct);
        Guid? secPersona; string secNombre;
        if (req.SecretarioUnidadId is Guid secUnidad)
        {
            if (secUnidad == req.PresidenteUnidadId) throw new InvalidOperationException("Presidente y secretario deben ser personas distintas.");
            var sec = await ParticipantePresenteAsync(sesionId, secUnidad, "secretario", ct);
            if (sec.PersonaId == pres.PersonaId) throw new InvalidOperationException("Presidente y secretario deben ser personas distintas.");
            secPersona = sec.PersonaId; secNombre = sec.Nombre;
        }
        else
        {
            // La administracion actua como secretaria de la reunion.
            secPersona = await _db.Users.AsNoTracking().Where(u => u.Id == GetUsuarioActualId()).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);
            var nombre = secPersona is Guid sp
                ? await _db.Personas.AsNoTracking().Where(x => x.Id == sp).Select(x => (x.Nombres + " " + x.Apellidos).Trim()).FirstOrDefaultAsync(ct)
                : null;
            secNombre = "Administración" + (string.IsNullOrWhiteSpace(nombre) ? "" : " - " + nombre);
            if (secPersona is not null && secPersona == pres.PersonaId)
                throw new InvalidOperationException("Presidente y secretario deben ser personas distintas: elige otro secretario.");
        }

        s.PresidentePersonaId = pres.PersonaId;
        s.PresidenteNombre = Recortar(pres.Nombre, 200);
        s.SecretarioPersonaId = secPersona;
        s.SecretarioNombre = Recortar(secNombre, 200);
        s.InstaladaEn = DateTimeOffset.UtcNow;
        s.QuorumInstalacionPct = q.PctRepresentado;
        Evento(s.Id, s.PuntoActualId, $"Asamblea instalada con el {q.PctRepresentado:0.00} % de los coeficientes ({q.ParticipantesPresentes} unidades). "
            + $"Presidente: {s.PresidenteNombre}. Secretario: {s.SecretarioNombre}.");
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string Recortar(string t, int max) => t.Length <= max ? t : t[..max];

    private async Task<(Guid PersonaId, string Nombre)> ParticipantePresenteAsync(Guid sesionId, Guid unidadId, string rol, CancellationToken ct)
    {
        var p = await _db.SesionParticipantes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SesionId == sesionId && x.UnidadPrivadaId == unidadId, ct)
            ?? throw new InvalidOperationException($"El {rol} debe ser un asistente de la sesion.");
        if (!p.Presente) throw new InvalidOperationException($"El {rol} debe estar presente en la sala.");
        // Si la unidad esta representada por poder aprobado, actua el apoderado.
        var apoderado = await _db.SesionPoderes.AsNoTracking()
            .Where(x => x.SesionId == sesionId && x.OtorganteUnidadId == unidadId && x.Estado == EstadoPoder.Aprobado)
            .Select(x => (Guid?)x.ApoderadoPersonaId).FirstOrDefaultAsync(ct);
        var personaId = apoderado ?? p.PersonaId;
        var nombre = await _db.Personas.AsNoTracking().Where(x => x.Id == personaId)
            .Select(x => (x.Nombres + " " + x.Apellidos).Trim()).FirstOrDefaultAsync(ct);
        return (personaId, string.IsNullOrWhiteSpace(nombre) ? "Sin nombre" : string.Join(" ", nombre!.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    // ===================== Puntos en curso =====================

    public async Task<bool> IniciarPuntoAsync(Guid puntoId, CancellationToken ct)
    {
        var p = await _db.SesionPuntos.Include(x => x.Sesion).ThenInclude(s => s!.Puntos).FirstOrDefaultAsync(x => x.Id == puntoId, ct);
        if (p is null) return false;
        var s = p.Sesion!;
        if (s.Estado != EstadoSesion.EnCurso) throw new InvalidOperationException("La sala debe estar abierta.");
        if (s.InstaladaEn is null) throw new InvalidOperationException("Instala la asamblea (presidente y secretario) antes de tratar los puntos.");
        if (p.Estado != EstadoPunto.Pendiente) throw new InvalidOperationException("Este punto ya se trato.");
        // El punto informativo que estaba en discusion se da por tratado al pasar al siguiente.
        foreach (var x in s.Puntos.Where(x => x.Id != p.Id && x.Estado == EstadoPunto.EnDiscusion && !x.RequiereVotacion))
        { x.Estado = EstadoPunto.Cerrado; x.UpdatedAt = DateTimeOffset.UtcNow; }
        p.Estado = EstadoPunto.EnDiscusion;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        s.PuntoActualId = p.Id;
        Evento(s.Id, p.Id, $"Se inicia el punto {p.Numero}: {p.Titulo}.");
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MarcarTratadoAsync(Guid puntoId, CancellationToken ct)
    {
        var p = await _db.SesionPuntos.Include(x => x.Sesion).FirstOrDefaultAsync(x => x.Id == puntoId, ct);
        if (p is null) return false;
        if (p.Sesion!.Estado != EstadoSesion.EnCurso) throw new InvalidOperationException("La sala debe estar abierta.");
        if (p.RequiereVotacion) throw new InvalidOperationException("Este punto se cierra con su votacion.");
        if (p.Estado is EstadoPunto.Cerrado or EstadoPunto.NoTratado) throw new InvalidOperationException("Este punto ya se trato.");
        p.Estado = EstadoPunto.Cerrado;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        if (p.Sesion.PuntoActualId == p.Id) p.Sesion.PuntoActualId = null;
        Evento(p.SesionId, p.Id, $"Punto {p.Numero} tratado.");
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Votos de la sala en bloque (presencial / mixta) =====================

    /// <summary>Registra de una vez el voto de varias unidades presentes (tabla "Registrar votos de la sala"). Cada voto
    /// sigue las reglas del voto individual (presente, un voto por unidad, opcion valida); los que fallan se informan
    /// y no frenan a los demas.</summary>
    public async Task<VotarBloqueResultado> VotarBloqueAsync(Guid votacionId, VotarBloqueRequest req, CancellationToken ct)
    {
        if (req.Votos is null || req.Votos.Count == 0) throw new InvalidOperationException("No hay votos para registrar.");
        if (req.Votos.Count > 5000) throw new InvalidOperationException("Demasiados votos en un solo registro.");
        var ok = 0;
        var errores = new List<string>();
        foreach (var v in req.Votos.GroupBy(x => x.UnidadPrivadaId).Select(g => g.First()))
        {
            try
            {
                if (await EmitirVotoCoreAsync(votacionId, new EmitirVotoRequest(v.UnidadPrivadaId, v.Opcion), personaActuanteId: null, ct)) ok++;
                else { errores.Add("Votacion no encontrada."); break; }
            }
            catch (InvalidOperationException ex)
            {
                var num = await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == v.UnidadPrivadaId).Select(u => u.Numero).FirstOrDefaultAsync(ct);
                errores.Add($"{num ?? "Unidad"}: {ex.Message}");
                _db.ChangeTracker.Clear();
                if (ex.Message.StartsWith("La votacion esta cerrada", StringComparison.Ordinal)) break;
            }
        }
        return new VotarBloqueResultado(ok, errores);
    }

    /// <summary>Desglose de una votacion CERRADA: votos y % por opcion sobre la misma base del resultado
    /// (base = coeficiente ganador / % ganador). Agregado: no expone el voto de ninguna unidad.</summary>
    internal static IReadOnlyList<DesgloseOpcionDto>? Desglose(Votacion v, string[] opciones)
    {
        if (v.Estado != EstadoVotacion.Cerrada || v.Votos.Count == 0) return null;
        var porOpcion = v.Votos.GroupBy(x => x.Opcion).ToDictionary(g => g.Key, g => (Votos: g.Count(), Coef: g.Sum(x => x.CoeficienteAportado)));
        var maxCoef = porOpcion.Values.Max(x => x.Coef);
        var baseCoef = v.ResultadoPct is > 0 ? maxCoef * 100m / v.ResultadoPct.Value : porOpcion.Values.Sum(x => x.Coef);
        return opciones.Concat(porOpcion.Keys.Except(opciones)).Select(o =>
        {
            porOpcion.TryGetValue(o, out var d);
            return new DesgloseOpcionDto(o, d.Votos, d.Coef, baseCoef > 0 ? Math.Round(d.Coef / baseCoef * 100m, 2) : 0m);
        }).ToList();
    }
}
