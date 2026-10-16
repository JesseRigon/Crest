using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Localization.Models;
using Grove.Contents.Localization.Records;
using Grove.Contents.Management.GraphQL;
using Grove.Contents.Management.GraphQL.Queries;
using Grove.Modules;

namespace Grove.Contents.Localization.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddInputObjectGraphType<LocalizationFieldSet, LocalizationInputObjectType>();
        services.AddObjectGraphType<LocalizationFieldSet, LocalizationQueryObjectType>();
        services.AddTransient<IIndexAliasProvider, LocalizationIndexAliasProvider>();
        services.AddWhereInputIndexPropertyProvider<LocalizedContentItemIndex>();
    }
}
