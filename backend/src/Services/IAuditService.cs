namespace PurviewOps.Api.Services;

public interface IAuditService
{
    void Write(string action, string correlationId, string tenantId, string details);
    IReadOnlyList<object> List();
}
