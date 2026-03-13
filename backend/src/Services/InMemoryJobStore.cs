using System.Collections.Concurrent;
using PurviewOps.Api.Models;

namespace PurviewOps.Api.Services;

public sealed class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, JobRecord> _jobs = new();

    public JobRecord Create(JobExecuteRequest request, OperationDefinition operation, string correlationId)
    {
        var job = new JobRecord(Guid.NewGuid(), operation.OperationId, operation.OperationName, request.TenantContext, JobStatus.Queued, DateTimeOffset.UtcNow, null, null, [], [], correlationId);
        _jobs[job.JobId] = job;
        return job;
    }

    public JobRecord? Get(Guid id) => _jobs.TryGetValue(id, out var record) ? record : null;

    public IReadOnlyList<JobRecord> List() => _jobs.Values.OrderByDescending(j => j.StartedAt).ToList();

    public JobRecord Update(JobRecord record)
    {
        _jobs[record.JobId] = record;
        return record;
    }
}
