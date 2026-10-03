using Crest.Workflows.Studio.ActivityPortProviders.Extensions;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.DomInterop.Extensions;
using Crest.Workflows.Studio.Extensions;
using Crest.Workflows.Studio.UIHints.Extensions;
using Crest.Workflows.Studio.Workflows.ActivityPickers.Accordion;
using Crest.Workflows.Studio.Workflows.Components.WorkflowDefinitionEditor.Components.Models;
using Crest.Workflows.Studio.Workflows.Components.WorkflowInstanceList.Models;
using Crest.Workflows.Studio.Workflows.Contracts;
using Crest.Workflows.Studio.Workflows.Designer.Extensions;
using Crest.Workflows.Studio.Workflows.DiagramDesigners.Fallback;
using Crest.Workflows.Studio.Workflows.DiagramDesigners.Flowcharts;
using Crest.Workflows.Studio.Workflows.Handlers;
using Crest.Workflows.Studio.Workflows.Menu;
using Crest.Workflows.Studio.Workflows.Services;
using Crest.Workflows.Studio.Workflows.Widgets;
using Microsoft.Extensions.DependencyInjection;


namespace Crest.Workflows.Studio.Workflows.Extensions;

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