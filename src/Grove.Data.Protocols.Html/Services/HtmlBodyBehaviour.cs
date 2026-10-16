using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Data.Protocols.Html.Models;
using Grove.Data.Protocols.Html.Settings;
using Grove.Display.Liquid;

namespace Grove.Data.Protocols.Html.Services;

/// <summary>
/// What the HTML body's system flows do (<see cref="Workflows.HtmlBodyWorkflowProvider"/>): the markup
/// is sanitized when the attachment says so, and a body that renders Liquid must parse.
/// </summary>
public sealed class HtmlBodyBehaviour(
    IContentDefinitionManager contentDefinitionManager,
    ILiquidTemplateManager liquidTemplateManager,
    Grove.Infrastructure.Html.IHtmlSanitizerService htmlSanitizerService,
    Microsoft.Extensions.Localization.IStringLocalizer<HtmlBodyBehaviour> S)
{
    /// <summary>Sanitizes the body's markup when the attachment's settings say so.</summary>
    public async Task SanitizeAsync(ContentItem item)
    {
        if (!item.TryGet<HtmlBodyFieldSet>(out var part) || string.IsNullOrEmpty(part.Html)
            || await SettingsAsync(item) is not { SanitizeHtml: true })
        {
            return;
        }

        var sanitized = htmlSanitizerService.Sanitize(part.Html);
        item.Alter<HtmlBodyFieldSet>(body => body.Html = sanitized);
    }

    /// <summary>The refusal of a body whose Liquid does not parse; null when it is fine.</summary>
    public async Task<string> LiquidErrorAsync(ContentItem item)
    {
        if (!item.TryGet<HtmlBodyFieldSet>(out var part) || string.IsNullOrEmpty(part.Html)
            || await SettingsAsync(item) is not { RenderLiquid: true }
            || liquidTemplateManager.Validate(part.Html, out var errors))
        {
            return null;
        }

        return S["The Body doesn't contain a valid Liquid expression. Details: {0}", string.Join(" ", errors)];
    }

    private async Task<HtmlBodyFieldSetSettings> SettingsAsync(ContentItem item) =>
        (await contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType))?.FieldSets
            .FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, ContentFieldSetName.Of<HtmlBodyFieldSet>(), StringComparison.Ordinal))?
            .GetSettings<HtmlBodyFieldSetSettings>();
}
