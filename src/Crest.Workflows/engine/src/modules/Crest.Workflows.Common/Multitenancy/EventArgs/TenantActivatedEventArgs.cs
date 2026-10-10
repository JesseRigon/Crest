namespace Crest.Workflows.Common.Multitenancy;

public record TenantActivatedEventArgs(Tenant Tenant, TenantScope TenantScope, CancellationToken CancellationToken) : TenantEventArgs(Tenant, TenantScope, CancellationToken);