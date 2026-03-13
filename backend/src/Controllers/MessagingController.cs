using Microsoft.AspNetCore.Mvc;
using PurviewOps.Api.Models;
using PurviewOps.Api.Services;

namespace PurviewOps.Api.Controllers;

[ApiController]
[Route("api/messaging")]
public sealed class MessagingController(ICorrelationIdProvider correlationIdProvider) : ControllerBase
{
    [HttpPost("test-message")]
    public ActionResult<ApiEnvelope<object>> TestMessage([FromBody] TestMessageRequest request) =>
        Accepted(new ApiEnvelope<object>("1.0", correlationIdProvider.GetOrCreate(), new { operation = "Test-Message", recipients = request.Recipients.Count }, [], []));
}
