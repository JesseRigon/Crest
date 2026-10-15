using Grove.Infrastructure.Recipes.Models;

namespace Grove.Infrastructure.Recipes.Services;

public interface IRecipeExecutor
{
    Task<string> ExecuteAsync(string executionId, RecipeDescriptor recipeDescriptor, IDictionary<string, object> environment, CancellationToken cancellationToken);
}
