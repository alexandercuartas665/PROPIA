using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.Asambleas;
using Propia.Application.Common;
using Propia.Application.Comunicaciones;
using Propia.Application.Documents;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;
using Propia.Infrastructure.Asambleas;
using Propia.Infrastructure.Documents;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Ola 3 del hand-off de Asambleas: acta legible. Borrador base con numero AAAA-NNN al cerrar, redaccion por IA (saneada,
/// sin bloque de firmas de la IA), edicion a mano, doble firma (cuenta del secretario + codigo al correo del presidente)
/// con hash SHA-256 e inmutabilidad, y publicacion (PDF en Documentos + comunicado en borrador). IA, PDF, storage,
/// correo y Comunicaciones son dobles de prueba.
/// </summary>
public partial class AsambleaFlowTests
{
    private sealed class IaFalsa : IAiInferenceService
    {
        public AiChatResult Respuesta { get; set; } = new(true, "<p>x</p>", null);
        public List<string> Prompts { get; } = new();
        public Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null, string? bearerToken = null,
            string? contactPhone = null, Guid? conversationId = null, CancellationToken ct = default)
        { Prompts.Add(turns[0].Text); return Task.FromResult(Respuesta); }
        public Task<string?> DescribeInboundMediaAsync(Guid agentId, string mimeType, string base64, string instruction, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }
    private sealed class AgentesFalsos : IAgentesSistema
    {
        public bool Activo { get; set; } = true;
        public Task<AgenteSistema?> AsegurarAsync(string clave, CancellationToken ct = default)
            => Task.FromResult<AgenteSistema?>(new AgenteSistema(Guid.NewGuid(), Activo));
    }
    private sealed class CorreoActa : IEmailSender
    {
        public List<(string To, string Html)> Enviados { get; } = new();
        public Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        { Enviados.Add((toEmail, htmlBody)); return Task.FromResult(new EmailSendResult(true, null)); }
    }
    private sealed class PdfFalso : IHtmlToPdfService
    {
        public Task<byte[]> RenderAsync(string html, CancellationToken ct) => Task.FromResult(Encoding.UTF8.GetBytes("%PDF-1.4 acta"));
    }
    private sealed class StorageFalso : IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct) => Task.FromResult("/uploads/" + key);
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => "/uploads/" + key;
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
    /// <summary>Comunicaciones falso (DispatchProxy): registra las llamadas; CrearBorrador devuelve un Id.</summary>
    public class ComunicacionesFalsas : DispatchProxy
    {
        public List<string> Llamadas { get; } = new();
        public Guid IdCreado { get; } = Guid.NewGuid();
        protected override object? Invoke(MethodInfo? m, object?[]? args)
        {
            Llamadas.Add(m!.Name);
            if (m.ReturnType == typeof(Task)) return Task.CompletedTask;
            var t = m.ReturnType.GetGenericArguments()[0];
            object? valor = null;
            if (t == typeof(ComunicadoDetalleDto))
            {
                valor = RuntimeHelpers.GetUninitializedObject(t);
                t.GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(valor, IdCreado);
            }
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(t).Invoke(null, new[] { valor });
        }
    }

    private (IAsambleaActaService Acta, IaFalsa Ia, AgentesFalsos Agentes, CorreoActa Correo, ComunicacionesFalsas Com) BuildActa(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var ia = new IaFalsa(); var agentes = new AgentesFalsos(); var correo = new CorreoActa();
        var com = DispatchProxy.Create<IComunicacionesService, ComunicacionesFalsas>();
        var acta = new AsambleaActaService(db, ctx, http, agentes, ia, correo, new MemoryCache(new MemoryCacheOptions()),
            new HostingEnvironment { EnvironmentName = "Production" }, new MembreteDocumentBuilder(), new PdfFalso(), new StorageFalso(),
            com, scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AsambleaActaService>.Instance);
        return (acta, ia, agentes, correo, (ComunicacionesFalsas)(object)com);
    }

    /// <summary>Sesion cerrada con quorum: presidente = propietario de la unidad 1 (con correo), secretario = la administracion.</summary>
    private async Task<(IAsambleaService Svc, Guid SesionId, Guid ActaId, string EmailPresidente)> SesionCerradaConActaAsync(Guid tenantId)
    {
        var u = await SeedUnidadesAsync(tenantId, 2, coeficientes: new[] { 0.5m, 0.5m });
        var presidente = await SeedPersonaEnUnidadAsync(tenantId, u[0], RolUnidadPersona.Propietario);
        await SeedPersonaEnUnidadAsync(tenantId, u[1], RolUnidadPersona.Propietario);
        var (svc, sid) = await SesionEnCursoAsync(tenantId, TipoSesion.AsambleaOrdinaria, PuntoReq(1, "Presupuesto"), PuntoReq(2, "Varios", vota: false));
        foreach (var x in u) await svc.CheckInParticipanteAsync(sid, new CheckInParticipanteRequest(x, true), Ct);
        await svc.InstalarAsync(sid, new InstalarSesionRequest(u[0], null), Ct);
        var p = (await svc.GetSesionAsync(sid, Ct))!.Puntos[0];
        var v = await svc.AbrirVotacionAsync(sid, new AbrirVotacionRequest(p.Id), Ct);
        await svc.VotarBloqueAsync(v.Id, new VotarBloqueRequest(u.Select(x => new VotoBloqueItem(x, "Si")).ToList()), Ct);
        await svc.CerrarVotacionAsync(v.Id, new CerrarVotacionRequest(), Ct);
        await svc.CerrarSesionAsync(sid, new CerrarSesionRequest(), Ct);
        var actaId = (await svc.GetSesionAsync(sid, Ct))!.Acta!.Id;
        await using var o = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());
        var email = await o.Personas.AsNoTracking().Where(x => x.Id == presidente).Select(x => x.Email!).SingleAsync();
        return (svc, sid, actaId, email);
    }

    [Fact]
    public async Task Acta_por_ia_edicion_doble_firma_inmutable_y_publicacion()
    {
        var tenantId = await SeedTenantAsync("Asam Acta IA");
        await SeedPersonaConApplicationUser(tenantId);
        var (svc, sid, actaId, emailPres) = await SesionCerradaConActaAsync(tenantId);
        var (acta, ia, _, correo, com) = BuildActa(tenantId);

        // Al cerrar: numero AAAA-NNN y borrador base con los datos del sistema + firmas en blanco.
        Assert.Equal(actaId, await acta.PrepararAsync(sid, Ct));
        var d = (await svc.GetSesionAsync(sid, Ct))!.Acta!;
        Assert.Matches(@"^\d{4}-001$", d.Numero!);
        Assert.Contains("ACTA No. " + d.Numero, d.BorradorHtml);
        Assert.Contains("APROBADO", d.BorradorHtml);
        Assert.Contains("asm-acta-firmas", d.BorradorHtml);
        Assert.Equal(EstadoRedaccionActa.Redactando, d.EstadoRedaccion);
        Assert.NotNull(d.PlazoPublicacion);

        // Redaccion por IA: se sanea, se descarta un bloque de firmas inventado y el sistema pone el suyo.
        ia.Respuesta = new AiChatResult(true, "```html\n<div class=\"asm-acta-enc\"><div class=\"asm-acta-num\">ACTA No. X</div></div><h4>Desarrollo</h4>"
            + "<p>La asamblea aprobó el presupuesto con el voto de todas las unidades presentes.</p><script>alert(1)</script>"
            + "<div class=\"asm-acta-firmas\">firmas falsas</div>\n```", null);
        await acta.RedactarAsync(actaId, Ct);
        d = (await svc.GetSesionAsync(sid, Ct))!.Acta!;
        Assert.Equal(EstadoRedaccionActa.Lista, d.EstadoRedaccion);
        Assert.True(d.RedactadaPorIa);
        Assert.Contains("aprobó el presupuesto", d.BorradorHtml);
        Assert.DoesNotContain("<script", d.BorradorHtml);
        Assert.DoesNotContain("firmas falsas", d.BorradorHtml);
        Assert.Contains("Presidente de la asamblea", d.BorradorHtml);
        Assert.Contains("\"UnidadesPresentes\": 2", ia.Prompts.Single());
        Assert.Contains("Verificación del quórum", ia.Prompts.Single());   // plantilla "Acta de asamblea" creada en la copropiedad

        // Edicion a mano (antes de firmar).
        Assert.True(await acta.GuardarBorradorAsync(actaId, new GuardarBorradorActaRequest("<h4>Desarrollo</h4><p>Texto corregido a mano por la administración del conjunto.</p>"), Ct));
        d = (await svc.GetSesionAsync(sid, Ct))!.Acta!;
        Assert.True(d.Editada);
        Assert.Contains("corregido a mano", d.BorradorHtml);

        // Presidente: no puede firmar "desde la cuenta" de la administracion; firma con el codigo de su correo.
        await Assert.ThrowsAsync<InvalidOperationException>(() => acta.FirmarAsync(actaId, new FirmarActaRolRequest(RolFirmaActa.Presidente, null), Ct));
        var cod = await acta.SolicitarCodigoAsync(actaId, new SolicitarCodigoFirmaRequest(RolFirmaActa.Presidente), Ct);
        Assert.Null(cod.CodigoDev);   // fuera de Development no se devuelve el codigo
        var (to, html) = correo.Enviados.Single();
        Assert.Equal(emailPres, to);
        var codigo = Regex.Match(html, @">(\d{6})<").Groups[1].Value;
        await Assert.ThrowsAsync<InvalidOperationException>(() => acta.FirmarAsync(actaId, new FirmarActaRolRequest(RolFirmaActa.Presidente, codigo == "000000" ? "111111" : "000000"), Ct));
        Assert.True(await acta.FirmarAsync(actaId, new FirmarActaRolRequest(RolFirmaActa.Presidente, codigo), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => acta.FirmarAsync(actaId, new FirmarActaRolRequest(RolFirmaActa.Presidente, codigo), Ct));

        // Con una firma el texto ya no cambia.
        await Assert.ThrowsAsync<InvalidOperationException>(() => acta.GuardarBorradorAsync(actaId, new GuardarBorradorActaRequest("<p>Intento de cambio despues de la primera firma del acta.</p>"), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => acta.RegenerarAsync(actaId, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => acta.PublicarAsync(actaId, null, Ct));

        // Secretario = la administracion: firma desde su cuenta -> acta inmutable con hash.
        Assert.True(await acta.FirmarAsync(actaId, new FirmarActaRolRequest(RolFirmaActa.Secretario, null), Ct));
        d = (await svc.GetSesionAsync(sid, Ct))!.Acta!;
        Assert.Equal(EstadoActa.Firmada, d.Estado);
        Assert.Equal(MetodoFirmaActa.Codigo, d.FirmaPresidente!.Metodo);
        Assert.Equal(MetodoFirmaActa.Cuenta, d.FirmaSecretario!.Metodo);
        Assert.Equal(2, Regex.Matches(d.BorradorHtml!, "Firmado digitalmente").Count);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{d.BorradorHtml}|{d.ContenidoGenerado}"))).ToLowerInvariant(), d.HashDocumento);
        await Assert.ThrowsAsync<InvalidOperationException>(() => acta.FirmarAsync(actaId, new FirmarActaRolRequest(RolFirmaActa.Secretario, null), Ct));

        // Publicar: PDF en Documentos (tipo Acta) + comunicado en borrador.
        var pub = await acta.PublicarAsync(actaId, "https://app.test", Ct);
        Assert.Null(pub.Aviso);
        Assert.Equal(com.IdCreado, pub.ComunicadoId);
        Assert.Contains("CrearBorradorAsync", com.Llamadas);
        Assert.DoesNotContain("EnviarAsync", com.Llamadas);
        var fin = (await svc.GetSesionAsync(sid, Ct))!;
        Assert.NotNull(fin.Acta!.PublicadaEn);
        Assert.Contains(fin.Documentos, x => x.Tipo == TipoDocumentoSesion.Acta && x.UrlStorage == pub.DocumentoUrl);
        await Assert.ThrowsAsync<InvalidOperationException>(() => acta.PublicarAsync(actaId, null, Ct));

        await LimpiarActaAsync(tenantId);
    }

    [Fact]
    public async Task Acta_sin_ia_disponible_queda_con_el_borrador_base_y_el_error()
    {
        var tenantId = await SeedTenantAsync("Asam Acta Sin IA");
        await SeedPersonaConApplicationUser(tenantId);
        var (svc, sid, actaId, _) = await SesionCerradaConActaAsync(tenantId);
        var (acta, ia, agentes, _, _) = BuildActa(tenantId);
        await acta.PrepararAsync(sid, Ct);
        var baseHtml = (await svc.GetSesionAsync(sid, Ct))!.Acta!.BorradorHtml;

        ia.Respuesta = new AiChatResult(false, null, "Cuota agotada");
        await acta.RedactarAsync(actaId, Ct);
        var d = (await svc.GetSesionAsync(sid, Ct))!.Acta!;
        Assert.Equal(EstadoRedaccionActa.Error, d.EstadoRedaccion);
        Assert.Equal("Cuota agotada", d.ErrorRedaccion);
        Assert.Equal(baseHtml, d.BorradorHtml);
        Assert.False(d.RedactadaPorIa);

        agentes.Activo = false;
        Assert.True(await acta.RegenerarAsync(actaId, Ct));
        await acta.RedactarAsync(actaId, Ct);
        d = (await svc.GetSesionAsync(sid, Ct))!.Acta!;
        Assert.Equal(EstadoRedaccionActa.Error, d.EstadoRedaccion);
        Assert.Contains("Redactor de actas", d.ErrorRedaccion);

        await LimpiarActaAsync(tenantId);
    }

    private async Task LimpiarActaAsync(Guid tenantId)
    {
        await using (var ctx = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext()))
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM plantillas_documento WHERE tenant_id = {tenantId}");
        await LimpiarPersonasAsync(tenantId);
        await CleanTenant(tenantId);
    }
}
