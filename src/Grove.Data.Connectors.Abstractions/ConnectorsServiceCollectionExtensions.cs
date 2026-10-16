using Grove.Data.Connectors;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class ConnectorsServiceCollectionExtensions
{
    /// <summary>Declares a connection in code (the registry's declared layer).</summary>
    public static IServiceCollection AddConnection(this IServiceCollection services, string key, Action<ConnectionBuilder> configure)
    {
        var builder = new ConnectionBuilder(key);
        configure(builder);
        return services.AddConnection(builder.Build());
    }

    public static IServiceCollection AddConnection(this IServiceCollection services, ConnectionDefinition definition)
    {
        services.AddSingleton(new ConnectionDeclaration(definition));
        return services;
    }

    /// <summary>Registers a protocol module's <see cref="IConnectorProtocol"/>.</summary>
    public static IServiceCollection AddConnectorProtocol<TProtocol>(this IServiceCollection services)
        where TProtocol : class, IConnectorProtocol
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConnectorProtocol, TProtocol>());
        return services;
    }

    public static IServiceCollection AddWebhookSignatureScheme<TScheme>(this IServiceCollection services)
        where TScheme : class, IWebhookSignatureScheme
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebhookSignatureScheme, TScheme>());
        return services;
    }

    /// <summary>Adds a credential scope (beside tenant and user).</summary>
    public static IServiceCollection AddCredentialScope<THandler>(this IServiceCollection services)
        where THandler : class, ICredentialScopeHandler
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICredentialScopeHandler, THandler>());
        return services;
    }
}
