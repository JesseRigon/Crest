using System.Reflection;
using Grove.Workflows.Common.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Grove.Workflows.Services;
using Xunit;

namespace Grove.Workflows.Tests;

// Grove disposes a tenant container synchronously when a shell is released (and from
// the ShellContext finalizer as a fallback); a registered root service that implements
// only IAsyncDisposable makes that throw and, on the finalizer thread, kills the process
// - seen in the suite with Grove.Workflows.Common.Multitenancy.DefaultTenantService. This pins the
// set of such types in the Grove.Workflows assemblies we load, so an Grove.Workflows bump that adds one fails
// here instead of in production, and checks the replacement that CoreStartup applies.
public class AsyncOnlyDisposablesScan
{
    private static readonly string[] Assemblies =
    [
        "Grove.Workflows.Common", "Grove.Workflows.Mediator", "Grove.Workflows.Core", "Grove.Workflows.Management", "Grove.Workflows.Runtime",
        "Grove.Workflows.Runtime.Distributed", "Grove.Workflows.Http", "Grove.Workflows.Resilience", "Grove.Workflows.Expressions.JavaScript",
        "Grove.Workflows.Expressions.Liquid", "Grove.Workflows.Api", "Grove.Workflows.Caching", "Grove.Workflows.Scheduling",
    ];

    [Fact]
    public void Only_the_known_engine_types_are_async_only_disposable()
    {
        var offenders = Assemblies
            .Select(name => { try { return Assembly.Load(name); } catch { return null; } })
            .Where(assembly => assembly is not null)
            .SelectMany(assembly => { try { return assembly!.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t is not null).ToArray()!; } })
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && typeof(IAsyncDisposable).IsAssignableFrom(type)
                && !typeof(IDisposable).IsAssignableFrom(type)
                && !type.Name.Contains('<')) // compiler-generated async iterators are not services
            .Select(type => type!.FullName!)
            .OrderBy(name => name)
            .ToList();

        // DefaultTenantService is replaced by SyncDisposableTenantService in CoreStartup;
        // TenantScope is created per scope by the scope factory and never held by the root.
        Assert.Equal(["Grove.Workflows.Common.Multitenancy.DefaultTenantService", "Grove.Workflows.Common.Multitenancy.TenantScope"], offenders);
    }

    [Fact]
    public void ReplaceIn_swaps_the_registration_and_keeps_the_lifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantService, DefaultTenantService>();

        SyncDisposableTenantService.ReplaceIn(services);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ITenantService));
        Assert.Equal(typeof(SyncDisposableTenantService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(SyncDisposableTenantService)));
    }
}
