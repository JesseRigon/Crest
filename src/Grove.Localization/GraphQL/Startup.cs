using Microsoft.Extensions.DependencyInjection;
using Grove.Apis.GraphQL;
using Grove.Modules;

namespace Grove.Localization.GraphQL;

/// <summary>
/// Represents the localization module entry point for Graph QL.
/// </summary>
[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    /// <inheritdocs />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SiteCulturesQuery>();
        services.AddTransient<CultureQueryObjectType>();
    }
}
