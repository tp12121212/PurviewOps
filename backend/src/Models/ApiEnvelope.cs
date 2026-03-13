namespace PurviewOps.Api.Models;

public sealed record ApiEnvelope<T>(
    string SchemaVersion,
    string CorrelationId,
    T Data,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
