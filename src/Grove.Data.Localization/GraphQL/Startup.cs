using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Protocols.GraphQL;
using Grove.Modules;

namespace Grove.Data.Localization.GraphQL;

/// <summary>
/// Represents the localization module entry point for Graph QL.
/// </summary>
[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    /// <inheritdocs />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SiteCulturesQuery>();
        services.AddTransient<CultureQueryObjectType>();
    }
}
