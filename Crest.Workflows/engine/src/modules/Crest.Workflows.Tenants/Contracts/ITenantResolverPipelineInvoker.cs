using Crest.Workflows.Common.Multitenancy;

namespace Crest.Workflows.Tenants;

public interface ITenantResolverPipelineInvoker
{
    Task<Tenant?> InvokePipelineAsync(CancellationToken cancellationToken = default);
}