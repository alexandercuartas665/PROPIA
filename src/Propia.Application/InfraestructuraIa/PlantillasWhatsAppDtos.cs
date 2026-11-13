using Propia.Domain.Enums;

namespace Propia.Application.InfraestructuraIa;

// =====================================================================================
// DTOs del modulo Plantillas de WhatsApp (HSM). Portado de ECOREX con nombre propio para no
// chocar con otros modulos de plantillas de PROPIA. El flujo principal de este corte es IMPORTAR
// las plantillas que ya existen en la cuenta YCloud de la linea.
// =====================================================================================

/// <summary>Una variable del cuerpo de la plantilla: token + ejemplo. En las importadas los tokens
/// son posicionales ("1", "2"...).</summary>
public sealed record PlantillaWaVariable(string Token, string Example);

/// <summary>Un boton de la plantilla, importado de Meta (solo lectura). <paramref name="Type"/> en
/// MAYUSCULAS (URL | QUICK_REPLY | PHONE_NUMBER | COPY_CODE | ...); <paramref name="HasUrlVariable"/> =
/// boton URL con sufijo variable {{1}} (dinamico por envio).</summary>
public sealed record PlantillaWaBotonDto(string Type, string? Text, string? Url, string? PhoneNumber, bool HasUrlVariable);

/// <summary>Fila del listado + detalle de una plantilla de WhatsApp (todos los campos).</summary>
public sealed record PlantillaWhatsAppDto(
    Guid Id,
    string Name,
    string Language,
    PlantillaWaCategoria Categoria,
    PlantillaWaEncabezado? Encabezado,
    string? EncabezadoTexto,
    string? EncabezadoMediaUrl,
    string Cuerpo,
    string? PieTexto,
    IReadOnlyList<PlantillaWaVariable> Variables,
    IReadOnlyList<PlantillaWaBotonDto> Botones,
    WhatsAppProvider? Provider,
    Guid WhatsAppLineId,
    string? LineaNombre,
    string? WabaId,
    PlantillaWaEstado Estado,
    string? ProviderTemplateId,
    string? MotivoRechazo,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    bool Activa);

/// <summary>Conteo del resultado de importar plantillas desde YCloud (cuantas se crearon, actualizaron,
/// quedaron sin cambios y se omitieron) mas mensajes por plantilla.</summary>
public sealed record ImportarPlantillasReporte(
    int Importadas, int Actualizadas, int SinCambios, int Omitidas, IReadOnlyList<string> Mensajes);

/// <summary>Resultado de la importacion: Ok + (si fallo) el error, y el reporte cuando Ok.</summary>
public sealed record ImportarPlantillasResultado(bool Ok, string? Error, ImportarPlantillasReporte? Reporte);

/// <summary>Sincronizacion de las plantillas de TODAS las lineas YCloud de la copropiedad (al abrir el modulo y por
/// el job cada 30 min). Omitida = se sincronizo hace menos de 2 minutos y no se volvio a llamar a YCloud.</summary>
public sealed record SincronizarPlantillasResultado(int Lineas, int Importadas, int Actualizadas, IReadOnlyList<string> Errores, bool Omitida = false);

/// <summary>Como quedo enlazada una plantilla que usa un modulo.</summary>
public enum EstadoUsoPlantilla { Ok = 0, NoExiste = 1, NoAprobada = 2, Archivada = 3 }

/// <summary>Donde se usa una plantilla: el modulo, para que, el nombre que tiene configurado y si esta bien
/// (existe en el modulo, aprobada por Meta y activa). PlantillaId = la del modulo si existe.</summary>
public sealed record UsoPlantillaWhatsAppDto(string Modulo, string Uso, string PlantillaNombre, string Idioma, Guid? PlantillaId, EstadoUsoPlantilla Estado);

/// <summary>Resultado de un envio de prueba de una plantilla.</summary>
public sealed record EnvioPruebaResultado(bool Ok, string? Error);
