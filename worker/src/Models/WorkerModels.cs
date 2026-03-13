namespace PurviewOps.Worker.Models;

public enum WorkerAuthMode { Delegated, AppOnly }
public enum WorkerExecutionTarget { ExchangeOnline, SecurityCompliance }

public sealed record AdapterContext(string OperationName, Dictionary<string, object?> Parameters, string CorrelationId, string TenantId, string UserPrincipalName);
public sealed record AdapterResult(string OperationName, object Result, IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors);
