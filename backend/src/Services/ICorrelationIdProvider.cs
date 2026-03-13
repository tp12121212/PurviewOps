namespace PurviewOps.Api.Services;

public interface ICorrelationIdProvider
{
    string GetOrCreate();
}
