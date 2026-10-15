using System.Text.Json;
using Grove.Services;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;

namespace Grove.Infrastructure.Recipes;

public sealed class AdminMenuLayoutStep(AdminMenuLayoutService layoutService) : NamedRecipeStepHandler("GroveAdminMenuLayout")
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var fileName = context.Step["file"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            context.Errors.Add("GroveAdminMenuLayout recipe step requires a 'file' value.");
            return;
        }

        var relativePath = Path.Combine(context.RecipeDescriptor.BasePath ?? string.Empty, fileName).Replace('\\', '/');
        var file = context.RecipeDescriptor.FileProvider.GetFileInfo(relativePath);
        if (!file.Exists)
        {
            // An optional layout is a LOCAL artifact (the exported file is gitignored):
            // a fresh clone provisions without one, and the tenant simply starts on the
            // synced default layout. A non-optional reference to a missing file stays
            // an error - the recipe author asked for something that is not there.
            if (context.Step["optional"]?.GetValue<bool>() == true)
            {
                return;
            }

            context.Errors.Add($"GroveAdminMenuLayout file '{fileName}' was not found.");
            return;
        }

        await using var stream = file.CreateReadStream();
        var model = await JsonSerializer.DeserializeAsync<AdminMenuLayoutFile>(stream, JsonOptions);
        if (model is null)
        {
            context.Errors.Add($"GroveAdminMenuLayout file '{fileName}' could not be parsed.");
            return;
        }

        await layoutService.ImportAsync(model);
    }
}
