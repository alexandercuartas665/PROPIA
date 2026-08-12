using Propia.Domain.Enums;

namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Definicion canonica del agente "Auxiliar Administrativo": el agente IA de proposito general
/// que la plataforma usa para RELLENAR y COMPLETAR campos de texto del sistema (condiciones de
/// uso de una zona, descripciones, observaciones, mensajes). Se siembra por tenant y ademas se
/// crea al vuelo la primera vez que se le invoca (get-or-create). Comparte estas constantes el
/// seeder (Infrastructure) y el servicio de completado.
/// </summary>
public static class AuxiliarAdministrativoAgente
{
    public const string Nombre = "Auxiliar Administrativo";
    public const string RoleTag = "Auxiliar administrativo";

    public const string SystemPrompt = """
Eres el "Auxiliar Administrativo" de PROPIA, una plataforma de administracion de
copropiedades en Colombia. Tu unica tarea es redactar y completar campos de texto
del sistema (descripciones, condiciones de uso, reglamentos breves, observaciones,
mensajes) a partir de una instruccion corta y del contexto que se te entregue.

Reglas:
- Responde UNICAMENTE con el texto final del campo. Sin saludos, sin comillas, sin
  encabezados, sin explicaciones. No agregues "Aqui tienes" ni nada por el estilo.
- Escribe en espanol claro, formal y en tono institucional de administracion PH.
- Usa solo caracteres ASCII: sin tildes, sin la letra ene con virgulilla ni simbolos
  especiales.
- Se concreto y practico. Ajusta la extension a lo que se pida; por defecto entre 2 y
  5 frases o una lista corta de vinetas con guion.
- No inventes datos que no te hayan dado (nombres, fechas, valores, telefonos). Si
  falta un dato, redacta de forma general sin inventarlo.
- Si te dan "puntos clave", desarrollalos y ordenalos de forma coherente.
""";
}

/// <summary>Peticion generica para que el Auxiliar Administrativo redacte el contenido de un campo.</summary>
/// <param name="Proposito">Que campo se redacta, en una frase. Ej: "condiciones de uso de una zona comun".</param>
/// <param name="Contexto">Datos de apoyo (nombre de la zona, categoria, aforo, etc.).</param>
/// <param name="PuntosClave">Notas del usuario a desarrollar (ej: "prohibido consumo de licor, no mascotas").</param>
/// <param name="MaxPalabras">Extension maxima aproximada (opcional).</param>
public sealed record AsistenteCampoRequest(
    string Proposito,
    string? Contexto = null,
    string? PuntosClave = null,
    int? MaxPalabras = null);

public sealed record AsistenteCampoResult(bool Ok, string? Texto, string? Error);

/// <summary>
/// Servicio reutilizable que genera el contenido de un campo con el agente "Auxiliar Administrativo"
/// de la copropiedad activa. Resuelve (o crea) el agente y delega la inferencia en IAiInferenceService.
/// </summary>
public interface IAsistenteCamposService
{
    Task<AsistenteCampoResult> CompletarAsync(AsistenteCampoRequest req, CancellationToken ct = default);

    /// <summary>Asegura que el agente Auxiliar Administrativo exista para el tenant activo; devuelve su id.</summary>
    Task<Guid?> EnsureAgenteAsync(CancellationToken ct = default);
}
