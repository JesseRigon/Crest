using Grove.Infrastructure.Recipes.Models;

namespace Grove.Infrastructure.Recipes.Services;

public interface IRecipeHarvester
{
    /// <summary>
    /// Returns a collection of all recipes.
    /// </summary>
    Task<IEnumerable<RecipeDescriptor>> HarvestRecipesAsync();
}
