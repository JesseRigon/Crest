using Microsoft.Extensions.FileProviders;
using Grove.Recipes.Models;

namespace Grove.Recipes.Services;

public interface IRecipeReader
{
    Task<RecipeDescriptor> GetRecipeDescriptorAsync(string recipeBasePath, IFileInfo recipeFileInfo, IFileProvider fileProvider);
}
