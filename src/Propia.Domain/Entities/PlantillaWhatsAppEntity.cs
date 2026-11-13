using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Plantilla HSM de WhatsApp (mensaje plantilla que Meta aprueba) de la copropiedad. Entidad
/// TENANT-SCOPED (RLS). Portada de ECOREX (WhatsAppTemplate) con nombre propio para NO chocar con
/// otros modulos de "plantillas" ya funcionales en PROPIA.
///
/// En este corte el flujo principal es IMPORTAR: se traen las plantillas que ya existen en la
/// cuenta YCloud de la linea y se guardan aqui para consultarlas, archivarlas y hacer envios de
/// prueba. El cuerpo llega con las variables POSICIONALES de Meta ({{1}}, {{2}}...); sus ejemplos
/// se guardan en <see cref="VariablesJson"/>. Unica por (TenantId, Name, Language).
/// </summary>
public class PlantillaWhatsApp : TenantEntity
{
    /// <summary>Nombre tecnico de la plantilla en Meta (minusculas, guion_bajo). Unico por tenant+idioma.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Codigo de idioma de Meta (ej. "es", "es_CO", "en_US").</summary>
    public string Language { get; set; } = "es";

    /// <summary>Categoria de Meta: Marketing, Utility o Authentication.</summary>
    public PlantillaWaCategoria Categoria { get; set; } = PlantillaWaCategoria.Utility;

    /// <summary>Tipo de header: Text, Image, Document, Video. Null/None = sin header.</summary>
    public PlantillaWaEncabezado? Encabezado { get; set; }

    public string? EncabezadoTexto { get; set; }

    /// <summary>
    /// URL PUBLICA de la imagen/documento/video del header cuando el encabezado es de media. En Meta/YCloud
    /// se usa como media al enviar (header.link). Null para header de texto o plantilla importada sin media.
    /// </summary>
    public string? EncabezadoMediaUrl { get; set; }

    /// <summary>Cuerpo de la plantilla. En las importadas de YCloud viene con variables posicionales {{1}}, {{2}}...</summary>
    public string Cuerpo { get; set; } = null!;

    public string? PieTexto { get; set; }

    /// <summary>
    /// JSON con las variables del cuerpo y su ejemplo: [{ "token": "1", "example": "Juan" }].
    /// Se guarda como jsonb. En las importadas los tokens son posicionales ("1","2"...).
    /// </summary>
    public string VariablesJson { get; set; } = "[]";

    /// <summary>
    /// JSON con los BOTONES de la plantilla, importados de Meta (solo lectura; Meta los pinta al entregar):
    /// [{ "type": "URL"|"QUICK_REPLY"|"PHONE_NUMBER"|..., "text": "...", "url": "...", "phoneNumber": "...",
    /// "hasUrlVariable": true }]. jsonb. Null/[] = plantilla sin botones.
    /// </summary>
    public string? BotonesJson { get; set; }

    // === Proveedor / origen =================================================
    /// <summary>Proveedor de la linea por la que se importo (referencia). Null hasta asociar linea.</summary>
    public WhatsAppProvider? Provider { get; set; }

    /// <summary>Linea por la que se importo (define la cuenta/WABA y las credenciales).</summary>
    public Guid WhatsAppLineId { get; set; }
    public WhatsAppLine? WhatsAppLine { get; set; }

    /// <summary>WABA (WhatsApp Business Account) de la que proviene la plantilla.</summary>
    public string? WabaId { get; set; }

    // === Estado de aprobacion ===============================================
    public PlantillaWaEstado Estado { get; set; } = PlantillaWaEstado.Draft;

    /// <summary>Id de la plantilla en el proveedor/Meta.</summary>
    public string? ProviderTemplateId { get; set; }
    public string? MotivoRechazo { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Archivada (soft-delete): no se borra fisicamente, se marca inactiva.</summary>
    public bool Activa { get; set; } = true;
}
