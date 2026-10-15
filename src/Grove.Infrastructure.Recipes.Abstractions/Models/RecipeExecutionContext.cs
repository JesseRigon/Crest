using System.Text.Json.Nodes;

namespace Grove.Infrastructure.Recipes.Models;

public class RecipeExecutionContext
{
    public string ExecutionId { get; set; }

    public object Environment { get; set; }

    public string Name { get; set; }

    public JsonObject Step { get; set; }

    public RecipeDescriptor RecipeDescriptor { get; set; }

    public IEnumerable<RecipeDescriptor> InnerRecipes { get; set; }

    public IList<string> Errors { get; } = [];
}
