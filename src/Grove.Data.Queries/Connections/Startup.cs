using Grove.Data.Queries.Connections;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Data.Queries.Connections;

/// <summary>Queries over connections: the operations of the engine's REST, GraphQL and SOAP connections as typed, paged rows.</summary>
[Feature(ConnectionQueriesFeature.Id)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddQuerySource<ConnectionQuerySource>(ConnectionQuerySource.SourceName);
        services.AddScoped<IQueryHandler, ConnectionQueryHandler>();
    }
}

public static class ConnectionQueriesFeature
{
    public const string Id = "Grove.Data.Queries.Connections";
}
