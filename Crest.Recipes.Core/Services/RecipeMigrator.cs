using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Crest.Data.Migration;
using Crest.Environment.Extensions;
using Crest.Modules;

namespace Crest.Recipes.Services;

public class RecipeMigrator : IRecipeMigrator
{
    private readonly IRecipeReader _recipeReader;
    private readonly IRecipeExecutor _recipeExecutor;
    private readonly IHostEnvironment _hostingEnvironment;
    private readonly ITypeFeatureProvider _typeFeatureProvider;
    private readonly IEnumerable<IRecipeEnvironmentProvider> _environmentProviders;
    private readonly ILogger _logger;

    public RecipeMigrator(
        IRecipeReader recipeReader,
        IRecipeExecutor recipeExecutor,
        IHostEnvironment hostingEnvironment,
        ITypeFeatureProvider typeFeatureProvider,
        IEnumerable<IRecipeEnvironmentProvider> environmentProviders,
        ILogger<RecipeMigrator> logger)
    {
        _recipeReader = recipeReader;
        _recipeExecutor = recipeExecutor;
        _hostingEnvironment = hostingEnvironment;
        _typeFeatureProvider = typeFeatureProvider;
        _environmentProviders = environmentProviders;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(string recipeFileName, IDataMigration migration)
    {
        var extensionInfo = _typeFeatureProvider.GetExtensionForDependency(migration.GetType());

        var recipeBasePath = Path.Combine(extensionInfo.SubPath, "Migrations").Replace('\\', '/');
        var recipeFilePath = Path.Combine(recipeBasePath, recipeFileName).Replace('\\', '/');
        var recipeFileInfo = _hostingEnvironment.ContentRootFileProvider.GetFileInfo(recipeFilePath);
        var recipeDescriptor = await _recipeReader.GetRecipeDescriptorAsync(recipeBasePath, recipeFileInfo, _hostingEnvironment.ContentRootFileProvider);

        if (recipeDescriptor == null)
        {
            return null;
        }

        recipeDescriptor.RequireNewScope = false;

        var environment = new Dictionary<string, object>();

        await _environmentProviders.OrderBy(x => x.Order)
            .InvokeAsync((provider, env) => provider.PopulateEnvironmentAsync(env), environment, _logger);

        var executionId = Guid.NewGuid().ToString("n");

        return await _recipeExecutor.ExecuteAsync(executionId, recipeDescriptor, environment, CancellationToken.None);
    }
}

