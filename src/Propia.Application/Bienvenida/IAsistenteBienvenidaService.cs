namespace Propia.Application.Bienvenida;

/// <summary>
/// Asistente del onboarding de bienvenida (/bienvenida). Es un agente de PLATAFORMA: su
/// definicion vive en una AiAgentTemplate con PlatformKey="bienvenida" (prompt, proveedor,
/// modelo y tools editables por el Super Admin en Plantillas de agentes IA) y las credenciales
/// salen de la config global AiProviderConfigs - nunca de un tenant. Sus tools MCP van por la
/// conexion "plataforma" (PlatformOnly), invisible para los agentes de los tenants.
/// bearerToken: JWT del usuario (puede no tener tenant); habilita la ejecucion de tools.
/// </summary>
public interface IAsistenteBienvenidaService
{
    Task<BienvenidaChatDto> ResponderAsync(BienvenidaChatRequest req, string? bearerToken, CancellationToken ct);
    Task<BienvenidaChatDto> GenerarDescripcionAsync(BienvenidaDescripcionRequest req, CancellationToken ct);

    /// <summary>
    /// Playground del Super Admin: conversa con una plantilla de PLATAFORMA por id (prompt y
    /// tools tal como estan GUARDADOS). Rechaza plantillas normales (no son ejecutables sin
    /// tenant: sus tools solo se siembran al desplegar). Funciona aunque este inactiva.
    /// </summary>
    Task<BienvenidaChatDto> ProbarAsync(Guid templateId, List<BienvenidaTurno> conversacion, string? bearerToken, CancellationToken ct);
}

/// <summary>Un turno de la conversacion. Rol: "user" o "model".</summary>
public sealed record BienvenidaTurno(string Rol, string Texto);

/// <summary>
/// Paso: indice 0..4 del recorrido (Bienvenida, Tu perfil, Co-propiedad, Estructura, Personas).
/// ContextoPaso: resumen en texto de lo que el usuario lleva diligenciado (lo arma la UI).
/// </summary>
public sealed record BienvenidaChatRequest(
    int Paso,
    string? NombreUsuario,
    string? ContextoPaso,
    List<BienvenidaTurno> Conversacion);

public sealed record BienvenidaChatDto(bool Ok, string? Texto, string? Error);

/// <summary>Datos de la ficha para redactar la descripcion breve de la copropiedad.</summary>
public sealed record BienvenidaDescripcionRequest(
    string Nombre,
    string? Tipo,
    string? Ciudad,
    string? Departamento,
    string? Estrato,
    string? TextoActual);

/// <summary>Prompt de sistema del agente de bienvenida (propiedad de la plataforma).</summary>
public static class BienvenidaPrompts
{
    public const string Sistema = """
        Eres el Auxiliar de PROPIA, el asistente de bienvenida de la plataforma PROPIA (software
        colombiano de administracion de copropiedades / propiedad horizontal). Acompanas a un usuario
        nuevo mientras crea su primera copropiedad en un recorrido de 5 pasos:
        1) Bienvenida: le preguntamos su nombre.
        2) Tu perfil: elige entre empresa administradora o administrador independiente.
        3) Co-propiedad: la ficha (nombre obligatorio; tipo, NIT con DV automatico, ubicacion,
           contacto, descripcion y logo son opcionales).
        4) Estructura: torres y unidades por plantilla Excel, o dejarlo para despues.
        5) Personas: propietarios y residentes por plantilla Excel, o dejarlo para despues.
        Al confirmar el ultimo paso la copropiedad queda creada y el usuario entra al producto.

        Reglas:
        - Responde SIEMPRE en espanol con acentos correctos, calido, concreto y breve (maximo 3 frases).
        - Nada del recorrido es destructivo: cada paso solo crea datos y todo se edita despues en Mi Copropiedad.
        - Solo el nombre de la copropiedad es obligatorio; cualquier otro dato se puede omitir.
        - Las plantillas Excel se descargan con los botones del paso; tu no puedes adjuntar archivos.
        - Si preguntan por temas del producto fuera del recorrido (cuotas, finanzas, reservas, porteria),
          responde en una frase y aclara que eso se configura despues, dentro del producto.
        - No inventes datos del usuario ni de la plataforma; si no sabes algo, dilo con franqueza.
        """;

    public const string Descripcion = """
        Eres un redactor de PROPIA (software colombiano de administracion de copropiedades).
        Redacta UNA sola frase (maximo 40 palabras) que presente una copropiedad en su ficha publica:
        calida, profesional, en espanol con acentos correctos, sin comillas ni saltos de linea.
        Usa solo los datos entregados; no inventes amenidades ni cifras.
        """;
}
