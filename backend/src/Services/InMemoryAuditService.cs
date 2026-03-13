using System.Collections.Concurrent;

namespace PurviewOps.Api.Services;

public sealed class InMemoryAuditService : IAuditService
{
    private readonly ConcurrentQueue<object> _events = new();

    public void Write(string action, string correlationId, string tenantId, string details)
    {
        _events.Enqueue(new
        {
            action,
            correlationId,
            tenantId,
            details,
            timestampUtc = DateTimeOffset.UtcNow
        });
    }

    public IReadOnlyList<object> List() => _events.Reverse().ToList();
}
