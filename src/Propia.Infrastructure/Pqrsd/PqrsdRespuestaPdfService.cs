using Microsoft.EntityFrameworkCore;
using Propia.Application.Pqrsd;
using Propia.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Propia.Infrastructure.Pqrsd;

/// <summary>
/// Genera el PDF de la respuesta oficial a un PQRSD con QuestPDF. Layout brand PROPIA:
/// header con identidad de la copropiedad + numero de radicado, bloque con datos del expediente
/// (tipo, categoria, solicitante, fechas), el cuerpo de la respuesta, y footer del operador.
/// </summary>
public class PqrsdRespuestaPdfService : IPqrsdRespuestaPdfService
{
    private readonly PropiaDbContext _db;

    public PqrsdRespuestaPdfService(PropiaDbContext db) => _db = db;

    public async Task<(byte[] Pdf, string FileName)?> GenerarRespuestaPdfAsync(Guid expedienteId, string textoRespuesta, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var exp = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.Categoria)
            .Include(e => e.TipoConfig)
            .Include(e => e.RadicadorPersona)
            .FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == exp.TenantId, ct);
        if (tenant is null) return null;

        var tipoNombre = exp.TipoConfig?.Nombre ?? exp.Tipo.ToString();
        var categoria = exp.Categoria?.Nombre ?? "-";
        var solicitante = exp.IdentidadReservada
            ? "Identidad reservada"
            : exp.RadicadorPersona is null
                ? "-"
                : $"{exp.RadicadorPersona.Nombres} {exp.RadicadorPersona.Apellidos}".Trim();

        // Colores brand PROPIA (alineados al comprobante de pago).
        var brandPrimary = "#6D4FE3";
        var brandSoft = "#F1ECFD";
        var textHi = "#1B2A3A";
        var textMid = "#516F90";
        var textLow = "#7C98B6";

        var respondidoAt = (exp.RespuestaAdminAt ?? DateTimeOffset.UtcNow).ToLocalTime();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10).FontColor(textHi));

                // ----- HEADER -----
                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(tenant.Nombre).FontSize(15).Bold().FontColor(brandPrimary);
                            if (!string.IsNullOrWhiteSpace(tenant.Nit))
                                c.Item().Text($"NIT {tenant.Nit}").FontSize(9).FontColor(textMid);
                            if (!string.IsNullOrWhiteSpace(tenant.Direccion))
                                c.Item().Text(tenant.Direccion).FontSize(9).FontColor(textMid);
                            if (!string.IsNullOrWhiteSpace(tenant.Ciudad))
                                c.Item().Text(tenant.Ciudad).FontSize(9).FontColor(textMid);
                        });
                        r.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().Background(brandSoft).PaddingHorizontal(10).PaddingVertical(6)
                                .Text("RESPUESTA A PQRSD").FontSize(9).Bold().FontColor(brandPrimary)
                                .LetterSpacing(0.06f);
                            c.Item().PaddingTop(8).AlignRight()
                                .Text(exp.NumeroRadicado).FontSize(12).Bold().FontColor(textHi);
                            c.Item().AlignRight()
                                .Text($"Respondido {respondidoAt:dd MMM yyyy HH:mm}")
                                .FontSize(8).FontColor(textLow);
                        });
                    });
                    col.Item().PaddingTop(14).LineHorizontal(1).LineColor(brandSoft);
                });

                // ----- BODY -----
                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Item().Element(BoxSeccion).Column(c =>
                    {
                        c.Item().Text("DATOS DEL EXPEDIENTE").FontSize(9).Bold().FontColor(textLow).LetterSpacing(0.05f);
                        c.Item().PaddingTop(8).Row(r =>
                        {
                            r.RelativeItem().Column(d =>
                            {
                                Kv(d, "Radicado", exp.NumeroRadicado, textMid);
                                Kv(d, "Tipo", tipoNombre, textMid);
                                Kv(d, "Categoria", categoria, textMid);
                            });
                            r.RelativeItem().Column(d =>
                            {
                                Kv(d, "Solicitante", solicitante, textMid);
                                Kv(d, "Radicado el", exp.CreatedAt.ToLocalTime().ToString("dd MMM yyyy"), textMid);
                                Kv(d, "Estado", exp.Estado.ToString(), textMid);
                            });
                        });
                    });

                    col.Item().PaddingTop(18).Text("RESPUESTA").FontSize(9).Bold().FontColor(textLow).LetterSpacing(0.05f);
                    col.Item().PaddingTop(6).Element(BoxSeccion).Column(c =>
                    {
                        c.Item().Text(textoRespuesta).FontSize(11).LineHeight(1.35f).FontColor(textHi);
                    });

                    col.Item().PaddingTop(20).PaddingHorizontal(8).Column(d =>
                    {
                        d.Item().Text("Constancia").FontSize(8).Bold().FontColor(textLow).LetterSpacing(0.05f);
                        d.Item().Text("Este documento es la respuesta oficial de la copropiedad al expediente indicado. "
                            + "Se genera automaticamente al enviar la respuesta y es valido sin firma manuscrita.")
                            .FontSize(8).FontColor(textLow);
                    });
                });

                // ----- FOOTER -----
                page.Footer().AlignCenter().Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(brandSoft);
                    c.Item().PaddingTop(6).Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8).FontColor(textLow));
                        t.Span("Operado por A&D GROUP S.A.S. - ");
                        t.Span("PROPIA").Bold().FontColor(brandPrimary);
                        t.Span("  - plataforma SaaS de copropiedades");
                    });
                });
            });
        });

        var pdf = doc.GeneratePdf();
        var fileName = $"respuesta-{exp.NumeroRadicado}.pdf";
        return (pdf, fileName);
    }

    private static IContainer BoxSeccion(IContainer container) =>
        container.Background("#FAFBFD").Border(1).BorderColor("#EEF3F8").Padding(14);

    private static void Kv(ColumnDescriptor d, string k, string v, string textMid)
    {
        d.Item().Row(r =>
        {
            r.ConstantItem(85).Text(k).FontSize(9).FontColor(textMid);
            r.RelativeItem().Text(v).FontSize(10).Bold();
        });
    }
}
