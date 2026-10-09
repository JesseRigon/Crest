using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Crest.Infrastructure.Html;
using Crest.Liquid;
using Crest.Markdown.Services;
using Crest.Shortcodes.Services;

namespace Crest;

public static class ContentRazorHelperExtensions
{
    /// <summary>
    /// Converts Markdown string to HTML.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="markdown">The markdown to convert.</param>
    /// <param name="sanitize">Whether to sanitize the markdown. Defaults to <see langword="true"/>.</param>
    /// <param name="renderLiquid">Whether Liquid should be rendered of displayed raw. Defaults to <see
    /// langword="false"/>.</param>
    public static async Task<IHtmlContent> MarkdownToHtmlAsync(
        this IPlatformHelper platformHelper,
        string markdown,
        bool sanitize = true,
        bool renderLiquid = false)
    {
        var shortcodeService = platformHelper.HttpContext.RequestServices.GetRequiredService<IShortcodeService>();
        var markdownService = platformHelper.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();

        if (renderLiquid)
        {
            var liquidTemplateManager = platformHelper.HttpContext.RequestServices.GetRequiredService<ILiquidTemplateManager>();
            var htmlEncoder = platformHelper.HttpContext.RequestServices.GetRequiredService<HtmlEncoder>();

            markdown = await liquidTemplateManager.RenderStringAsync(markdown, htmlEncoder);
        }

        // The default Markdown option is to entity escape html so filters must be run after the markdown has been
        // processed.
        var html = markdownService.ToHtml(markdown ?? string.Empty);

        // TODO: provide context argument (optional on this helper as with the liquid helper?).
        html = await shortcodeService.ProcessAsync(html);

        if (sanitize)
        {
            var sanitizer = platformHelper.HttpContext.RequestServices.GetRequiredService<IHtmlSanitizerService>();
            html = sanitizer.Sanitize(html);
        }

        return new HtmlString(html);
    }
}
