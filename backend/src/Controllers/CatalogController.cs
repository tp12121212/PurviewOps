using Microsoft.AspNetCore.Mvc;
using PurviewOps.Api.Models;
using PurviewOps.Api.Services;

namespace PurviewOps.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController(IOperationCatalogService catalog, ICorrelationIdProvider correlationIdProvider) : ControllerBase
{
    [HttpGet("operations")]
    public ActionResult<ApiEnvelope<IReadOnlyList<OperationDefinition>>> GetOperations()
    {
        var correlationId = correlationIdProvider.GetOrCreate();
        return Ok(new ApiEnvelope<IReadOnlyList<OperationDefinition>>("1.0", correlationId, catalog.GetOperations(), [], []));
    }
}
