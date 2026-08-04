using Propia.Application.Common;

namespace Propia.Api.Middleware;

/// <summary>
/// Deposita en IAgentCallContext (scoped) el telefono real del contacto y la conversacion que el
/// McpGateway envia como headers server-set (X-Contact-Phone / X-Conversation-Id) al llamar a /mcp.
/// Asi una tool MCP (ej. verificar_residencia) accede al numero REAL de la conversacion sin que sea
/// un argumento del LLM. Los headers los pone SOLO nuestro gateway desde conv.ContactPhone (el LLM no
/// puede fijarlos), por eso son de confianza. Debe correr DESPUES de TenantMiddleware (mismo scope).
/// </summary>
public sealed class AgentCallContextMiddleware
{
    private readonly RequestDelegate _next;

    public AgentCallContextMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context, IAgentCallContext call)
    {
        var phone = context.Request.Headers["X-Contact-Phone"].FirstOrDefault();
        Guid? conversationId = Guid.TryParse(context.Request.Headers["X-Conversation-Id"].FirstOrDefault(), out var g)
            ? g : null;

        if (!string.IsNullOrWhiteSpace(phone) || conversationId is not null)
        {
            call.Set(phone, conversationId);
        }

        await _next(context);
    }
}
