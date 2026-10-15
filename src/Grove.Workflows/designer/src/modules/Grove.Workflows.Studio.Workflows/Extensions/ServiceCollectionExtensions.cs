using Grove.Workflows.Studio.ActivityPortProviders.Extensions;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.DomInterop.Extensions;
using Grove.Workflows.Studio.Extensions;
using Grove.Workflows.Studio.UIHints.Extensions;
using Grove.Workflows.Studio.Workflows.ActivityPickers.Accordion;
using Grove.Workflows.Studio.Workflows.Components.WorkflowDefinitionEditor.Components.Models;
using Grove.Workflows.Studio.Workflows.Components.WorkflowInstanceList.Models;
using Grove.Workflows.Studio.Workflows.Contracts;
using Grove.Workflows.Studio.Workflows.Designer.Extensions;
using Grove.Workflows.Studio.Workflows.DiagramDesigners.Fallback;
using Grove.Workflows.Studio.Workflows.DiagramDesigners.Flowcharts;
using Grove.Workflows.Studio.Workflows.Handlers;
using Grove.Workflows.Studio.Workflows.Menu;
using Grove.Workflows.Studio.Workflows.Services;
using Grove.Workflows.Studio.Workflows.Widgets;
using Microsoft.Extensions.DependencyInjection;


namespace Grove.Workflows.Studio.Workflows.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="IServiceCollection"/> interface.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the workflows module.
    /// </summary>
    public static IServiceCollection AddWorkflowsModule(this IServiceCollection services)
    {
        services
            .AddScoped<IFeature, Feature>()
            .AddScoped<IMenuProvider, WorkflowsMenu>()
            .AddScoped<IWorkflowInstanceObserverFactory, WorkflowInstanceObserverFactory>()
            .AddScoped<IWorkflowCloningDialogService, WorkflowCloningDialogService>()
            .AddScoped<IWorkflowExportDialogService, WorkflowExportDialogService>()
            .AddDefaultUIHintHandlers()
            .AddDefaultActivityPortProviders()
            .AddWorkflowsCore()
            .AddWorkflowsDesigner()
            .AddDomInterop()
            .AddClipboardInterop()
            .AddDownloadInterop()
            ;

        services
            .AddDiagramDesignerProvider<FallbackDesignerProvider>()
            .AddDiagramDesignerProvider<FlowchartDiagramDesignerProvider>();

        services.AddNotificationHandler<RefreshActivityRegistry>();
        services.AddScoped<IWidget, WorkflowDefinitionMetadataWidget>();
        services.AddScoped<IWidget, WorkflowDefinitionSettingsWidget>();
        services.AddScoped<IWidget, WorkflowDefinitionInfoWidget>();
        services.AddScoped<IActivityPickerComponentProvider, AccordionActivityPickerComponentProvider>();
        services.AddScoped<ICreateWorkflowDialogComponentProvider, DefaultCreateWorkflowDialogComponentProvider>();
        
        services.Configure<WorkflowDefinitionOptions>(opts =>
        {
            opts.AutoSaveChangesByDefault = true;
            opts.AutoApplyCodeViewChangesByDefault = true;
        });
        services.Configure<WorkflowInstanceListPollingOptions>(opts =>
        {
            opts.IsEnabledByDefault = true;
            opts.IntervalSeconds = 10;
        });

        return services;
    }
}