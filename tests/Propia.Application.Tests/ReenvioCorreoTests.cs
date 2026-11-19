using Propia.Application.Integraciones;
using Xunit;

namespace Propia.Application.Tests;

/// <summary>
/// PQRSD por correo (Ola P2): en un correo REENVIADO el solicitante es el remitente original. Los cuerpos imitan la forma
/// de los reenvios reales de Outlook ("RV:", sin texto propio, encabezado "De: ... Enviado el:"), de Gmail dentro de
/// Outlook ("---------- Forwarded message ---------") y de una cadena interna (la casa reenvia lo que reenvio la casa).
/// </summary>
public class ReenvioCorreoTests
{
    private const string Reenviador = "facturacion@adm-ejemplo.com";

    // Outlook reenvia sin escribir nada; adentro, el reenvio de Gmail del mismo correo.
    private const string OutlookConGmail =
        "De: Mario Gomez <mario.gomez72@gmail.com> \n" +
        "Enviado el: martes, 17 de noviembre de 2026 12:55 p. m.\n" +
        "Para: facturacion@adm-ejemplo.com; contabilidad@adm-ejemplo.com\n" +
        "Asunto: Fwd: Derecho de petición - documentos laborales\n" +
        "\n \n \n" +
        "---------- Forwarded message ---------\n" +
        "De: Mario Gomez <mario.gomez72@gmail.com <mailto:mario.gomez72@gmail.com> >\n" +
        "Date: mar, 17 nov 2026 a la(s) 8:00 a.m.\n" +
        "Subject: Derecho de petición - documentos laborales\n" +
        "To: <gerencia@adm-ejemplo.com <mailto:gerencia@adm-ejemplo.com> >\n" +
        "\n \n" +
        "Señores\nADMINISTRACIONES EJEMPLO.\n\nRemito derecho de petición para solicitar los documentos laborales de mi poderdante.\n\nMario Gomez";

    // Cadena interna: la casa reenvia un reenvio de la casa; el original es de un residente.
    private const string CadenaInterna =
        "De: Administración - Facturación <facturacion@adm-ejemplo.com> \n" +
        "Enviado el: miércoles, 28 de octubre de 2026 8:59 a. m.\n" +
        "Para: expansion@adm-ejemplo.com\n" +
        "Asunto: RV: Derecho de petición Art 23 CN\n" +
        "\n \n" +
        "De: Wilson Ramirez <wramirez180@gmail.com <mailto:wramirez180@gmail.com> > \n" +
        "Enviado el: martes, 27 de octubre de 2026 2:07 p. m.\n" +
        "Para: info@adm-ejemplo.com <mailto:info@adm-ejemplo.com> ; facturacion@adm-ejemplo.com <mailto:facturacion@adm-ejemplo.com> \n" +
        "Asunto: Derecho de petición Art 23 CN\n" +
        "\n \n" +
        "Pereira, 27 de octubre de 2026\n\nSeñores\nAdministraciones Ejemplo\n\nPor medio de la presente solicito copia del contrato de administración.\n\n" +
        "El lun, 26 oct 2026 a las 9:00, Administración <info@adm-ejemplo.com> escribió:\n> respuesta anterior";

    [Fact]
    public void Outlook_con_gmail_adentro_toma_al_remitente_original_y_su_texto()
    {
        var r = RadicadorPqrsdCorreoAgente.DetectarReenvio("RV: Radicación de derecho de petición", OutlookConGmail, Reenviador)!;
        Assert.Equal("mario.gomez72@gmail.com", r.Correo);
        Assert.Equal("Mario Gomez", r.Nombre);
        Assert.Equal("Derecho de petición - documentos laborales", r.Asunto);   // sin el "Fwd:"
        Assert.StartsWith("Señores", r.Cuerpo);                                // sin los encabezados de Gmail
        Assert.DoesNotContain("Forwarded message", r.Cuerpo);
        Assert.DoesNotContain("Subject:", r.Cuerpo);
        Assert.Null(r.Nota);   // quien reenvio no escribio nada
    }

    [Fact]
    public void Cadena_interna_salta_a_la_casa_y_quita_citas_del_original()
    {
        var r = RadicadorPqrsdCorreoAgente.DetectarReenvio("RV: Derecho de petición Art 23 CN", CadenaInterna, Reenviador)!;
        Assert.Equal("wramirez180@gmail.com", r.Correo);
        Assert.Equal("Wilson Ramirez", r.Nombre);
        Assert.Equal("Derecho de petición Art 23 CN", r.Asunto);
        Assert.Contains("copia del contrato de administración", r.Cuerpo);
        Assert.DoesNotContain("respuesta anterior", r.Cuerpo);
    }

    [Fact]
    public void Nota_de_quien_reenvia_queda_antes_del_original()
    {
        var cuerpo = "Les reenvío esta queja que llegó al correo de la portería.\n\n" +
                     "-----Mensaje original-----\nDe: Laura <laura.res@hotmail.com>\nEnviado: lunes\nPara: porteria@adm-ejemplo.com\nAsunto: Ruido\n\n" +
                     "El vecino del 502 pone música hasta la madrugada.";
        var r = RadicadorPqrsdCorreoAgente.DetectarReenvio("RV: Ruido", cuerpo, Reenviador)!;
        Assert.Equal("laura.res@hotmail.com", r.Correo);
        Assert.Equal("Les reenvío esta queja que llegó al correo de la portería.", r.Nota);
        Assert.Equal("El vecino del 502 pone música hasta la madrugada.", r.Cuerpo);
        Assert.StartsWith("Les reenvío", RadicadorPqrsdCorreoAgente.TextoReenvio(r));
    }

    [Fact]
    public void Respuesta_normal_o_correo_directo_no_es_reenvio()
    {
        var respuesta = "Gracias, quedo atento.\n\nEl lun, 16 nov 2026 a las 9:00, Administración <info@adm-ejemplo.com> escribió:\n> Cordial saludo";
        Assert.Null(RadicadorPqrsdCorreoAgente.DetectarReenvio("RE: Cuota de octubre", respuesta, "residente@gmail.com"));
        Assert.Null(RadicadorPqrsdCorreoAgente.DetectarReenvio("Queja por ruido", "El vecino pone música muy alto.", "residente@gmail.com"));
        // Reenvio sin un bloque reconocible: lo decide la IA.
        Assert.Null(RadicadorPqrsdCorreoAgente.DetectarReenvio("RV: algo", "mira esto", Reenviador));
        Assert.True(RadicadorPqrsdCorreoAgente.PareceReenvio("RV: algo"));
        Assert.True(RadicadorPqrsdCorreoAgente.PareceReenvio("Fwd: algo"));
        Assert.False(RadicadorPqrsdCorreoAgente.PareceReenvio("RE: algo"));
    }

    [Fact]
    public void Descripcion_de_un_reenvio_usa_el_asunto_y_el_texto_originales()
    {
        var r = RadicadorPqrsdCorreoAgente.DetectarReenvio("RV: Derecho de petición Art 23 CN", CadenaInterna, Reenviador)!;
        var d = RadicadorPqrsdCorreoAgente.ArmarDescripcion("RV: Derecho de petición Art 23 CN", CadenaInterna, null,
            "[Reenviado por <facturacion@adm-ejemplo.com>]", r);
        Assert.StartsWith("[Reenviado por <facturacion@adm-ejemplo.com>]\n\nAsunto: Derecho de petición Art 23 CN", d);
        Assert.Contains("copia del contrato de administración", d);
        Assert.DoesNotContain("Enviado el:", d);
    }

    [Fact]
    public void Parsear_lee_el_remitente_original_que_ve_la_ia()
    {
        var tipos = new[] { new TipoPqrsdOpcion(Guid.NewGuid(), "Petición") };
        var cats = new[] { new CategoriaPqrsdOpcion(Guid.NewGuid(), "Administración") };
        var p = RadicadorPqrsdCorreoAgente.Parsear(
            "{\"es_pqrsd\":true,\"tipo\":\"Petición\",\"categoria\":\"Administración\",\"remitente_original_nombre\":\"Ana Ruiz\",\"remitente_original_correo\":\"Ana Ruiz <ANA.RUIZ@correo.co>\",\"confianza\":0.9}",
            tipos, cats)!;
        Assert.Equal("Ana Ruiz", p.RemitenteOriginalNombre);
        Assert.Equal("ana.ruiz@correo.co", p.RemitenteOriginalCorreo);
        var sin = RadicadorPqrsdCorreoAgente.Parsear("{\"es_pqrsd\":true,\"tipo\":\"Petición\",\"remitente_original_correo\":\"no se\"}", tipos, cats)!;
        Assert.Null(sin.RemitenteOriginalCorreo);
    }
}
