using Elsa.Extensions;
using Elsa.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Crest.Workflows.Contents.Handlers;
using OrchardCore.Crest.Workflows.Contents.UIHints;
using OrchardCore.Modules;

namespace OrchardCore.Crest.Workflows.Contents;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureElsa(elsa =>
        {
            elsa.AddActivitiesFrom<Startup>();
            elsa.AddVariableTypeAndAlias<ContentItem>("ContentItem", "CMS");
        });

        services
            .AddScoped<IContentHandler, ContentEventHandler>()
            .AddScoped<IPropertyUIHandler, ContentTypeCheckListOptionsProvider>()
            .AddScoped<IPropertyUIHandler, ContentTypeOptionsProvider>();
    }
}
