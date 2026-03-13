using PurviewOps.Api.Services;
using Xunit;

namespace PurviewOps.Api.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void Operations_AreDeterministicallyOrdered()
    {
        var service = new OperationCatalogService();
        var operations = service.GetOperations();
        var sorted = operations.OrderBy(o => o.OperationName, StringComparer.Ordinal).Select(o => o.OperationName);
        Assert.Equal(sorted, operations.Select(o => o.OperationName));
    }

    [Fact]
    public void UnsupportedOperations_AreExplicitlyFlagged()
    {
        var service = new OperationCatalogService();
        var export = service.FindByName("Export-DlpPolicyCollection");
        Assert.NotNull(export);
        Assert.False(export!.Supported);
    }
}
