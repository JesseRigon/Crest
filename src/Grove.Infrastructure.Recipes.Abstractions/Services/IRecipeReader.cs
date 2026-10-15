using Microsoft.Extensions.FileProviders;
using Grove.Infrastructure.Recipes.Models;

namespace Grove.Infrastructure.Recipes.Services;

public interface IRecipeReader
{
    Task<RecipeDescriptor> GetRecipeDescriptorAsync(string recipeBasePath, IFileInfo recipeFileInfo, IFileProvider fileProvider);
}
