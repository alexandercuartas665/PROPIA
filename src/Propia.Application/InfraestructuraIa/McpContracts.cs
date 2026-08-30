namespace Propia.Application.InfraestructuraIa;

// =====================================================================================
// Contratos de la integracion Agente <-> servidores MCP (Capa MCP de 2.3 y futuras).
// El catalogo de CONEXIONES es estatico (infra del sistema); la lista de TOOLS de cada
// conexion se descubre en vivo del servidor MCP via tools/list. La seleccion por agente
// (que tools puede usar) se persiste en AiAgentMcpTool (tenant-scoped, RLS).
// =====================================================================================

/// <summary>
/// Una conexion MCP que el sistema conoce (ej. el servidor interno /mcp de 2.3).
/// PlatformOnly: la conexion es de PLATAFORMA (agentes del Super Admin, ej. bienvenida) y NO se
/// ofrece a los agentes de tenant. ToolPrefix: las tools de esta conexion se identifican por
/// prefijo de nombre (el servidor fisico /mcp es uno solo; el prefijo particiona el catalogo).
/// </summary>
public sealed record McpConnectionInfo(string Code, string DisplayName, string Description,
    bool PlatformOnly = false, string? ToolPrefix = null);

/// <summary>
/// Catalogo estatico de conexiones MCP disponibles. El servidor fisico es uno solo (/mcp); las
/// conexiones son particiones logicas del catalogo. "plataforma" agrupa las tools de los agentes
/// de PLATAFORMA (Super Admin, ej. el asistente de bienvenida) y sus tools llevan el prefijo
/// "plataforma_": esa conexion NUNCA se ofrece ni se despliega a agentes de tenant.
/// </summary>
public static class McpConnectionCatalog
{
    public const string Copropiedades = "copropiedades";
    public const string Plataforma = "plataforma";

    public static readonly IReadOnlyList<McpConnectionInfo> All = new[]
    {
        new McpConnectionInfo(
            Copropiedades,
            "MCP Copropiedades",
            "Consulta y gestion de la ficha de la copropiedad activa (unidades, contratos, zonas, equipos, finanzas, bitacora) y del modulo de Servicios: servicios contratados, polizas y servicios publicos (registrar consumo de recibos, crear cuentas, alertas)."),
        new McpConnectionInfo(
            Plataforma,
            "MCP Plataforma",
            "Tools de los agentes de PLATAFORMA (Super Admin): asistente de bienvenida y futuros. Operan sin copropiedad activa y jamas se ofrecen a los agentes de los tenants.",
            PlatformOnly: true,
            ToolPrefix: "plataforma_")
    };

    public static McpConnectionInfo? Find(string code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True si la tool (por nombre) pertenece a la conexion indicada: las conexiones con prefijo
    /// solo ven sus tools; el resto ve todo lo que NO tenga prefijo de otra conexion.
    /// </summary>
    public static bool ToolBelongsTo(McpConnectionInfo connection, string toolName)
    {
        if (!string.IsNullOrEmpty(connection.ToolPrefix))
            return toolName.StartsWith(connection.ToolPrefix, StringComparison.OrdinalIgnoreCase);
        return !All.Any(c => !string.IsNullOrEmpty(c.ToolPrefix)
            && toolName.StartsWith(c.ToolPrefix!, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Tool tal cual la expone un servidor MCP (descubierta en vivo via tools/list).</summary>
public sealed record McpToolInfo(string Name, string? Description, string? InputSchemaJson);

/// <summary>Tool de una conexion en el contexto de un agente: incluye si esta habilitada.</summary>
public sealed record AgentMcpToolDto(string Name, string? Description, bool Enabled);

/// <summary>Una conexion MCP con sus tools (en vivo) y el estado de seleccion del agente.</summary>
public sealed record AgentMcpConnectionDto(
    string Code,
    string DisplayName,
    string Description,
    bool Reachable,
    string? Error,
    IReadOnlyList<AgentMcpToolDto> Tools);

/// <summary>Seleccion de una tool concreta de una conexion.</summary>
public sealed record AgentMcpToolSelection(string ConnectionCode, string ToolName);

/// <summary>Cuerpo para guardar la seleccion completa de tools MCP de un agente (reemplaza todo).</summary>
public sealed record SaveAgentMcpToolsRequest(IReadOnlyList<AgentMcpToolSelection> Tools);

/// <summary>
/// Cliente de los servidores MCP que el sistema conoce. Habla el protocolo MCP sobre HTTP
/// contra el endpoint /mcp, presentando el bearer token del usuario (hereda tenant + permisos).
/// </summary>
public interface IMcpGateway
{
    /// <summary>Descubre en vivo las tools de una conexion MCP (tools/list). Lanza si no se puede contactar.</summary>
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(string connectionCode, string bearerToken, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta una tool MCP y devuelve el texto del resultado (tools/call). Usado por el runtime del
    /// agente (Fase B). contactPhone/conversationId viajan como headers server-set (X-Contact-Phone /
    /// X-Conversation-Id) para que tools como verificar_residencia usen el numero REAL de la
    /// conversacion sin exponerlo como argumento del LLM.
    /// </summary>
    Task<string> CallToolAsync(string connectionCode, string toolName, IReadOnlyDictionary<string, object?> arguments,
        string bearerToken, string? contactPhone = null, Guid? conversationId = null, CancellationToken ct = default);
}
