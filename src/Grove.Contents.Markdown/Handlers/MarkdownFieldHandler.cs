using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Liquid;
using Grove.Contents.Markdown.Fields;
using Grove.Contents.Markdown.Settings;

namespace Grove.Contents.Markdown.Handlers;

/// <summary>A Markdown field that renders Liquid must hold Liquid that parses, on every save path.</summary>
public class MarkdownFieldHandler : ContentFieldHandler<MarkdownField>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    protected readonly IStringLocalizer S;

    public MarkdownFieldHandler(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<MarkdownFieldHandler> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, MarkdownField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<MarkdownFieldSettings>();
        if (settings.RenderLiquid && !string.IsNullOrEmpty(field.Markdown) && !_liquidTemplateManager.Validate(field.Markdown, out var errors))
        {
            context.Fail(S["{0} doesn't contain a valid Liquid expression. Details: {1}", context.ContentPartFieldDefinition.DisplayName(), string.Join(" ", errors)], nameof(field.Markdown));
        }

        return Task.CompletedTask;
    }
}
