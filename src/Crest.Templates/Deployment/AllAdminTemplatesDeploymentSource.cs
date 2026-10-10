using System.Text;
using System.Text.Json.Nodes;
using Crest.Deployment;
using Crest.Templates.Models;
using Crest.Templates.Services;

namespace Crest.Templates.Deployment;

public sealed class AllAdminTemplatesDeploymentSource
    : DeploymentSourceBase<AllAdminTemplatesDeploymentStep>
{
    private readonly AdminTemplatesManager _templatesManager;

    public AllAdminTemplatesDeploymentSource(AdminTemplatesManager templatesManager)
    {
        _templatesManager = templatesManager;
    }

    protected override async Task ProcessAsync(AllAdminTemplatesDeploymentStep step, DeploymentPlanResult result)
    {
        var templateObjects = new JsonObject();
        var templates = await _templatesManager.GetTemplatesDocumentAsync();

        if (step.ExportAsFiles)
        {
            foreach (var template in templates.Templates)
            {
                var fileName = "AdminTemplates/" + template.Key.Replace("__", "-").Replace("_", ".") + ".liquid";
                var templateValue = new Template { Description = template.Value.Description, Content = $"[file:text('{fileName}')]" };
                await result.FileBuilder.SetFileAsync(fileName, Encoding.UTF8.GetBytes(template.Value.Content));
                templateObjects[template.Key] = JObject.FromObject(templateValue);
            }
        }
        else
        {
            foreach (var template in templates.Templates)
            {
                templateObjects[template.Key] = JObject.FromObject(template.Value);
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = "AdminTemplates",
            ["AdminTemplates"] = templateObjects,
        });
    }
}
