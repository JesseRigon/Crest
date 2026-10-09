using Crest.Recipes.Models;

namespace Crest.Recipes.Services;

public interface IRecipeExecutor
{
    Task<string> ExecuteAsync(string executionId, RecipeDescriptor recipeDescriptor, IDictionary<string, object> environment, CancellationToken cancellationToken);
}
