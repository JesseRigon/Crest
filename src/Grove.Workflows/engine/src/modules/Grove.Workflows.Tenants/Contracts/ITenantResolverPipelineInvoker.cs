using Grove.Workflows.Common.Multitenancy;

namespace Grove.Workflows.Tenants;

public interface ITenantResolverPipelineInvoker
{
    Task<Tenant?> InvokePipelineAsync(CancellationToken cancellationToken = default);
}