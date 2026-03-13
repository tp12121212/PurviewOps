namespace PurviewOps.Api.Models;

public enum JobStatus { Queued, Running, Succeeded, Failed, Rejected }

public sealed record TenantContext(string TenantId, string TenantDomain, string UserObjectId, string UserPrincipalName);

public sealed record JobExecuteRequest(string OperationName, Dictionary<string, object?> Parameters, TenantContext TenantContext);

public sealed record JobRecord(
    Guid JobId,
    string OperationId,
    string OperationName,
    TenantContext TenantContext,
    JobStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    object? Result,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    string CorrelationId);
