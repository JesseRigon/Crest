using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement.Components;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;
using Grove.Templates.Deployment;
using Grove.Templates.Handlers;
using Grove.Templates.Models;
using Grove.Templates.Recipes;
using Grove.Templates.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Templates;

/// <summary>
/// Templates as content (docs/blazor-display.md § 5, rework step 4): Liquid whose tags emit
/// display-tree nodes, bound to shapes ahead of the modules' components or placed by name as
/// composites, published like content. Edited on the admin Templates page through
/// <c>api/grove/templates</c>.
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
        services.AddScoped<Grove.ContentManagement.Handlers.IContentHandler, TemplatePublishHandler>();

        services.AddSingleton<TemplateTreeParser>();
        services.AddScoped<TemplateTreeEvaluator>();
        services.AddScoped<TemplateStore>();
        services.AddScoped<ITemplateCatalog>(sp => sp.GetRequiredService<TemplateStore>());
        services.AddScoped<TemplateResolver>();
        // Named templates to HTML (email bodies; step 7 retires the Razor email shapes).
        services.AddScoped<Grove.DisplayManagement.Components.ITemplateHtmlRenderer, TemplateHtmlRenderer>();
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
