using Propia.Application.Common;

namespace Propia.Infrastructure.Persistence;

/// <summary>
/// Impl scoped de IAgentCallContext: simple portador de valores por request. A diferencia de
/// TenantContext (AsyncLocal, para cruzar el pool de conexiones), aqui basta un campo scoped porque
/// el middleware lo fija al inicio del request /mcp y la tool lo lee en el mismo scope.
/// </summary>
public sealed class AgentCallContext : IAgentCallContext
{
    public string? ContactPhone { get; private set; }
    public Guid? ConversationId { get; private set; }

    public void Set(string? contactPhone, Guid? conversationId)
    {
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone;
        ConversationId = conversationId;
    }
}
