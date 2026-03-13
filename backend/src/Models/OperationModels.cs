namespace PurviewOps.Api.Models;

public enum AuthMode { Delegated, AppOnly }
public enum ExecutionTarget { ExchangeOnline, SecurityCompliance }

public sealed record OperationParameter(string Name, string Type, bool Required, string Description);

public sealed record OperationDefinition(
    string OperationId,
    string OperationName,
    string Phase,
    bool Supported,
    bool Dangerous,
    AuthMode AuthMode,
    ExecutionTarget ExecutionTarget,
    IReadOnlyList<OperationParameter> AllowedParameters,
    string Notes);
