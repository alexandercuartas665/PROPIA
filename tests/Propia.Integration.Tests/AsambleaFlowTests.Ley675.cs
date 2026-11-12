using Microsoft.EntityFrameworkCore;
using Propia.Application.Asambleas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Ola 2 del hand-off de Asambleas (Ley 675): instalacion obligatoria, quorum "mas de la mitad", cierre con el quorum
/// de la instalacion (correccion justificada), No tratado, segunda convocatoria (3er dia habil 8 p. m., numero plural),
/// limite de poderes, votos en bloque, elecciones por planchas, orden del dia editable solo en Borrador, plazo de
/// citacion, cancelacion con motivo y citacion a nombre del propietario.
/// </summary>
public partial class AsambleaFlowTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static CrearPuntoRequest PuntoReq(int n, string t, bool vota = true, IReadOnlyList<string>? opciones = null)
        => new(n, t, null, vota, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null, opciones);

    private async Task<(IAsambleaService Svc, Guid SesionId)> SesionEnCursoAsync(Guid tenantId, TipoSesion tipo, params CrearPuntoRequest[] puntos)
    {
        await DuenosAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        var s = await svc.CrearSesionAsync(new CrearSesionRequest(tipo, ModalidadSesion.Presencial, "Sesion Ley 675",
            DateTimeOffset.UtcNow.AddDays(10), "Salon", null, puntos), Ct);
        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), Ct);
        await svc.AbrirSalaAsync(s.Id, Ct);
        return (svc, s.Id);
    }

    [Fact]
    public async Task Instalacion_obligatoria_quorum_mas_de_la_mitad_y_cierre_con_quorum_de_instalacion()
    {
        var tenantId = await SeedTenantAsync("Asam Instalacion");
        await SeedPersonaConApplicationUser(tenantId);
        var u = await SeedUnidadesAsync(tenantId, 2, coeficientes: new[] { 0.5m, 0.5m });
        var (svc, sid) = await SesionEnCursoAsync(tenantId, TipoSesion.AsambleaOrdinaria,
            PuntoReq(1, "Presupuesto"), PuntoReq(2, "Reglamento"), PuntoReq(3, "Varios", vota: false));
        var puntos = (await svc.GetSesionAsync(sid, Ct))!.Puntos;

        // Sin instalacion no se abren votaciones.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AbrirVotacionAsync(sid, new AbrirVotacionRequest(puntos[0].Id), Ct));

        // Exactamente el 50 %: con el criterio por defecto (MAS de la mitad) no hay quorum para instalar.
        await svc.CheckInParticipanteAsync(sid, new CheckInParticipanteRequest(u[0], true), Ct);
        Assert.False((await svc.GetSesionAsync(sid, Ct))!.QuorumActual.Alcanzado);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.InstalarAsync(sid, new InstalarSesionRequest(u[0], null), Ct));

        await svc.CheckInParticipanteAsync(sid, new CheckInParticipanteRequest(u[1], true), Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.InstalarAsync(sid, new InstalarSesionRequest(u[0], u[0]), Ct));
        Assert.True(await svc.InstalarAsync(sid, new InstalarSesionRequest(u[0], null), Ct));
        var inst = (await svc.GetSesionAsync(sid, Ct))!.Instalacion;
        Assert.NotNull(inst);
        Assert.Equal(100m, inst!.QuorumPct);
        Assert.StartsWith("Administraci", inst.SecretarioNombre);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.InstalarAsync(sid, new InstalarSesionRequest(u[1], null), Ct));

        // Una votacion a la vez; no se cierra la sesion con una votacion abierta.
        var v = await svc.AbrirVotacionAsync(sid, new AbrirVotacionRequest(puntos[0].Id), Ct);
        Assert.Equal(puntos[0].Id, (await svc.GetSesionAsync(sid, Ct))!.PuntoActualId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AbrirVotacionAsync(sid, new AbrirVotacionRequest(puntos[1].Id), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CerrarSesionAsync(sid, new CerrarSesionRequest(), Ct));
        await svc.EmitirVotoAsync(v.Id, new EmitirVotoRequest(u[0], OpcionesVotoBase.Si), Ct);
        var cerrada = await svc.CerrarVotacionAsync(v.Id, new CerrarVotacionRequest(), Ct);
        Assert.NotNull(cerrada.Desglose);

        // Cierre: el quorum es el de la instalacion; los puntos sin tratar quedan No tratado.
        Assert.True(await svc.CerrarSesionAsync(sid, new CerrarSesionRequest(), Ct));
        var d = (await svc.GetSesionAsync(sid, Ct))!;
        Assert.Equal(EstadoSesion.Cerrada, d.Estado);
        Assert.True(d.QuorumAlcanzado);
        Assert.NotNull(d.Acta);
        Assert.Equal(EstadoPunto.Cerrado, d.Puntos[0].Estado);
        Assert.Equal(EstadoPunto.NoTratado, d.Puntos[1].Estado);
        Assert.Equal(EstadoPunto.NoTratado, d.Puntos[2].Estado);
        Assert.Contains("Instalacion", d.Acta!.ContenidoGenerado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Sin_instalacion_cierra_en_quorum_fallido_y_segunda_convocatoria_sesiona_con_numero_plural()
    {
        var tenantId = await SeedTenantAsync("Asam Segunda");
        await SeedPersonaConApplicationUser(tenantId);
        var u = await SeedUnidadesAsync(tenantId, 3, coeficientes: new[] { 0.1m, 0.1m, 0.8m });
        var (svc, sid) = await SesionEnCursoAsync(tenantId, TipoSesion.AsambleaExtraordinaria, PuntoReq(1, "Cambio de administrador"), PuntoReq(2, "Varios", vota: false));

        // Corregir el quorum calculado exige justificacion.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CerrarSesionAsync(sid, new CerrarSesionRequest(true, " "), Ct));
        Assert.True(await svc.CerrarSesionAsync(sid, new CerrarSesionRequest(), Ct));
        var fallida = (await svc.GetSesionAsync(sid, Ct))!;
        Assert.Equal(EstadoSesion.QuorumFallido, fallida.Estado);
        Assert.Null(fallida.Acta);

        var hija = await svc.ConvocarSegundaAsync(sid, Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConvocarSegundaAsync(sid, Ct));
        Assert.Equal(hija, (await svc.GetSesionAsync(sid, Ct))!.SesionHijaId);
        var s2 = (await svc.GetSesionAsync(hija, Ct))!;
        Assert.True(s2.SegundaConvocatoria);
        Assert.Equal(sid, s2.SesionPadreId);
        Assert.Equal(EstadoSesion.Borrador, s2.Estado);
        Assert.Equal(2, s2.Puntos.Count);

        // Tercer dia habil siguiente (sin sabados, domingos ni festivos) a las 8:00 p. m. hora de Colombia.
        var co = TimeSpan.FromHours(-5);
        var f = s2.FechaSesion.ToOffset(co);
        Assert.Equal(20, f.Hour);
        Assert.Equal(0, f.Minute);
        await using (var o = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext()))
        {
            var desde = DateOnly.FromDateTime(fallida.HoraCierre!.Value.ToOffset(co).DateTime);
            var hasta = DateOnly.FromDateTime(f.DateTime);
            var festivos = (await o.FestivosColombianos.AsNoTracking().Where(x => x.Fecha > desde && x.Fecha <= hasta).Select(x => x.Fecha).ToListAsync()).ToHashSet();
            var habiles = 0;
            for (var d = desde.AddDays(1); d <= hasta; d = d.AddDays(1))
                if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !festivos.Contains(d)) habiles++;
            Assert.Equal(3, habiles);
            Assert.False(hasta.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        }

        // Se cita sin el plazo de la ordinaria y sesiona con un numero plural (2 unidades, 20 % de coeficientes).
        await svc.EnviarCitacionAsync(hija, new EnviarCitacionRequest(null), Ct);
        await svc.AbrirSalaAsync(hija, Ct);
        await svc.CheckInParticipanteAsync(hija, new CheckInParticipanteRequest(u[0], true), Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.InstalarAsync(hija, new InstalarSesionRequest(u[0], null), Ct));
        await svc.CheckInParticipanteAsync(hija, new CheckInParticipanteRequest(u[1], true), Ct);
        var q = (await svc.GetSesionAsync(hija, Ct))!.QuorumActual;
        Assert.True(q.SegundaConvocatoria);
        Assert.True(q.Alcanzado);
        Assert.True(await svc.InstalarAsync(hija, new InstalarSesionRequest(u[0], null), Ct));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Votos_en_bloque_y_eleccion_por_planchas_con_empate_sin_resultado()
    {
        var tenantId = await SeedTenantAsync("Asam Planchas");
        await SeedPersonaConApplicationUser(tenantId);
        var u = await SeedUnidadesAsync(tenantId, 4, coeficientes: new[] { 0.25m, 0.25m, 0.25m, 0.25m });
        var planchas = new[] { "Plancha 1", "Plancha 2" };
        var (svc, sid) = await SesionEnCursoAsync(tenantId, TipoSesion.AsambleaOrdinaria,
            PuntoReq(1, "Eleccion del consejo", opciones: planchas), PuntoReq(2, "Eleccion del revisor", opciones: planchas));
        foreach (var x in u) await svc.CheckInParticipanteAsync(sid, new CheckInParticipanteRequest(x, true), Ct);
        await svc.InstalarAsync(sid, new InstalarSesionRequest(u[0], null), Ct);
        var puntos = (await svc.GetSesionAsync(sid, Ct))!.Puntos;
        Assert.Equal(planchas, puntos[0].OpcionesVoto);

        var v1 = await svc.AbrirVotacionAsync(sid, new AbrirVotacionRequest(puntos[0].Id), Ct);
        var r = await svc.VotarBloqueAsync(v1.Id, new VotarBloqueRequest(new[]
        {
            new VotoBloqueItem(u[0], "Plancha 1"), new VotoBloqueItem(u[1], "Plancha 1"),
            new VotoBloqueItem(u[2], "Plancha 1"), new VotoBloqueItem(u[3], "Plancha 2"),
        }), Ct);
        Assert.Equal(4, r.Registrados);
        Assert.Empty(r.Errores);
        var repetido = await svc.VotarBloqueAsync(v1.Id, new VotarBloqueRequest(new[] { new VotoBloqueItem(u[0], "Plancha 2") }), Ct);
        Assert.Equal(0, repetido.Registrados);
        Assert.Single(repetido.Errores);
        var opcionMala = await svc.VotarBloqueAsync(v1.Id, new VotarBloqueRequest(new[] { new VotoBloqueItem(u[0], "Si") }), Ct);
        Assert.Single(opcionMala.Errores);

        var c1 = await svc.CerrarVotacionAsync(v1.Id, new CerrarVotacionRequest(), Ct);
        Assert.Equal(ResultadoVotacion.Elegido, c1.ResultadoFinal);
        Assert.Equal("Plancha 1", c1.ResultadoOpcion);
        Assert.Equal(75m, c1.ResultadoPct);
        Assert.Equal(new[] { 75m, 25m }, c1.Desglose!.Select(x => x.Pct).ToArray());

        var v2 = await svc.AbrirVotacionAsync(sid, new AbrirVotacionRequest(puntos[1].Id), Ct);
        await svc.VotarBloqueAsync(v2.Id, new VotarBloqueRequest(new[]
        {
            new VotoBloqueItem(u[0], "Plancha 1"), new VotoBloqueItem(u[1], "Plancha 1"),
            new VotoBloqueItem(u[2], "Plancha 2"), new VotoBloqueItem(u[3], "Plancha 2"),
        }), Ct);
        var c2 = await svc.CerrarVotacionAsync(v2.Id, new CerrarVotacionRequest(), Ct);
        Assert.Equal(ResultadoVotacion.SinResultado, c2.ResultadoFinal);
        Assert.Null(c2.ResultadoOpcion);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Poderes_validan_unidad_apoderado_del_tenant_y_limite_por_persona()
    {
        var tenantId = await SeedTenantAsync("Asam Poderes");
        await SeedPersonaConApplicationUser(tenantId);
        var u = await SeedUnidadesAsync(tenantId, 3);
        var apoderado = await SeedPersonaEnUnidadAsync(tenantId, u[2], RolUnidadPersona.Residente);
        var ajeno = await SeedPersonaEnUnidadAsync(null, null, RolUnidadPersona.Residente);
        var (svc, _, _) = Build(tenantId);
        await svc.ActualizarConfigAsync(new ActualizarAsambleaConfigRequest(null, 1, null, null, null), Ct);
        var s = await svc.CrearSesionAsync(new CrearSesionRequest(TipoSesion.AsambleaOrdinaria, ModalidadSesion.Presencial, "Poderes",
            DateTimeOffset.UtcNow.AddDays(10), "Salon", null, new[] { PuntoReq(1, "P") }), Ct);

        var p1 = await svc.OtorgarPoderAsync(s.Id, new OtorgarPoderRequest(u[0], apoderado, TipoPoder.Digital, null), Ct);
        Assert.Equal(EstadoPoder.Aprobado, p1.Estado);
        // Un poder por unidad; el apoderado debe ser del directorio de la copropiedad.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.OtorgarPoderAsync(s.Id, new OtorgarPoderRequest(u[0], apoderado, TipoPoder.Pdf, null), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.OtorgarPoderAsync(s.Id, new OtorgarPoderRequest(u[1], ajeno, TipoPoder.Pdf, null), Ct));
        // Limite 1 por persona: el escrito queda pendiente y no se puede aprobar.
        var p2 = await svc.OtorgarPoderAsync(s.Id, new OtorgarPoderRequest(u[1], apoderado, TipoPoder.Pdf, null), Ct);
        Assert.Equal(EstadoPoder.Pendiente, p2.Estado);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DecidirPoderAsync(p2.Id, new DecidirPoderRequest(true, null), Ct));
        Assert.True(await svc.DecidirPoderAsync(p2.Id, new DecidirPoderRequest(false, "Excede el limite"), Ct));

        await LimpiarPersonasAsync(tenantId);
        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Orden_del_dia_solo_en_borrador_plazo_de_citacion_cancelacion_y_citacion_a_nombre_del_propietario()
    {
        var tenantId = await SeedTenantAsync("Asam Borrador");
        await SeedPersonaConApplicationUser(tenantId);
        var u = await SeedUnidadesAsync(tenantId, 2);
        var dueno = await SeedPersonaEnUnidadAsync(tenantId, u[0], RolUnidadPersona.Propietario);
        var (svc, _, _) = Build(tenantId);

        // Ordinaria dentro del plazo (5 dias por defecto): no se cita.
        var ord = await svc.CrearSesionAsync(new CrearSesionRequest(TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "Ordinaria",
            DateTimeOffset.UtcNow.AddDays(2), null, null, new[] { PuntoReq(1, "P1") }, SalaPropia: true), Ct);
        Assert.True(ord.SalaPropia);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnviarCitacionAsync(ord.Id, new EnviarCitacionRequest(null, true), Ct));

        // Extraordinaria dentro del plazo: pide confirmacion explicita.
        var ext = await svc.CrearSesionAsync(new CrearSesionRequest(TipoSesion.AsambleaExtraordinaria, ModalidadSesion.Presencial, "Extra",
            DateTimeOffset.UtcNow.AddDays(2), "Salon", null, new[] { PuntoReq(1, "P1") }), Ct);
        // Orden del dia editable en Borrador: agregar, editar (opciones de eleccion) y quitar con renumeracion.
        Assert.True(await svc.AgregarPuntoAsync(ext.Id, PuntoReq(0, "P2"), Ct));
        Assert.True(await svc.AgregarPuntoAsync(ext.Id, PuntoReq(0, "P3"), Ct));
        var pts = (await svc.GetSesionAsync(ext.Id, Ct))!.Puntos;
        Assert.Equal(new[] { 1, 2, 3 }, pts.Select(p => p.Numero).ToArray());
        Assert.True(await svc.EliminarPuntoAsync(pts[1].Id, Ct));
        Assert.True(await svc.ActualizarPuntoAsync(pts[2].Id, new ActualizarPuntoRequest("P3 eleccion", null, true, TipoMayoria.Simple, 50m,
            ModalidadVoto.Publico, null, new[] { "Candidato A", "Candidato B" }), Ct));
        pts = (await svc.GetSesionAsync(ext.Id, Ct))!.Puntos;
        Assert.Equal(new[] { 1, 2 }, pts.Select(p => p.Numero).ToArray());
        Assert.Equal(new[] { "Candidato A", "Candidato B" }, pts[1].OpcionesVoto);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnviarCitacionAsync(ext.Id, new EnviarCitacionRequest(null), Ct));
        Assert.StartsWith("FUERA_DE_PLAZO:", ex.Message);
        Assert.True(await svc.EnviarCitacionAsync(ext.Id, new EnviarCitacionRequest(null, true), Ct));

        // Citada: el orden del dia ya no cambia (la narrativa del secretario si).
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AgregarPuntoAsync(ext.Id, PuntoReq(0, "Tarde"), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarPuntoAsync(pts[0].Id,
            new ActualizarPuntoRequest("Otro titulo", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null), Ct));
        Assert.True(await svc.ActualizarPuntoAsync(pts[0].Id,
            new ActualizarPuntoRequest(pts[0].Titulo, pts[0].Descripcion, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, "Narrativa"), Ct));

        // Citacion a nombre del propietario (Ley 675: vota el propietario).
        var citada = (await svc.GetSesionAsync(ext.Id, Ct))!;
        Assert.Equal(dueno, citada.Participantes.Single(p => p.UnidadPrivadaId == u[0]).PersonaId);

        // Cancelar: Borrador o Citada, con motivo obligatorio.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CancelarSesionAsync(ext.Id, new CancelarSesionRequest(" "), Ct));
        Assert.True(await svc.CancelarSesionAsync(ext.Id, new CancelarSesionRequest("Se aplaza por obras"), Ct));
        var cancelada = (await svc.GetSesionAsync(ext.Id, Ct))!;
        Assert.Equal(EstadoSesion.Cancelada, cancelada.Estado);
        Assert.Equal("Se aplaza por obras", cancelada.MotivoCancelacion);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CancelarSesionAsync(ext.Id, new CancelarSesionRequest("Otra vez"), Ct));

        await LimpiarPersonasAsync(tenantId);
        await CleanTenant(tenantId);
    }

    // ===================== Helpers de la ola 2 =====================

    private readonly List<Guid> _personasExtra = new();

    /// <summary>Un propietario por cada unidad del tenant que aun no lo tenga (la citacion cita a nombre del propietario).</summary>
    private async Task DuenosAsync(Guid tenantId)
    {
        List<Guid> sinDueno;
        await using (var ctx = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext()))
            sinDueno = await ctx.UnidadesPrivadas.IgnoreQueryFilters().Where(u => u.TenantId == tenantId
                && !ctx.UnidadPersonas.IgnoreQueryFilters().Any(up => up.UnidadId == u.Id && up.Rol == RolUnidadPersona.Propietario)).Select(u => u.Id).ToListAsync();
        foreach (var u in sinDueno) await SeedPersonaEnUnidadAsync(tenantId, u, RolUnidadPersona.Propietario);
    }

    /// <summary>Persona vinculada a una unidad del tenant (unidad_personas). Sin unidad = persona de otra copropiedad.</summary>
    private async Task<Guid> SeedPersonaEnUnidadAsync(Guid? tenantId, Guid? unidadId, RolUnidadPersona rol)
    {
        await using var ctx = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"P{Guid.NewGuid():N}"[..18],
            Nombres = "Persona",
            Apellidos = rol.ToString(),
            Email = $"p.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        if (tenantId is Guid t && unidadId is Guid uid)
            ctx.UnidadPersonas.Add(new UnidadPersona { TenantId = t, UnidadId = uid, PersonaId = p.Id, Rol = rol, Activo = true });
        await ctx.SaveChangesAsync();
        _personasExtra.Add(p.Id);
        return p.Id;
    }

    private async Task LimpiarPersonasAsync(Guid tenantId)
    {
        await using var ctx = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_poderes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_participantes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_personas WHERE tenant_id = {tenantId}");
        foreach (var id in _personasExtra) await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {id}");
        _personasExtra.Clear();
    }
}
