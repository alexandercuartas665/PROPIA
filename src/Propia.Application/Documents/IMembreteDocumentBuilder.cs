using Propia.Domain.Entities;

namespace Propia.Application.Documents;

/// <summary>
/// Contenido variable de un documento oficial (respuesta PQRSD, paz y salvo, certificado...).
/// La identidad y el membrete (header/footer) se derivan del Tenant + su config de membrete.
/// </summary>
public record MembreteDocContenido(
    string TipoBadge,            // etiqueta del documento, ej. "Respuesta PQRSD"
    string RadicadoLabel,        // "Radicado" | "Consecutivo"
    string Radicado,             // ej. "PQRSD-2026-0042"
    DateTimeOffset Fecha,        // fecha del documento (se muestra "Ciudad, dd de mes de yyyy")
    string CuerpoHtml,           // cuerpo rico (TinyMCE) - va tal cual dentro del documento
    string? DestinatarioNombre = null,
    string? DestinatarioLinea = null,
    string? FirmanteNombre = null,   // override; si null usa la config de membrete del Tenant
    string? FirmanteCargo = null);

/// <summary>
/// Compone el HTML completo (standalone, con estilos inline y @page A4) de un documento oficial: la hoja, los
/// datos del documento (tipo, radicado, fecha), destinatario, cuerpo, firma y numeracion. El encabezado y el pie
/// de pagina vienen de la biblioteca de membretes de la copropiedad (IMembretesService.ParaDocumentoAsync), ya
/// resueltos; null = sin encabezado / sin pie. Ese mismo HTML es el que luego se renderiza a PDF.
/// </summary>
public interface IMembreteDocumentBuilder
{
    string Construir(Tenant tenant, MembreteDocContenido contenido, string? encabezadoHtml, string? pieHtml);
}
