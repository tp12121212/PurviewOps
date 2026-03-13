using PurviewOps.Api.Models;

namespace PurviewOps.Api.Services;

public interface IOperationCatalogService
{
    IReadOnlyList<OperationDefinition> GetOperations();
    OperationDefinition? FindByName(string operationName);
}
