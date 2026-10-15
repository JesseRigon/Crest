using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.AspNetCore.Html;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Models;
using Grove.Html.Models;
using Grove.Html.Settings;
using Grove.Html.ViewModels;
using Grove.Liquid;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Html.Handlers;

public class HtmlBodyPartHandler : ContentPartHandler<HtmlBodyPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IShortcodeService _shortcodeService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly Grove.Infrastructure.Html.IHtmlSanitizerService _htmlSanitizerService;
    private readonly Microsoft.Extensions.Localization.IStringLocalizer S;

    public HtmlBodyPartHandler(IContentDefinitionManager contentDefinitionManager,
        IShortcodeService shortcodeService,
        ILiquidTemplateManager liquidTemplateManager,
        HtmlEncoder htmlEncoder,
        Grove.Infrastructure.Html.IHtmlSanitizerService htmlSanitizerService,
        Microsoft.Extensions.Localization.IStringLocalizer<HtmlBodyPartHandler> stringLocalizer)
    {
        _htmlSanitizerService = htmlSanitizerService;
        S = stringLocalizer;
        _contentDefinitionManager = contentDefinitionManager;
        _shortcodeService = shortcodeService;
        _liquidTemplateManager = liquidTemplateManager;
        _htmlEncoder = htmlEncoder;
    }

    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, HtmlBodyPart part)
    {
        return context.ForAsync<BodyAspect>(async bodyAspect =>
        {
            try
            {
                var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
                var contentTypePartDefinition = contentTypeDefinition.Parts.FirstOrDefault(x => string.Equals(x.PartDefinition.Name, "HtmlBodyPart", StringComparison.Ordinal));
                var settings = contentTypePartDefinition?.GetSettings<HtmlBodyPartSettings>() ?? new();

                var html = part.Html;

                if (settings.RenderLiquid)
                {
                    var model = new HtmlBodyPartViewModel()
                    {
                        Html = part.Html,
                        HtmlBodyPart = part,
                        ContentItem = part.ContentItem,
                    };

                    html = await _liquidTemplateManager.RenderStringAsync(html, _htmlEncoder, model,
                        new Dictionary<string, FluidValue> { ["ContentItem"] = new ObjectValue(model.ContentItem) });
                }

                html = await _shortcodeService.ProcessAsync(html,
                    new Context
                    {
                        ["ContentItem"] = part.ContentItem,
                        ["TypePartDefinition"] = contentTypePartDefinition,
                    });

                bodyAspect.Body = new HtmlString(html);
            }
            catch
            {
                bodyAspect.Body = HtmlString.Empty;
            }
        });
    }

    // Every save path: a body that renders Liquid must parse; the markup is sanitized when the
    // type's part settings say so.
    protected override Task ValidatingAsync(ValidateContentPartContext context, HtmlBodyPart part)
    {
        var settings = context.ContentTypePartDefinition?.GetSettings<HtmlBodyPartSettings>();
        if (settings is { RenderLiquid: true } && !string.IsNullOrEmpty(part.Html) && !_liquidTemplateManager.Validate(part.Html, out var errors))
        {
            context.Fail(S["The Body doesn't contain a valid Liquid expression. Details: {0}", string.Join(" ", errors)], nameof(part.Html));
        }

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreateContentContext context, HtmlBodyPart part) => SanitizeAsync(part);

    public override Task UpdatingAsync(UpdateContentContext context, HtmlBodyPart part) => SanitizeAsync(part);

    public override Task DraftSavingAsync(SaveDraftContentContext context, HtmlBodyPart part) => SanitizeAsync(part);

    private async Task SanitizeAsync(HtmlBodyPart part)
    {
        if (string.IsNullOrEmpty(part.Html))
        {
            return;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
        var typePart = definition?.Parts.FirstOrDefault(x => string.Equals(x.PartDefinition.Name, nameof(HtmlBodyPart), StringComparison.Ordinal));
        if (typePart?.GetSettings<HtmlBodyPartSettings>() is { SanitizeHtml: true }
            && ((System.Text.Json.Nodes.JsonObject)part.ContentItem.Content)[typePart.Name] is System.Text.Json.Nodes.JsonObject stored)
        {
            stored["Html"] = _htmlSanitizerService.Sanitize(part.Html);
        }
    }
}
