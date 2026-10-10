using Microsoft.Extensions.DependencyInjection;
using Crest.ContentTypes.Editors;
using Crest.Deployment;
using Crest.DisplayManagement;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;
using Crest.Templates.Deployment;
using Crest.Templates.Recipes;
using Crest.Templates.Services;
using Crest.Templates.Settings;

namespace Crest.Templates;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        services.AddScoped<IShapeBindingResolver, TemplatesShapeBindingResolver>();
        services.AddScoped<PreviewTemplatesProvider>();
        services.AddScoped<TemplatesManager>();
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddRecipeExecutionStep<TemplateStep>();

        // Template shortcuts in settings
        services.AddScoped<IContentPartDefinitionDisplayDriver, TemplateContentPartDefinitionDriver>();
        services.AddScoped<IContentTypeDefinitionDisplayDriver, TemplateContentTypeDefinitionDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, TemplateContentTypePartDefinitionDriver>();

        services.AddDeployment<AllTemplatesDeploymentSource, AllTemplatesDeploymentStep, AllTemplatesDeploymentStepDriver>();

        services.AddScoped<AdminTemplatesManager>();
        services.AddPermissionProvider<AdminTemplatesPermissions>();
    }
}

[Feature("Crest.AdminTemplates")]
public sealed class AdminTemplatesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IShapeBindingResolver, AdminTemplatesShapeBindingResolver>();
        services.AddScoped<AdminPreviewTemplatesProvider>();
        services.AddNavigationProvider<AdminTemplatesAdminMenu>();
        services.AddRecipeExecutionStep<AdminTemplateStep>();
        services.AddDeployment<AllAdminTemplatesDeploymentSource, AllAdminTemplatesDeploymentStep, AllAdminTemplatesDeploymentStepDriver>();
    }
}
