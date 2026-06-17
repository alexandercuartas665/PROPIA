using Microsoft.EntityFrameworkCore;
using Propia.Application.Cartera;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Propia.Infrastructure.Cartera;

/// <summary>
/// Genera el comprobante de pago en PDF con QuestPDF. Layout limpio:
/// header con identidad de la copropiedad + titulo "Comprobante de pago",
/// bloque "Pagado por" con unidad y persona, bloque "Detalle del pago"
/// con monto/canal/referencia, footer con codigo de verificacion y disclaimer
/// "Operado por A&amp;D GROUP S.A.S. - PROPIA".
/// </summary>
public class ComprobantePdfService : IComprobantePdfService
{
    private readonly PropiaDbContext _db;

    public ComprobantePdfService(PropiaDbContext db) => _db = db;

    public async Task<(byte[] Pdf, string FileName)?> GenerarComprobantePagoAsync(Guid pagoId, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var pago = await _db.PagosCuotas.AsNoTracking()
            .Include(p => p.UnidadPrivada!).ThenInclude(u => u.Torre)
            .FirstOrDefaultAsync(p => p.Id == pagoId, ct);
        if (pago is null) return null;

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == pago.TenantId, ct);
        if (tenant is null) return null;

        var persona = pago.PersonaId.HasValue
            ? await _db.Personas.AsNoTracking()
                .Where(p => p.Id == pago.PersonaId.Value)
                .Select(p => new { p.Nombres, p.Apellidos, p.Documento })
                .FirstOrDefaultAsync(ct)
            : null;

        var nombreUnidad = pago.UnidadPrivada is null
            ? "-"
            : pago.UnidadPrivada.Torre is null
                ? pago.UnidadPrivada.Numero
                : $"{pago.UnidadPrivada.Torre.Nombre} - {pago.UnidadPrivada.Numero}";

        var moneda = string.IsNullOrEmpty(tenant.Moneda) ? "COP" : tenant.Moneda;
        var codigoVerificacion = $"PG-{pago.Id.ToString("N").Substring(0, 10).ToUpper()}";

        // Colores brand PROPIA (alineados al prototipo).
        var brandPrimary = "#6D4FE3";
        var brandSoft = "#F1ECFD";
        var textHi = "#1B2A3A";
        var textMid = "#516F90";
        var textLow = "#7C98B6";

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
                        r.ConstantItem(180).AlignRight().Column(c =>
                        {
                            c.Item().Background(brandSoft).PaddingHorizontal(10).PaddingVertical(6)
                                .Text("COMPROBANTE DE PAGO").FontSize(9).Bold().FontColor(brandPrimary)
                                .LetterSpacing(0.06f);
                            c.Item().PaddingTop(8).AlignRight()
                                .Text(codigoVerificacion).FontSize(11).Bold().FontColor(textHi);
                            c.Item().AlignRight()
                                .Text($"Emitido {DateTime.UtcNow.ToLocalTime():dd MMM yyyy HH:mm}")
                                .FontSize(8).FontColor(textLow);
                        });
                    });
                    col.Item().PaddingTop(14).LineHorizontal(1).LineColor(brandSoft);
                });

                // ----- BODY -----
                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Element(BoxSeccion).Column(c =>
                        {
                            c.Item().Text("PAGADO POR").FontSize(9).Bold().FontColor(textLow).LetterSpacing(0.05f);
                            c.Item().PaddingTop(4).Text(persona is null
                                ? "(sin persona registrada)"
                                : $"{persona.Nombres} {persona.Apellidos}".Trim()).FontSize(12).Bold();
                            if (persona is not null && !string.IsNullOrEmpty(persona.Documento))
                                c.Item().Text($"Doc. {persona.Documento}").FontSize(9).FontColor(textMid);
                            c.Item().PaddingTop(6).Text("UNIDAD").FontSize(8).Bold().FontColor(textLow);
                            c.Item().Text(nombreUnidad).FontSize(11).Bold();
                        });
                        r.ConstantItem(14);
                        r.RelativeItem().Element(BoxSeccion).Column(c =>
                        {
                            c.Item().Text("MONTO PAGADO").FontSize(9).Bold().FontColor(textLow).LetterSpacing(0.05f);
                            c.Item().PaddingTop(4).Text(FormatoMoneda(pago.Monto, moneda)).FontSize(20).Bold().FontColor(brandPrimary);
                            c.Item().Text(pago.Tipo.ToString()).FontSize(9).FontColor(textMid);
                        });
                    });

                    col.Item().PaddingTop(18).Element(BoxSeccion).Column(c =>
                    {
                        c.Item().Text("DETALLE DEL PAGO").FontSize(9).Bold().FontColor(textLow).LetterSpacing(0.05f);
                        c.Item().PaddingTop(8).Row(r =>
                        {
                            r.RelativeItem().Column(d =>
                            {
                                Kv(d, "Fecha pago", pago.FechaPago is null ? "Pendiente" : pago.FechaPago.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm"), textMid);
                                Kv(d, "Canal", pago.Canal.ToString(), textMid);
                                Kv(d, "Estado", pago.Estado.ToString(), textMid);
                            });
                            r.RelativeItem().Column(d =>
                            {
                                Kv(d, "Referencia", string.IsNullOrEmpty(pago.ReferenciaExterna) ? "-" : pago.ReferenciaExterna, textMid);
                                Kv(d, "Registro", pago.EsManual ? "Manual" : "Automatico (pasarela)", textMid);
                                Kv(d, "ID interno", pago.Id.ToString().Substring(0, 8) + "...", textMid);
                            });
                        });
                        if (!string.IsNullOrWhiteSpace(pago.Notas))
                        {
                            c.Item().PaddingTop(6).Text(pago.Notas).FontSize(9).Italic().FontColor(textMid);
                        }
                    });

                    col.Item().PaddingTop(20).PaddingHorizontal(8).Column(d =>
                    {
                        d.Item().Text("Validacion").FontSize(8).Bold().FontColor(textLow).LetterSpacing(0.05f);
                        d.Item().Text($"Codigo: {codigoVerificacion}").FontSize(10).Bold().FontColor(textHi);
                        d.Item().Text("Este documento es un comprobante valido sin firma manuscrita. "
                            + "Validar el codigo en la copropiedad si se requiere autenticidad. "
                            + "El dinero abonado se acredita en la cuenta de la copropiedad emisora.")
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
        var fileName = $"comprobante-{pago.Id.ToString("N").Substring(0, 8)}.pdf";
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

    private static string FormatoMoneda(decimal monto, string isoMoneda)
    {
        var simbolo = isoMoneda switch { "COP" => "$", "USD" => "US$", _ => isoMoneda + " " };
        return $"{simbolo}{monto:N0}";
    }
}
