using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Components;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;
using Crest.Templates.Deployment;
using Crest.Templates.Handlers;
using Crest.Templates.Models;
using Crest.Templates.Recipes;
using Crest.Templates.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Templates;

/// <summary>
/// Templates as content (docs/blazor-display.md § 5, rework step 4): Liquid whose tags emit
/// display-tree nodes, bound to shapes ahead of the modules' components or placed by name as
/// composites, published like content. Edited on the admin Templates page through
/// <c>api/crest/templates</c>.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TemplatePart>();
        services.AddContentPart<DesignedPagePart>();

        // The per-item page body (docs/widgets.md): its shape binds to its stored tree, validated on save.
        services.AddContentPart<DisplayTreePart>()
            .UseDisplayDriver<Drivers.DisplayTreePartDisplayDriver>()
            .AddHandler<DisplayTreePartHandler>();
        services.AddScoped<IDisplayTemplateBinding, DisplayTreePartBinding>();
        services.AddDataMigration<Migrations>();
        services.AddScoped<Crest.ContentManagement.Handlers.IContentHandler, TemplatePublishHandler>();

        services.AddSingleton<TemplateTreeParser>();
        services.AddScoped<TemplateTreeEvaluator>();
        services.AddScoped<TemplateStore>();
        services.AddScoped<ITemplateCatalog>(sp => sp.GetRequiredService<TemplateStore>());
        services.AddScoped<TemplateResolver>();
        // Named templates to HTML (email bodies; step 7 retires the Razor email shapes).
        services.AddScoped<Crest.DisplayManagement.Components.ITemplateHtmlRenderer, TemplateHtmlRenderer>();
        services.AddScoped<TemplateValidator>();
        services.AddScoped<TemplatePreview>();
        services.AddScoped<IDisplayTemplateBinding, TemplateDisplayBinding>();
        services.AddScoped<ComponentOverrides>();
        services.AddScoped<IDisplayTreeOverrides>(sp => sp.GetRequiredService<ComponentOverrides>());
        services.AddScoped<IDisplayTreeVersionPart, TemplatesTreeVersion>();

        // Designed pages (decision 9): their trees bind as shipped templates, their sets are permissions (§ 10).
        services.AddScoped<DesignedPageStore>();
        services.AddScoped<IShippedTemplateProvider, DesignedPageTemplates>();
        services.AddPermissionProvider<DesignedPagePermissions>();

        services.AddPermissionProvider<Permissions>();
        services.AddApiContributor<Api.TemplatesApi>();
        services.AddApiContributor<Api.ComponentOverridesApi>();
        services.AddApiContributor<Api.DesignedPagesApi>();
        services.AddApiContributor<Api.ComponentsApi>();
        services.AddPermissionProvider<AdminTemplatesPermissions>();
        services.AddRecipeExecutionStep<TemplateStep>();
        services.AddRecipeExecutionStep<DesignedPageStep>();
        services.AddDeployment<AllTemplatesDeploymentSource, AllTemplatesDeploymentStep, AllTemplatesDeploymentStepDriver>();
    }
}
