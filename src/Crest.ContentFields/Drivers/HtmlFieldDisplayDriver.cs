using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;
using Crest.Html.Services;
using Crest.Infrastructure.Html;
using Crest.Liquid;
using Crest.Mvc.ModelBinding;
using Shortcodes;

namespace Crest.ContentFields.Drivers;

public sealed class HtmlFieldDisplayDriver : ContentFieldDisplayDriver<HtmlField>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IHtmlDisplayService _htmlDisplayService;
    private readonly IHtmlSanitizerService _htmlSanitizerService;

    internal readonly IStringLocalizer S;

    public HtmlFieldDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IHtmlDisplayService htmlDisplayService,
        IHtmlSanitizerService htmlSanitizerService,
        IStringLocalizer<HtmlFieldDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlDisplayService = htmlDisplayService;
        _htmlSanitizerService = htmlSanitizerService;
        S = localizer;
    }

    public override IDisplayResult Display(HtmlField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayHtmlFieldViewModel, HtmlFieldDisplayDriver, HtmlField, BuildFieldDisplayContext> (GetDisplayShapeType(context), static async (model, driver, field, context) =>
        {
            model.Html = field.Html;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

            var settings = context.PartFieldDefinition.GetSettings<HtmlFieldSettings>();

            await driver._htmlDisplayService.UpdateModelHtmlAsync(
                model,
                settings.RenderLiquid,
                new Context { ["PartFieldDefinition"] = context.PartFieldDefinition },
                settings.SanitizeHtml);
        }, this, field, context)
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(HtmlField field, BuildFieldEditorContext context)
    {
        return Initialize<EditHtmlFieldViewModel>(GetEditorShapeType(context), model =>
        {
            model.Html = field.Html;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(HtmlField field, UpdateFieldEditorContext context)
    {
        var viewModel = new EditHtmlFieldViewModel();
        var settings = context.PartFieldDefinition.GetSettings<HtmlFieldSettings>();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix, f => f.Html);

        field.Html = settings.SanitizeHtml
            ? _htmlSanitizerService.Sanitize(viewModel.Html)
            : viewModel.Html;

        if (settings.RenderLiquid
            && !string.IsNullOrEmpty(field.Html)
            && !_liquidTemplateManager.Validate(field.Html, out var errors))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.Html),
                S[settings.SanitizeHtml
                    ? "{0} contains invalid Liquid expression. Note that HTML sanitization affects the value being saved and thus can break Liquid code: {1}"
                    : "{0} contains invalid Liquid expression: {1}",
                    context.PartFieldDefinition.DisplayName(),
                    string.Join(" ", errors)]);
        }

        return Edit(field, context);
    }
}
