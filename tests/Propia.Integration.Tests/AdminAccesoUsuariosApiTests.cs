using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propia.Application.Auth;
using Propia.Application.Common;
using Propia.Application.Directorio;
using Propia.Application.EquipoOrg;
using Propia.Application.SuperAdmin;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// API Super Admin para dejar usuarios LISTOS sin la UI (token pak_ real): envio de invitaciones, cuenta con
/// clave temporal generada y entregada por el servidor (nunca en la respuesta), alta idempotente con
/// advertencias, correccion de datos de la persona (incluido el usuario de Identity), busqueda por documento,
/// equipo de la organizacion y admin de organizacion con persona existente.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AdminAccesoUsuariosApiTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private Factory _factory = null!;
    private readonly List<Guid> _tenants = new();
    private readonly List<Guid> _orgs = new();
    private readonly List<string> _emails = new();
    private readonly List<string> _docs = new();
    private Guid _tokenId;
    private HttpClient _api = null!;

    public AdminAccesoUsuariosApiTests(PostgresFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        _factory = new Factory(_fx.OwnerConnectionString);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
            Assert.Equal(new Npgsql.NpgsqlConnectionStringBuilder(_fx.OwnerConnectionString).Port,
                new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Port);
            var founder = await db.SuperAdminUsuarios.FirstAsync(u => u.Email == "founder@adgroup.com.co");
            if (founder.MfaConfigurado || !founder.Activo)
            {
                founder.MfaConfigurado = false; founder.MfaSecret = null; founder.Activo = true;
                await db.SaveChangesAsync();
            }
        }
        var sesion = _factory.CreateClient();
        var login = await (await sesion.PostAsJsonAsync("/admin/login", new SuperAdminLoginRequest("founder@adgroup.com.co", "PropiaFounder2026!")))
            .Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        sesion.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        var creado = await (await sesion.PostAsJsonAsync("/api/admin/api-tokens", new CrearApiTokenRequest("[TEST] acceso usuarios", 1)))
            .Content.ReadFromJsonAsync<ApiTokenCreadoDto>();
        _tokenId = creado!.Info.Id;
        _api = _factory.CreateClient();
        _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creado.Token);
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = OwnerDb())
        {
            await ctx.Database.OpenConnectionAsync();
            await ctx.Database.ExecuteSqlRawAsync("SET session_replication_role = replica");
            var tablas = await ctx.Database.SqlQueryRaw<string>(
                "SELECT c.table_name AS \"Value\" FROM information_schema.columns c JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name WHERE c.table_schema = 'public' AND c.column_name = 'tenant_id' AND t.table_type = 'BASE TABLE'").ToListAsync();
            foreach (var tid in _tenants)
            {
                foreach (var t in tablas)
                    await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM \"{t}\" WHERE tenant_id = '{tid}'");
                await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM tenants WHERE id = '{tid}'");
            }
            foreach (var o in _orgs)
            {
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM org_colaborador_historial WHERE colaborador_id IN (SELECT id FROM org_colaboradores WHERE organizacion_id = {0})", o);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM org_colaborador_copropiedades WHERE colaborador_id IN (SELECT id FROM org_colaboradores WHERE organizacion_id = {0})", o);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM org_colaboradores WHERE organizacion_id = {0}", o);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM org_cargos WHERE organizacion_id = {0}", o);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM organizaciones WHERE id = {0}", o);
            }
            foreach (var e in _emails)
            {
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM asp_net_user_claims WHERE user_id IN (SELECT id FROM asp_net_users WHERE lower(email) = lower({0}))", e);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM personas WHERE id IN (SELECT persona_id FROM asp_net_users WHERE lower(email) = lower({0}))", e);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM asp_net_users WHERE lower(email) = lower({0})", e);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM personas WHERE lower(email) = lower({0})", e);
            }
            foreach (var d in _docs)
            {
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM asp_net_user_claims WHERE user_id IN (SELECT u.id FROM asp_net_users u JOIN personas p ON p.id = u.persona_id WHERE p.documento = {0})", d);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM asp_net_users WHERE persona_id IN (SELECT id FROM personas WHERE documento = {0})", d);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM personas WHERE documento = {0}", d);
            }
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM super_admin_api_tokens WHERE id = {0}", _tokenId);
            await ctx.Database.ExecuteSqlRawAsync("SET session_replication_role = DEFAULT");
        }
        await _factory.DisposeAsync();
    }

    // ===================== 1. Invitaciones que SI se envian =====================

    [Fact]
    public async Task Invitacion_y_reenvio_se_envian_por_correo_y_el_reenvio_devuelve_200()
    {
        var tA = await SeedTenantAsync("[TEST] acceso invitacion");
        var email = Email("invita");
        var antes = _factory.Correos.Count;
        var c = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, Nueva("Invita", email), null, null, true, CanalEnvioInvitacion.Ambos));

        Assert.Equal(EstadoEnvioCanal.Enviado, c.Invitacion!.Envio!.Correo);
        // Sin plantilla propia_invitacion aprobada el WhatsApp se omite (no falla la invitacion).
        Assert.Equal(EstadoEnvioCanal.Omitido, c.Invitacion.Envio.Whatsapp);
        Assert.Contains(PlantillasAcceso.Invitacion, c.Invitacion.Envio.DetalleWhatsapp);
        var correo = _factory.Correos.Skip(antes).Single(x => x.Para == email);
        Assert.Contains("https://app.test/invitacion/", correo.Html);
        Assert.Contains("72 horas", correo.Html);

        var r = await _api.PutAsync($"/admin/tenants/{tA}/invitaciones/{c.Invitacion.Id}/reenviar", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var re = (await r.Content.ReadFromJsonAsync<AdminInvitacionCreadaDto>())!;
        Assert.Equal(EstadoEnvioCanal.Enviado, re.Envio!.Correo);
        Assert.NotEqual(c.Invitacion.LinkAceptacion, re.LinkAceptacion);
        Assert.Equal(2, _factory.Correos.Skip(antes).Count(x => x.Para == email));
    }

    // ===================== 2. Cuenta lista con clave temporal =====================

    [Fact]
    public async Task Activar_con_clave_temporal_crea_la_cuenta_entrega_la_clave_y_obliga_a_cambiarla()
    {
        var tA = await SeedTenantAsync("[TEST] acceso clave temporal");
        var email = Email("clave");
        var c = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, Nueva("Clave", email), null, null, false));
        Assert.Equal(EstadoUsuarioTenant.Pendiente, c.Estado);

        var antes = _factory.Correos.Count;
        var r = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios/{c.UsuarioTenantId}/activar-con-clave-temporal",
            new AdminActivarClaveTemporalRequest(CanalEnvioInvitacion.Ambos));
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.OK, cuerpo);
        var acceso = System.Text.Json.JsonSerializer.Deserialize<AccesoEntregadoDto>(cuerpo, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal("temporal", acceso.Modo);
        Assert.True(acceso.CuentaCreada);
        Assert.Equal(new[] { "correo" }, acceso.EntregadoPor);   // WhatsApp sin plantilla: omitido, no fallido
        Assert.Empty(acceso.Fallidos);
        Assert.StartsWith(email[..2] + "***@", acceso.Destino);

        // La clave solo esta en el correo del destinatario; NUNCA en la respuesta.
        var correo = _factory.Correos.Skip(antes).Single(x => x.Para == email);
        Assert.Contains("Tu acceso a PROPIA", correo.Asunto);
        var clave = Regex.Match(correo.Html, "monospace;font-size:15px\"><strong>([^<]+)</strong>").Groups[1].Value;
        Assert.True(clave.Length >= 10, "no se encontro la clave en el correo");
        Assert.DoesNotContain(clave, cuerpo);
        Assert.Contains("https://app.test/login", correo.Html);

        // Quedo activo, con cuenta ligada a la persona y la marca de clave temporal.
        await using (var db = await TenantDbAsync(tA))
            Assert.Equal(EstadoUsuarioTenant.Activo, (await db.UsuariosTenant.SingleAsync(u => u.Id == c.UsuarioTenantId)).Estado);
        await using (var db = OwnerDb())
        {
            var user = await db.Users.SingleAsync(u => u.PersonaId == c.PersonaId);
            Assert.True(await db.UserClaims.AnyAsync(x => x.UserId == user.Id && x.ClaimType == ClaveTemporal.ClaimTipo));
        }

        // Entra con la temporal y el middleware lo bloquea hasta que la cambie.
        var anon = _factory.CreateClient();
        var login = (await (await anon.PostAsJsonAsync("/connect/login", new LoginRequest(email, clave))).Content.ReadFromJsonAsync<UnifiedLoginResponse>())!;
        Assert.True(login.DebeCambiarClave);
        var tmp = _factory.CreateClient();
        tmp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken!);
        var bloqueado = await tmp.GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
        Assert.Contains(ClaveTemporal.ErrorDebeCambiar, await bloqueado.Content.ReadAsStringAsync());

        // Repetirlo regenera la clave (cuenta existente, solo en esta copropiedad -> temporal otra vez).
        var otra = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios/{c.UsuarioTenantId}/activar-con-clave-temporal",
            new AdminActivarClaveTemporalRequest());
        Assert.Equal(HttpStatusCode.OK, otra.StatusCode);
        Assert.False((await otra.Content.ReadFromJsonAsync<AccesoEntregadoDto>())!.CuentaCreada);
    }

    [Fact]
    public async Task Cuenta_que_tambien_esta_en_otra_copropiedad_recibe_solo_enlace()
    {
        var tA = await SeedTenantAsync("[TEST] acceso multi A");
        var tB = await SeedTenantAsync("[TEST] acceso multi B");
        var email = Email("multi");
        var datos = Nueva("Multi", email);
        var enA = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, datos, null, null, false, CanalEnvioInvitacion.Email, AdminModoAcceso.ClaveTemporal));
        Assert.Equal(EstadoUsuarioTenant.Activo, enA.Estado);
        Assert.Equal("temporal", enA.AccesoEntregado!.Modo);

        // En B la misma persona (por documento): ya tiene cuenta y acceso en A -> no se le fija clave.
        var enB = await CrearAsync(tB, new AdminCrearUsuarioRequest(null, datos, null, null, false));
        var antes = _factory.Correos.Count;
        var r = await _api.PostAsJsonAsync($"/admin/tenants/{tB}/usuarios/{enB.UsuarioTenantId}/activar-con-clave-temporal", new AdminActivarClaveTemporalRequest());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var acceso = (await r.Content.ReadFromJsonAsync<AccesoEntregadoDto>())!;
        Assert.Equal("enlace", acceso.Modo);
        Assert.Contains(_factory.Correos.Skip(antes), x => x.Para == email && x.Html.Contains("/restablecer-contrasena?"));
        // Su usuario en B quedo activo (la cuenta existente ya le sirve).
        await using var db = await TenantDbAsync(tB);
        Assert.Equal(EstadoUsuarioTenant.Activo, (await db.UsuariosTenant.SingleAsync(u => u.Id == enB.UsuarioTenantId)).Estado);
    }

    [Fact]
    public async Task Sin_correo_o_sin_canal_entregable_es_400_y_no_crea_nada()
    {
        var tA = await SeedTenantAsync("[TEST] acceso sin canal");
        var sinCorreo = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, Nueva("SinCorreo", null), null, null, false));
        var r = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios/{sinCorreo.UsuarioTenantId}/activar-con-clave-temporal", new AdminActivarClaveTemporalRequest());
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("correo", await r.Content.ReadAsStringAsync());

        // Solo WhatsApp y la copropiedad no tiene la plantilla propia_acceso: 400, sin cuenta.
        var email = Email("solowa");
        var c = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, Nueva("SoloWa", email), null, null, false));
        var wa = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios/{c.UsuarioTenantId}/activar-con-clave-temporal",
            new AdminActivarClaveTemporalRequest(CanalEnvioInvitacion.Whatsapp));
        Assert.Equal(HttpStatusCode.BadRequest, wa.StatusCode);
        Assert.Contains(PlantillasAcceso.Acceso, await wa.Content.ReadAsStringAsync());

        // Alta en un paso con clave temporal y sin correo: 400 y la persona NO se crea.
        var doc = Doc();
        var alta = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios", new AdminCrearUsuarioRequest(
            null, new AdminPersonaNuevaRequest("Nadie", "Sin Correo", TipoDocumento.CC, doc, null, "3001234567"), null, null, false,
            CanalEnvioInvitacion.Email, AdminModoAcceso.ClaveTemporal));
        Assert.Equal(HttpStatusCode.BadRequest, alta.StatusCode);
        await using var db = OwnerDb();
        Assert.False(await db.Personas.AnyAsync(p => p.Documento == doc));
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
        // Modo de acceso desconocido: 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios",
            new AdminCrearUsuarioRequest(null, Nueva("X", Email("x")), null, null, false, CanalEnvioInvitacion.Email, "magia"))).StatusCode);
    }

    // ===================== 4-5. Alta repetida, advertencias, datos de contacto, busqueda =====================

    [Fact]
    public async Task Alta_repetida_da_200_ya_existia_y_avisa_datos_distintos_sin_sobrescribir()
    {
        var tA = await SeedTenantAsync("[TEST] acceso alta repetida");
        var email = Email("repite");
        var datos = Nueva("Repite", email);
        var primero = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, datos, null, null, false));

        var otroCorreo = Email("otro");
        var r = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios",
            new AdminCrearUsuarioRequest(null, datos with { Email = otroCorreo, Telefono = "3109998877" }, null, null, false));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var segundo = (await r.Content.ReadFromJsonAsync<AdminCrearUsuarioResponse>())!;
        Assert.True(segundo.YaExistia);
        Assert.Equal(primero.UsuarioTenantId, segundo.UsuarioTenantId);
        Assert.Contains(segundo.Advertencias!, a => a.Contains("correo enviado difiere"));
        Assert.Contains(segundo.Advertencias!, a => a.Contains("teléfono enviado difiere"));
        await using (var db = OwnerDb())
            Assert.Equal(email, (await db.Personas.SingleAsync(p => p.Id == primero.PersonaId)).Email);

        // Busqueda por documento, tambien escrito con puntos.
        var conPuntos = string.Join(".", Enumerable.Range(0, (datos.Documento!.Length + 2) / 3).Select(i => datos.Documento.Substring(i * 3, Math.Min(3, datos.Documento.Length - i * 3))));
        foreach (var q in new[] { datos.Documento, conPuntos })
        {
            var personas = await _api.GetFromJsonAsync<List<PersonaResumenDto>>($"/admin/tenants/{tA}/personas?q={Uri.EscapeDataString(q)}");
            Assert.Contains(personas!, p => p.Id == primero.PersonaId);
        }
    }

    [Fact]
    public async Task Corregir_persona_cambia_tambien_el_usuario_de_ingreso_y_da_409_si_el_correo_es_de_otra()
    {
        var tA = await SeedTenantAsync("[TEST] acceso corregir persona");
        var tB = await SeedTenantAsync("[TEST] acceso corregir persona B");
        var email = Email("viejo");
        var c = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, Nueva("Corrige", email), null, null, false, CanalEnvioInvitacion.Email, AdminModoAcceso.ClaveTemporal));
        var ajena = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, Nueva("Ajena", Email("ajena")), null, null, false));
        var ajenaEmail = (await _api.GetFromJsonAsync<UsuarioDetalleDto>($"/admin/tenants/{tA}/usuarios/{ajena.UsuarioTenantId}"))!.Email!;

        var nuevo = Email("nuevo");
        var r = await _api.PutAsJsonAsync($"/admin/tenants/{tA}/personas/{c.PersonaId}",
            new AdminActualizarPersonaRequest(null, null, nuevo, "3157654321"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var dto = (await r.Content.ReadFromJsonAsync<AdminPersonaActualizadaDto>())!;
        Assert.True(dto.CuentaActualizada);
        Assert.Equal(nuevo, dto.Email);
        Assert.Equal("Prueba API", dto.Apellidos);   // MERGE: lo no enviado no cambia
        await using (var db = OwnerDb())
        {
            var user = await db.Users.SingleAsync(u => u.PersonaId == c.PersonaId);
            Assert.Equal(nuevo, user.Email);
            Assert.Equal(nuevo, user.UserName);
            Assert.True(await db.SuperAdminLogs.AnyAsync(l => l.Accion == "TENANT_PERSONA_ACTUALIZAR" && l.EntidadAfectada == $"Tenant:{tA}/{c.PersonaId}"));
        }

        // Correo que ya es de otra persona -> 409; persona de otra copropiedad -> 404.
        Assert.Equal(HttpStatusCode.Conflict, (await _api.PutAsJsonAsync($"/admin/tenants/{tA}/personas/{c.PersonaId}",
            new AdminActualizarPersonaRequest(null, null, ajenaEmail, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PutAsJsonAsync($"/admin/tenants/{tB}/personas/{c.PersonaId}",
            new AdminActualizarPersonaRequest("X", null, null, null))).StatusCode);
    }

    // ===================== 3. Equipo de la organizacion =====================

    [Fact]
    public async Task Equipo_de_la_organizacion_por_api_agrega_asigna_y_es_idempotente()
    {
        var org = await SeedOrgAsync("[TEST] Org API equipo");
        var otraOrg = await SeedOrgAsync("[TEST] Org API equipo otra");
        var tA = await SeedTenantAsync("[TEST] acceso org copropiedad", org);
        var tAjena = await SeedTenantAsync("[TEST] acceso org ajena", otraOrg);

        var cargos = await _api.GetFromJsonAsync<List<CargoDto>>($"/api/admin/organizaciones/{org}/cargos");
        Assert.NotEmpty(cargos!);
        var cargo = cargos![0];
        var admin = (await _api.GetFromJsonAsync<List<AdminRolTenantDto>>($"/admin/tenants/{tA}/roles"))!.Single(r => r.Nombre == "Administrador");
        var email = Email("equipo");
        var persona = Nueva("Equipo", email);

        // Copropiedad de otra organizacion -> 404 y no se crea nada.
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PostAsJsonAsync($"/api/admin/organizaciones/{org}/equipo",
            new AdminAgregarColaboradorRequest(null, null, null, persona, cargo.Id, new[] { new AdminAsignacionColaboradorRequest(tAjena, admin.Id) }))).StatusCode);

        var r = await _api.PostAsJsonAsync($"/api/admin/organizaciones/{org}/equipo",
            new AdminAgregarColaboradorRequest(null, null, null, persona, cargo.Id, new[] { new AdminAsignacionColaboradorRequest(tA, admin.Id) }));
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var res = (await r.Content.ReadFromJsonAsync<AdminColaboradorResultadoDto>())!;
        Assert.False(res.YaExistia);
        var asig = Assert.Single(res.Asignaciones);
        // Sin cuenta: el acceso queda Pendiente en la copropiedad (para clave temporal o invitacion).
        Assert.Equal(EstadoUsuarioTenant.Pendiente, asig.Estado);
        Assert.True(asig.UsuarioCreado);
        await using (var db = await TenantDbAsync(tA))
        {
            var ut = await db.UsuariosTenant.SingleAsync(u => u.Id == asig.UsuarioTenantId);
            Assert.Equal(res.Colaborador.PersonaId, ut.PersonaId);
            Assert.True(await db.UsuarioTenantRoles.AnyAsync(x => x.UsuarioTenantId == ut.Id && x.RolId == admin.Id));
        }

        // Repetido (por documento): 200 + yaExistia, sin duplicar.
        var rep = await _api.PostAsJsonAsync($"/api/admin/organizaciones/{org}/equipo",
            new AdminAgregarColaboradorRequest(null, persona.TipoDocumento, persona.Documento, null, cargo.Id, new[] { new AdminAsignacionColaboradorRequest(tA, admin.Id) }));
        Assert.Equal(HttpStatusCode.OK, rep.StatusCode);
        var repRes = (await rep.Content.ReadFromJsonAsync<AdminColaboradorResultadoDto>())!;
        Assert.True(repRes.YaExistia);
        Assert.Equal(res.Colaborador.Id, repRes.Colaborador.Id);

        // Con la cuenta lista (clave temporal en la copropiedad) el colaborador queda con acceso real.
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios/{asig.UsuarioTenantId}/activar-con-clave-temporal",
            new AdminActivarClaveTemporalRequest())).StatusCode);

        var colab = res.Colaborador.Id;
        var otroCargo = cargos.First(x => x.Id != cargo.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.PutAsJsonAsync($"/api/admin/organizaciones/{org}/equipo/{colab}/cargo", new AdminCambiarCargoRequest(otroCargo.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.PutAsJsonAsync($"/api/admin/organizaciones/{org}/equipo/{colab}/desactivar", new AdminDesactivarColaboradorRequest("prueba"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.PutAsync($"/api/admin/organizaciones/{org}/equipo/{colab}/reactivar", null)).StatusCode);
        // Desde otra organizacion el colaborador no existe.
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PutAsync($"/api/admin/organizaciones/{otraOrg}/equipo/{colab}/reactivar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/api/admin/organizaciones/{Guid.NewGuid()}/equipo")).StatusCode);

        var equipo = await _api.GetFromJsonAsync<List<ColaboradorListaDto>>($"/api/admin/organizaciones/{org}/equipo");
        var fila = Assert.Single(equipo!, x => x.Id == colab);
        Assert.Equal(otroCargo.Id, fila.CargoId);
        await using (var db = OwnerDb())
            Assert.True(await db.SuperAdminLogs.AnyAsync(l => l.Accion == "ORG_EQUIPO_AGREGAR" && l.EntidadAfectada == $"Organizacion:{org}/{colab}"));
    }

    [Fact]
    public async Task Admin_de_organizacion_con_persona_existente_y_sin_clave_recibe_la_temporal_por_correo()
    {
        var org = await SeedOrgAsync("[TEST] Org API admin");
        var tA = await SeedTenantAsync("[TEST] acceso org admin", org);
        var email = Email("orgadmin");
        var datos = Nueva("OrgAdmin", email);
        // La persona ya existe (alta como usuario pendiente en la copropiedad, sin cuenta).
        var previo = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, datos, null, null, false));

        var antes = _factory.Correos.Count;
        var r = await _api.PostAsJsonAsync($"/api/admin/organizaciones/{org}/admin",
            new CrearAdminOrganizacionRequest(datos.Nombres!, datos.Apellidos!, datos.Documento!, email, null));
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.OK, cuerpo);
        var dto = (await r.Content.ReadFromJsonAsync<AdminOrganizacionDto>())!;
        Assert.True(dto.PersonaReutilizada);
        Assert.Equal(previo.PersonaId, dto.PersonaId);
        Assert.Equal(new[] { "correo" }, dto.AccesoEntregado!.EntregadoPor);
        var correo = _factory.Correos.Skip(antes).Single(x => x.Para == email);
        var clave = Regex.Match(correo.Html, "monospace;font-size:15px\"><strong>([^<]+)</strong>").Groups[1].Value;
        Assert.DoesNotContain(clave, cuerpo);
        // Ya era usuario de la copropiedad: no se duplica la fila (unica tenant+persona).
        await using var db = await TenantDbAsync(tA);
        Assert.Equal(1, await db.UsuariosTenant.CountAsync(u => u.PersonaId == previo.PersonaId));
    }

    // ===================== Helpers =====================

    public sealed record CorreoEnviado(string Para, string Asunto, string Html);

    private sealed class FakeEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<CorreoEnviado> _cola;
        public FakeEmailSender(ConcurrentQueue<CorreoEnviado> cola) => _cola = cola;
        public Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            _cola.Enqueue(new CorreoEnviado(toEmail, subject, htmlBody));
            return Task.FromResult(new EmailSendResult(true, null));
        }
    }

    private sealed class Factory : AuthFlowTests.ApiFactory
    {
        private readonly string _conn;
        public ConcurrentQueue<CorreoEnviado> Correos { get; } = new();
        public Factory(string conn) : base(conn) => _conn = conn;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ConnectionStrings:Propia", _conn);
            builder.UseSetting("Jobs:Enabled", "false");
            builder.UseSetting("App:PublicBaseUrl", "https://app.test");
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<IEmailSender>();
                s.AddSingleton<IEmailSender>(new FakeEmailSender(Correos));
            });
        }
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task<PropiaDbContext> TenantDbAsync(Guid tenantId)
    {
        var tc = new TenantContext();
        tc.SetTenant(tenantId);
        var db = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, tc);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT set_config('app.tenant_id', {0}, false)", tenantId.ToString());
        return db;
    }

    private async Task<Guid> SeedTenantAsync(string nombre, Guid? orgId = null)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin, OrganizacionId = orgId };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        _tenants.Add(t.Id);
        return t.Id;
    }

    private async Task<Guid> SeedOrgAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var o = new Organizacion { Nombre = nombre };
        ctx.Organizaciones.Add(o);
        await ctx.SaveChangesAsync();
        _orgs.Add(o.Id);
        return o.Id;
    }

    private async Task<AdminCrearUsuarioResponse> CrearAsync(Guid tenantId, AdminCrearUsuarioRequest req)
    {
        var r = await _api.PostAsJsonAsync($"/admin/tenants/{tenantId}/usuarios", req);
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<AdminCrearUsuarioResponse>())!;
    }

    private string Email(string prefijo)
    {
        var e = $"api-acc-{prefijo}-{Guid.NewGuid():N}"[..30] + "@example.com";
        _emails.Add(e);
        return e;
    }

    private string Doc()
    {
        var d = $"7{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        _docs.Add(d);
        return d;
    }

    private AdminPersonaNuevaRequest Nueva(string nombre, string? email)
        => new(nombre, "Prueba API", TipoDocumento.CC, Doc(), email, "3001234567");
}
