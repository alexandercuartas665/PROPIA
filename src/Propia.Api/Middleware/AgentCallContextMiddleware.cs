using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Propia.Application.Common;
using Propia.Infrastructure.InfraestructuraIa;

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
        // S-13: solo confiamos en X-Contact-Phone / X-Conversation-Id si quien llama a /mcp es el
        // principal de servicio del dispatcher (sub == DispatcherServiceUserId). Antes se aceptaban
        // de CUALQUIER cliente con JWT valido, permitiendo impersonar a otro contacto/conversacion
        // (p.ej. verificar_residencia sobre el telefono de un tercero) inyectando estos headers.
        string? phone = null;
        Guid? conversationId = null;
        if (EsPrincipalDispatcher(context.User))
        {
            phone = context.Request.Headers["X-Contact-Phone"].FirstOrDefault();
            conversationId = Guid.TryParse(context.Request.Headers["X-Conversation-Id"].FirstOrDefault(), out var g) ? g : null;
        }

        // X-Agent-Id (Ola H): a diferencia de telefono/conversacion, NO es un vector de suplantacion ni de
        // acceso a datos; solo SELECCIONA que config del MISMO tenant aplica la tool (whitelist de tableros,
        // reparto round-robin), y la whitelist solo RESTRINGE. La RLS sigue acotando todo al tenant del JWT.
        // Por eso se acepta de cualquier llamante autenticado, para que el playground tambien lo ejercite.
        Guid? agentId = Guid.TryParse(context.Request.Headers["X-Agent-Id"].FirstOrDefault(), out var aid) ? aid : null;

        if (!string.IsNullOrWhiteSpace(phone) || conversationId is not null || agentId is not null)
        {
            call.Set(phone, conversationId, agentId);
        }

        await _next(context);
    }

    private static bool EsPrincipalDispatcher(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;
        var sub = user.FindFirstValue("user_id")
                  ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) && id == AgentDispatcher.DispatcherServiceUserId;
    }
}
