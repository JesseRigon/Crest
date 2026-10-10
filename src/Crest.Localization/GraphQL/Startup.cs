using Microsoft.Extensions.DependencyInjection;
using Crest.Apis.GraphQL;
using Crest.Modules;

namespace Crest.Localization.GraphQL;

/// <summary>
/// Represents the localization module entry point for Graph QL.
/// </summary>
[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    /// <inheritdocs />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SiteCulturesQuery>();
        services.AddTransient<CultureQueryObjectType>();
    }
}
