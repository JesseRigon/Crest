namespace Crest.Workflows.Common.Multitenancy;

public record TenantDeactivatedEventArgs(Tenant Tenant, TenantScope TenantScope, CancellationToken CancellationToken) : TenantEventArgs(Tenant, TenantScope, CancellationToken);