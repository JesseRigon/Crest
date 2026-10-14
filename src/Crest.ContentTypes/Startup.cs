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
        services.AddScoped<IContentDefinitionService, ContentDefinitionService>();
        services.AddScoped<IStereotypeService, StereotypeService>();
        services.AddScoped<IContentDefinitionDisplayHandler, ContentDefinitionDisplayCoordinator>();
        services.AddScoped<IContentDefinitionDisplayManager, DefaultContentDefinitionDisplayManager>();

        // TODO: Put in its own feature to be able to execute this recipe without having to enable
        // Content Types management UI
        services.AddRecipeExecutionStep<ContentDefinitionStep>();
        services.AddRecipeExecutionStep<ReplaceContentDefinitionStep>();
        services.AddRecipeExecutionStep<DeleteContentDefinitionStep>();

        services.AddTransient<IRecipeEventHandler, LuceneRecipeEventHandler>();

        // The definitions APIs (docs/MVC/api.md § Placement): types, parts, content groups, field
        // visibility and definition locks.
        services.AddApiContributor<Api.ContentDefinitionsApi>();
        services.AddApiContributor<Api.ContentTypesEditorApi>();
        services.AddApiContributor<Api.ContentGroupsApi>();
        services.AddApiContributor<Api.FieldVisibilityApi>();
    }
}

/// <summary>
/// Definition locks (docs/content-items.md › Definition locks). Last of all startups so the
/// decorators wrap whatever the content-types services registered; a locked change answers 409.
/// </summary>
public sealed class DefinitionLocksStartup : StartupBase
{
    public override int Order => int.MaxValue;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<ContentDefinitionPermissions>();
        services.AddDefinitionLocks();
        services.AddApiExceptionMapper<DefinitionLockedExceptionMapper>();
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
