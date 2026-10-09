using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Shapes;
using Crest.Shortcodes.Services;
using Shortcodes;

#pragma warning disable CA1050 // Declare types in namespaces
public static class ShortcodesPlatformRazorHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Processes shortcodes contained inside html.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="html">The html string contained shortcodes.</param>
    /// <param name="model">The ambient shape view model.</param>
    public static async Task<IHtmlContent> ShortcodesToHtmlAsync(this IPlatformHelper platformHelper, string html, object model = null)
    {
        var shortcodeService = platformHelper.HttpContext.RequestServices.GetRequiredService<IShortcodeService>();

        var context = new Context();

        // Retrieve the 'ContentItem' from the ambient shape view model.
        if (model is Shape shape && shape.TryGetProperty("ContentItem", out object contentItem))
        {
            context["ContentItem"] = contentItem;
        }
        else
        {
            context["ContentItem"] = null;
        }

        html = await shortcodeService.ProcessAsync(html, context);

        return new HtmlString(html);
    }
}
