using Microsoft.AspNetCore.Mvc;
using PurviewOps.Api.Models;
using PurviewOps.Api.Services;

namespace PurviewOps.Api.Controllers;

[ApiController]
[Route("api/classification")]
public sealed class ClassificationController(ICorrelationIdProvider correlationIdProvider) : ControllerBase
{
    [HttpPost("test-file")]
    public ActionResult<ApiEnvelope<object>> TestFile([FromBody] TestFileRequest request)
    {
        var cid = correlationIdProvider.GetOrCreate();
        if (request.Content.Length == 0)
        {
            return BadRequest(new ApiEnvelope<object>("1.0", cid, new { }, [], ["File is empty."]));
        }

        return Accepted(new ApiEnvelope<object>("1.0", cid, new { operation = "Test-TextExtraction", request.FileName }, ["Ephemeral file storage required in worker pipeline."], []));
    }

    [HttpPost("test-text")]
    public ActionResult<ApiEnvelope<object>> TestText([FromBody] TestTextRequest request) =>
        Accepted(new ApiEnvelope<object>("1.0", correlationIdProvider.GetOrCreate(), new { operation = "Test-DataClassification", length = request.Text.Length }, [], []));
}
