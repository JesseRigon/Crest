using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Recipes.Models;
using Grove.Recipes.Services;
using Grove.Templates.Models;
using Grove.Templates.Services;

namespace Grove.Templates.Recipes;

/// <summary>
/// Creates and publishes templates: <c>{ "name": "Templates", "Templates": { "Content__BlogPost":
/// { "Source": "...", "Theme": null, "DisplayType": "Detail", "Description": "..." } } }</c>.
/// A template of the same name, theme and display type is replaced.
/// </summary>
public sealed class TemplateStep(TemplateStore store) : NamedRecipeStepHandler("Templates")
{
    private static readonly System.Text.Json.JsonSerializerOptions s_json = new(System.Text.Json.JsonSerializerDefaults.Web);

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (!context.Step.TryGetPropertyValue("Templates", out var node) || node is not JsonObject templates)
        {
            return;
        }

        var existing = await store.ListLatestAsync();
        foreach (var (name, value) in templates)
        {
            if (value is not JsonObject template)
            {
                continue;
            }

            // The declaration reads straight off the step (Source, Theme, DisplayType, Description,
            // IsComposite, DisplayName, Category, Icon, Inputs, Outputs); "Content" is the old name of Source.
            var part = template.Deserialize<TemplatePart>(s_json) ?? new TemplatePart();
            part.Name = name;
            if (string.IsNullOrEmpty(part.Source) && template["Content"]?.GetValue<string>() is { } content)
            {
                part.Source = content;
            }

            var match = existing.FirstOrDefault(record =>
                string.Equals(record.Name, part.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Theme ?? string.Empty, part.Theme ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.DisplayType ?? string.Empty, part.DisplayType ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            var saved = await store.SaveDraftAsync(match?.ContentItemId, part);
            await store.PublishAsync(saved.ContentItemId);
        }
    }
}
