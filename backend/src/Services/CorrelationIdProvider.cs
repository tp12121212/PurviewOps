namespace PurviewOps.Api.Services;

public sealed class CorrelationIdProvider(IHttpContextAccessor accessor) : ICorrelationIdProvider
{
    public string GetOrCreate()
    {
        var context = accessor.HttpContext;
        if (context is null)
        {
            return Guid.NewGuid().ToString("N");
        }

        if (context.Items.TryGetValue("CorrelationId", out var value) && value is string existing)
        {
            return existing;
        }

        var incoming = context.Request.Headers["x-correlation-id"].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString("N") : incoming;
        context.Items["CorrelationId"] = correlationId;
        return correlationId;
    }
}
