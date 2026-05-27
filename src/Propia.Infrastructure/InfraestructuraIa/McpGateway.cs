using System.Text;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Propia.Application.InfraestructuraIa;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Cliente de los servidores MCP que el sistema conoce. Habla el protocolo MCP (Streamable HTTP)
/// contra el endpoint /mcp, presentando el bearer token del usuario: asi hereda el tenant (RLS) y
/// los permisos (RBAC). Por ahora todas las conexiones del catalogo apuntan al servidor interno
/// (misma API). La base url es configurable (Mcp:InternalBaseUrl), default https://localhost:7205.
///
/// El HttpClient "Mcp" (registrado en DI) acepta el certificado self-signed SOLO para localhost
/// (dev); contra cualquier host real exige certificado valido.
/// </summary>
public sealed class McpGateway : IMcpGateway
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _baseUrl;

    public McpGateway(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _baseUrl = (config["Mcp:InternalBaseUrl"] ?? "https://localhost:7205").TrimEnd('/');
    }

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(string connectionCode, string bearerToken, CancellationToken ct = default)
    {
        await using var client = await ConnectAsync(connectionCode, bearerToken, ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return tools
            .Select(t => new McpToolInfo(t.Name, t.Description, t.JsonSchema.GetRawText()))
            .ToList();
    }

    public async Task<string> CallToolAsync(string connectionCode, string toolName, IReadOnlyDictionary<string, object?> arguments, string bearerToken, CancellationToken ct = default)
    {
        await using var client = await ConnectAsync(connectionCode, bearerToken, ct);
        // El SDK espera IReadOnlyDictionary<string, object> (sin null); filtramos los nulos.
        var args = arguments
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value!);
        var result = await client.CallToolAsync(toolName, args, cancellationToken: ct);

        var sb = new StringBuilder();
        foreach (var block in result.Content)
        {
            if (block is TextContentBlock tb) { sb.AppendLine(tb.Text); }
        }
        var text = sb.ToString().Trim();

        if (result.IsError == true)
        {
            throw new InvalidOperationException(text.Length > 0 ? text : $"La tool MCP '{toolName}' devolvio un error.");
        }
        return text;
    }

    private async Task<McpClient> ConnectAsync(string connectionCode, string bearerToken, CancellationToken ct)
    {
        if (McpConnectionCatalog.Find(connectionCode) is null)
        {
            throw new InvalidOperationException($"Conexion MCP desconocida: {connectionCode}");
        }

        var http = _httpFactory.CreateClient("Mcp");
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri($"{_baseUrl}/mcp"),
            Name = $"propia-{connectionCode}",
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" }
        }, http, null, false);

        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }
}
