using Crest.Workflows.Extensions;
using Crest.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using Crest.Workflows.Contents.Handlers;
using Crest.Workflows.Contents.UIHints;
using OrchardCore.Modules;

namespace Crest.Workflows.Contents;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureCrestWorkflows(crestWorkflows =>
        {
            crestWorkflows.AddActivitiesFrom<Startup>();
            crestWorkflows.AddVariableTypeAndAlias<ContentItem>("ContentItem", "CMS");
        });

        services
            .AddScoped<IContentHandler, ContentEventHandler>()
            .AddScoped<IPropertyUIHandler, ContentTypeCheckListOptionsProvider>()
            .AddScoped<IPropertyUIHandler, ContentTypeOptionsProvider>();
    }
}
