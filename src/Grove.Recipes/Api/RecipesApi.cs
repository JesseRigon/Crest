#nullable enable

using System.Security.Claims;
using Grove.Api;
using Grove.Recipes.Models;
using Grove.Recipes.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Recipes.Api;

/// <summary>The recipes list and execution (<c>api/grove/recipes</c>).</summary>
public sealed class RecipesApi : ApiContributor
{
    public override string Name => "Recipes";

    public override string FeatureId => "Grove.Recipes";

    public override void Map(IEndpointRouteBuilder group)
    {
        var recipes = group.MapGroup("recipes");
        recipes.MapGet("", ListAsync);
        recipes.MapPost("execute", ExecuteAsync);
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await requestAccess.AuthorizeAsync(user, RecipePermissions.ManageRecipes);
        if (access is null)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok((await HarvestAsync(access)).Select(RecipeSummary.From).OrderBy(recipe => recipe.DisplayName).ToArray());
    }

    private static async Task<IResult> ExecuteAsync([FromBody] RecipeKey key, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await requestAccess.AuthorizeAsync(user, RecipePermissions.ManageRecipes);
        if (access is null)
        {
            return TypedResults.Forbid();
        }

        var recipe = (await HarvestAsync(access)).FirstOrDefault(candidate => candidate.BasePath == key.BasePath && candidate.RecipeFileInfo.Name == key.FileName);
        if (recipe is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            await access.GetRequiredService<IRecipeExecutor>().ExecuteAsync(Guid.NewGuid().ToString("n"), recipe, new Dictionary<string, object>(), CancellationToken.None);
            return TypedResults.NoContent();
        }
        catch (Exception exception)
        {
            return ApiResults.BadRequest(exception.Message);
        }
    }

    private static async Task<IEnumerable<RecipeDescriptor>> HarvestAsync(AuthorizedRequest access)
    {
        var harvesters = access.GetRequiredService<IEnumerable<IRecipeHarvester>>();
        return (await Task.WhenAll(harvesters.Select(harvester => harvester.HarvestRecipesAsync())))
            .SelectMany(recipes => recipes)
            .Where(recipe => !recipe.IsSetupRecipe && (recipe.Tags is null || !recipe.Tags.Contains("hidden", StringComparer.OrdinalIgnoreCase)));
    }
}

public sealed record RecipeSummary(string Name, string? DisplayName, string? Description, string? FileName, string? BasePath, string[]? Tags)
{
    public static RecipeSummary From(RecipeDescriptor recipe) => new(recipe.Name, recipe.DisplayName, recipe.Description, recipe.RecipeFileInfo.Name, recipe.BasePath, recipe.Tags);
}

public sealed record RecipeKey(string? BasePath, string? FileName);
