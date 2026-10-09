using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.ContentLocalization.Models;
using Crest.ContentLocalization.Records;
using Crest.ContentManagement.GraphQL;
using Crest.ContentManagement.GraphQL.Queries;
using Crest.Modules;

namespace Crest.ContentLocalization.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
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
