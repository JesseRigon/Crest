namespace Grove.Workflows.Common.Multitenancy;

public interface ITenantFinder
{
    Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken = default);
}