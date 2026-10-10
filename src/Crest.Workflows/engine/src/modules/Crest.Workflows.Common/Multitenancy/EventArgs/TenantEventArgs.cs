namespace Crest.Workflows.Common.Multitenancy;

public record TenantEventArgs(Tenant Tenant, TenantScope TenantScope, CancellationToken CancellationToken);