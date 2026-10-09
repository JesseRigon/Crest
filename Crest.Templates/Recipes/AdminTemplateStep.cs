using System.Text.Json.Nodes;
using Crest.Recipes.Models;
using Crest.Recipes.Services;
using Crest.Templates.Models;
using Crest.Templates.Services;

namespace Crest.Templates.Recipes;

/// <summary>
/// This recipe step creates a set of templates.
/// </summary>
public sealed class AdminTemplateStep : NamedRecipeStepHandler
{
    private readonly AdminTemplatesManager _adminTemplatesManager;

    public AdminTemplateStep(AdminTemplatesManager templatesManager)
        : base("AdminTemplates")
    {
        _adminTemplatesManager = templatesManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step.TryGetPropertyValue("AdminTemplates", out var jsonNode) && jsonNode is JsonObject templates)
        {
            foreach (var property in templates)
            {
                var name = property.Key;
                var value = property.Value.ToObject<Template>();

                await _adminTemplatesManager.UpdateTemplateAsync(name, value);
            }
        }
    }
}
