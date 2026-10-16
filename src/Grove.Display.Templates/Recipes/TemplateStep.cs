using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;
using Grove.Display.Templates.Models;
using Grove.Display.Templates.Services;

namespace Grove.Display.Templates.Recipes;

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
            var declaration = template.Deserialize<Declaration>(s_json) ?? new Declaration();
            var part = new TemplateFieldSet
            {
                Name = name,
                Source = declaration.Source ?? string.Empty,
                Theme = declaration.Theme,
                DisplayType = declaration.DisplayType,
                Description = declaration.Description,
                IsComposite = declaration.IsComposite,
                DisplayName = declaration.DisplayName,
                Category = declaration.Category,
                Icon = declaration.Icon,
                Inputs = declaration.Inputs ?? [],
                Outputs = declaration.Outputs ?? [],
            };
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

    // A template as the step declares it; the field set's values are fields, read through its accessors.
    private sealed class Declaration
    {
        public string? Source { get; set; }

        public string? Theme { get; set; }

        public string? DisplayType { get; set; }

        public string? Description { get; set; }

        public bool IsComposite { get; set; }

        public string? DisplayName { get; set; }

        public string? Category { get; set; }

        public string? Icon { get; set; }

        public List<TemplateInput>? Inputs { get; set; }

        public List<TemplateOutput>? Outputs { get; set; }
    }
}
