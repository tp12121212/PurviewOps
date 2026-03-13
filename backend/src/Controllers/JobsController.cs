using Microsoft.AspNetCore.Mvc;
using PurviewOps.Api.Models;
using PurviewOps.Api.Services;

namespace PurviewOps.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(IOperationCatalogService catalog, IJobStore jobs, IAuditService audit, ICorrelationIdProvider correlationIdProvider) : ControllerBase
{
    [HttpPost("execute")]
    public ActionResult<ApiEnvelope<JobRecord>> Execute([FromBody] JobExecuteRequest request)
    {
        var correlationId = correlationIdProvider.GetOrCreate();
        var operation = catalog.FindByName(request.OperationName);
        if (operation is null)
        {
            return BadRequest(new ApiEnvelope<object>("1.0", correlationId, new { }, [], ["Unknown operation."]));
        }

        if (!operation.Supported)
        {
            return BadRequest(new ApiEnvelope<object>("1.0", correlationId, new { }, [], [$"{request.OperationName} is retired/unsupported in Exchange Online."]));
        }

        var job = jobs.Create(request, operation, correlationId);
        audit.Write("job-submitted", correlationId, request.TenantContext.TenantId, request.OperationName);
        return Accepted(new ApiEnvelope<JobRecord>("1.0", correlationId, job, [], []));
    }

    [HttpGet("{id:guid}")]
    public ActionResult<ApiEnvelope<JobRecord>> GetJob(Guid id)
    {
        var correlationId = correlationIdProvider.GetOrCreate();
        var job = jobs.Get(id);
        if (job is null)
        {
            return NotFound(new ApiEnvelope<object>("1.0", correlationId, new { }, [], ["Job not found."]));
        }

        return Ok(new ApiEnvelope<JobRecord>("1.0", correlationId, job, [], []));
    }

    [HttpGet("{id:guid}/result")]
    public ActionResult<ApiEnvelope<object>> GetResult(Guid id)
    {
        var correlationId = correlationIdProvider.GetOrCreate();
        var job = jobs.Get(id);
        if (job is null)
        {
            return NotFound(new ApiEnvelope<object>("1.0", correlationId, new { }, [], ["Job not found."]));
        }

        return Ok(new ApiEnvelope<object>("1.0", correlationId, new
        {
            job.OperationId,
            job.OperationName,
            job.TenantContext,
            job.Status,
            job.StartedAt,
            job.CompletedAt,
            job.Result,
            job.Warnings,
            job.Errors,
            job.CorrelationId
        }, [], []));
    }
}
