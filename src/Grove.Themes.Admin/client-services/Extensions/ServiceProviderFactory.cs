using System.Collections;
using Grove.Display.Components.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Themes.Admin;

/// <summary>
/// The WASM host's service provider factory: the usual container, wrapped so that a request
/// it cannot satisfy (null, or an empty sequence) is tried against the containers attached
/// to <see cref="GroveLateServiceProviders"/>. Scopes are wrapped the same way.
/// </summary>
public sealed class ServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
{
    public IServiceCollection CreateBuilder(IServiceCollection services) => services;

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        var late = new GroveLateServiceProviders();
        containerBuilder.AddSingleton(late);
        return new CompositeServiceProvider(containerBuilder.BuildServiceProvider(), late);
    }
}

internal sealed class CompositeServiceProvider(IServiceProvider inner, GroveLateServiceProviders late)
    : IServiceProvider, IServiceScopeFactory, IServiceProviderIsService, IKeyedServiceProvider, IDisposable, IAsyncDisposable
{
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory) || serviceType == typeof(IServiceProviderIsService) || serviceType == typeof(IKeyedServiceProvider))
        {
            return this;
        }

        var service = inner.GetService(serviceType);
        if (service is null || (GroveLateServiceProviders.IsEnumerable(serviceType) && !((IEnumerable)service).GetEnumerator().MoveNext()))
        {
            return late.Resolve(serviceType) ?? service;
        }

        return service;
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey) => (inner as IKeyedServiceProvider)?.GetKeyedService(serviceType, serviceKey);

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
        GetKeyedService(serviceType, serviceKey) ?? throw new InvalidOperationException($"No service for type '{serviceType}' with key '{serviceKey}'.");

    public IServiceScope CreateScope() => new Scope(inner.CreateScope(), late);

    public bool IsService(Type serviceType) =>
        inner.GetService<IServiceProviderIsService>()?.IsService(serviceType) == true || late.IsService(serviceType);

    public void Dispose() => (inner as IDisposable)?.Dispose();

    public ValueTask DisposeAsync() => inner is IAsyncDisposable disposable ? disposable.DisposeAsync() : ValueTask.CompletedTask;

    private sealed class Scope(IServiceScope scope, GroveLateServiceProviders late) : IServiceScope, IAsyncDisposable
    {
        public IServiceProvider ServiceProvider { get; } = new CompositeServiceProvider(scope.ServiceProvider, late);

        public void Dispose() => scope.Dispose();

        public ValueTask DisposeAsync() => scope is IAsyncDisposable disposable ? disposable.DisposeAsync() : ValueTask.CompletedTask;
    }
}
