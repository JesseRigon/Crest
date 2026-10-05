using System.Collections;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Components.Modules;

/// <summary>
/// Service containers attached after startup: a module whose client library is loaded on
/// demand (lazy assemblies) builds its own container when its first page opens and attaches
/// it here. Components from that library are created by the app's renderer, so the app's
/// provider falls back to these containers for
/// anything it does not have itself (Crest.AdminTheme's CrestServiceProviderFactory). The app's own
/// registrations always win.
/// </summary>
public sealed class CrestLateServiceProviders
{
    private readonly ConcurrentDictionary<string, IServiceProvider> _providers = new();

    /// <summary>Attaches a module's container once (by key); returns the one attached first.</summary>
    public IServiceProvider Attach(string key, Func<IServiceProvider> build) => _providers.GetOrAdd(key, _ => build());

    public bool IsAttached(string key) => _providers.ContainsKey(key);

    /// <summary>The first attached container's service of this type (a non-empty sequence for IEnumerable requests).</summary>
    public object? Resolve(Type serviceType)
    {
        foreach (var provider in _providers.Values)
        {
            var service = provider.GetService(serviceType);
            if (service is not null && !(IsEnumerable(serviceType) && IsEmpty(service)))
            {
                return service;
            }
        }

        return null;
    }

    public bool IsService(Type serviceType) =>
        _providers.Values.Any(provider => provider.GetService<IServiceProviderIsService>()?.IsService(serviceType) == true);

    public static bool IsEnumerable(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);

    private static bool IsEmpty(object service) => service is IEnumerable sequence && !sequence.GetEnumerator().MoveNext();
}
