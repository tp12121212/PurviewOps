using PurviewOps.Worker.Adapters;

namespace PurviewOps.Worker.Services;

public sealed class AdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IOperationAdapter> _adapters;

    public AdapterRegistry()
    {
        var adapters = new IOperationAdapter[]
        {
            new GetDlpSensitiveInformationTypeAdapter()
        };
        _adapters = adapters.ToDictionary(a => a.OperationName, StringComparer.Ordinal);
    }

    public bool TryGet(string operationName, out IOperationAdapter adapter) => _adapters.TryGetValue(operationName, out adapter!);
}
