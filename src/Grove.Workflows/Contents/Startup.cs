using Grove.Workflows.Extensions;
using Grove.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Workflows.Contents.UIHints;
using Grove.Modules;

namespace Grove.Workflows.Contents;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureGroveWorkflows(groveWorkflows =>
        {
            groveWorkflows.AddActivitiesFrom<Startup>();
            groveWorkflows.AddVariableTypeAndAlias<ContentItem>("ContentItem", "CMS");
        });

        services
            .AddScoped<IContentLifecycleHandler, Hooks.WorkflowContentLifecycleHandler>()
            .AddScoped<IWorkflowHookSlotSource, Hooks.ContentHookSlotSource>()
            .AddScoped<IPropertyUIHandler, ContentTypeCheckListOptionsProvider>()
            .AddScoped<IPropertyUIHandler, ContentTypeOptionsProvider>();
    }
}
