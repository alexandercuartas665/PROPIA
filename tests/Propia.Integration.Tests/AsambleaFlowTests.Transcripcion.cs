using Microsoft.EntityFrameworkCore;
using Propia.Application.Asambleas;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Ola 4 del hand-off de Asambleas: bitacora de la sesion. Eventos del sistema automaticos (sala, instalacion, puntos,
/// votaciones con resultado, cierre), voz a texto asociada al punto en curso solo con la transcripcion activa,
/// subtitulos del residente, transcripcion en el acta (borrador base y prompt de la IA) y aislamiento por copropiedad.
/// </summary>
public partial class AsambleaFlowTests
{
    [Fact]
    public async Task Transcripcion_eventos_del_sistema_voz_por_punto_subtitulos_y_acta()
    {
        var tenantId = await SeedTenantAsync("Asam Transcripcion");
        await SeedPersonaConApplicationUser(tenantId);
        var u = await SeedUnidadesAsync(tenantId, 2, coeficientes: new[] { 0.5m, 0.5m });
        var (svc, sid) = await SesionEnCursoAsync(tenantId, TipoSesion.AsambleaOrdinaria, PuntoReq(1, "Presupuesto"), PuntoReq(2, "Varios", vota: false));
        foreach (var x in u) await svc.CheckInParticipanteAsync(sid, new CheckInParticipanteRequest(x, true), Ct);
        await svc.InstalarAsync(sid, new InstalarSesionRequest(u[0], null), Ct);
        var p1 = (await svc.GetSesionAsync(sid, Ct))!.Puntos[0];

        // Pausada: no entra voz.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AgregarTranscripcionAsync(sid, new AgregarTranscripcionRequest("Hola"), Ct));
        Assert.True(await svc.ActivarTranscripcionAsync(sid, new ActivarTranscripcionRequest(true), Ct));
        Assert.True((await svc.GetSesionAsync(sid, Ct))!.TranscripcionActiva);

        await svc.IniciarPuntoAsync(p1.Id, Ct);
        var l = await svc.AgregarTranscripcionAsync(sid, new AgregarTranscripcionRequest("  Propongo aprobar el presupuesto con un ajuste del 5 %.  ", 0.91m), Ct);
        Assert.Equal(TipoLineaTranscripcion.Voz, l.Tipo);
        Assert.Equal(1, l.PuntoNumero);
        Assert.Equal("Admin Test", l.AutorNombre);   // sin persona = el usuario actual
        Assert.Equal("Propongo aprobar el presupuesto con un ajuste del 5 %.", l.Texto);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AgregarTranscripcionAsync(sid, new AgregarTranscripcionRequest("Intruso", PersonaId: Guid.NewGuid()), Ct));

        var v = await svc.AbrirVotacionAsync(sid, new AbrirVotacionRequest(p1.Id), Ct);
        await svc.VotarBloqueAsync(v.Id, new VotarBloqueRequest(u.Select(x => new VotoBloqueItem(x, "Si")).ToList()), Ct);
        await svc.CerrarVotacionAsync(v.Id, new CerrarVotacionRequest(), Ct);

        // Subtitulos del residente (propietario de la unidad 1): ultimas lineas de voz, sin eventos internos.
        Guid dueno;
        await using (var o = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext()))
            dueno = await o.UnidadPersonas.IgnoreQueryFilters().Where(x => x.UnidadId == u[0] && x.Rol == RolUnidadPersona.Propietario).Select(x => x.PersonaId!.Value).FirstAsync();
        var res = (await svc.GetSesionParaResidenteAsync(sid, dueno, Ct))!;
        Assert.True(res.TranscripcionActiva);
        Assert.Single(res.Subtitulos!);
        Assert.Equal(TipoLineaTranscripcion.Voz, res.Subtitulos![0].Tipo);

        await svc.CerrarSesionAsync(sid, new CerrarSesionRequest(), Ct);
        var d = (await svc.GetSesionAsync(sid, Ct))!;
        Assert.False(d.TranscripcionActiva);
        var tx = await svc.GetTranscripcionAsync(sid, null, Ct);
        var sistema = tx.Where(x => x.Tipo == TipoLineaTranscripcion.Sistema).Select(x => x.Texto).ToList();
        Assert.Contains(sistema, t => t.StartsWith("Se abre la sala"));
        Assert.Contains(sistema, t => t.StartsWith("Asamblea instalada"));
        Assert.Contains(sistema, t => t.StartsWith("Se activa la transcripción"));
        Assert.Contains(sistema, t => t.StartsWith("Se inicia el punto 1"));
        Assert.Contains(sistema, t => t.StartsWith("Se abre la votación del punto 1"));
        Assert.Contains(sistema, t => t.StartsWith("Se cierra la votación del punto 1: Aprobado"));
        Assert.Contains(sistema, t => t.StartsWith("Se levanta la sesión"));
        Assert.Equal(tx.OrderBy(x => x.Hora).Select(x => x.Id), tx.Select(x => x.Id));
        Assert.Single(await svc.GetTranscripcionAsync(sid, tx.Last().Hora.AddMilliseconds(-1), Ct));   // ?desde= trae solo lo nuevo

        // El acta usa la transcripcion: borrador base con la intervencion y el prompt de la IA con la linea del punto.
        var (acta, ia, _, _, _) = BuildActa(tenantId);
        var actaId = (await acta.PrepararAsync(sid, Ct))!.Value;
        Assert.Contains("Intervinieron 1 persona", (await svc.GetSesionAsync(sid, Ct))!.Acta!.BorradorHtml);
        ia.Respuesta = new Propia.Application.InfraestructuraIa.AiChatResult(true, "<h4>Desarrollo</h4><p>Se aprobó el presupuesto con el ajuste propuesto en la sesión.</p>", null);
        await acta.RedactarAsync(actaId, Ct);
        Assert.Contains("Admin Test | P1: Propongo aprobar el presupuesto", ia.Prompts.Single());

        // Aislamiento: otra copropiedad no ve la bitacora.
        var otro = await SeedTenantAsync("Asam Transcripcion Otro");
        var (svcOtro, _, _) = Build(otro);
        Assert.Empty(await svcOtro.GetTranscripcionAsync(sid, null, Ct));

        await LimpiarActaAsync(tenantId);
        await CleanTenant(otro);
    }
}
