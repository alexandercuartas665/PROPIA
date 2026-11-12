using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Asambleas;
using Propia.Application.Common;
using Propia.Application.Documents;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Convocatoria de la sesion en PDF (se genera al citar). Reusa el motor de documentos oficiales de PQRSD:
/// membrete predeterminado de la copropiedad (MembretesService) + hoja (IMembreteDocumentBuilder) + Chromium
/// (IHtmlToPdfService). El archivo va al storage y queda como SesionDocumento tipo Convocatoria.
/// </summary>
public class AsambleaConvocatoriaService : IAsambleaConvocatoriaService
{
    private static readonly CultureInfo Es = new("es-CO");
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMembreteDocumentBuilder _builder;
    private readonly IHtmlToPdfService _pdf;
    private readonly IBlobStorage _storage;
    private readonly IHttpContextAccessor _http;

    public AsambleaConvocatoriaService(PropiaDbContext db, ITenantContext tenant, IMembreteDocumentBuilder builder,
        IHtmlToPdfService pdf, IBlobStorage storage, IHttpContextAccessor http)
    {
        _db = db; _tenant = tenant; _builder = builder; _pdf = pdf; _storage = storage; _http = http;
    }

    public async Task<SesionDocumentoDto?> GenerarAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones.AsNoTracking().Include(x => x.Puntos).FirstOrDefaultAsync(x => x.Id == sesionId, ct);
        if (s is null) return null;
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == s.TenantId, ct);
        if (tenant is null) return null;

        var (enc, pie) = await EncabezadoYPieAsync(ct);
        var html = _builder.Construir(tenant, new MembreteDocContenido(
            TipoBadge: "Convocatoria",
            RadicadoLabel: "Sesion",
            Radicado: s.FechaSesion.ToOffset(Colombia).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Fecha: DateTimeOffset.UtcNow,
            CuerpoHtml: Cuerpo(s, tenant.Nombre),
            DestinatarioNombre: "Propietarios y apoderados",
            DestinatarioLinea: tenant.Nombre), enc, pie);

        var bytes = await _pdf.RenderAsync(html, ct);
        var key = $"tenants/{s.TenantId:N}/asambleas/{s.Id:N}/convocatoria-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.pdf";
        using var ms = new MemoryStream(bytes);
        var url = await _storage.UploadAsync(key, ms, "application/pdf", ct);

        var usuario = _http.HttpContext?.User?.FindFirstValue("user_id");
        var d = new SesionDocumento
        {
            SesionId = s.Id,
            Nombre = $"Convocatoria - {s.Titulo}.pdf",
            Descripcion = "Generada al citar la sesion.",
            UrlStorage = url,
            TipoArchivo = "application/pdf",
            TamanioBytes = bytes.LongLength,
            Tipo = TipoDocumentoSesion.Convocatoria,
            Visibilidad = VisibilidadDocumento.PreAsamblea,
            SubidoPorUsuarioId = Guid.TryParse(usuario, out var uid) ? uid : Guid.Empty
        };
        _db.SesionDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);
        return new SesionDocumentoDto(d.Id, null, d.Nombre, d.Descripcion, d.UrlStorage, d.TipoArchivo, d.TamanioBytes,
            d.Visibilidad, d.CreatedAt, d.Tipo);
    }

    private async Task<(string? Encabezado, string? Pie)> EncabezadoYPieAsync(CancellationToken ct)
        => await new Plantillas.MembretesService(_db, _tenant).ParaDocumentoAsync(null, null, ct);

    private static string TipoTexto(TipoSesion t) => t switch
    {
        TipoSesion.AsambleaOrdinaria => "Asamblea General Ordinaria de Propietarios",
        TipoSesion.AsambleaExtraordinaria => "Asamblea General Extraordinaria de Propietarios",
        TipoSesion.Consejo => "sesión del Consejo de Administración",
        TipoSesion.Comite => "sesión del Comité de Convivencia",
        _ => t.ToString()
    };

    private static string Cuerpo(Sesion s, string copropiedad)
    {
        string E(string? t) => WebUtility.HtmlEncode(t ?? "");
        var f = s.FechaSesion.ToOffset(Colombia);
        var fecha = f.ToString("dddd d 'de' MMMM 'de' yyyy", Es);
        var hora = f.ToString("h:mm tt", Es);
        var donde = s.Modalidad switch
        {
            ModalidadSesion.Presencial => $"en {E(s.LugarFisico ?? "el lugar que informe la administración")}",
            ModalidadSesion.Virtual => s.SalaPropia ? "de manera virtual, en la sala virtual de PROPIA (cada propietario recibe su enlace personal)"
                : $"de manera virtual{(string.IsNullOrWhiteSpace(s.EnlaceVideo) ? "" : $" ({E(s.EnlaceVideo)})")}",
            _ => $"de manera mixta: presencial en {E(s.LugarFisico ?? "el lugar que informe la administración")} y virtual"
                 + (s.SalaPropia ? " en la sala virtual de PROPIA" : string.IsNullOrWhiteSpace(s.EnlaceVideo) ? "" : $" ({E(s.EnlaceVideo)})"),
        };
        var esAsamblea = s.Tipo is TipoSesion.AsambleaOrdinaria or TipoSesion.AsambleaExtraordinaria;
        var html = new System.Text.StringBuilder();
        html.Append($"<p>La administración de <b>{E(copropiedad)}</b> convoca a la <b>{E(TipoTexto(s.Tipo))}</b>")
            .Append(s.SegundaConvocatoria ? " <b>(segunda convocatoria)</b>" : "")
            .Append($" que se realizará el <b>{E(fecha)}</b> a las <b>{E(hora)}</b>, {donde}.</p>");
        html.Append($"<p><b>{E(s.Titulo)}</b></p>");
        html.Append("<p><b>Orden del día</b></p><ol>");
        foreach (var p in s.Puntos.OrderBy(p => p.Numero))
            html.Append($"<li>{E(p.Titulo)}{(string.IsNullOrWhiteSpace(p.Descripcion) ? "" : $" <span style=\"color:#5B6B7D\">- {E(p.Descripcion)}</span>")}</li>");
        html.Append("</ol>");
        if (esAsamblea)
        {
            html.Append(s.SegundaConvocatoria
                ? "<p>Por tratarse de una reunión de segunda convocatoria (Ley 675 de 2001, art. 41), la asamblea sesionará y decidirá válidamente con un número plural de propietarios, cualquiera sea el porcentaje de coeficientes representados.</p>"
                : "<p>La asamblea deliberará con un número plural de propietarios que represente más de la mitad de los coeficientes de copropiedad (Ley 675 de 2001, art. 45). Si no se completa el quórum, se convocará a una reunión de segunda convocatoria (art. 41).</p>");
            html.Append("<p>Los propietarios que no puedan asistir podrán hacerse representar mediante poder escrito, que deberá presentarse a la administración antes de la reunión.</p>");
        }
        return html.ToString();
    }
}
