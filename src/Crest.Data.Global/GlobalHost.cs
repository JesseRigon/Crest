using System.Runtime.CompilerServices;
using Crest.Data.Global.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Crest.Data.Global;

/// <summary>
/// The one tenant-less store per host. The <c>Crest.Data.Global</c> feature registers it in each
/// tenant container that enables the feature, so a host needs no call of its own; every
/// tenant of a host resolves the same instance, built on first use from host-level services
/// only (configuration, shell options, logging), never kept on a tenant's container.
/// </summary>
/// <remarks>
/// Keyed by the host's <see cref="IHostApplicationLifetime"/> (a host singleton every tenant
/// container shares), so two hosts in one process - test hosts - never share a store.
/// </remarks>
internal static class GlobalHost
{
    private static readonly ConditionalWeakTable<object, Entry> s_hosts = [];
    private static readonly object s_noHost = new();

    public static GlobalStore Store(IServiceProvider services) => EntryFor(services).Store;

    public static GlobalCache Cache(IServiceProvider services) => EntryFor(services).Cache;

    private static Entry EntryFor(IServiceProvider services)
    {
        var lifetime = services.GetService<IHostApplicationLifetime>();

        return s_hosts.GetValue(lifetime ?? s_noHost, _ =>
        {
            var store = ActivatorUtilities.CreateInstance<GlobalStore>(services);

            // The only disposal the store ever gets: tenant containers come and go.
            lifetime?.ApplicationStopping.Register(store.Shutdown);

            // One cache per host, so a write through any tenant's Default-shell endpoint
            // invalidates every tenant's reads.
            return new Entry(store, ActivatorUtilities.CreateInstance<GlobalCache>(services, store));
        });
    }

    private sealed record Entry(GlobalStore Store, GlobalCache Cache);
}
