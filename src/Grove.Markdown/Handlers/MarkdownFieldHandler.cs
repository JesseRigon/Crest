using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;
using Grove.Liquid;
using Grove.Markdown.Fields;
using Grove.Markdown.Settings;

namespace Grove.Markdown.Handlers;

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
