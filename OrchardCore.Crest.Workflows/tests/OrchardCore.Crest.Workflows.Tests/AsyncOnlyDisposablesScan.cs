using System.Reflection;
using Elsa.Common.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Crest.Workflows.Services;
using Xunit;

namespace OrchardCore.Crest.Workflows.Tests;

// Orchard disposes a tenant container synchronously when a shell is released (and from
// the ShellContext finalizer as a fallback); a registered root service that implements
// only IAsyncDisposable makes that throw and, on the finalizer thread, kills the process
// - seen in the suite with Elsa.Common.Multitenancy.DefaultTenantService. This pins the
// set of such types in the Elsa assemblies we load, so an Elsa bump that adds one fails
// here instead of in production, and checks the replacement that CoreStartup applies.
public class AsyncOnlyDisposablesScan
{
    private static readonly string[] Assemblies =
    [
        "Elsa.Common", "Elsa.Mediator", "Elsa.Workflows.Core", "Elsa.Workflows.Management", "Elsa.Workflows.Runtime",
        "Elsa.Workflows.Runtime.Distributed", "Elsa.Http", "Elsa.Resilience", "Elsa.Expressions.JavaScript",
        "Elsa.Expressions.Liquid", "Elsa.Workflows.Api", "Elsa.Caching", "Elsa.Scheduling",
    ];

    [Fact]
    public void Only_the_known_Elsa_types_are_async_only_disposable()
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
        Assert.Equal(["Elsa.Common.Multitenancy.DefaultTenantService", "Elsa.Common.Multitenancy.TenantScope"], offenders);
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
