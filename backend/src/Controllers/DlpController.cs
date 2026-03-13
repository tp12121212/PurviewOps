using Microsoft.AspNetCore.Mvc;
using PurviewOps.Api.Models;
using PurviewOps.Api.Services;

namespace PurviewOps.Api.Controllers;

[ApiController]
[Route("api/dlp")]
public sealed class DlpController(ICorrelationIdProvider correlationIdProvider) : ControllerBase
{
    [HttpGet("policies")]
    public ActionResult<ApiEnvelope<object>> Policies() =>
        Ok(new ApiEnvelope<object>("1.0", correlationIdProvider.GetOrCreate(), new { source = "async job endpoint", operation = "Get-DlpCompliancePolicy" }, [], []));

    [HttpGet("rules")]
    public ActionResult<ApiEnvelope<object>> Rules() =>
        Ok(new ApiEnvelope<object>("1.0", correlationIdProvider.GetOrCreate(), new { source = "async job endpoint", operation = "Get-DlpComplianceRule" }, [], []));

    [HttpGet("sits")]
    public ActionResult<ApiEnvelope<object>> Sits() =>
        Ok(new ApiEnvelope<object>("1.0", correlationIdProvider.GetOrCreate(), new { source = "async job endpoint", operation = "Get-DlpSensitiveInformationType" }, [], []));
}
