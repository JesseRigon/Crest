using Microsoft.AspNetCore.Mvc;
using Crest.Services;
using Crest.Recipes;
using Crest.Recipes.Models;
using Crest.Recipes.Services;

namespace Crest.ViewModels;

public sealed record Recipe(string Name, string? DisplayName, string? Description, string? FileName, string? BasePath, string[]? Tags)
{ public static Recipe From(RecipeDescriptor recipe) => new(recipe.Name, recipe.DisplayName, recipe.Description, recipe.RecipeFileInfo.Name, recipe.BasePath, recipe.Tags); }
public sealed record RecipeKey(string? BasePath, string? FileName);
