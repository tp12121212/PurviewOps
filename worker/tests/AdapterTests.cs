using PurviewOps.Worker.Adapters;
using PurviewOps.Worker.Models;
using Xunit;

namespace PurviewOps.Worker.Tests;

public sealed class AdapterTests
{
    [Fact]
    public void BuildPowerShell_DoesNotAllowArbitraryCmdletInjection()
    {
        var adapter = new GetDlpSensitiveInformationTypeAdapter();
        var script = adapter.BuildPowerShell(new AdapterContext(adapter.OperationName, new Dictionary<string, object?>(), "cid", "tenant", "user@contoso.com"));
        Assert.DoesNotContain("Invoke-Expression", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get-DlpSensitiveInformationType", script, StringComparison.Ordinal);
    }
}
