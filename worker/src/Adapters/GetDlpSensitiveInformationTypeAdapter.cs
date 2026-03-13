using System.Text.Json;
using PurviewOps.Worker.Models;

namespace PurviewOps.Worker.Adapters;

public sealed class GetDlpSensitiveInformationTypeAdapter : IOperationAdapter
{
    public string OperationName => "Get-DlpSensitiveInformationType";
    public WorkerAuthMode AuthMode => WorkerAuthMode.Delegated;
    public WorkerExecutionTarget Target => WorkerExecutionTarget.SecurityCompliance;
    public IReadOnlyList<string> AllowedParameters => [];

    public string BuildPowerShell(AdapterContext context) =>
        "Connect-IPPSSession -EnableErrorReporting:$true; Get-DlpSensitiveInformationType | ConvertTo-Json -Depth 8 -Compress";

    public object Normalize(string rawResult)
    {
        using var doc = JsonDocument.Parse(rawResult);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return new List<object>();
        }

        return doc.RootElement
            .EnumerateArray()
            .Select(e => new
            {
                name = e.TryGetProperty("Name", out var n) ? n.GetString() : string.Empty,
                id = e.TryGetProperty("Id", out var i) ? i.GetString() : string.Empty
            })
            .OrderBy(e => e.name, StringComparer.Ordinal)
            .ToList();
    }
}
