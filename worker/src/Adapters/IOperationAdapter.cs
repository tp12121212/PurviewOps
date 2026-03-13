using PurviewOps.Worker.Models;

namespace PurviewOps.Worker.Adapters;

public interface IOperationAdapter
{
    string OperationName { get; }
    WorkerAuthMode AuthMode { get; }
    WorkerExecutionTarget Target { get; }
    IReadOnlyList<string> AllowedParameters { get; }
    string BuildPowerShell(AdapterContext context);
    object Normalize(string rawResult);
}
