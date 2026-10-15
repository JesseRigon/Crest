using Grove.Recipes.Models;

namespace Grove.Recipes.Services;

public interface IRecipeHarvester
{
    /// <summary>
    /// Returns a collection of all recipes.
    /// </summary>
    Task<IEnumerable<RecipeDescriptor>> HarvestRecipesAsync();
}
