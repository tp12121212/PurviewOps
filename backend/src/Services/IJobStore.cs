using PurviewOps.Api.Models;

namespace PurviewOps.Api.Services;

public interface IJobStore
{
    JobRecord Create(JobExecuteRequest request, OperationDefinition operation, string correlationId);
    JobRecord? Get(Guid id);
    IReadOnlyList<JobRecord> List();
    JobRecord Update(JobRecord record);
}
