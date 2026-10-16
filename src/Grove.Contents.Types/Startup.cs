using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Types.Deployment;
using Grove.Contents.Types.RecipeSteps;
using Grove.Contents.Types.Services;
using Grove.Deployment;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Infrastructure.Recipes.Events;
using Grove.Security.Permissions;

namespace Grove.Contents.Types;

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
            var configSection = _shellConfiguration.GetSection("Grove_ContentTypes");

            var defaultThumbnailPath = configSection.GetValue<string>("DefaultThumbnailPath");
            if (!string.IsNullOrWhiteSpace(defaultThumbnailPath))
            {
                options.DefaultThumbnailPath = defaultThumbnailPath;
            }
            else
            {
                options.DefaultThumbnailPath = "~/Grove.Contents.Types/placeholder.png";
            }

            options.DefaultCategory = configSection.GetValue<string>("DefaultCategory");
        });
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IContentDefinitionService, ContentDefinitionService>();
        services.AddScoped<IStereotypeService, StereotypeService>();

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
        // Visibility conditions are enforced on save; the option picker resolves its fields'
        // ids to keys through IFieldComparisonValueResolver.
        services.AddScoped<IFieldVisibilityEnforcer, FieldVisibilityEnforcer>();
    }
}

/// <summary>
/// Definition locks (docs/content-engine.md › Definition locks). Last of all startups so the
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

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContentDefinitionDeploymentSource, ContentDefinitionDeploymentStep>();
        services.AddDeployment<ReplaceContentDefinitionDeploymentSource, ReplaceContentDefinitionDeploymentStep>();
        services.AddDeployment<DeleteContentDefinitionDeploymentSource, DeleteContentDefinitionDeploymentStep>();
    }
}
