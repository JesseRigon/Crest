using System.Text.Json.Nodes;
using Crest.Deployment;
using Crest.Shortcodes.Services;

namespace Crest.Shortcodes.Deployment;

public sealed class AllShortcodeTemplatesDeploymentSource
    : DeploymentSourceBase<AllShortcodeTemplatesDeploymentStep>
{
    private readonly ShortcodeTemplatesManager _templatesManager;

    public AllShortcodeTemplatesDeploymentSource(ShortcodeTemplatesManager templatesManager)
    {
        _templatesManager = templatesManager;
    }

    protected override async Task ProcessAsync(AllShortcodeTemplatesDeploymentStep step, DeploymentPlanResult result)
    {
        var templateObjects = new JsonObject();
        var templates = await _templatesManager.GetShortcodeTemplatesDocumentAsync();

        foreach (var template in templates.ShortcodeTemplates)
        {
            templateObjects[template.Key] = JObject.FromObject(template.Value);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = "ShortcodeTemplates",
            ["ShortcodeTemplates"] = templateObjects,
        });
    }
}
