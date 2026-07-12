using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BaGetter.Git;

public sealed class GitRepositoryHealthCheck : IHealthCheck
{
    private readonly IGitRepositoryClient _repository;

    public GitRepositoryHealthCheck(IGitRepositoryClient repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var commitSha = await _repository.UpdateAsync(cancellationToken);
            return HealthCheckResult.Healthy($"Git repository is current at {commitSha}");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Git repository synchronization failed", exception);
        }
    }
}
