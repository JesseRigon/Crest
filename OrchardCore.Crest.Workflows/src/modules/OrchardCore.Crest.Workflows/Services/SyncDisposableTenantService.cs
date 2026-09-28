using Elsa.Common.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OrchardCore.Crest.Workflows.Services;

/// <summary>
/// Elsa's <see cref="DefaultTenantService"/> is a root singleton that implements only
/// <see cref="IAsyncDisposable"/>. Orchard disposes a tenant container synchronously when
/// a shell is released or, as a fallback, from the ShellContext finalizer; the service
/// provider then throws "type only implements IAsyncDisposable", and on the finalizer
/// thread that takes the whole process down (seen in the test suite on the first shell
/// reload). This subclass adds the synchronous path. The scan test in the test project
/// pins that this is the only async-only root service Elsa registers.
/// </summary>
public sealed class SyncDisposableTenantService(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopeFactory,
    TenantEventsManager tenantEvents,
    ITenantAccessor tenantAccessor)
    : DefaultTenantService(scopeFactory, tenantScopeFactory, tenantEvents, tenantAccessor), IDisposable
{
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>Swaps every registration of the Elsa type for this one, keeping the lifetime.</summary>
    public static void ReplaceIn(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ImplementationType == typeof(DefaultTenantService)).ToList())
        {
            services.Remove(descriptor);
            services.Add(new ServiceDescriptor(descriptor.ServiceType, typeof(SyncDisposableTenantService), descriptor.Lifetime));
        }
    }
}
