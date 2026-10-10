using Microsoft.Extensions.FileProviders;
using Crest.Recipes.Models;

namespace Crest.Recipes.Services;

public interface IRecipeReader
{
    Task<RecipeDescriptor> GetRecipeDescriptorAsync(string recipeBasePath, IFileInfo recipeFileInfo, IFileProvider fileProvider);
}
