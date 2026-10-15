using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Infrastructure.Html;
using Grove.Contents.SiteMenu.Models;
using Grove.Contents.SiteMenu.Settings;

namespace Grove.Contents.SiteMenu.Handlers;

/// <summary>A menu item's address is a well-formed, safe URL on every save path.</summary>
internal static class MenuItemUrls
{
    public static bool IsValid(string url, IHtmlSanitizerService sanitizer, HtmlEncoder encoder)
    {
        if (string.IsNullOrEmpty(url))
        {
            return true;
        }

        var toValidate = url.Split('#', 2)[0];
        if (toValidate.StartsWith("~/", StringComparison.Ordinal))
        {
            toValidate = toValidate[1..];
        }

        toValidate = toValidate.ToUriComponents();
        if (!Uri.IsWellFormedUriString(toValidate, UriKind.RelativeOrAbsolute))
        {
            return false;
        }

        var link = $"<a href=\"{encoder.Encode(toValidate)}\"></a>";
        return string.Equals(link, sanitizer.Sanitize(link), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class LinkMenuItemPartHandler(IHtmlSanitizerService sanitizer, HtmlEncoder encoder, IStringLocalizer<LinkMenuItemPartHandler> S)
    : ContentPartHandler<LinkMenuItemPart>
{
    protected override Task ValidatingAsync(ValidateContentPartContext context, LinkMenuItemPart part)
    {
        if (!MenuItemUrls.IsValid(part.Url, sanitizer, encoder))
        {
            context.Fail(S["{0} is an invalid url.", part.Url], nameof(part.Url));
        }

        return Task.CompletedTask;
    }
}

/// <summary>An HTML menu item: a safe address, and its markup sanitized on save when the type's part settings say so.</summary>
public sealed class HtmlMenuItemPartHandler(
    IContentDefinitionManager contentDefinitionManager,
    IHtmlSanitizerService sanitizer,
    HtmlEncoder encoder,
    IStringLocalizer<HtmlMenuItemPartHandler> S) : ContentPartHandler<HtmlMenuItemPart>
{
    protected override Task ValidatingAsync(ValidateContentPartContext context, HtmlMenuItemPart part)
    {
        if (!MenuItemUrls.IsValid(part.Url, sanitizer, encoder))
        {
            context.Fail(S["{0} is an invalid url.", part.Url], nameof(part.Url));
        }

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreateContentContext context, HtmlMenuItemPart part) => SanitizeAsync(context.ContentItem, part);

    public override Task UpdatingAsync(UpdateContentContext context, HtmlMenuItemPart part) => SanitizeAsync(context.ContentItem, part);

    public override Task DraftSavingAsync(SaveDraftContentContext context, HtmlMenuItemPart part) => SanitizeAsync(context.ContentItem, part);

    public override Task ImportedAsync(ImportContentContext context, HtmlMenuItemPart part) => SanitizeAsync(context.ContentItem, part);

    private async Task SanitizeAsync(ContentItem item, HtmlMenuItemPart part)
    {
        if (string.IsNullOrEmpty(part.Html))
        {
            return;
        }

        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType);
        var typePart = definition?.Parts.FirstOrDefault(x => x.PartDefinition.Name == nameof(HtmlMenuItemPart));
        if ((typePart?.GetSettings<HtmlMenuItemPartSettings>() ?? new HtmlMenuItemPartSettings()).SanitizeHtml
            && ((JsonObject)item.Content)[typePart?.Name ?? nameof(HtmlMenuItemPart)] is JsonObject stored)
        {
            stored["Html"] = sanitizer.Sanitize(part.Html);
        }
    }
}
