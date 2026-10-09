using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;
using Crest.Infrastructure.Html;
using Crest.Liquid;
using Crest.Markdown.Fields;
using Crest.Markdown.Services;
using Crest.Markdown.Settings;
using Crest.Markdown.ViewModels;
using Crest.Mvc.ModelBinding;
using Crest.Shortcodes.Services;
using Shortcodes;

namespace Crest.Markdown.Drivers;

public sealed class MarkdownFieldDisplayDriver : ContentFieldDisplayDriver<MarkdownField>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly IShortcodeService _shortcodeService;
    private readonly IMarkdownService _markdownService;

    internal readonly IStringLocalizer S;

    public MarkdownFieldDisplayDriver(ILiquidTemplateManager liquidTemplateManager,
        HtmlEncoder htmlEncoder,
        IHtmlSanitizerService htmlSanitizerService,
        IShortcodeService shortcodeService,
        IMarkdownService markdownService,
        IStringLocalizer<MarkdownFieldDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlEncoder = htmlEncoder;
        _htmlSanitizerService = htmlSanitizerService;
        _shortcodeService = shortcodeService;
        _markdownService = markdownService;
        S = localizer;
    }

    public override IDisplayResult Display(MarkdownField field, BuildFieldDisplayContext context)
    {
        return Initialize<MarkdownFieldViewModel>(GetDisplayShapeType(context), async model =>
        {
            model.Markdown = field.Markdown;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

            var settings = context.PartFieldDefinition.GetSettings<MarkdownFieldSettings>();

            if (settings.RenderLiquid)
            {
                model.Markdown = await _liquidTemplateManager.RenderStringAsync(model.Markdown, _htmlEncoder, model,
                    new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(field.ContentItem) });
            }

            // The default Markdown option is to entity escape html
            // so filters must be run after the markdown has been processed.
            model.Html = _markdownService.ToHtml(model.Markdown ?? string.Empty);

            model.Html = await _shortcodeService.ProcessAsync(model.Html,
                new Context
                {
                    ["ContentItem"] = field.ContentItem,
                    ["PartFieldDefinition"] = context.PartFieldDefinition,
                });

            if (settings.SanitizeHtml)
            {
                model.Html = _htmlSanitizerService.Sanitize(model.Html ?? string.Empty);
            }
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(MarkdownField field, BuildFieldEditorContext context)
    {
        return Initialize<EditMarkdownFieldViewModel>(GetEditorShapeType(context), model =>
        {
            model.Markdown = field.Markdown;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(MarkdownField field, UpdateFieldEditorContext context)
    {
        var viewModel = new EditMarkdownFieldViewModel();
        var settings = context.PartFieldDefinition.GetSettings<MarkdownFieldSettings>();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix, vm => vm.Markdown);

        if (settings.RenderLiquid
            && !string.IsNullOrEmpty(viewModel.Markdown)
            && !_liquidTemplateManager.Validate(viewModel.Markdown, out var errors))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Markdown),
                S["{0} contains invalid Liquid expression: {1}",
                    context.PartFieldDefinition.DisplayName(),
                    string.Join(" ", errors)]);
        }
        else
        {
            field.Markdown = viewModel.Markdown;
        }

        return Edit(field, context);
    }
}
