using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentTypes.Deployment;
using Crest.ContentTypes.Editors;
using Crest.ContentTypes.RecipeSteps;
using Crest.ContentTypes.Services;
using Crest.Deployment;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Recipes.Events;
using Crest.Security.Permissions;

namespace Crest.ContentTypes;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<ContentTypesOptions>(options =>
        {
            var configSection = _shellConfiguration.GetSection("Crest_ContentTypes");

            var defaultThumbnailPath = configSection.GetValue<string>("DefaultThumbnailPath");
            if (!string.IsNullOrWhiteSpace(defaultThumbnailPath))
            {
                options.DefaultThumbnailPath = defaultThumbnailPath;
            }
            else
            {
                options.DefaultThumbnailPath = "~/Crest.ContentTypes/placeholder.png";
            }

            options.DefaultCategory = configSection.GetValue<string>("DefaultCategory");
        });
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IContentDefinitionService, ContentDefinitionService>();
        services.AddScoped<IStereotypeService, StereotypeService>();
        services.AddScoped<IContentDefinitionDisplayHandler, ContentDefinitionDisplayCoordinator>();
        services.AddScoped<IContentDefinitionDisplayManager, DefaultContentDefinitionDisplayManager>();
        services.AddScoped<IContentPartDefinitionDisplayDriver, ContentPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypeDefinitionDisplayDriver, ContentTypeSettingsDisplayDriver>();
        services.AddScoped<IContentTypeDefinitionDisplayDriver, DefaultContentTypeDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ContentTypePartSettingsDisplayDriver>();

        // TODO: Put in its own feature to be able to execute this recipe without having to enable
        // Content Types management UI
        services.AddRecipeExecutionStep<ContentDefinitionStep>();
        services.AddRecipeExecutionStep<ReplaceContentDefinitionStep>();
        services.AddRecipeExecutionStep<DeleteContentDefinitionStep>();

        services.AddTransient<IRecipeEventHandler, LuceneRecipeEventHandler>();
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContentDefinitionDeploymentSource, ContentDefinitionDeploymentStep, ContentDefinitionDeploymentStepDriver>();
        services.AddDeployment<ReplaceContentDefinitionDeploymentSource, ReplaceContentDefinitionDeploymentStep, ReplaceContentDefinitionDeploymentStepDriver>();
        services.AddDeployment<DeleteContentDefinitionDeploymentSource, DeleteContentDefinitionDeploymentStep, DeleteContentDefinitionDeploymentStepDriver>();
    }
}
