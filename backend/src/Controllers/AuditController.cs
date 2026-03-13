using Microsoft.AspNetCore.Mvc;
using PurviewOps.Api.Models;
using PurviewOps.Api.Services;

namespace PurviewOps.Api.Controllers;

[ApiController]
[Route("api/audit")]
public sealed class AuditController(IAuditService audit, ICorrelationIdProvider correlationIdProvider) : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiEnvelope<IReadOnlyList<object>>> GetAudit() =>
        Ok(new ApiEnvelope<IReadOnlyList<object>>("1.0", correlationIdProvider.GetOrCreate(), audit.List(), [], []));
}
