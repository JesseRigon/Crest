using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.ContentLocalization.Models;
using Grove.ContentLocalization.Records;
using Grove.ContentManagement.GraphQL;
using Grove.ContentManagement.GraphQL.Queries;
using Grove.Modules;

namespace Grove.ContentLocalization.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddInputObjectGraphType<LocalizationPart, LocalizationInputObjectType>();
        services.AddObjectGraphType<LocalizationPart, LocalizationQueryObjectType>();
        services.AddTransient<IIndexAliasProvider, LocalizationPartIndexAliasProvider>();
        services.AddWhereInputIndexPropertyProvider<LocalizedContentItemIndex>();
    }
}
