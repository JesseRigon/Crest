using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Display.Templates.Services;

namespace Grove.Display.Templates.Deployment;

/// <summary>Exports the published templates as a Templates step, their sources inline or as files.</summary>
public sealed class AllTemplatesDeploymentSource(TemplateStore store) : DeploymentSourceBase<AllTemplatesDeploymentStep>
{
    protected override async Task ProcessAsync(AllTemplatesDeploymentStep step, DeploymentPlanResult result)
    {
        var templates = new JsonObject();
        foreach (var template in await store.ListPublishedAsync())
        {
            var source = template.Source;
            if (step.ExportAsFiles)
            {
                var fileName = "Templates/" + template.Name.Replace("__", "-").Replace("_", ".") + (template.Theme is null ? string.Empty : "." + template.Theme) + (template.DisplayType is null ? string.Empty : "." + template.DisplayType) + ".liquid";
                await result.FileBuilder.SetFileAsync(fileName, Encoding.UTF8.GetBytes(template.Source));
                source = $"[file:text('{fileName}')]";
            }

            templates[template.Name] = new JsonObject
            {
                ["Source"] = source,
                ["Theme"] = template.Theme,
                ["DisplayType"] = template.DisplayType,
                ["Description"] = template.Description,
                ["IsComposite"] = template.IsComposite,
                ["DisplayName"] = template.DisplayName,
                ["Category"] = template.Category,
                ["Icon"] = template.Icon,
                ["Inputs"] = JsonSerializer.SerializeToNode(template.Inputs),
                ["Outputs"] = JsonSerializer.SerializeToNode(template.Outputs),
            };
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = "Templates",
            ["Templates"] = templates,
        });
    }
}
