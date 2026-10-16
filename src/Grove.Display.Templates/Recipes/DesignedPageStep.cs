using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;
using Grove.Display.Templates.Models;
using Grove.Display.Templates.Services;

namespace Grove.Display.Templates.Recipes;

/// <summary>
/// Creates and publishes designed pages (decision 9): <c>{ "name": "DesignedPages", "Pages": {
/// "landing": { "Route": "/landing", "Bucket": "site", "Title": "...", "AllowAnonymous": true,
/// "Source": "..." } } }</c>. A page of the same name is replaced.
/// </summary>
public sealed class DesignedPageStep(DesignedPageStore store) : NamedRecipeStepHandler("DesignedPages")
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (!context.Step.TryGetPropertyValue("Pages", out var node) || node is not JsonObject pages)
        {
            return;
        }

        var existing = await store.ListLatestAsync();
        foreach (var (name, value) in pages)
        {
            if (value is not JsonObject page)
            {
                continue;
            }

            var declaration = page.Deserialize<Declaration>(s_json) ?? new Declaration();
            var part = new DesignedPageFieldSet
            {
                Name = name,
                Route = declaration.Route ?? string.Empty,
                Bucket = declaration.Bucket ?? string.Empty,
                Title = declaration.Title,
                Description = declaration.Description,
                AllowAnonymous = declaration.AllowAnonymous,
                Source = declaration.Source ?? string.Empty,
            };
            if (string.IsNullOrWhiteSpace(part.Bucket))
            {
                part.Bucket = DesignedPagesConstants.SiteBucket;
            }

            var match = existing.FirstOrDefault(record => string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase));
            var saved = await store.SaveDraftAsync(match?.ContentItemId, part);
            await store.PublishAsync(saved.ContentItemId);
        }
    }

    // A page as the step declares it; the field set's values are fields, read through its accessors.
    private sealed class Declaration
    {
        public string? Route { get; set; }

        public string? Bucket { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public bool AllowAnonymous { get; set; }

        public string? Source { get; set; }
    }
}
