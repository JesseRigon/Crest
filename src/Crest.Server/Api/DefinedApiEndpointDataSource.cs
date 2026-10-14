using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Crest.Api;

/// <summary>
/// The registry's defined layer (docs/MVC/api.md P15): the tenant's endpoints that come from data.
/// One instance per tenant container, added to the tenant's own route builder, so a tenant only
/// ever serves the entries its own sources returned. <see cref="Invalidate"/> (a publish) reloads
/// the sources after the current shell scope has completed, so the reload reads committed data,
/// then swaps the endpoints and signals routing through the change token.
/// </summary>
internal sealed class DefinedApiEndpointDataSource(
    IShellHost shellHost,
    ShellSettings shellSettings,
    ILogger<DefinedApiEndpointDataSource> logger) : EndpointDataSource, IDefinedApiInvalidator
{
    private readonly Lock _lock = new();
    private IReadOnlyList<DefinedApiEntry> _entries = [];
    private IReadOnlyList<Endpoint> _endpoints = [];
    private CancellationTokenSource _changes = new();
    private HashSet<string> _keys = new(StringComparer.Ordinal);
    private bool _reloadPending;

    public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

    public IReadOnlyList<DefinedApiEntry> Entries => _entries;

    public override IChangeToken GetChangeToken() => new CancellationChangeToken(_changes.Token);

    public void Invalidate(string source)
    {
        lock (_lock)
        {
            if (_reloadPending)
            {
                return;
            }

            _reloadPending = true;
        }

        if (ShellScope.Current is { } scope && scope.ShellContext.Settings.Name == shellSettings.Name)
        {
            scope.AddDeferredTask(deferred => ReloadAsync(deferred.ServiceProvider));
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var tenantScope = await shellHost.GetScopeAsync(shellSettings);
                await tenantScope.UsingAsync(scope => ReloadAsync(scope.ServiceProvider));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reloading the defined API entries of tenant '{Tenant}' failed.", shellSettings.Name);
            }
        });
    }

    /// <summary>Loads every source of the tenant and swaps the endpoints.</summary>
    public async Task ReloadAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _reloadPending = false;
        }

        var entries = new List<DefinedApiEntry>();
        foreach (var source in services.GetServices<IDefinedApiSource>())
        {
            try
            {
                entries.AddRange(await source.GetEntriesAsync(cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One failing source must not take the tenant's other defined endpoints down.
                logger.LogError(exception, "The defined API source '{Source}' failed to load.", source.Name);
            }
        }

        Swap(entries);
    }

    internal void Swap(IReadOnlyList<DefinedApiEntry> entries)
    {
        // A reload that yields the same routes (a run suspended at a route already served) leaves
        // the tenant's endpoints alone, so routing does not rebuild its matcher for nothing.
        var keys = entries.Select(entry => $"{entry.Source}|{entry.Id}|{entry.Order}|{string.Join(',', entry.HttpMethods)}|{entry.RoutePattern}").ToHashSet(StringComparer.Ordinal);
        lock (_lock)
        {
            if (_endpoints.Count > 0 && keys.SetEquals(_keys))
            {
                return;
            }
        }

        var endpoints = new List<Endpoint>(entries.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var key = string.Join(',', entry.HttpMethods.Order(StringComparer.OrdinalIgnoreCase)) + " " + entry.RoutePattern;
            if (!seen.Add(key))
            {
                continue;
            }

            RoutePattern pattern;
            try
            {
                pattern = RoutePatternFactory.Parse(entry.RoutePattern);
            }
            catch (RoutePatternException exception)
            {
                logger.LogWarning(exception, "Ignoring the defined API entry '{Id}' of '{Source}': invalid route '{Route}'.", entry.Id, entry.Source, entry.RoutePattern);
                continue;
            }

            var builder = new RouteEndpointBuilder(entry.Handler, pattern, entry.Order)
            {
                DisplayName = $"{entry.Source}: {entry.RoutePattern}",
            };

            if (entry.HttpMethods.Count > 0)
            {
                builder.Metadata.Add(new HttpMethodMetadata(entry.HttpMethods));
            }

            builder.Metadata.Add(entry);
            foreach (var metadata in entry.Metadata ?? [])
            {
                builder.Metadata.Add(metadata);
            }

            endpoints.Add(builder.Build());
        }

        CancellationTokenSource previous;
        lock (_lock)
        {
            _entries = entries;
            _endpoints = endpoints;
            _keys = keys;
            previous = _changes;
            _changes = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }
}
