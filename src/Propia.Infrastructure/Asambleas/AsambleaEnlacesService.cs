using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.Asambleas;
using Propia.Application.Common;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Envio por correo del enlace self-service de Asambleas (Ola 1.5). Destinatarios = mismo gate que el
/// enlace (AsambleaService.Residente): propietario activo de una unidad citada o apoderado con poder
/// aprobado en la sesion. Residente/Familiar/Arrendatario no reciben enlace (Ley 675). Un dueno Empresa
/// no recibe: vota su apoderado. Sin tabla de seguimiento: el resultado del lote va al log (Ola 3).
/// </summary>
public sealed class AsambleaEnlacesService : IAsambleaEnlacesService
{
    private static readonly TimeSpan OffsetColombia = TimeSpan.FromHours(-5);

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAsambleaTokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AsambleaEnlacesService> _logger;

    public AsambleaEnlacesService(PropiaDbContext db, ITenantContext tenantContext, IAsambleaTokenService tokens,
        IEmailSender email, IServiceScopeFactory scopeFactory, ILogger<AsambleaEnlacesService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _tokens = tokens;
        _email = email;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DestinatarioEnlaceDto>?> ResolverDestinatariosAsync(
        Guid sesionId, IReadOnlyCollection<Guid>? personaIds, CancellationToken ct)
    {
        var s = await _db.Sesiones.AsNoTracking()
            .Where(x => x.Id == sesionId)
            .Select(x => new { x.Estado })
            .FirstOrDefaultAsync(ct);
        if (s is null) return null;
        if (s.Estado is not (EstadoSesion.Citada or EstadoSesion.EnCurso))
            throw new InvalidOperationException("Solo se envian enlaces de una sesion citada o en curso.");

        var unidadesSesion = await _db.SesionParticipantes.AsNoTracking()
            .Where(p => p.SesionId == sesionId)
            .Select(p => p.UnidadPrivadaId)
            .ToListAsync(ct);

        var propios = await _db.UnidadPersonas.AsNoTracking()
            .Where(x => x.Activo && x.Rol == RolUnidadPersona.Propietario && x.PersonaId != null
                        && unidadesSesion.Contains(x.UnidadId))
            .Select(x => new { PersonaId = x.PersonaId!.Value, x.UnidadId })
            .ToListAsync(ct);
        var porPoder = await _db.SesionPoderes.AsNoTracking()
            .Where(x => x.SesionId == sesionId && x.Estado == EstadoPoder.Aprobado
                        && unidadesSesion.Contains(x.OtorganteUnidadId))
            .Select(x => new { PersonaId = x.ApoderadoPersonaId, UnidadId = x.OtorganteUnidadId })
            .ToListAsync(ct);

        var porPersona = propios.Concat(porPoder)
            .Where(x => personaIds is null || personaIds.Contains(x.PersonaId))
            .GroupBy(x => x.PersonaId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UnidadId).Distinct().ToList());
        if (porPersona.Count == 0) return [];

        var ids = porPersona.Keys.ToList();
        var todasUnidades = porPersona.Values.SelectMany(u => u).Distinct().ToList();
        var numeros = await _db.UnidadesPrivadas.AsNoTracking()
            .Where(u => todasUnidades.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Numero, ct);
        // Personas es global: se lee por id (los ids salen de vinculos/poderes del tenant actual).
        var personas = await _db.Personas.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = (p.Nombres + " " + p.Apellidos).Trim(), p.Email })
            .ToListAsync(ct);

        return personas
            .OrderBy(p => p.Nombre)
            .Select(p => new DestinatarioEnlaceDto(p.Id, p.Nombre, EmailValido(p.Email),
                porPersona[p.Id].Select(u => numeros.GetValueOrDefault(u, "")).OrderBy(n => n).ToList()))
            .ToList();
    }

    public async Task<EnvioEnlacesResultado> EnviarAsync(
        Guid sesionId, IReadOnlyList<DestinatarioEnlaceDto> destinatarios, string baseUrl, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("Tenant no fijado para el envio de enlaces.");
        var s = await _db.Sesiones.AsNoTracking()
            .Where(x => x.Id == sesionId)
            .Select(x => new { x.Titulo, x.FechaSesion, x.LugarFisico })
            .FirstAsync(ct);
        var copropiedad = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Nombre)
            .FirstOrDefaultAsync(ct) ?? "tu copropiedad";

        var expira = AsambleaEnlace.Vencimiento(s.FechaSesion, DateTimeOffset.UtcNow);
        var raiz = baseUrl.Trim().TrimEnd('/');
        var asunto = $"{copropiedad}: tu enlace para la asamblea \"{s.Titulo}\"";
        int enviados = 0, fallidos = 0;
        var errores = new List<string>();

        foreach (var d in destinatarios)
        {
            if (d.Email is null) continue;
            var token = _tokens.Emitir(new AsambleaEnlaceToken(tenantId, sesionId, d.PersonaId), expira);
            var link = raiz + AsambleaEnlace.Ruta(tenantId, token);
            var html = CuerpoCorreo(d, copropiedad, s.Titulo, s.FechaSesion, s.LugarFisico, link, expira);

            EmailSendResult r;
            try { r = await _email.SendAsync(d.Email, asunto, html, ct); }
            catch (Exception ex) { r = new EmailSendResult(false, ex.Message); }

            if (r.Success) enviados++;
            else
            {
                fallidos++;
                if (errores.Count < 10) errores.Add($"{d.Nombre}: {r.Error}");
            }
        }
        return new EnvioEnlacesResultado(enviados, fallidos, errores);
    }

    public async Task<EnvioEnlacesDto?> ProgramarEnvioAsync(
        Guid sesionId, IReadOnlyCollection<Guid>? personaIds, string baseUrl, CancellationToken ct)
    {
        var destinatarios = await ResolverDestinatariosAsync(sesionId, personaIds, ct);
        if (destinatarios is null) return null;

        var conCorreo = destinatarios.Where(d => d.Email is not null).ToList();
        var sinCorreo = destinatarios.Where(d => d.Email is null).Select(d => d.Nombre).ToList();
        var tenantId = _tenantContext.CurrentTenantId!.Value;

        if (conCorreo.Count > 0)
        {
            // Fuera del request: scope propio + tenant fijado antes de la primera consulta (RLS). No usa
            // el CancellationToken del request, que se cancela al responder.
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var svc = scope.ServiceProvider.GetRequiredService<IAsambleaEnlacesService>();
                    var r = await svc.EnviarAsync(sesionId, conCorreo, baseUrl, CancellationToken.None);
                    _logger.LogInformation(
                        "Asambleas: enlaces de la sesion {SesionId} (tenant {TenantId}): {Enviados} enviados, {Fallidos} fallidos de {Total}. {Errores}",
                        sesionId, tenantId, r.Enviados, r.Fallidos, conCorreo.Count, string.Join(" | ", r.Errores));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Asambleas: fallo el envio de enlaces de la sesion {SesionId}.", sesionId);
                }
            });
        }

        return new EnvioEnlacesDto(destinatarios.Count, conCorreo.Count, sinCorreo);
    }

    private static string? EmailValido(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return MailAddress.TryCreate(email.Trim(), out var a) ? a.Address : null;
    }

    private static string CuerpoCorreo(DestinatarioEnlaceDto d, string copropiedad, string titulo,
        DateTimeOffset fecha, string? lugar, string link, DateTimeOffset expira)
    {
        static string H(string? v) => WebUtility.HtmlEncode(v ?? "");
        var co = System.Globalization.CultureInfo.GetCultureInfo("es-CO");
        var cuando = fecha.ToOffset(OffsetColombia).ToString("dddd d 'de' MMMM 'a las' h:mm tt", co);
        var vence = expira.ToOffset(OffsetColombia).ToString("d 'de' MMMM", co);
        var unidades = d.Unidades.Count == 1 ? $"la unidad <strong>{H(d.Unidades[0])}</strong>"
            : $"las unidades <strong>{H(string.Join(", ", d.Unidades))}</strong>";
        var donde = string.IsNullOrWhiteSpace(lugar) ? "" : $" en {H(lugar)}";

        return $@"<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;color:#1f2937"">
  <h2 style=""color:#111827"">Hola {H(d.Nombre)},</h2>
  <p><strong>{H(copropiedad)}</strong> te convoca a <strong>{H(titulo)}</strong>, el {H(cuando)}{donde}.</p>
  <p>Con este enlace registras tu asistencia y votas por {unidades} desde el celular, sin crear cuenta.</p>
  <p style=""margin:28px 0"">
    <a href=""{link}"" style=""background:#6D4FE3;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600"">Entrar a la asamblea</a>
  </p>
  <p style=""font-size:13px;color:#374151"">El enlace es personal: no lo compartas. Vence el {H(vence)}.</p>
  <p style=""font-size:12px;color:#6b7280"">Si el boton no funciona, copia y pega este enlace en tu navegador:<br>{link}</p>
</div>";
    }
}
