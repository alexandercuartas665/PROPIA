using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.Catalogos;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Integraciones;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Pqrsd;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// PQRSD por correo (Ola P1) contra PostgreSQL real con propia_app y SIN usuario autenticado (como el job del buzon):
/// un correo que el Coordinador clasifica como PQRSD pasa al Radicador de PQRSD; con ambas confianzas sobre el umbral del
/// buzon se radica solo (medio Correo, fecha de recibido = fecha del correo, correo original adjunto, persona externa si
/// el remitente no esta en el directorio); por debajo, o con el buzon en "nunca", queda la propuesta y se radica a mano.
/// Nunca se radica dos veces el mismo correo, y una copropiedad no puede radicar el correo de otra.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CorreoPqrsdFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private static readonly CancellationToken Ct = CancellationToken.None;

    public CorreoPqrsdFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddScoped<ITenantContext, TenantContext>();
        // Sin HttpContext: asi corre el job del buzon (no hay usuario que radique).
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString).AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<PropiaDbContext>().AddDefaultTokenProviders();
        sc.AddSingleton<Propia.Application.Notificaciones.INotificacionDispatcher, FakeNotificacionDispatcher>();
        sc.AddScoped<Propia.Application.Tareas.ITareasService, Propia.Infrastructure.Tareas.TareasService>();
        sc.AddSingleton<Propia.Application.Documents.IMembreteDocumentBuilder, Propia.Infrastructure.Documents.MembreteDocumentBuilder>();
        sc.AddScoped<Propia.Application.Pqrsd.IPqrsdService, PqrsdService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private const string CuerpoQueja =
        "Buenas noches, el vecino de arriba pone musica a alto volumen todas las noches despues de las 11 pm.\n" +
        "Les pido tomar medidas.\n\nJuan Perez\n\n" +
        "El lun, 16 nov 2026 a las 9:00, Administracion <admin@conjunto.com> escribió:\n> Respuesta vieja que no debe radicarse";

    [Fact]
    public async Task Confianza_alta_radica_solo_con_persona_externa_y_no_duplica()
    {
        var t = await SeedTenantAsync("[CPQ] auto");
        var remitente = $"vecino.{Guid.NewGuid():N}@ejemplo.com";
        try
        {
            var ia = new IaRuteada(coordinador: 0.95m, radicador: 0.93m);
            var bz = await SeedBuzonAsync(t, 0.90m);
            var fecha = DateTimeOffset.UtcNow.AddDays(-3);
            var id = await SeedCorreoAsync(t, bz, remitente, "Ruido en la noche", CuerpoQueja, fecha);

            await using (var scope = Scope(t))
            {
                var r = await Entrante(scope, ia).ReclasificarAsync(id, Ct);
                Assert.Equal(EstadoCorreoRecibido.Procesado, r!.Estado);
                Assert.NotNull(r.PqrsdRadicado);
            }

            await using (var scope = Scope(t))
            {
                var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
                var c = await db.CorreosRecibidos.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.True(c.PqrsdAutomatico);
                var exp = await db.PqrsdExpedientes.AsNoTracking().SingleAsync(x => x.Id == c.PqrsdExpedienteId);
                Assert.Equal(MedioRecepcionPqrsd.CorreoElectronico, exp.MedioRecepcion);
                Assert.Equal(DateOnly.FromDateTime(fecha.ToOffset(TimeSpan.FromHours(-5)).DateTime), exp.FechaRecibido);
                Assert.Equal(TipoPqrsd.Queja, exp.Tipo);
                Assert.Contains("alto volumen", exp.Descripcion);
                Assert.DoesNotContain("Respuesta vieja", exp.Descripcion);

                // Radicador: persona externa nueva con el correo del remitente y documento pendiente.
                var p = await db.Personas.AsNoTracking().SingleAsync(x => x.Id == exp.RadicadorPersonaId);
                Assert.Equal(remitente, p.Email);
                Assert.StartsWith("PENDIENTE-", p.Documento);
                Assert.True(p.PerfilIncompleto);

                var adj = await db.PqrsdAdjuntos.AsNoTracking().Where(a => a.ExpedienteId == exp.Id).Select(a => a.NombreArchivo).ToListAsync();
                Assert.Contains(RadicadorPqrsdCorreoAgente.NombreAdjuntoOriginal, adj);
                Assert.Contains("foto.jpg", adj);
            }

            // Reclasificar o volver a pedir la radicacion no crea otro expediente.
            await using (var scope = Scope(t))
            {
                await Entrante(scope, ia).ReclasificarAsync(id, Ct);
                var again = await Pqrsd(scope, ia).RadicarAsync(id, new RadicarCorreoPqrsdRequest(), Ct);
                Assert.True(again.Ok);
                var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
                Assert.Equal(1, await db.PqrsdExpedientes.CountAsync());
            }
        }
        finally { await CleanupAsync(t, remitente); }
    }

    [Fact]
    public async Task Confianza_baja_deja_propuesta_y_se_radica_a_mano_con_la_persona_del_directorio()
    {
        var t = await SeedTenantAsync("[CPQ] manual");
        var remitente = $"residente.{Guid.NewGuid():N}@ejemplo.com";
        var personaId = await SeedPersonaAsync(remitente);
        try
        {
            var ia = new IaRuteada(coordinador: 0.95m, radicador: 0.60m);
            var bz = await SeedBuzonAsync(t, 0.90m);
            var id = await SeedCorreoAsync(t, bz, remitente, "Ruido", CuerpoQueja, DateTimeOffset.UtcNow);

            await using (var scope = Scope(t))
            {
                var r = await Entrante(scope, ia).ReclasificarAsync(id, Ct);
                Assert.Equal(EstadoCorreoRecibido.Clasificado, r!.Estado);
                Assert.Null(r.PqrsdRadicado);
                var det = await Entrante(scope, ia).ObtenerAsync(id, Ct);
                Assert.NotNull(det!.PropuestaPqrsd);
                Assert.Contains("umbral", det.PropuestaPqrsd!.Pendiente);
                Assert.NotNull(det.PropuestaPqrsd.TipoId);
            }

            await using (var scope = Scope(t))
            {
                var svc = Pqrsd(scope, ia);
                var r = await svc.RadicarAsync(id, new RadicarCorreoPqrsdRequest(), Ct);   // toma tipo y categoria de la propuesta
                Assert.True(r.Ok, r.Error);
                var r2 = await svc.RadicarAsync(id, new RadicarCorreoPqrsdRequest(), Ct);
                Assert.Equal(r.Numero, r2.Numero);   // idempotente

                var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
                var exp = await db.PqrsdExpedientes.AsNoTracking().SingleAsync();
                Assert.Equal(personaId, exp.RadicadorPersonaId);   // la persona que ya existia, sin crear otra
                var c = await db.CorreosRecibidos.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.False(c.PqrsdAutomatico);
                Assert.Equal(EstadoCorreoRecibido.Procesado, c.Estado);
            }
        }
        finally { await CleanupAsync(t, remitente); }
    }

    [Fact]
    public async Task Buzon_en_nunca_no_radica_solo_y_otra_copropiedad_no_puede_radicar()
    {
        var tA = await SeedTenantAsync("[CPQ] nunca A");
        var tB = await SeedTenantAsync("[CPQ] nunca B");
        var remitente = $"otro.{Guid.NewGuid():N}@ejemplo.com";
        try
        {
            var ia = new IaRuteada(coordinador: 0.99m, radicador: 0.99m);
            var bz = await SeedBuzonAsync(tA, null);
            var id = await SeedCorreoAsync(tA, bz, remitente, "Queja", CuerpoQueja, DateTimeOffset.UtcNow);

            await using (var scope = Scope(tA))
            {
                var r = await Entrante(scope, ia).ReclasificarAsync(id, Ct);
                Assert.Null(r!.PqrsdRadicado);
                var det = await Entrante(scope, ia).ObtenerAsync(id, Ct);
                Assert.Contains("no radica PQRSD solo", det!.PropuestaPqrsd!.Pendiente);
            }

            await using (var scope = Scope(tB))
            {
                var r = await Pqrsd(scope, ia)
                    .RadicarAsync(id, new RadicarCorreoPqrsdRequest(), Ct);
                Assert.False(r.Ok);
                Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PropiaDbContext>().PqrsdExpedientes.CountAsync());
            }
            await using (var scope = Scope(tA))
                Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PropiaDbContext>().PqrsdExpedientes.CountAsync());
        }
        finally { await CleanupAsync(tA, remitente); await CleanupAsync(tB, remitente); }
    }

    // ----------------------------- Ola P2: respuesta por el buzon y reenvios -----------------------------

    [Fact]
    public async Task Radicado_se_responde_desde_el_buzon_en_el_hilo_sin_duplicar_la_confirmacion()
    {
        var t = await SeedTenantAsync("[CPQ] acuse");
        var remitente = $"vecino.{Guid.NewGuid():N}@ejemplo.com";
        try
        {
            var ia = new IaRuteada(coordinador: 0.95m, radicador: 0.93m);
            var sender = new FakeSender();
            var bz = await SeedBuzonAsync(t, 0.90m, conClave: true);
            var id = await SeedCorreoAsync(t, bz, remitente, "Ruido en la noche", CuerpoQueja, DateTimeOffset.UtcNow.AddDays(-1));

            await using (var scope = Scope(t))
                Assert.NotNull((await Entrante(scope, ia, sender).ReclasificarAsync(id, Ct))!.PqrsdRadicado);

            await using (var scope = Scope(t))
            {
                var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
                var c = await db.CorreosRecibidos.AsNoTracking().SingleAsync(x => x.Id == id);
                var exp = await db.PqrsdExpedientes.AsNoTracking().SingleAsync(x => x.Id == c.PqrsdExpedienteId);

                // Una respuesta, a quien escribio, en el hilo, con la clave del buzon y la plantilla resuelta.
                var env = Assert.Single(sender.Enviados);
                Assert.Equal(remitente, env.Para);
                Assert.Equal("Ruido en la noche", env.Asunto);
                Assert.Equal(c.MessageId, env.EnRespuestaA);
                Assert.Equal("clave-de-aplicacion", env.Pass);
                Assert.Contains(exp.NumeroRadicado, env.Html);
                Assert.Contains(exp.FechaVencimiento.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-CO")), env.Html);
                Assert.DoesNotContain("{radicado.", env.Html);

                Assert.NotNull(c.AcuseEnviadoAt);
                Assert.Equal(remitente, c.AcuseDestino);
                Assert.Null(c.AcuseError);
                // La plataforma no manda su confirmacion (ya la mando el buzon).
                Assert.DoesNotContain(Noti.EventosUsuario, e => e.PersonaId == exp.RadicadorPersonaId && e.Asunto.StartsWith("Radicado recibido"));
                // La plantilla predeterminada quedo sembrada en la copropiedad, editable.
                var pl = await db.PlantillasDocumento.AsNoTracking().SingleAsync(x => x.Nombre == RadicadorPqrsdCorreoAgente.NombrePlantillaAcuse);
                Assert.Equal("pqrsd", pl.Categoria);
            }

            // El residente responde en el hilo citando el radicado: aunque la IA este segura, NO se abre otro expediente.
            string numero;
            await using (var scope = Scope(t))
                numero = (await scope.ServiceProvider.GetRequiredService<PropiaDbContext>().PqrsdExpedientes.AsNoTracking().SingleAsync()).NumeroRadicado;
            var seguimiento = await SeedCorreoAsync(t, bz, remitente, "Re: Ruido en la noche",
                $"Gracias, pero anoche siguió la música hasta la 1 am.\n\nEl jue, 19 nov 2026, Administración escribió:\n> Quedó radicada con el número {numero}", DateTimeOffset.UtcNow);
            await using (var scope = Scope(t))
            {
                var r = await Entrante(scope, ia, sender).ReclasificarAsync(seguimiento, Ct);
                Assert.Null(r!.PqrsdRadicado);
                var det = await Entrante(scope, ia, sender).ObtenerAsync(seguimiento, Ct);
                Assert.Contains($"Menciona el radicado {numero}", det!.PropuestaPqrsd!.Pendiente);
                Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<PropiaDbContext>().PqrsdExpedientes.CountAsync());
                Assert.Single(sender.Enviados);
            }
        }
        finally { await CleanupAsync(t, remitente); }
    }

    [Fact]
    public async Task Si_el_buzon_no_puede_responder_sale_la_confirmacion_de_la_plataforma()
    {
        var t = await SeedTenantAsync("[CPQ] acuse falla");
        var remitente = $"vecino.{Guid.NewGuid():N}@ejemplo.com";
        try
        {
            var ia = new IaRuteada(coordinador: 0.95m, radicador: 0.93m);
            var sender = new FakeSender { Error = "SMTP caido" };
            var bz = await SeedBuzonAsync(t, 0.90m, conClave: true);
            var id = await SeedCorreoAsync(t, bz, remitente, "Ruido", CuerpoQueja, DateTimeOffset.UtcNow);

            await using (var scope = Scope(t))
                await Entrante(scope, ia, sender).ReclasificarAsync(id, Ct);

            await using (var scope = Scope(t))
            {
                var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
                var c = await db.CorreosRecibidos.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.NotNull(c.PqrsdExpedienteId);
                Assert.Null(c.AcuseEnviadoAt);
                Assert.Equal("SMTP caido", c.AcuseError);
                var exp = await db.PqrsdExpedientes.AsNoTracking().SingleAsync(x => x.Id == c.PqrsdExpedienteId);
                Assert.Contains(Noti.EventosUsuario, e => e.PersonaId == exp.RadicadorPersonaId && e.Asunto == $"Radicado recibido: {exp.NumeroRadicado}");
            }
        }
        finally { await CleanupAsync(t, remitente); }
    }

    [Fact]
    public async Task Buzon_que_no_responde_deja_la_confirmacion_de_la_plataforma()
    {
        var t = await SeedTenantAsync("[CPQ] sin acuse");
        var remitente = $"vecino.{Guid.NewGuid():N}@ejemplo.com";
        try
        {
            var ia = new IaRuteada(coordinador: 0.95m, radicador: 0.93m);
            var sender = new FakeSender();
            var bz = await SeedBuzonAsync(t, 0.90m, conClave: true, responder: false);
            var id = await SeedCorreoAsync(t, bz, remitente, "Ruido", CuerpoQueja, DateTimeOffset.UtcNow);

            await using (var scope = Scope(t))
                await Entrante(scope, ia, sender).ReclasificarAsync(id, Ct);

            Assert.Empty(sender.Enviados);
            await using (var scope = Scope(t))
            {
                var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
                var c = await db.CorreosRecibidos.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Null(c.AcuseEnviadoAt);
                Assert.Null(c.AcuseError);
                var exp = await db.PqrsdExpedientes.AsNoTracking().SingleAsync(x => x.Id == c.PqrsdExpedienteId);
                Assert.Contains(Noti.EventosUsuario, e => e.PersonaId == exp.RadicadorPersonaId && e.Asunto.StartsWith("Radicado recibido"));
            }
        }
        finally { await CleanupAsync(t, remitente); }
    }

    [Fact]
    public async Task Reenvio_se_radica_a_nombre_del_remitente_original_y_se_responde_a_quien_reenvio()
    {
        var t = await SeedTenantAsync("[CPQ] reenvio");
        var reenviador = $"facturacion.{Guid.NewGuid():N}@adm-ejemplo.com";
        var original = $"wramirez.{Guid.NewGuid():N}@gmail.com";
        var cuerpo =
            $"De: Administración - Facturación <{reenviador}> \n" +
            "Enviado el: miércoles, 28 de octubre de 2026 8:59 a. m.\nPara: expansion@adm-ejemplo.com\nAsunto: RV: Derecho de petición\n\n \n" +
            $"De: Wilson Ramirez <{original} <mailto:{original}> > \n" +
            "Enviado el: martes, 27 de octubre de 2026 2:07 p. m.\nPara: info@adm-ejemplo.com\nAsunto: Derecho de petición\n\n \n" +
            "Señores administración: solicito copia del contrato de administración y del último informe de gestión.\n\nWilson Ramirez";
        try
        {
            var ia = new IaRuteada(coordinador: 0.95m, radicador: 0.93m);
            var sender = new FakeSender();
            var bz = await SeedBuzonAsync(t, 0.90m, conClave: true);
            var id = await SeedCorreoAsync(t, bz, reenviador, "RV: Derecho de petición", cuerpo, DateTimeOffset.UtcNow);

            await using (var scope = Scope(t))
                Assert.NotNull((await Entrante(scope, ia, sender).ReclasificarAsync(id, Ct))!.PqrsdRadicado);

            await using (var scope = Scope(t))
            {
                var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
                var c = await db.CorreosRecibidos.AsNoTracking().SingleAsync(x => x.Id == id);
                var exp = await db.PqrsdExpedientes.AsNoTracking().SingleAsync(x => x.Id == c.PqrsdExpedienteId);
                // Solicitante = remitente original (persona externa nueva), no quien reenvio.
                var p = await db.Personas.AsNoTracking().SingleAsync(x => x.Id == exp.RadicadorPersonaId);
                Assert.Equal(original, p.Email);
                Assert.Equal("Wilson", p.Nombres);
                Assert.False(await db.Personas.AnyAsync(x => x.Email == reenviador));
                // Descripcion con el texto original, sin los encabezados del reenvio.
                Assert.StartsWith("[Reenviado por", exp.Descripcion);
                Assert.Contains("copia del contrato de administración", exp.Descripcion);
                Assert.DoesNotContain("Enviado el:", exp.Descripcion);
                // La respuesta va a quien escribio al buzon (quien reenvio), en su hilo.
                Assert.Equal(reenviador, Assert.Single(sender.Enviados).Para);
                var det = await Entrante(scope, ia).ObtenerAsync(id, Ct);
                Assert.Equal(original, det!.PropuestaPqrsd!.RemitenteOriginalCorreo);
                Assert.Equal("Wilson Ramirez", det.PropuestaPqrsd.RemitenteOriginalNombre);
            }
        }
        finally { await CleanupAsync(t, reenviador); await CleanupAsync(t, original); }
    }

    // ----------------------------- infraestructura -----------------------------

    private sealed class ScopeT : IAsyncDisposable
    {
        private readonly IServiceScope _s;
        public ScopeT(IServiceScope s) => _s = s;
        public IServiceProvider ServiceProvider => _s.ServiceProvider;
        public ValueTask DisposeAsync() { _s.Dispose(); return ValueTask.CompletedTask; }
    }

    private ScopeT Scope(Guid tenantId)
    {
        var s = _services.CreateScope();
        s.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        return new ScopeT(s);
    }

    /// <summary>Servicio de PQRSD por correo del scope con la IA falsa (sin registrar la IA en el contenedor).</summary>
    private static ICorreoPqrsdService Pqrsd(ScopeT scope, IAiInferenceService ia, IRespuestaCorreoSender? sender = null) => new CorreoPqrsdService(
        scope.ServiceProvider.GetRequiredService<PropiaDbContext>(), scope.ServiceProvider.GetRequiredService<ITenantContext>(),
        scope.ServiceProvider.GetRequiredService<Propia.Application.Pqrsd.IPqrsdService>(), new FakeAgentes(), ia, new FakeBlob(),
        NullLogger<CorreoPqrsdService>.Instance, new FakeSecret(), sender);

    private static ICorreoEntranteService Entrante(ScopeT scope, IAiInferenceService ia, IRespuestaCorreoSender? sender = null) => new CorreoEntranteService(
        scope.ServiceProvider.GetRequiredService<PropiaDbContext>(), scope.ServiceProvider.GetRequiredService<ITenantContext>(),
        new FakeSecret(), new FakeAgentes(), ia, new FakeBlob(), new FakeOpciones(), NullLogger<CorreoEntranteService>.Instance,
        Pqrsd(scope, ia, sender));

    /// <summary>Envio SMTP falso: guarda lo que se habria mandado; con Error, falla como un SMTP caido.</summary>
    private sealed class FakeSender : IRespuestaCorreoSender
    {
        public string? Error { get; init; }
        public List<(string Cuenta, string Pass, string Para, string Asunto, string Html, string? EnRespuestaA)> Enviados { get; } = new();
        public Task<string?> ResponderAsync(BuzonCorreo buzon, string password, string para, string asunto, string html,
            string? enRespuestaA, CancellationToken ct = default)
        {
            if (Error is null) Enviados.Add((buzon.Cuenta, password, para, asunto, html, enRespuestaA));
            return Task.FromResult(Error);
        }
    }

    private FakeNotificacionDispatcher Noti => (FakeNotificacionDispatcher)_services.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>();

    /// <summary>Responde como Coordinador o como Radicador segun el mensaje (el del radicador trae "TIPOS PERMITIDOS").</summary>
    private sealed class IaRuteada : IAiInferenceService
    {
        private readonly decimal _coord, _rad;
        public IaRuteada(decimal coordinador, decimal radicador) { _coord = coordinador; _rad = radicador; }
        public Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null,
            string? bearerToken = null, string? contactPhone = null, Guid? conversationId = null, CancellationToken ct = default)
        {
            var msg = turns.LastOrDefault()?.Text ?? "";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var json = msg.Contains("TIPOS PERMITIDOS")
                ? "{\"es_pqrsd\":true,\"tipo\":\"Queja\",\"categoria\":\"Convivencia\",\"unidad\":\"\",\"urgencia\":\"media\",\"confianza\":" + _rad.ToString(inv) + ",\"motivo\":\"Inconformidad por ruido\"}"
                : "{\"tema\":\"pqrsd\",\"confianza\":" + _coord.ToString(inv) + ",\"resumen\":\"Queja por ruido nocturno\",\"accion_sugerida\":\"Radicar\",\"datos\":{}}";
            return Task.FromResult(new AiChatResult(true, json, null));
        }
        public Task<string?> DescribeInboundMediaAsync(Guid agentId, string mimeType, string base64, string instruction, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeOpciones : IOpcionesEfectivas
    {
        public Task<IReadOnlyList<OpcionEfectivaDto>> OpcionesAsync(string lista, bool incluirOcultas = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OpcionEfectivaDto>>(CoordinadorCorreoAgente.TemasBase
                .Select((x, i) => new OpcionEfectivaDto(x.Clave, x.Label, i, x.Color, true, true, null)).ToList());
    }

    private sealed class FakeAgentes : IAgentesSistema
    {
        public Task<AgenteSistema?> AsegurarAsync(string clave, CancellationToken ct = default)
            => Task.FromResult<AgenteSistema?>(new AgenteSistema(Guid.NewGuid(), true));
    }

    private sealed class FakeSecret : ISecretProtector
    {
        public string Protect(string plaintext) => "enc:" + plaintext;
        public string Unprotect(string ciphertext) => ciphertext[4..];
    }

    private sealed class FakeBlob : IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct) => Task.FromResult("/mem/" + key);
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => "/mem/" + key;
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey is null ? null : "/mem/" + storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task<Guid> SeedPersonaAsync(string email)
    {
        await using var ctx = OwnerDb();
        var p = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"C{Guid.NewGuid():N}"[..18], Nombres = "Ana", Apellidos = "Residente", Email = email };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Guid> SeedBuzonAsync(Guid tenantId, decimal? umbral, bool conClave = false, bool responder = true)
    {
        await using var scope = Scope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var b = new BuzonCorreo
        {
            Nombre = "Administración",
            Cuenta = $"bz.{Guid.NewGuid():N}@ejemplo.com",
            UmbralAutoPqrsd = umbral,
            PasswordCifrada = conClave ? "enc:clave-de-aplicacion" : null,
            ResponderRadicado = responder,
        };
        db.BuzonesCorreo.Add(b);
        await db.SaveChangesAsync();
        return b.Id;
    }

    private async Task<Guid> SeedCorreoAsync(Guid tenantId, Guid buzonId, string remitente, string asunto, string cuerpo, DateTimeOffset fecha)
    {
        await using var scope = Scope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var c = new CorreoRecibido
        {
            BuzonId = buzonId,
            MessageId = $"<{Guid.NewGuid():N}@ejemplo.com>",
            ImapUid = 1,
            RemitenteCorreo = remitente,
            RemitenteNombre = "Juan Perez",
            Asunto = asunto,
            FechaCorreo = fecha,
            Cuerpo = cuerpo,
            AdjuntosJson = "[{\"nombre\":\"foto.jpg\",\"mime\":\"image/jpeg\",\"tamano\":2048,\"ruta\":\"tenants/x/correos/y/1-foto.jpg\"}]",
        };
        db.CorreosRecibidos.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    /// <summary>Borra todo lo de la copropiedad de prueba (todas las tablas con tenant_id, sin triggers ni FK: superusuario
    /// del contenedor) y las personas creadas con el correo del remitente.</summary>
    private async Task CleanupAsync(Guid tenantId, string remitente)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.ExecuteSqlRawAsync("SET session_replication_role = replica");
        var tablas = await ctx.Database.SqlQueryRaw<string>(
            "SELECT c.table_name AS \"Value\" FROM information_schema.columns c JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name WHERE c.table_schema = 'public' AND c.column_name = 'tenant_id' AND t.table_type = 'BASE TABLE'").ToListAsync();
        foreach (var t in tablas)
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM \"{t}\" WHERE tenant_id = '{tenantId}'");
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM personas WHERE email = {0}", remitente);
        await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM tenants WHERE id = '{tenantId}'");
        await ctx.Database.ExecuteSqlRawAsync("SET session_replication_role = DEFAULT");
    }
}
