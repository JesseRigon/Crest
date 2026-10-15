using System.Text.Json.Nodes;
using Grove.Infrastructure.Html;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;
using Grove.Shortcodes.Models;
using Grove.Shortcodes.Services;

namespace Grove.Shortcodes.Recipes;

/// <summary>
/// This recipe step creates a set of Shortcodes.
/// </summary>
public sealed class ShortcodeTemplateStep : NamedRecipeStepHandler
{
    private readonly ShortcodeTemplatesManager _templatesManager;
    private readonly IHtmlSanitizerService _htmlSanitizerService;

    public ShortcodeTemplateStep(ShortcodeTemplatesManager templatesManager, IHtmlSanitizerService htmlSanitizerService)
        : base("ShortcodeTemplates")
    {
        _templatesManager = templatesManager;
        _htmlSanitizerService = htmlSanitizerService;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step.TryGetPropertyValue("ShortcodeTemplates", out var jsonNode) && jsonNode is JsonObject templates)
        {
            foreach (var property in templates)
            {
                var name = property.Key;
                var value = property.Value.ToObject<ShortcodeTemplate>();

                value.Usage = _htmlSanitizerService.Sanitize(value.Usage);

                await _templatesManager.UpdateShortcodeTemplateAsync(name, value);
            }
        }
    }
}
