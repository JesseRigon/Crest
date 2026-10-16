using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Markdown.Models;
using Grove.Contents.Markdown.Settings;
using Grove.Display.Liquid;

namespace Grove.Contents.Markdown.Services;

/// <summary>What the Markdown body's system flow does (<see cref="Workflows.MarkdownBodyWorkflowProvider"/>): a body that renders Liquid must parse.</summary>
public sealed class MarkdownBodyBehaviour(
    IContentDefinitionManager contentDefinitionManager,
    ILiquidTemplateManager liquidTemplateManager,
    Microsoft.Extensions.Localization.IStringLocalizer<MarkdownBodyBehaviour> S)
{
    /// <summary>The refusal of a body whose Liquid does not parse; null when it is fine.</summary>
    public async Task<string> LiquidErrorAsync(ContentItem item)
    {
        if (!item.TryGet<MarkdownBodyFieldSet>(out var part) || string.IsNullOrEmpty(part.Markdown))
        {
            return null;
        }

        var settings = (await contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType))?.FieldSets
            .FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, ContentFieldSetName.Of<MarkdownBodyFieldSet>(), StringComparison.Ordinal))?
            .GetSettings<MarkdownBodyFieldSetSettings>();
        return settings is { RenderLiquid: true } && !liquidTemplateManager.Validate(part.Markdown, out var errors)
            ? S["The Markdown doesn't contain a valid Liquid expression. Details: {0}", string.Join(" ", errors)]
            : null;
    }
}
