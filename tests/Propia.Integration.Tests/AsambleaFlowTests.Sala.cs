using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Asambleas;
using Propia.Application.Common;
using Propia.Domain.Enums;
using Propia.Infrastructure.Asambleas;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Ola 5 del hand-off de Asambleas: sala virtual con LiveKit. Token de acceso firmado (moderador vs copropietario), firma
/// del webhook (JWT + hash del cuerpo), asistencia automatica (entrar = presente con TODAS sus unidades habilitadas; salir
/// = salida pendiente que se confirma pasada la gracia; reconectar la cancela), grabacion -> documento "Grabacion"
/// (idempotente) y cierre que detiene la grabacion. El egress se simula: no se llama a LiveKit.
/// </summary>
public partial class AsambleaFlowTests
{
    private const string LkKey = "devkey", LkSecret = "secret";

    private static IConfiguration LkConfig(bool grabacion = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["LiveKit:Url"] = "ws://localhost:7880",
        ["LiveKit:ApiKey"] = LkKey,
        ["LiveKit:ApiSecret"] = LkSecret,
        ["LiveKit:Grabacion:Bucket"] = grabacion ? "propia-test" : null,
        ["LiveKit:Grabacion:AccessKey"] = grabacion ? "ak" : null,
        ["LiveKit:Grabacion:Secret"] = grabacion ? "sk" : null
    }).Build();

    /// <summary>Proveedor real para tokens y webhooks; el egress (HTTP a LiveKit) se simula.</summary>
    private sealed class VideoFalso : IVideoSalaProvider
    {
        private readonly LiveKitVideoSalaProvider _real;
        public VideoFalso(IConfiguration cfg) => _real = new LiveKitVideoSalaProvider(cfg, null!);
        public List<string> Iniciadas { get; } = new();
        public List<string> Detenidas { get; } = new();
        public string Nombre => _real.Nombre;
        public bool Configurada => _real.Configurada;
        public bool GrabacionConfigurada => _real.GrabacionConfigurada;
        public string Url => _real.Url;
        public string NombreSala(Guid t, Guid s) => _real.NombreSala(t, s);
        public bool TryLeerSala(string? sala, out Guid t, out Guid s) => _real.TryLeerSala(sala, out t, out s);
        public string CrearToken(string sala, string id, string nombre, bool mod, TimeSpan v) => _real.CrearToken(sala, id, nombre, mod, v);
        public VideoSalaEvento? LeerWebhook(string cuerpo, string? auth) => _real.LeerWebhook(cuerpo, auth);
        public Task<string> IniciarGrabacionAsync(string sala, string ruta, CancellationToken ct) { Iniciadas.Add(ruta); return Task.FromResult("EG_" + Iniciadas.Count); }
        public Task DetenerGrabacionAsync(string egressId, CancellationToken ct) { Detenidas.Add(egressId); return Task.CompletedTask; }
    }

    private (IAsambleaService svc, PropiaDbContext db) BuildSala(Guid tenantId, IVideoSalaProvider? video)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        return (new AsambleaService(db, ctx, scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(), video), db);
    }

    private static JsonObject Claims(string jwt)
    {
        var p = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        return (JsonNode.Parse(Convert.FromBase64String(p.PadRight(p.Length + (4 - p.Length % 4) % 4, '=')))) as JsonObject ?? throw new InvalidOperationException();
    }

    /// <summary>Firma un webhook como LiveKit: JWT HS256 (iss = ApiKey) con el sha256 del cuerpo en base64.</summary>
    private static string FirmarWebhook(string cuerpo, string secret = LkSecret)
    {
        static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var h = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var p = B64(Encoding.UTF8.GetBytes(new JsonObject
        {
            ["iss"] = LkKey,
            ["nbf"] = ahora - 5,
            ["exp"] = ahora + 300,
            ["sha256"] = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(cuerpo)))
        }.ToJsonString()));
        return $"{h}.{p}.{B64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(h + "." + p)))}";
    }

    private static string Cuerpo(string evento, string sala, string? identidad = null, JsonObject? egress = null)
    {
        var o = new JsonObject { ["event"] = evento, ["room"] = new JsonObject { ["name"] = sala }, ["id"] = "EV_" + Guid.NewGuid().ToString("N")[..8] };
        if (identidad is not null) o["participant"] = new JsonObject { ["identity"] = identidad, ["name"] = "Prueba" };
        if (egress is not null) o["egressInfo"] = egress;
        return o.ToJsonString();
    }

    [Fact]
    public void Sala_token_firmado_y_webhook_con_firma_y_hash_del_cuerpo()
    {
        var lk = new LiveKitVideoSalaProvider(LkConfig(), null!);
        Assert.True(lk.Configurada);
        Assert.True(lk.GrabacionConfigurada);
        var t = Guid.NewGuid(); var s = Guid.NewGuid();
        var sala = lk.NombreSala(t, s);
        Assert.True(lk.TryLeerSala(sala, out var t2, out var s2));
        Assert.Equal((t, s), (t2, s2));
        Assert.False(lk.TryLeerSala("otra-sala", out _, out _));

        var mod = Claims(lk.CrearToken(sala, IdentidadSala.Usuario(Guid.NewGuid()), "Admin", true, TimeSpan.FromHours(1)));
        Assert.Equal(LkKey, mod["iss"]!.GetValue<string>());
        Assert.Equal(sala, mod["video"]!["room"]!.GetValue<string>());
        Assert.True(mod["video"]!["roomAdmin"]!.GetValue<bool>());
        var per = Guid.NewGuid();
        var res = Claims(lk.CrearToken(sala, IdentidadSala.Persona(per), "Copropietario", false, TimeSpan.FromHours(1)));
        Assert.Equal(IdentidadSala.Persona(per), res["sub"]!.GetValue<string>());
        Assert.Null(res["video"]!["roomAdmin"]);
        Assert.Equal(per, IdentidadSala.PersonaDe(res["sub"]!.GetValue<string>()));
        Assert.Null(IdentidadSala.PersonaDe(mod["sub"]!.GetValue<string>()));

        var cuerpo = Cuerpo("participant_joined", sala, IdentidadSala.Persona(per));
        var ev = lk.LeerWebhook(cuerpo, FirmarWebhook(cuerpo));
        Assert.NotNull(ev);
        Assert.Equal(("participant_joined", sala, IdentidadSala.Persona(per)), (ev!.Evento, ev.Sala, ev.Identidad));
        Assert.Null(lk.LeerWebhook(cuerpo, FirmarWebhook(cuerpo, "otro-secreto")));                       // firma ajena
        Assert.Null(lk.LeerWebhook(cuerpo.Replace("participant_joined", "participant_left"), FirmarWebhook(cuerpo)));   // cuerpo alterado
        Assert.Null(lk.LeerWebhook(cuerpo, null));

        // egress_ended: archivo, tamano (int64 como texto) y duracion en nanosegundos; con PublicUrl se arma la URL publica.
        var eg = new JsonObject
        {
            ["egressId"] = "EG_1",
            ["roomName"] = sala,
            ["status"] = "EGRESS_COMPLETE",
            ["fileResults"] = new JsonArray(new JsonObject { ["filename"] = "asambleas/x/grabacion.mp4", ["location"] = "https://s3/x", ["size"] = "2048", ["duration"] = "90000000000" })
        };
        var c2 = Cuerpo("egress_ended", sala, egress: eg);
        var e2 = lk.LeerWebhook(c2, FirmarWebhook(c2))!;
        Assert.Equal(("EG_1", "EGRESS_COMPLETE", "https://s3/x", 2048L, 90d), (e2.EgressId, e2.EstadoEgress, e2.ArchivoUrl, e2.TamanioBytes, e2.DuracionSeg));

        // Sin configuracion la sala no esta disponible.
        var vacio = new LiveKitVideoSalaProvider(new ConfigurationBuilder().Build(), null!);
        Assert.False(vacio.Configurada);
        Assert.Throws<InvalidOperationException>(() => vacio.CrearToken(sala, "u:x", "x", false, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Sala_entrar_marca_presente_salir_con_gracia_reconexion_y_grabacion_a_documentos()
    {
        var tenantId = await SeedTenantAsync("Asam Sala Virtual");
        await SeedPersonaConApplicationUser(tenantId);
        var u = await SeedUnidadesAsync(tenantId, 3, coeficientes: new[] { 0.4m, 0.3m, 0.3m });
        await DuenosAsync(tenantId);
        Guid dueno0, dueno1;
        await using (var o = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext()))
        {
            dueno0 = await o.UnidadPersonas.IgnoreQueryFilters().Where(x => x.UnidadId == u[0] && x.Rol == RolUnidadPersona.Propietario).Select(x => x.PersonaId!.Value).FirstAsync();
            dueno1 = await o.UnidadPersonas.IgnoreQueryFilters().Where(x => x.UnidadId == u[1] && x.Rol == RolUnidadPersona.Propietario).Select(x => x.PersonaId!.Value).FirstAsync();
            // El dueno de la unidad 0 tambien es dueno de la unidad 2: entra una vez y quedan presentes las dos.
            var up2 = await o.UnidadPersonas.IgnoreQueryFilters().FirstAsync(x => x.UnidadId == u[2] && x.Rol == RolUnidadPersona.Propietario);
            up2.PersonaId = dueno0;
            await o.SaveChangesAsync();
        }
        var video = new VideoFalso(LkConfig());
        var (svc, db) = BuildSala(tenantId, video);
        var s = await svc.CrearSesionAsync(new CrearSesionRequest(TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "Sala virtual",
            DateTimeOffset.UtcNow.AddDays(10), null, null, new[] { PuntoReq(1, "Presupuesto") }, SalaPropia: true), Ct);
        var sid = s.Id;
        await svc.EnviarCitacionAsync(sid, new EnviarCitacionRequest(null), Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AccesoSalaAdminAsync(sid, Ct));   // sala cerrada
        await svc.AbrirSalaAsync(sid, Ct);
        await svc.ActualizarConfigAsync(new ActualizarAsambleaConfigRequest(null, null, 60, null, null), Ct);

        // Acceso: moderador (administracion) y copropietario; quien no tiene unidades habilitadas no entra.
        Assert.True(svc.ConfigSala().Configurada);
        var adm = (await svc.AccesoSalaAdminAsync(sid, Ct))!;
        Assert.True(adm.Moderador);
        Assert.True(Claims(adm.Token)["video"]!["roomAdmin"]!.GetValue<bool>());
        var res = (await svc.AccesoSalaResidenteAsync(sid, dueno0, Ct))!;
        Assert.False(res.Moderador);
        Assert.Equal(IdentidadSala.Persona(dueno0), res.Identidad);
        Assert.Contains("\u00b7", Claims(res.Token)["name"]!.GetValue<string>());
        var ajeno = await SeedPersonaEnUnidadAsync(null, null, RolUnidadPersona.Propietario);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.AccesoSalaResidenteAsync(sid, ajeno, Ct));

        async Task<Dictionary<Guid, bool>> Presentes()
            => (await svc.GetSesionAsync(sid, Ct))!.Participantes.ToDictionary(p => p.UnidadPrivadaId, p => p.Presente);
        VideoSalaEvento Ev(string evento, Guid persona) => new(evento, adm.Sala, IdentidadSala.Persona(persona), null, null, null, null, null, null, null, null);

        // Entrar = presente con TODAS sus unidades habilitadas (0 y 2); el equipo de la administracion no suma quorum.
        Assert.Null(await svc.ProcesarEventoSalaAsync(sid, new VideoSalaEvento("participant_joined", adm.Sala, adm.Identidad, null, null, null, null, null, null, null, null), Ct));
        Assert.DoesNotContain(true, (await Presentes()).Values);
        await svc.ProcesarEventoSalaAsync(sid, Ev("participant_joined", dueno0), Ct);
        await svc.ProcesarEventoSalaAsync(sid, Ev("participant_joined", dueno1), Ct);
        var pres = await Presentes();
        Assert.True(pres[u[0]] && pres[u[1]] && pres[u[2]]);

        // Salir: queda pendiente durante la gracia (sigue presente); reconectar dentro de la gracia la cancela.
        var vence = await svc.ProcesarEventoSalaAsync(sid, Ev("participant_left", dueno0), Ct);
        Assert.NotNull(vence);
        Assert.InRange((vence!.Value - DateTimeOffset.UtcNow).TotalSeconds, 50, 61);
        Assert.Equal(0, await svc.ConfirmarSalidasPendientesAsync(sid, Ct));
        Assert.True((await Presentes())[u[0]]);
        await svc.ProcesarEventoSalaAsync(sid, Ev("participant_joined", dueno0), Ct);
        Assert.False(await db.SesionParticipantes.AnyAsync(p => p.SesionId == sid && p.SalidaPendienteEn != null, Ct));

        // Sale y no vuelve: al vencer la gracia se registra la salida de sus dos unidades en el quorum.
        await svc.ProcesarEventoSalaAsync(sid, Ev("participant_left", dueno0), Ct);
        await db.Database.ExecuteSqlAsync($"UPDATE sesion_participantes SET salida_pendiente_en = now() - interval '1 second' WHERE sesion_id = {sid} AND salida_pendiente_en IS NOT NULL", Ct);
        Assert.Equal(2, await svc.ConfirmarSalidasPendientesAsync(sid, Ct));
        pres = await Presentes();
        Assert.False(pres[u[0]]); Assert.True(pres[u[1]]); Assert.False(pres[u[2]]);
        Assert.Equal(2, await db.SesionQuorumLog.CountAsync(l => l.SesionId == sid && l.Evento == EventoQuorum.Salida, Ct));

        // Grabacion: iniciar (una a la vez), terminar por webhook -> documento Grabacion (idempotente).
        Assert.True(await svc.IniciarGrabacionAsync(sid, Ct));
        Assert.StartsWith($"asambleas/{tenantId:N}/{sid:N}/grabacion-", video.Iniciadas.Single());
        Assert.NotNull((await svc.GetSesionAsync(sid, Ct))!.GrabandoDesde);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.IniciarGrabacionAsync(sid, Ct));
        var fin = new VideoSalaEvento("egress_ended", adm.Sala, null, null, "EG_1", "EGRESS_COMPLETE", "https://grabaciones.test/sala.mp4", "sala.mp4", 4096, 75, null);
        await svc.ProcesarEventoSalaAsync(sid, fin, Ct);
        await svc.ProcesarEventoSalaAsync(sid, fin, Ct);
        var d = (await svc.GetSesionAsync(sid, Ct))!;
        Assert.Null(d.GrabandoDesde);
        var doc = Assert.Single(d.Documentos, x => x.Tipo == TipoDocumentoSesion.Grabacion);
        Assert.Equal(("https://grabaciones.test/sala.mp4", "video/mp4", 4096L), (doc.UrlStorage, doc.TipoArchivo, doc.TamanioBytes));
        Assert.Equal("Duraci\u00f3n 00:01:15", doc.Descripcion);

        // Una grabacion fallida no crea documento y queda en la bitacora.
        await svc.IniciarGrabacionAsync(sid, Ct);
        await svc.ProcesarEventoSalaAsync(sid, fin with { EgressId = "EG_2", EstadoEgress = "EGRESS_FAILED", ArchivoUrl = null, Error = "sin espacio" }, Ct);
        Assert.Single((await svc.GetSesionAsync(sid, Ct))!.Documentos, x => x.Tipo == TipoDocumentoSesion.Grabacion);
        Assert.Contains(await svc.GetTranscripcionAsync(sid, null, Ct), l => l.Texto.Contains("sin espacio"));

        // Levantar la sesion detiene la grabacion en curso; despues la sala ya no recibe asistencia.
        await svc.IniciarGrabacionAsync(sid, Ct);
        await svc.ProcesarEventoSalaAsync(sid, Ev("participant_joined", dueno0), Ct);
        await svc.InstalarAsync(sid, new InstalarSesionRequest(u[1], null), Ct);
        await svc.CerrarSesionAsync(sid, new CerrarSesionRequest(), Ct);
        Assert.Equal("EG_3", video.Detenidas.Last());
        Assert.Null((await svc.GetSesionAsync(sid, Ct))!.GrabandoDesde);
        Assert.Null(await svc.ProcesarEventoSalaAsync(sid, Ev("participant_left", dueno0), Ct));
        Assert.False(await db.SesionParticipantes.AnyAsync(p => p.SesionId == sid && p.SalidaPendienteEn != null, Ct));

        // Sin proveedor configurado o en una presencial no hay sala; otra copropiedad no ve la sesion.
        var (sinVideo, _) = BuildSala(tenantId, new VideoFalso(new ConfigurationBuilder().Build()));
        Assert.False(sinVideo.ConfigSala().Configurada);
        var (svcPres, sidPres) = await SesionEnCursoAsync(tenantId, TipoSesion.Consejo, PuntoReq(1, "Varios", vota: false));
        var (conVideo, _) = BuildSala(tenantId, video);
        await Assert.ThrowsAsync<InvalidOperationException>(() => conVideo.AccesoSalaAdminAsync(sidPres, Ct));
        var otro = await SeedTenantAsync("Asam Sala Otro");
        var (svcOtro, _) = BuildSala(otro, video);
        Assert.Null(await svcOtro.AccesoSalaAdminAsync(sid, Ct));
        _ = svcPres;

        await LimpiarActaAsync(tenantId);
        await CleanTenant(otro);
    }
}
