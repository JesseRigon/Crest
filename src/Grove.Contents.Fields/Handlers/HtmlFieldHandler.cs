using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Infrastructure.Html;
using Grove.Display.Liquid;

namespace Grove.Contents.Fields.Handlers;

/// <summary>
/// An HTML field on every save path: its markup is sanitized when the field's settings say so
/// (created, updated, saved as a draft, imported), and a field that renders Liquid must hold
/// Liquid that parses.
/// </summary>
public class HtmlFieldHandler : ContentFieldHandler<HtmlField>
{
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    protected readonly IStringLocalizer S;

    public HtmlFieldHandler(
        IHtmlSanitizerService htmlSanitizerService,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<HtmlFieldHandler> stringLocalizer)
    {
        _htmlSanitizerService = htmlSanitizerService;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, HtmlField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<HtmlFieldSettings>();
        if (settings.RenderLiquid && !string.IsNullOrEmpty(field.Html) && !_liquidTemplateManager.Validate(field.Html, out var errors))
        {
            context.Fail(S["{0} doesn't contain a valid Liquid expression. Details: {1}", context.ContentPartFieldDefinition.DisplayName(), string.Join(" ", errors)], nameof(field.Html));
        }

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreateContentFieldContext context, HtmlField field) => SanitizeAsync(context, field);

    public override Task UpdatingAsync(UpdateContentFieldContext context, HtmlField field) => SanitizeAsync(context, field);

    public override Task DraftSavingAsync(SaveDraftContentFieldContext context, HtmlField field) => SanitizeAsync(context, field);

    public override Task ImportedAsync(ImportContentFieldContext context, HtmlField field) => SanitizeAsync(context, field);

    private Task SanitizeAsync(ContentFieldContextBase context, HtmlField field)
    {
        if (context.ContentPartFieldDefinition?.GetSettings<HtmlFieldSettings>() is { SanitizeHtml: true } && !string.IsNullOrEmpty(field.Html))
        {
            var partName = context.ContentPartFieldDefinition.ContentTypePartDefinition?.Name ?? context.PartName;
            if (((JsonObject)context.ContentItem.Content)[partName]?[context.ContentPartFieldDefinition.Name] is JsonObject stored)
            {
                stored["Html"] = _htmlSanitizerService.Sanitize(field.Html);
            }
        }

        return Task.CompletedTask;
    }
}
